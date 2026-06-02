using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HPML;
using TFM.Simulation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

using static Unity.Mathematics.math;

namespace TFM.Components.Solvers
{
    public class StochasticSimulation
    {
        public enum EventId
        {
            MeltStep,
            TransportStep,
            DiffusionStep,
            SnowfallStep,
            SnowfallStart,
            SnowfallEnd,
            AvalancheStep,
            AvalancheStart,
        }
        
        private Snow.Parameters _parameters;
        private doubleF _height;
        private doubleF _temperature;
        private double4F _snow;
        private double2F _wind;
        private ScalarField2D _windAltitude;
        private ScalarField2D _windTerrain;
        
        private NativeArray<double> _tempTimeline;
        private NativeArray<double> _windTimeline;
        private NativeArray<double> _cloudTimeline;
        private NativeArray<double> _precipTimeline;
        
        private NativeArray<double> _flow;
        private NativeArray<bool> _moving;
        private doubleF _hazard;
        
        public Snow.Parameters parameters { set => _parameters = value; }
        public doubleF height { set => _height = value; }
        public doubleF temperature { set => _temperature = value; }
        public double4F snow { set => _snow = value; }
        public double2F wind { set => _wind = value; }
        public ScalarField2D windAltitude { set => _windAltitude = value; }
        public ScalarField2D windTerrain { set => _windTerrain = value; }
        public NativeArray<double> tempTimeline { set => _tempTimeline = value; }
        public NativeArray<double> windTimeline { set => _windTimeline = value; }
        public NativeArray<double> cloudTimeline { set => _cloudTimeline = value; }
        public NativeArray<double> precipTimeline { set => _precipTimeline = value; }
        public NativeArray<double> flow { set => _flow = value; }
        public NativeArray<bool> moving { set => _moving = value; }
        public doubleF hazard {set => _hazard = value; }
        public float simulationTime { get; private set; }
        
        private Random _rng;
        private readonly uint _seed;
        
        private Dictionary<EventId, float> _periods;
        private Dictionary<EventId, float> _frequencies;
        private Dictionary<EventId, bool> _enabled;

        private bool _profilingEnabled;
        private (EventId, float, double)[] _profilingData;
        private int _profilingDataIndex;

        private bool _useTimeline = true;
        
        public StochasticSimulation(uint seed)
        {
            _seed = seed;
            _rng = new Random();
            _periods = new(8);
            _frequencies = new(8);
            _enabled = new(8);
            
            Reset();
        }

        public void Reset()
        {
            simulationTime = 0f;
            _rng.InitState(_seed);
            _periods[EventId.MeltStep] = 0.5f;
            _periods[EventId.TransportStep] = 0.5f;
            _periods[EventId.DiffusionStep] = 0.5f;
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
            if (_useTimeline) _frequencies[EventId.SnowfallStart] = 0f;
        }

        public void SetEventEnabled(EventId id, bool enabled)
        {
            _enabled[id] = enabled;
        }

        private EventId Next()
        {
            var totalProb = 0f;
            foreach (var id in _frequencies.Keys)
            {
                if (!_enabled[id]) continue;
                totalProb += _frequencies[id];
            }

            if (totalProb < 0.00001)
            {
                Debug.LogError("At least one simulation event must be enabled.");
                return (EventId)(-1);
            }
            
            simulationTime += 1f / totalProb;
            var random = _rng.NextFloat() * totalProb;

            totalProb = 0f;
            foreach (var id in _frequencies.Keys)
            {
                if (!_enabled[id]) continue;
                totalProb += _frequencies[id];
                if (totalProb >= random) return id;
            }
            return (EventId)(-1);
        }
        
        public void Step()
        {
            if (_useTimeline)
            {
                if (simulationTime > _tempTimeline.Length) return;
                
                var low = (int)floor(simulationTime);
                var high = (int)ceil(simulationTime);
                var t = frac(simulationTime);
                _parameters.TempBase = lerp(_tempTimeline[low], _tempTimeline[high], t);
                _parameters.WindSpeed = lerp(_windTimeline[low], _windTimeline[high], t);
                _parameters.CloudCover = lerp(_cloudTimeline[low], _cloudTimeline[high], t);
                _parameters.SnowfallStrength = lerp(_precipTimeline[low], _precipTimeline[high], t);

                if (_parameters.SnowfallStrength > 0.0000000001)
                    _frequencies[EventId.SnowfallStep] = 1f / _periods[EventId.SnowfallStep];
                else
                    _frequencies[EventId.SnowfallStep] = 0f;
            }
            
            var ev = Next();
            var jh = new JobHandle();
            var preEventTime = DateTime.Now;
            switch (ev)
            {
                case EventId.MeltStep:
                    jh = Snow.Melt(_snow, _temperature, _height, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.TransportStep:
                    jh = Snow.Transport(_snow, _wind, _windAltitude, _windTerrain, _height, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.DiffusionStep:
                    jh = Snow.Diffusion(_snow, _height, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.SnowfallStep:
                    jh = Snow.Snowfall(_snow, _height, _temperature, _periods[ev], ref _parameters, jh);
                    break;
                case EventId.SnowfallStart:
                    _frequencies[EventId.SnowfallStep] = 1f / _periods[EventId.SnowfallStep];
                    _frequencies[EventId.SnowfallEnd] = 1f / _periods[EventId.SnowfallEnd];
                    _frequencies[EventId.SnowfallStart] = 0;
                    break;
                case EventId.SnowfallEnd:
                    jh = Snow.Snowfall(_snow, _height, _temperature, _periods[ev], ref _parameters, jh);
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
                _profilingData[_profilingDataIndex] = (ev, simulationTime, (postEventTime - preEventTime).TotalMilliseconds);
                _profilingDataIndex = (_profilingDataIndex + 1) % _profilingData.Length;
            }
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
                    cell = i; break;
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
    }
}
