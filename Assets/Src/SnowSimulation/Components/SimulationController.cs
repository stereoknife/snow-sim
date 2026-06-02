using System;
using System.Collections;
using System.IO;
using EasyButtons;
using HPML;
using HPML.Serialization;
using TFM.Components.Solvers;
using TFM.Components.Visualization;
using Unity.Collections;
using UnityEngine;
using static Unity.Mathematics.math;
using TFM.Simulation;
using TFM.SnowSimulation.Data;
using TFM.Utils;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Serialization.Binary;
using noise = Unity.Mathematics.noise;
using Terrain = TFM.Simulation.Terrain;

namespace TFM.Components
{
    public class SimulationController : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private SnowSimulation.Data.SimulationTerrain terrain;
        [SerializeField] private SimulationParameters parameters;
        [SerializeField] private double4 initialSnowValue;
        [SerializeField] private bool forceGeneration = false;
        [Header("Events")]
        [SerializeField] private bool snowfall;
        [SerializeField] private bool windTransport;
        [SerializeField] private bool melting;
        [SerializeField] private bool powderDiffusion;
        [SerializeField] private bool avalanche;
        [Header("Profiling")]
        [SerializeField] private bool enableProfiling;
        [SerializeField] private string outputFile;
        [SerializeField][Min(1)] private int bufferSize;
        [Header("Run parameters")]
        [SerializeField] private int frames;
        [SerializeField] private float time;

        [SerializeField] private Material material;
        [SerializeField] private bool drawWindMesh;
        private Mesh _windMesh;
        private RenderParams _rp;
        private Matrix4x4 _mat;
        
        // Cached
        private doubleF _height;
        private doubleF _illumination;
        private double2F _wind;
        private ScalarField2D _windAltitude;
        private ScalarField2D _terrainEffect;
        
        // Uncached
        private double4F _snow;
        private doubleF _hazard;
        private NativeArray<double> _flow;
        private NativeArray<bool> _moving;

        private NativeArray<double> _tempTimeline;
        private NativeArray<double> _cloudTimeline;
        private NativeArray<double> _windTimeline;
        private NativeArray<double> _precipTimeline;

        private NativeHashSet<int> _selectedPoints, _highlightedPoints;

        public doubleF Heightfield => _height;
        public double4F Snowfield => _snow;
        public NativeHashSet<int> SelectedPoints => _selectedPoints;
        public NativeHashSet<int> HighlightedPoints => _highlightedPoints;
        public ScalarField2D WindAltitude => _windAltitude;

        private StochasticSimulation _simulation;
        private bool runSim = false;

        private TerrainMeshRenderer _renderer;

        [Button]
        void StartSimulation()
        {
            runSim = true;
            //_simulation.Reset();
            StartCoroutine(RunSimulationCR());
        }

        [Button]
        void PauseSimulation()
        {
            runSim = false;
        }
        
        [Button]
        private void RunFrames()
        {
            runSim = true;
            //_simulation.Reset();
            StartCoroutine(RunFramesCR());
        }
        
        [Button]
        private void RunTime()
        {
            runSim = true;
            //_simulation.Reset();
            StartCoroutine(RunTimeCR());
        }

        [Button]
        private void Reset()
        {
            for (int i = 0; i < _snow.Length; i++)
            {
                _snow[i] = initialSnowValue;
            }
            _simulation.Reset();
        }
        
        [Button]
        private void ExportProfilingData()
        {
            _simulation.ExportProfilingData(Path.Combine(Application.dataPath, "results", $"{outputFile}.csv"));
            Debug.Log($"File written at {outputFile}.csv");
        }

        [Button]
        private void ForceAvalanche()
        {
            if (_selectedPoints.IsEmpty)
            {
                _simulation.TriggerAvalanche();
            }
            else
            {
                foreach (var point in _selectedPoints)
                {
                    _simulation.TriggerAvalanche(point);
                }
            }
        }

        private IEnumerator RunSimulationCR()
        {
            while (runSim)
            {
                _simulation.Step();
                yield return null;
            }
        }

        private IEnumerator RunFramesCR()
        {
            for (int i = 0; i < frames && runSim; i++)
            {
                _simulation.Step();
                yield return null;
            }
            Debug.Log("Done");
        }
        
        private IEnumerator RunTimeCR()
        {
            while(runSim && _simulation.simulationTime < time)
            {
                _simulation.Step();
                yield return null;
            }
            Debug.Log("Done");
        }

        public void HighlightPoint(int index, bool clearHighlights = true)
        {
            if (clearHighlights) _highlightedPoints.Clear();
            if (0 <= index && index < _height.Length) _highlightedPoints.Add(index);
        }
        
        public void SelectPoint(int index, bool clearSelection = true)
        {
            if (clearSelection) _selectedPoints.Clear();
            if (0 <= index && index < _height.Length) _selectedPoints.Add(index);
        }

        private void SetSimulationParams()
        {
            _simulation.SetEventEnabled(StochasticSimulation.EventId.MeltStep, melting);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.TransportStep, windTransport);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.DiffusionStep, powderDiffusion);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.SnowfallStep, snowfall);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.SnowfallStart, snowfall);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.SnowfallEnd, snowfall);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.AvalancheStart, avalanche);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.AvalancheStep, avalanche);
            
            if (enableProfiling)
                _simulation.EnableProfiling(bufferSize);
            else
                _simulation.DisableProfiling();
        }

        private void OnValidate()
        {
            if (_simulation != null) SetSimulationParams();
        }

        private void Awake()
        {
            _renderer = GetComponent<TerrainMeshRenderer>();
            
            InitializeSimulation();

            _windMesh = new Mesh
            {
                name = "Wind mesh"
            };
            _rp = new RenderParams(material)
            {
                matProps = new MaterialPropertyBlock()
            };
            _rp.matProps.SetColor("BaseColor", Color.cyan);

            _windAltitude.Layer(_windAltitude.layers - 1).GenerateMesh(out var mda, default).Complete();
            Mesh.ApplyAndDisposeWritableMeshData(mda, _windMesh);
            _windMesh.RecalculateBounds();
            _mat = Matrix4x4.Scale(new Vector3(0.01f, 0.01f, 0.01f));
        }

        private void Update()
        {
            if (drawWindMesh)
                Graphics.RenderMesh(_rp, _windMesh, 0, _mat, _mat);
        }

        private void InitializeSimulation()
        {
            BinarySerialization.AddGlobalAdapter(new doubleFAdapter(Allocator.Persistent));
            BinarySerialization.AddGlobalAdapter(new double2FAdapter(Allocator.Persistent));
            BinarySerialization.AddGlobalAdapter(new ScalarField2DAdapter(Allocator.Persistent));
            _height = doubleF.FromTexture(terrain.heightmap, terrain.size, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, initialSnowValue);
            _hazard = new doubleF(_height, Allocator.Persistent);
            _flow = new NativeArray<double>(_height.Length * 4 * 2, Allocator.Persistent);
            _moving = new NativeArray<bool>(_height.Length, Allocator.Persistent);

            var success = (false, false);
            if (!forceGeneration) success = TryLoadCacheData();
            Debug.Log($"Successfully loaded from cache: {success}");
            
            var lh = new JobHandle();
            var wh = new JobHandle();
            if (!success.Item1)
                lh = GenerateLightingData(lh);
            if (!success.Item2)
                wh = GenerateWindData(wh);

            lh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.CombinedIllumination, _illumination, lh);
            
            var hh = Terrain.Hazard(_height, _hazard, default);
            
            JobHandle.CombineDependencies(lh, wh, hh).Complete();
            SaveCacheData();
            
            _renderer.Apply();
            
            var startDay = parameters.LightingParameters.DirectStartingDay;
            var endDay = parameters.LightingParameters.DirectEndDay;
            if (startDay > endDay) endDay += 365;
            var tlLength = endDay - startDay;
            _tempTimeline = new NativeArray<double>(tlLength, Allocator.Persistent);
            _cloudTimeline = new NativeArray<double>(tlLength, Allocator.Persistent);
            _windTimeline = new NativeArray<double>(tlLength, Allocator.Persistent);
            _precipTimeline = new NativeArray<double>(tlLength, Allocator.Persistent);
            _selectedPoints = new NativeHashSet<int>(10, Allocator.Persistent);
            _highlightedPoints = new NativeHashSet<int>(10, Allocator.Persistent);
            time = tlLength;
            
            GenerateTemperatureTimeline();
            GenerateCloudPrecipTimelines();
            GenerateWindTimeline();
            
            _simulation = new StochasticSimulation(1337)
            {
                snow = _snow,
                height = _height,
                temperature = _illumination,
                wind = _wind,
                windAltitude = _windAltitude,
                windTerrain = _terrainEffect,
                flow = _flow,
                moving = _moving,
                hazard = _hazard,
                tempTimeline = _tempTimeline,
                cloudTimeline = _cloudTimeline,
                precipTimeline = _precipTimeline,
                windTimeline = _windTimeline,
                parameters = Snow.Parameters.Default,
            };
            
            SetSimulationParams();
        }

        private void GenerateTemperatureTimeline()
        {
            // Temperature follows a sin curve starting between spring equinox and summer solstice
            // a * sin(b * (x - c)) + d
            var maxTemp = 30f;
            var minTemp = -5;
            
            var a = (maxTemp - minTemp) / 2f;
            var b = PI2 / 365f;
            var c = -10f;
            var d = (maxTemp + minTemp) / 2f;

            var startDay = parameters.LightingParameters.DirectStartingDay;
            for (int i = 0; i < _tempTimeline.Length; i++)
            {
                _tempTimeline[i] = a * sin(b * (i + startDay - c) - PI * 5f/8f) + d;
            }
        }

        private void GenerateCloudPrecipTimelines()
        {
            for (int i = 0; i < _cloudTimeline.Length; i++)
            {
                _cloudTimeline[i] = noise.cnoise(float2(-420f, i / 5f)) / 2f + 0.5f;
                _precipTimeline[i] = saturate(unlerp(0.7, 1, _cloudTimeline[i])) * 0.005f;
            }
        }

        private void GenerateWindTimeline()
        {
            var maxWindSpeed = parameters.WindParameters.SurfaceSpeedIncrement *
                               parameters.WindParameters.SurfaceSamples;
            for (int i = 0; i < _windTimeline.Length; i++)
            {
                _windTimeline[i] = 8f;//(noise.cnoise(float2(i / 2f, -20f)) + 1f) / 2f * maxWindSpeed;
            }
        }

        private unsafe (bool, bool) TryLoadCacheData()
        {
            Debug.Log("Trying to load cache");
            
            var lightHash = xxHash3.Hash64(parameters.LightingParameters);
            var windHash = xxHash3.Hash64(parameters.WindParameters);

            var lightLoaded = false;
            var windLoaded = false;
            
            var path = Path.Combine(Application.dataPath, "cache", $"{terrain.name}.{lightHash.x:x}{lightHash.y:x}.bytes");
            if (File.Exists(path))
            {
                var file = File.ReadAllBytes(path);
                fixed (byte* bytes = file)
                {
                    var buffer = new UnsafeAppendBuffer(bytes, file.Length);
                    buffer.Length = buffer.Capacity;
                    var reader = buffer.AsReader();
                    
                    Debug.Log("Loading temp");
                    _illumination = BinarySerialization.FromBinary<doubleF>(&reader);
                }

                lightLoaded = true;
            }
            
            path = Path.Combine(Application.dataPath, "cache", $"{terrain.name}.{windHash.x:x}{windHash.y:x}.bytes");
            if (File.Exists(path))
            {
                var file = File.ReadAllBytes(path);
                fixed (byte* bytes = file)
                {
                    var buffer = new UnsafeAppendBuffer(bytes, file.Length);
                    buffer.Length = buffer.Capacity;
                    var reader = buffer.AsReader();
                    
                    Debug.Log("Loading wind");
                    _wind = BinarySerialization.FromBinary<double2F>(&reader);
                    Debug.Log("Loading altitude");
                    _windAltitude = BinarySerialization.FromBinary<ScalarField2D>(&reader);
                    Debug.Log("Loading terrain effect");
                    _terrainEffect = BinarySerialization.FromBinary<ScalarField2D>(&reader);
                }

                windLoaded = true;
            }

            return (lightLoaded, windLoaded);
        }

        private unsafe void SaveCacheData()
        {
            var lightHash = xxHash3.Hash64(parameters.LightingParameters);
            var windHash = xxHash3.Hash64(parameters.WindParameters);

            var capacity = max(
                doubleFAdapter.SizeOf(_illumination)
                , double2FAdapter.SizeOf(_wind)
                  + ScalarField2DAdapter.SizeOf(_windAltitude)
                  + ScalarField2DAdapter.SizeOf(_terrainEffect)
            );
                
            var buffer = new UnsafeAppendBuffer(capacity, 4, Allocator.Temp);
                
            BinarySerialization.ToBinary(&buffer, _illumination);

            var path = Path.Combine(Application.dataPath, "cache", $"{terrain.name}.{lightHash.x:x}{lightHash.y:x}.bytes");
            new FileInfo(path).Directory?.Create();
            using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                writer.Write(new ReadOnlySpan<byte>(buffer.Ptr, buffer.Length));
            }
            
            buffer.Reset();
            
            BinarySerialization.ToBinary(&buffer, _wind);
            BinarySerialization.ToBinary(&buffer, _windAltitude);
            BinarySerialization.ToBinary(&buffer, _terrainEffect);
            
            path = Path.Combine(Application.dataPath, "cache", $"{terrain.name}.{windHash.x:x}{windHash.y:x}.bytes");
            new FileInfo(path).Directory?.Create();
            using (var writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                writer.Write(new ReadOnlySpan<byte>(buffer.Ptr, buffer.Length));
            }
            
            buffer.Dispose();
        }
        
        private JobHandle GenerateLightingData(JobHandle dependsOn)
        {
            if (!_height.IsCreated)
                _height = doubleF.FromTexture(terrain.heightmap, terrain.size, Allocator.Persistent);
            if (!_illumination.IsCreated)
                _illumination = new doubleF(_height, Allocator.Persistent);
            
            var lightParams = parameters.LightingParameters;
            var dlf = new doubleF(_height, Allocator.TempJob);
            var ilf = new doubleF(_height, Allocator.TempJob);
            var alf = new doubleF(_height, Allocator.TempJob);

            _renderer.AddTexture(TerrainMeshRenderer.TextureId.Height, _height);
            
            var dlh = Lighting.DirectLighting(_height, dlf, ref lightParams, dependsOn);
            var ilh = Lighting.IndirectLighting(_height, dlf, ilf, ref lightParams, dlh);
            var alh = Lighting.AmbientLighting(_height, alf, dependsOn);

            dlh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.DirectIllumination, dlf, dlh);
            ilh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.IndirectIllumination, ilf, ilh);
            alh = _renderer.AddTexture(TerrainMeshRenderer.TextureId.AmbientIllumination, alf, alh);

            var clh = JobHandle.CombineDependencies(dlh, ilh, alh);
            clh = Lighting.TemperatureParallel(_illumination, dlf, alf, ilf, _height, ref lightParams, clh);
            
            dlh = dlf.Dispose(clh);
            ilh = ilf.Dispose(clh);
            alh = alf.Dispose(clh);
            
            return JobHandle.CombineDependencies(dlh, ilh, alh);
        }
        
        private JobHandle GenerateWindData(JobHandle dependsOn)
        {
            var windParams = parameters.WindParameters;
            if (!_height.IsCreated)
                _height = doubleF.FromTexture(terrain.heightmap, terrain.size, Allocator.Persistent);
            if (!_wind.IsCreated)
                _wind = new double2F(_height, Allocator.Persistent, normalize(double2(1, -1)));
            if (!_windAltitude.IsCreated)
                _windAltitude = new (_height.dimension, windParams.SurfaceSamples, _height.size.xz, Allocator.Persistent);
            if (!_terrainEffect.IsCreated)
                _terrainEffect = new(_windAltitude, Allocator.Persistent);
            
            var wsf = new doubleF(_height, Allocator.TempJob);
            
            dependsOn = Wind.VenturiParallel(_wind, _height, ref windParams, dependsOn);
            dependsOn = Wind.TerrainDeflectionParallel(_wind, _height, ref windParams, dependsOn);
            dependsOn = Wind.WindEffectSurface(_wind, wsf, _height, _windAltitude,  _terrainEffect, ref windParams, dependsOn);

            return wsf.Dispose(dependsOn);
        }

        private void OnGUI()
        {
            var t = _simulation.simulationTime;
            var p = (int)floor(t);
            var n = min((int)ceil(t), _cloudTimeline.Length - 1);
            var f = frac(t);
            var style = new GUIStyle { fontSize = 20 };
            GUILayout.Label($"Simulation time: {t}", style);
            GUILayout.Label($"Temperature: {lerp(_tempTimeline[p], _tempTimeline[n], f)}", style);
            GUILayout.Label($"Cloud cover: {lerp(_cloudTimeline[p], _cloudTimeline[n], f)}", style);
            GUILayout.Label($"Precipitation: {lerp(_precipTimeline[p], _precipTimeline[n], f)}", style);
            GUILayout.Label($"Wind speed: {lerp(_windTimeline[p], _windTimeline[n], f)}", style);
        }

        private void OnDestroy()
        {
            runSim = false;
            _height.Dispose();
            _illumination.Dispose();
            _wind.Dispose();
            _windAltitude.Dispose();
            _terrainEffect.Dispose();
            _snow.Dispose();
            _flow.Dispose();
            _tempTimeline.Dispose();
            _cloudTimeline.Dispose();
            _windTimeline.Dispose();
            _precipTimeline.Dispose();
            _hazard.Dispose();
            _selectedPoints.Dispose();
            _highlightedPoints.Dispose();
        }
    }
}