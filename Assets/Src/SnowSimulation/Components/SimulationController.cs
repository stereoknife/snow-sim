using System;
using System.Collections;
using System.IO;
using EasyButtons;
using HPML;
using TFM.Components.Solvers;
using TFM.Components.Visualization;
using Unity.Collections;
using UnityEngine;
using static Unity.Mathematics.math;
using TFM.Simulation;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace TFM.Components
{
    [RequireComponent(typeof(Terrain), typeof(Sun), typeof(Weather))]
    public class SimulationController : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private double4 initialSnowValue;
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
        
        private doubleF _height;
        private doubleF _temperature;
        private doubleF _windAltitude;
        private double3F _wind;
        private double4F _snow;
        private double4F _flow;

        public doubleF Heightfield => _height;
        public double4F Snowfield => _snow;

        private StochasticSimulation _simulation;

        [Button]
        private void RunFrames()
        {
            _simulation.Reset();
            StartCoroutine(RunFramesCR());
        }
        
        [Button]
        private void RunTime()
        {
            _simulation.Reset();
            StartCoroutine(RunTimeCR());
        }

        [Button]
        private void Reset()
        {
            for (int i = 0; i < _snow.Length; i++)
            {
                _snow[i] = initialSnowValue;
            }
        }
        
        [Button]
        private void ExportProfilingData()
        {
            _simulation.ExportProfilingData(Path.Combine(Application.dataPath, "results", $"{outputFile}.csv"));
            Debug.Log($"File written at {outputFile}.csv");
        }

        private IEnumerator RunFramesCR()
        {
            for (int i = 0; i < frames; i++)
            {
                _simulation.Step();
                yield return null;
            }
            Debug.Log("Done");
        }
        
        private IEnumerator RunTimeCR()
        {
            for (;;)
            {
                _simulation.Step();
                if (_simulation.simulationTime >= time) break;
                yield return null;
            }
            Debug.Log("Done");
        }

        private void SetSimulationParams()
        {
            _simulation.SetEventEnabled(StochasticSimulation.EventId.MeltStep, melting);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.TransportStep, windTransport);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.DiffusionStep, powderDiffusion);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.SnowfallStep, snowfall);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.SnowfallStart, snowfall);
            _simulation.SetEventEnabled(StochasticSimulation.EventId.SnowfallEnd, snowfall);
            
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
            var sun = GetComponent<Sun>();
            var terrain = GetComponent<Terrain>();
            var fieldRenderer = GetComponent<FieldRenderer>();

            var terrainSize = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * terrain.units;
            
            // Init fields
            _height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            _temperature = new doubleF(_height, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, initialSnowValue);
            _wind = new double3F(_height, Allocator.Persistent, double3(right() * 10));
            _flow = new double4F(_height, Allocator.Persistent);
            _windAltitude = new doubleF(_height, Allocator.Persistent);
            
            _ = fieldRenderer.RegisterField(_height, FieldRenderer.Name.Heightmap, default);
            
            // Illumination and temperature
            var ilh = sun.Illumination(_height, _temperature, Allocator.Persistent, fieldRenderer, new JobHandle());
            ilh = new NormalizeTemperature { Temperature = _temperature }.Schedule(ilh);
            ilh = new ComputeTemperature(_height, _temperature).Schedule(_temperature.Length, ilh);
            ilh = fieldRenderer.RegisterField(_temperature, FieldRenderer.Name.CombinedLighting, ilh);
            
            // Wind
            var wp = Wind.Parameters.Default;
            var wnd = Wind.Venturi(_wind, _height, ref wp, default);
            wnd = fieldRenderer.RegisterField(_wind, FieldRenderer.Name.VenturiWind, wnd);
            wnd = Wind.TerrainDeflection(_wind, _height, ref wp, wnd);
            wnd = fieldRenderer.RegisterField(_wind, FieldRenderer.Name.DeflectedWind, wnd);
            wnd = Wind.WindEffectSurface(_wind, _height, _windAltitude, ref wp, wnd);
            wnd = fieldRenderer.RegisterField(_windAltitude, FieldRenderer.Name.WindAltitude, wnd);
            wnd = fieldRenderer.RegisterField(_wind, FieldRenderer.Name.WindSurface, wnd);

            var all = JobHandle.CombineDependencies(ilh, wnd);
            all.Complete();

            var p = Snow.Parameters.Default;
            //p.TempBase = -15;
            //p.SnowfallMinHeight = -1500; // Temp base / inc per metre

            _simulation = new StochasticSimulation(1337)
            {
                snow = _snow,
                height = _height,
                temperature = _temperature,
                wind = _wind,
                windAltitude = _windAltitude,
                parameters = p,
            };
            
            SetSimulationParams();
        }

        private void OnDestroy()
        {
            _height.Dispose();
            _temperature.Dispose();
            _windAltitude.Dispose();
            _wind.Dispose();
            _snow.Dispose();
            _flow.Dispose();
        }

        [BurstCompile]
        private struct ComputeTemperature : IJobFor
        {
            [ReadOnly] public doubleF Heightfield;
            public doubleF Temperature;
            public const double Kt = -0.01;
            public const double Ki = 10;

            public ComputeTemperature(doubleF heightfield, doubleF temperature)
            {
                this.Heightfield = heightfield;
                this.Temperature = temperature;
            }
            
            public void Execute(int index)
            {
                var light = Temperature[index];
                Temperature[index] = Heightfield[index] * Kt + light * Ki;
            }
        }

        [BurstCompile]
        private struct NormalizeTemperature : IJob
        {
            public doubleF Temperature;
            
            public void Execute()
            {
                field.normalize(Temperature);
            }
        }
    }
}