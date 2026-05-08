using HPML;
using TFM.Components.Solvers;
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
    //[RequireComponent(typeof(Terrain), typeof(Sun), typeof(Weather))]
    public class SimulationController : MonoBehaviour, ISnowSimulation
    {
        [SerializeField] private float timeMultiplier = 1f;
        [SerializeField] private Material windMaterial;
        
        public doubleF height;
        public doubleF temperature;
        public doubleF windAltitude;
        public double3F wind;
        public double4F snow;
        public double4F flow;
        public NativeBitArray moving;

        public doubleF Heightfield => height;
        public double4F Snowfield => snow;

        private Events events = new (1337);
        private Weather _weather;

        private Mesh windMesh;
        private RenderParams rp;

        private Snow.Parameters _parameters = Snow.Parameters.Default;
        
        private async void Awake()
        {
            enabled = false;
            
            var sun = GetComponent<Sun>();
            var terrain = GetComponent<Terrain>();
            var fieldRenderer = GetComponent<FieldRenderer>();
            _weather = GetComponent<Weather>();

            var terrainSize = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * terrain.units;
            
            height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            temperature = new doubleF(height, Allocator.Persistent);
            snow = new double4F(height, Allocator.Persistent, 2);
            wind = new double3F(height, Allocator.Persistent, right() * 10);
            windAltitude = new doubleF(height, Allocator.Persistent);
            flow = new double4F(height, Allocator.Persistent);
            moving = new NativeBitArray(height.Length, Allocator.Persistent);
            
            _ = fieldRenderer.RegisterField(height, FieldRenderer.Name.Heightmap, default);
            
            var ilh = sun.Illumination(height, temperature, Allocator.Persistent, fieldRenderer, new JobHandle());
            ilh = new NormalizeTemperature { temperature = temperature }.Schedule(ilh);
            ilh = new ComputeTemperature(height, temperature).Schedule(temperature.Length, ilh);
            ilh = fieldRenderer.RegisterField(temperature, FieldRenderer.Name.CombinedLighting, ilh);
            
            doubleF vspeed = new doubleF(height, Allocator.Persistent);
            
            var wp = Wind.Parameters.Default;
            var wnd = Wind.Venturi(wind, height, ref wp, default);
            wnd = fieldRenderer.RegisterField(wind, FieldRenderer.Name.VenturiWind, wnd);
            wnd = Wind.TerrainDeflection(wind, height, ref wp, wnd);
            wnd = fieldRenderer.RegisterField(wind, FieldRenderer.Name.DeflectedWind, wnd);
            wnd = Wind.WindEffectSurface(wind, height, windAltitude, vspeed, ref wp, wnd);
            wnd = fieldRenderer.RegisterField(windAltitude, FieldRenderer.Name.WindAltitude, wnd);
            wnd = fieldRenderer.RegisterField(wind, FieldRenderer.Name.WindSurface, wnd);
            vspeed.Dispose(wnd);
            wnd = windAltitude.GenerateMesh(out var mda, wnd);

            var all = JobHandle.CombineDependencies(ilh, wnd);
            await all.WaitForComplete();
            all.Complete();

            windMesh = new Mesh();
            Mesh.ApplyAndDisposeWritableMeshData(mda, windMesh);
            windMesh.RecalculateBounds();
            rp = new RenderParams(windMaterial);

            enabled = true;
            Debug.Log("Complete");
        }

        private void Update()
        {
            _parameters.TempBase = 5;
            _parameters.SnowfallMinHeight = _parameters.TempBase * 100;
            var (ev, dt) = events.Step(Time.deltaTime * timeMultiplier);
            JobHandle jh = default;
            switch (ev)
            {
                case null:
                    return;
                case Events.Name.MeltStep:
                    jh = Snow.Melt(snow, temperature, height, dt.Value, ref _parameters, jh);
                    break;
                case Events.Name.TransportStep:
                    jh = Snow.Transport(snow, wind, windAltitude, height, dt.Value, ref _parameters, jh);
                    break;
                case Events.Name.DiffusionStep:
                    jh = Snow.Diffusion(snow, height, dt.Value, ref _parameters, jh);
                    break;
                case Events.Name.SnowfallStep:
                    jh = Snow.Snowfall(snow, height, temperature, dt.Value, ref _parameters, jh);
                    break;
                case Events.Name.SnowfallEnd:
                case Events.Name.SnowfallStart:
                    break;
            }
            jh.Complete();
        }

        private void OnDestroy()
        {
            height.Dispose();
            temperature.Dispose();
            windAltitude.Dispose();
            wind.Dispose();
            snow.Dispose();
            flow.Dispose();
            moving.Dispose();
        }

        [BurstCompile]
        private struct ComputeTemperature : IJobFor
        {
            [ReadOnly] public doubleF heightfield;
            public doubleF temperature;
            public const double kt = -0.01;
            public const double ki = 10;

            public ComputeTemperature(doubleF heightfield, doubleF temperature)
            {
                this.heightfield = heightfield;
                this.temperature = temperature;
            }
            
            public void Execute(int index)
            {
                var light = temperature[index];
                temperature[index] = heightfield[index] * kt + light * ki;
            }
        }

        [BurstCompile]
        private struct NormalizeTemperature : IJob
        {
            public doubleF temperature;
            
            public void Execute()
            {
                field.normalize(temperature);
            }
        }

        private struct Events
        {
            public enum Name
            {
                MeltStep,
                TransportStep,
                DiffusionStep,
                SnowfallStep,
                SnowfallStart,
                SnowfallEnd,
            }
            
            private float meltStep, transportStep, snowfallStep, snowfallStart, snowfallEnd, diffusionStep;
            private float meltStepDt, transportStepDt, snowfallStepDt, snowfallStartDt, snowfallEndDt, diffusionStepDt;
            
            private Random rng;
            
            private const float meltStepLambda = 0.5f;
            private const float transportStepLambda = 0.5f;
            private const float snowfallStepLambda = 1f;
            private const float snowfallStartLambda = 7f;
            private const float snowfallEndLambda = 3f;
            private const float diffusionStepLambda = 0.5f;

            public Events(uint seed)
            {
                rng = new Random(seed);
                meltStep = meltStepDt = Q(rng.NextFloat(), meltStepLambda);
                transportStep = transportStepDt = Q(rng.NextFloat(), transportStepLambda);
                snowfallStep = snowfallStepDt = float.PositiveInfinity;
                snowfallStart = snowfallStartDt = Q(rng.NextFloat(), snowfallStartLambda);
                snowfallEnd = snowfallEndDt = float.PositiveInfinity;
                diffusionStep = diffusionStepDt = Q(rng.NextFloat(), diffusionStepLambda);
            }
            
            public (Name?, float?) Step(float dt)
            {
                meltStep -= dt;
                transportStep -= dt;
                snowfallStep -= dt;
                snowfallStart -= dt;
                snowfallEnd -= dt;
                diffusionStep -= dt;

                if (meltStep <= 0)
                {
                    meltStep = meltStepDt = Q(rng.NextFloat(), meltStepLambda);
                    return (Name.MeltStep, meltStepDt);
                }

                if (transportStep <= 0)
                {
                    transportStep = transportStepDt = Q(rng.NextFloat(), transportStepLambda);
                    return (Name.TransportStep, transportStepDt);
                }

                if (diffusionStep <= 0)
                {
                    diffusionStep = diffusionStepDt = Q(rng.NextFloat(), diffusionStepLambda);
                    return (Name.DiffusionStep, diffusionStepDt);
                }

                if (snowfallStep <= 0)
                {
                    snowfallStep = snowfallStepDt = Q(rng.NextFloat(), snowfallStepLambda);
                    return (Name.SnowfallStep, snowfallStepDt);
                }

                if (snowfallStart <= 0)
                {
                    snowfallStart = snowfallStartDt = float.PositiveInfinity;
                    snowfallStep = snowfallStepDt = Q(rng.NextFloat(), snowfallStepLambda);
                    snowfallEnd = snowfallEndDt = Q(rng.NextFloat(), snowfallEndLambda);
                    return (Name.SnowfallStart, snowfallStartDt);
                }

                if (snowfallEnd <= 0)
                {
                    snowfallStart = snowfallStartDt = Q(rng.NextFloat(), snowfallStartLambda);
                    snowfallStep = snowfallStepDt = float.PositiveInfinity;
                    snowfallEnd = snowfallEndDt = float.PositiveInfinity;
                    return (Name.SnowfallEnd, snowfallEndDt);
                }

                return (null, null);
            }
            
            private static float Q(float p, float lambda) => -log(1 - p) / lambda;
        }
    }
}