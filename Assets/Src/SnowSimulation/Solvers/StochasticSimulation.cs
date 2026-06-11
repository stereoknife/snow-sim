using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HPML;
using HPML.Serialization;
using TFM.Simulation;
using TFM.SnowSimulation.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Serialization.Binary;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using static Unity.Mathematics.math;
using Terrain = TFM.Simulation.Terrain;

namespace TFM.Solvers
{
    public class StochasticSimulation : IDisposable
    {
        public enum EventId
        {
            MeltStep,
            TransportStep,
            DiffusionStep,
            StabilityStep,
            SnowfallStep,
            SnowfallStart,
            SnowfallEnd,
            AvalancheStep,
            AvalancheStart,
        }

        private Snow.Parameters _parameters;
        private doubleF _height;
        private doubleF _illumination;
        private double4F _snow;
        private double2F _windDirection;
        private doubleF _hazard;
        private ScalarField2D _windAltitude;
        private ScalarField2D _windTerrain;
        private NativeArray<double> _flow;
        private NativeArray<bool> _moving;

        private NativeList<double> _tempTimeline;
        private NativeList<double> _windTimeline;
        private NativeList<double> _cloudTimeline;
        private NativeList<double> _precipTimeline;

        private Random _rng;
        private uint _seed;

        private readonly Dictionary<EventId, float> _periods = new(Enum.GetValues(typeof(EventId)).Length);
        private readonly Dictionary<EventId, float> _frequencies = new(Enum.GetValues(typeof(EventId)).Length);
        public Dictionary<EventId, bool> Enabled { get; } = new(Enum.GetValues(typeof(EventId)).Length);

        private bool _profilingEnabled;
        private (EventId, float, double)[] _profilingData;
        private int _profilingDataIndex;
        
        public NativeList<double> TempTimeline => _tempTimeline;
        public NativeList<double> WindTimeline => _windTimeline;
        public NativeList<double> CloudTimeline => _cloudTimeline;
        public NativeList<double> PrecipTimeline => _precipTimeline;

        public Snow.Parameters Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }

        public doubleF Height => _height;
        public doubleF Illumination => _illumination;
        public double4F SnowLayers => _snow;
        public double2F WindDirection => _windDirection;
        public doubleF Hazard => _hazard;
        public ScalarField2D WindAltitude => _windAltitude;
        public ScalarField2D WindTerrain => _windTerrain;
        public NativeArray<double> Flow => _flow;
        public NativeArray<bool> Moving => _moving;

        public bool UsePrecipTimeline { get; set; } = true;
        public bool UseCloudTimeline { get; set; } = true;
        public bool UseWindTimeline { get; set; } = true;
        public bool UseTempTimeline { get; set; } = true;
        public bool UseSimpleMelt { get; set; } = false;

        public float simulationTime { get; private set; }
        public int simulationFrames { get; private set; }
        public EventId lastEventId { get; private set; }

        public void SetEventPeriod(EventId eventId, float period)
        {
            _periods[eventId] = period;
            if (_frequencies[eventId] > 0) _frequencies[eventId] = 1f / period;
        }

        public void Init(SimulationTerrain terrain, SimulationParameters parameters)
        {
            BinarySerialization.AddGlobalAdapter(new doubleFAdapter(Allocator.Persistent));
            BinarySerialization.AddGlobalAdapter(new double2FAdapter(Allocator.Persistent));
            BinarySerialization.AddGlobalAdapter(new ScalarField2DAdapter(Allocator.Persistent));
            
            _height = doubleF.FromTexture(terrain.heightmap, terrain.size, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, parameters.initialSnowValue);
            _hazard = new doubleF(_height, Allocator.Persistent);
            _flow = new NativeArray<double>(_height.Length * 4 * 2, Allocator.Persistent);
            _moving = new NativeArray<bool>(_height.Length, Allocator.Persistent);
            
            var startDay = parameters.LightingParameters.DirectStartingDay;
            var endDay = parameters.LightingParameters.DirectEndDay;
            if (startDay > endDay) endDay += 365;
            var tlLength = endDay - startDay;
            _tempTimeline = new NativeList<double>(tlLength, Allocator.Persistent);
            _cloudTimeline = new NativeList<double>(tlLength, Allocator.Persistent);
            _windTimeline = new NativeList<double>(tlLength, Allocator.Persistent);
            _precipTimeline = new NativeList<double>(tlLength, Allocator.Persistent);
            
            var success = (false, false);
            CachePaths(terrain, parameters, out var lightPath, out var windPath);
            if (parameters.loadFromCache) success = TryLoadCacheData(lightPath, windPath);

            var lh = new JobHandle();
            var wh = new JobHandle();
            if (!success.Item1) lh = GenerateLightingData(parameters.LightingParameters, lh);
            if (!success.Item2) wh = GenerateWindData(parameters.WindParameters, wh);
            var hh = GenerateHazardData(new JobHandle());
            JobHandle.CombineDependencies(lh, wh, hh).Complete();
            
            SaveCacheData(lightPath, windPath);
            
            _seed = parameters.seed;
            _parameters = Snow.Parameters.Default;
            _parameters.WindSpeedPerLayer = parameters.surfaceSpeedIncrement;
            _parameters.WindSpeed = _parameters.WindMaxSpeed = parameters.surfaceSpeedIncrement * parameters.surfaceSamples;
            Reset();
        }
        
        private JobHandle GenerateLightingData(Lighting.Parameters parameters, JobHandle dependsOn)
        {
            _illumination = new doubleF(_height, Allocator.Persistent);
            
            var dlf = new doubleF(_height, Allocator.TempJob);
            var ilf = new doubleF(_height, Allocator.TempJob);
            var alf = new doubleF(_height, Allocator.TempJob);

            //_renderer.AddTexture(TerrainMeshRenderer.TextureId.Height, _height);
            
            var dlh = Lighting.DirectLighting(_height, dlf, ref parameters, dependsOn);
            var ilh = Lighting.IndirectLighting(_height, dlf, ilf, ref parameters, dlh);
            var alh = Lighting.AmbientLighting(_height, alf, dependsOn);

            //dlh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.DirectIllumination, dlf, dlh);
            //ilh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.IndirectIllumination, ilf, ilh);
            //alh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.AmbientIllumination, alf, alh);

            var clh = JobHandle.CombineDependencies(dlh, ilh, alh);
            clh = Lighting.TemperatureParallel(_illumination, dlf, alf, ilf, _height, ref parameters, clh);
            
            dlh = dlf.Dispose(clh);
            ilh = ilf.Dispose(clh);
            alh = alf.Dispose(clh);
            
            return JobHandle.CombineDependencies(dlh, ilh, alh);
        }
        
        private JobHandle GenerateWindData(Wind.Parameters parameters, JobHandle dependsOn)
        {
            normalize(double2(1, -1));
            if (!_windDirection.IsCreated) _windDirection = new double2F(_height, Allocator.Persistent, parameters.WindDirection);
            if (!_windAltitude.IsCreated) _windAltitude = new ScalarField2D(_height.dimension, parameters.SurfaceSamples, _height.size.xz, Allocator.Persistent);
            if (!_windTerrain.IsCreated) _windTerrain = new ScalarField2D(_windAltitude, Allocator.Persistent);
            
            var wsf = new doubleF(_height, Allocator.TempJob);
            dependsOn = Wind.VenturiParallel(_windDirection, _height, ref parameters, dependsOn);
            dependsOn = Wind.TerrainDeflectionParallel(_windDirection, _height, ref parameters, dependsOn);
            dependsOn = Wind.WindEffectSurface(_windDirection, wsf, _height, _windAltitude,  _windTerrain, ref parameters, dependsOn);
            dependsOn = wsf.Dispose(dependsOn);
            
            return dependsOn;
        }
        
        private JobHandle GenerateHazardData(JobHandle dependsOn)
        {
            var rf = new doubleF(_height, Allocator.TempJob);
            var gf = new doubleF(_height, Allocator.TempJob);
            var cf = new NativeArray<int>(_height.Length, Allocator.TempJob);
            
            var rh = Terrain.Roughness(_height, rf, 5, dependsOn);
            var gh = Terrain.Gradient(_height, gf, dependsOn);
            var ch = Terrain.Curvature(_height, cf, 5, dependsOn);

            //rh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.TerrainRoughness, rf, rh);
            //gh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.TerrainGradient, gf, gh);
            //ch = _renderer.AddTexture(TerrainMeshRenderer.TextureId.TerrainCurvatureCategory, _height, cf, ch);

            rh = Terrain.RoughnessDist(rf, rh);
            gh = Terrain.GradientDist(gf, gh);
            
            //rh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.RoughnessHazard, rf, rh);
            //gh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.GradientHazard, gf, gh);
            
            var hh = JobHandle.CombineDependencies(rh, gh, ch);
            hh = Terrain.Hazard(_hazard, gf, rf, cf, hh);
            
            return hh;
        }

        public void Reset()
        {
            simulationTime = 0f;
            simulationFrames = 0;
            _rng.InitState(_seed);
            _periods[EventId.MeltStep] = 1f;
            _periods[EventId.TransportStep] = 0.5f;
            _periods[EventId.DiffusionStep] = 0.5f;
            _periods[EventId.StabilityStep] = 0.5f;
            _periods[EventId.SnowfallStep] = 0.5f;
            _periods[EventId.SnowfallStart] = 7f;
            _periods[EventId.SnowfallEnd] = 5f;
            _periods[EventId.AvalancheStep] = 1f / 24f / 3600f * 0.1f;
            _periods[EventId.AvalancheStart] = 15f;

            foreach (EventId id in Enum.GetValues(typeof(EventId)))
            {
                _frequencies[id] = 1f / _periods[id];
            }

            _frequencies[EventId.SnowfallStep] = 0f;
            _frequencies[EventId.SnowfallEnd] = 0f;
            _frequencies[EventId.AvalancheStep] = 0f;
            if (UsePrecipTimeline) _frequencies[EventId.SnowfallStart] = 0f;

            _parameters.CloudCover = 0;
        }
        
        public void SetSnow(double4 value)
        {
            for (int i = 0; i < _snow.Length; i++)
            {
                _snow[i] = value;
                if (any(_snow.index(i) == 0 | _snow.index(i) == _snow.dimension - 1)) _snow[i] = 0;
            }
        }

        private EventId Next(out float dt)
        {
            var totalProb = 0f;
            foreach (var id in _frequencies.Keys)
            {
                if (!Enabled[id]) continue;
                totalProb += _frequencies[id];
            }

            if (totalProb < 0.00001)
            {
                dt = 0;
                Debug.LogError("At least one simulation event must be enabled.");
                return (EventId)(-1);
            }

            dt = 1f / totalProb;
            var random = _rng.NextFloat() * totalProb;

            totalProb = 0f;
            foreach (var id in _frequencies.Keys)
            {
                if (!Enabled[id]) continue;
                totalProb += _frequencies[id];
                if (totalProb >= random) return id;
            }

            return (EventId)(-1);
        }

        public void Step()
        {
            var low = (int)floor(simulationTime);
            var high = (int)ceil(simulationTime);
            var t = frac(simulationTime);

            if (UseTempTimeline)
                _parameters.TempBase = lerp(_tempTimeline[low], _tempTimeline[high], t);
            if (UseWindTimeline)
                _parameters.WindSpeed = lerp(_windTimeline[low], _windTimeline[high], t);
            if (UseCloudTimeline)
                _parameters.CloudCover = lerp(_cloudTimeline[low], _cloudTimeline[high], t);
            if (UsePrecipTimeline)
            {
                _parameters.SnowfallIntensity = lerp(_precipTimeline[low], _precipTimeline[high], t);
                if (_parameters.SnowfallIntensity > 0.0000000001)
                    _frequencies[EventId.SnowfallStep] = 1f / _periods[EventId.SnowfallStep];
                else
                    _frequencies[EventId.SnowfallStep] = 0f;
            }

            var ev = lastEventId = Next(out var dt);
            var jh = new JobHandle();
            var preEventTime = DateTime.Now;
            switch (ev)
            {
                case EventId.MeltStep:
                    jh = UseSimpleMelt
                        ? Snow.MeltSimple(_snow, _illumination, _height, _periods[ev], ref _parameters, jh)
                        : Snow.Melt(_snow, _illumination, _height, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.TransportStep:
                    jh = Snow.Transport(_snow, _windDirection, _windAltitude, _windTerrain, _height, _periods[ev],
                        ref _parameters, jh);
                    break;
                case EventId.DiffusionStep:
                    jh = Snow.Diffusion(_snow, _height, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.StabilityStep:
                    jh = Snow.Stability(_snow, _illumination, _height, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.SnowfallStep:
                    jh = Snow.Snowfall(_snow, _height, _illumination, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.SnowfallStart:
                    if (!UseCloudTimeline) _parameters.CloudCover = 1;
                    _frequencies[EventId.SnowfallStep] = 1f / _periods[EventId.SnowfallStep];
                    _frequencies[EventId.SnowfallEnd] = 1f / _periods[EventId.SnowfallEnd];
                    _frequencies[EventId.SnowfallStart] = 0;
                    break;
                case EventId.SnowfallEnd:
                    if (!UseCloudTimeline) _parameters.CloudCover = 0;
                    jh = Snow.Snowfall(_snow, _height, _illumination, _periods[ev], ref _parameters, jh);
                    _frequencies[EventId.SnowfallStep] = 0;
                    _frequencies[EventId.SnowfallEnd] = 0;
                    _frequencies[EventId.SnowfallStart] = 1f / _periods[EventId.SnowfallStart];
                    break;
                case EventId.AvalancheStart:
                    TriggerAvalanche();
                    break;
                case EventId.AvalancheStep:
                    jh = Snow.AvalancheParallel(_snow, _flow, _height, _moving, 0.1, ref _parameters, default);
                    break;
                case (EventId)(-1):
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            jh.Complete();
            var postEventTime = DateTime.Now;

            if (ev == EventId.AvalancheStep && !_moving.Any(x => x))
            {
                _frequencies[EventId.AvalancheStart] = 1f / _periods[EventId.AvalancheStart];
                _frequencies[EventId.AvalancheStep] = 0f;
            }

            if (_profilingEnabled)
            {
                _profilingData[_profilingDataIndex] =
                    (ev, simulationTime, (postEventTime - preEventTime).TotalMilliseconds);
                _profilingDataIndex = (_profilingDataIndex + 1) % _profilingData.Length;
            }

            simulationTime += dt;
            simulationFrames++;
        }

        public void TriggerAvalanche(int cell = -1)
        {
            _frequencies[EventId.AvalancheStep] = 1f / _periods[EventId.AvalancheStep];
            _frequencies[EventId.AvalancheStart] = 0f;

            if (cell == -1)
            {
                var trigger = _rng.NextDouble() * _hazard[^1];
                for (int i = 0; i < _hazard.Length; i++)
                {
                    if (_hazard[i] < trigger) continue;
                    cell = i;
                    break;
                }
            }

            if (cell >= 0) _moving[cell] = true;
        }

        public void EnableProfiling(int capacity)
        {
            _profilingEnabled = true;
            _profilingData = new (EventId, float, double)[capacity];
            _profilingDataIndex = 0;
        }

        public void DisableProfiling()
        {
            _profilingEnabled = false;
            _profilingData = null;
        }

        public void ExportProfilingData(string file)
        {
            using var writer = new StreamWriter(File.Open(file, FileMode.Create));
            writer.WriteLine("number, event, time, duration");
            var totalDuration = 0d;
            for (int i = _profilingDataIndex, j = 0; i < _profilingData.Length; i++, j++)
            {
                var (ev, time, duration) = _profilingData[i];
                writer.WriteLine($"{j},{ev.ToString()},{time},{duration}");
                totalDuration += duration;
            }

            for (int i = 0, j = _profilingData.Length - _profilingDataIndex; i < _profilingDataIndex; i++, j++)
            {
                var (ev, time, duration) = _profilingData[i];
                writer.WriteLine($"{j},{ev.ToString()},{time},{duration}");
                totalDuration += duration;
            }
        }
        
        private void CachePaths(SimulationTerrain terrain, SimulationParameters parameters, out string lightPath, out string windPath) {
            var lightHash = xxHash3.Hash64(parameters.LightingParameters);
            var windHash = xxHash3.Hash64(parameters.WindParameters);
            lightPath = $"{Application.persistentDataPath}/{parameters.cacheLocation}/{terrain.name}.{lightHash.x:x}{lightHash.y:x}.bytes";
            windPath = $"{Application.persistentDataPath}/{parameters.cacheLocation}/{terrain.name}.{windHash.x:x}{windHash.y:x}.bytes";
        }
        
        private unsafe (bool, bool) TryLoadCacheData(string lightPath, string windPath)
        {
            var lightLoaded = false;
            var windLoaded = false;
            
            if (File.Exists(lightPath))
            {
                var file = File.ReadAllBytes(lightPath);
                fixed (byte* bytes = file)
                {
                    var buffer = new UnsafeAppendBuffer(bytes, file.Length);
                    buffer.Length = buffer.Capacity;
                    var reader = buffer.AsReader();
                    
                    Debug.Log($"Loading temp from {lightPath}");
                    _illumination = BinarySerialization.FromBinary<doubleF>(&reader);
                }

                lightLoaded = true;
            }
            
            if (File.Exists(windPath))
            {
                var file = File.ReadAllBytes(windPath);
                fixed (byte* bytes = file)
                {
                    var buffer = new UnsafeAppendBuffer(bytes, file.Length);
                    buffer.Length = buffer.Capacity;
                    var reader = buffer.AsReader();
                    
                    Debug.Log($"Loading wind from {windPath}");
                    _windDirection = BinarySerialization.FromBinary<double2F>(&reader);
                    Debug.Log("Loading altitude");
                    _windAltitude = BinarySerialization.FromBinary<ScalarField2D>(&reader);
                    Debug.Log("Loading terrain effect");
                    _windTerrain = BinarySerialization.FromBinary<ScalarField2D>(&reader);
                }

                windLoaded = true;
            }

            return (lightLoaded, windLoaded);
        }
        
        private unsafe void SaveCacheData(string lightPath, string windPath)
        {
            var capacity = max(
                doubleFAdapter.SizeOf(_illumination)
                , double2FAdapter.SizeOf(_windDirection)
                  + ScalarField2DAdapter.SizeOf(_windAltitude)
                  + ScalarField2DAdapter.SizeOf(_windTerrain)
            );
                
            var buffer = new UnsafeAppendBuffer(capacity, 4, Allocator.Temp);
                
            BinarySerialization.ToBinary(&buffer, _illumination);
            
            new FileInfo(lightPath).Directory?.Create();
            using (var writer = new BinaryWriter(File.Open(lightPath, FileMode.Create)))
            {
                Debug.Log($"Writing data to {lightPath}");
                writer.Write(new ReadOnlySpan<byte>(buffer.Ptr, buffer.Length));
            }
            
            buffer.Reset();
            
            BinarySerialization.ToBinary(&buffer, _windDirection);
            BinarySerialization.ToBinary(&buffer, _windAltitude);
            BinarySerialization.ToBinary(&buffer, _windTerrain);
            
            new FileInfo(windPath).Directory?.Create();
            using (var writer = new BinaryWriter(File.Open(windPath, FileMode.Create)))
            {
                Debug.Log($"Writing data to {windPath}");
                writer.Write(new ReadOnlySpan<byte>(buffer.Ptr, buffer.Length));
            }
            
            buffer.Dispose();
        }

        public void Dispose()
        {
            _height.Dispose();
            _illumination.Dispose();
            _snow.Dispose();
            _windDirection.Dispose();
            _hazard.Dispose();
            _windAltitude.Dispose();
            _windTerrain.Dispose();
            _flow.Dispose();
            _moving.Dispose();
            _tempTimeline.Dispose();
            _windTimeline.Dispose();
            _cloudTimeline.Dispose();
            _precipTimeline.Dispose();
        }
    }
}