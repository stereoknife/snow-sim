using System;
using System.Collections.Generic;
using System.IO;
using HPML;
using TFM.Simulation;
using Unity.Jobs;
using UnityEngine;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

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
        }
        
        private Snow.Parameters _parameters;
        private doubleF _height;
        private doubleF _temperature;
        private double4F _snow;
        private double3F _wind;
        private doubleF _windAltitude;
        
        public Snow.Parameters parameters { set => _parameters = value; }
        public doubleF height { set => _height = value; }
        public doubleF temperature { set => _temperature = value; }
        public double4F snow { set => _snow = value; }
        public double3F wind { set => _wind = value; }
        public doubleF windAltitude { set => _windAltitude = value; }
        public float simulationTime { get; private set; }
        
        private Random _rng;
        private readonly uint _seed;
        
        private const float MeltStepLambda = 0.5f;
        private const float TransportStepLambda = 0.5f;
        private const float DiffusionStepLambda = 0.5f;
        private const float SnowfallStepLambda = 0.5f;
        private const float SnowfallStartLambda = 7f;
        private const float SnowfallEndLambda = 5f;
        private const float AvalancheStartLambda = 2f;
        private const float AvalancheStepLambda = 0.01f;

        private float[] _eventPeriods, _eventFreqs;
        private bool[] _enabledEvents;

        private bool _profilingEnabled;
        private (EventId, float, double)[] _profilingData;
        private int _profilingDataIndex;
        
        public StochasticSimulation(uint seed)
        {
            _seed = seed;
            _rng = new Random();
            _eventPeriods = new float[6];
            _eventFreqs = new float[6];
            _enabledEvents = new bool[6];
            for (int i = 0; i < _enabledEvents.Length; i++)
            {
                _enabledEvents[i] = true;
            }
            Reset();
        }

        public void Reset()
        {
            simulationTime = 0f;
            _rng.InitState(_seed);
            _eventPeriods[(int)EventId.MeltStep] = MeltStepLambda;
            _eventPeriods[(int)EventId.TransportStep] = TransportStepLambda;
            _eventPeriods[(int)EventId.DiffusionStep] = DiffusionStepLambda;
            _eventPeriods[(int)EventId.SnowfallStep] = SnowfallStepLambda;
            _eventPeriods[(int)EventId.SnowfallStart] = SnowfallStartLambda;
            _eventPeriods[(int)EventId.SnowfallEnd] = SnowfallEndLambda;

            for (int i = 0; i < _eventPeriods.Length; i++)
            {
                _eventFreqs[i] = 1 / _eventPeriods[i];
            }

            _eventFreqs[(int)EventId.SnowfallStep] = 0f;
            _eventFreqs[(int)EventId.SnowfallEnd] = 0f;
        }

        public void SetEventEnabled(EventId ev, bool enabled)
        {
            _enabledEvents[(int)ev] = enabled;
        }
        
        public void Step()
        {
            var totalProb = 0f;
            for (var i = 0; i < _eventFreqs.Length; i++)
            {
                if (!_enabledEvents[i]) continue;
                var freq = _eventFreqs[i];
                totalProb += freq;
            }

            var dt = 1 / totalProb;
            var prob = _rng.NextFloat() * totalProb;
            var ev = (EventId)(-1);
            totalProb = 0f;
            for (int i = 0; i < _eventFreqs.Length; i++)
            {
                if (!_enabledEvents[i]) continue;
                totalProb += _eventFreqs[i];
                if (totalProb >= prob)
                {
                    ev = (EventId)i;
                    break;
                }
            }

            var jh = new JobHandle();
            var preEventTime = DateTime.Now;
            switch (ev)
            {
                case EventId.MeltStep:
                    jh = Snow.Melt(_snow, _temperature, _height, MeltStepLambda, ref _parameters, jh);
                    break;
                case EventId.TransportStep:
                    jh = Snow.Transport(_snow, _wind, _windAltitude, _height, TransportStepLambda, ref _parameters, jh);
                    break;
                case EventId.DiffusionStep:
                    jh = Snow.Diffusion(_snow, _height, DiffusionStepLambda, ref _parameters, jh);
                    break;
                case EventId.SnowfallStep:
                    jh = Snow.Snowfall(_snow, _height, _temperature, SnowfallStepLambda, ref _parameters, jh);
                    break;
                case EventId.SnowfallStart:
                    _eventFreqs[(int)EventId.SnowfallStep] = 1f / _eventPeriods[(int)EventId.SnowfallStep];
                    _eventFreqs[(int)EventId.SnowfallEnd] = 1f / _eventPeriods[(int)EventId.SnowfallEnd];
                    _eventFreqs[(int)EventId.SnowfallStart] = 0;
                    break;
                case EventId.SnowfallEnd:
                    jh = Snow.Snowfall(_snow, _height, _temperature, SnowfallStepLambda, ref _parameters, jh);
                    _eventFreqs[(int)EventId.SnowfallStep] = 0;
                    _eventFreqs[(int)EventId.SnowfallEnd] = 0;
                    _eventFreqs[(int)EventId.SnowfallStart] = 1f / _eventPeriods[(int)EventId.SnowfallStart];
                    break;
                case (EventId)(-1):
                    Debug.LogWarning("No event enabled in the simulation");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            jh.Complete();
            var postEventTime = DateTime.Now;

            if (_profilingEnabled)
            {
                _profilingData[_profilingDataIndex] = (ev, simulationTime, (postEventTime - preEventTime).TotalMilliseconds);
                _profilingDataIndex = (_profilingDataIndex + 1) % _profilingData.Length;
            }

            simulationTime += dt;
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

            //totalDuration /= _profilingData.Length;
            //writer.WriteLine($", , , {totalDuration}");
        }
    }
}
