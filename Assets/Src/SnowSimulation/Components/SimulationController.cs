using System;
using HPML;
using TFM.Components.Solvers;
using TFM.Components.Visualization;
using Unity.Collections;
using UnityEngine;
using Utils;
using Random = Unity.Mathematics.Random;
using static Unity.Mathematics.math;
using TFM.Simulation;
using TFM.Utils;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace TFM.Components
{
    [RequireComponent(typeof(Terrain), typeof(Sun), typeof(Weather))]
    public class SimulationController : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private float timeMultiplier = 1f;
        [SerializeField] private bool fastForward;
        
        private doubleF _height;
        private doubleF _temperature;
        private doubleF _windAltitude;
        private double3F _wind;
        private double4F _snow;
        private double4F _flow;

        public doubleF Heightfield => _height;
        public double4F Snowfield => _snow;

        private MonolithicSimulation _simulation;
        
        private void Awake()
        {
            var sun = GetComponent<Sun>();
            var terrain = GetComponent<Terrain>();
            var fieldRenderer = GetComponent<FieldRenderer>();

            var terrainSize = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * terrain.units;
            
            // Init fields
            _height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            _temperature = new doubleF(_height, Allocator.Persistent);
            _snow = new double4F(_height, Allocator.Persistent, 0);
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

            _simulation = new MonolithicSimulation(1337)
            {
                snow = _snow,
                height = _height,
                temperature = _temperature,
                wind = _wind,
                windAltitude = _windAltitude,
                parameters = Snow.Parameters.Default,
            };
        }

        private void Update()
        {
            if (fastForward)
                _simulation.FastForward();
            else
                _simulation.Step(Time.deltaTime * timeMultiplier);
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