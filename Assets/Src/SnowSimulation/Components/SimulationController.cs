using HPML;
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
    public class SimulationController : MonoBehaviour
    {
        [SerializeField] private float timeMultiplier = 1f;
        [SerializeField] private Material windMaterial;
        
        public doubleF height;
        public doubleF temperature;
        public double3F wind;
        public double4F snow;

        private Events events = new (1337);
        private Weather _weather;

        private Snow.Parameters _parameters = Snow.Parameters.Default;
        
        private async void Awake()
        {
            enabled = false;
            var sun = GetComponent<Sun>();
            var terrain = GetComponent<Terrain>();
            var fieldRenderer = GetComponent<FieldRenderer>();
            _weather = GetComponent<Weather>();

            var terrainSize = double3(terrain.sizeX, terrain.height, terrain.sizeZ) * 1000;
            height = doubleF.FromTexture(terrain.heightmap, terrainSize, Allocator.Persistent);
            temperature = new doubleF(height, Allocator.Persistent);
            snow = new double4F(height, Allocator.Persistent, 0.01);
            wind = new double3F(height, Allocator.Persistent, right() * 0.01f);
            _ = fieldRenderer.RegisterField(height, FieldRenderer.Name.Heightmap, default);
            
            doubleF dlf = new doubleF(height, Allocator.Persistent);
            doubleF aef = new doubleF(height, Allocator.Persistent);
            doubleF ilf = new doubleF(height, Allocator.Persistent);

            // Compute maps
            JobHandle dlj = sun.DirectLighting(height, dlf, default);
            JobHandle aej = sun.AmbientLighting(height, aef, default);
            JobHandle ilj = sun.IndirectLightingParallel(height, dlf, ilf, dlj);

            var wp = Wind.Parameters.Default;
            JobHandle wnd = Wind.Venturi(wind, height, ref wp, default);
            wnd = fieldRenderer.RegisterField(wind, FieldRenderer.Name.VenturiWind, wnd);
            wnd = Wind.TerrainDeflection(wind, height, ref wp, wnd);
            wnd = fieldRenderer.RegisterField(wind, FieldRenderer.Name.DeflectedWind, wnd);
            doubleF altitude = new doubleF(height, Allocator.Persistent);
            doubleF vspeed = new doubleF(height, Allocator.Persistent);
            wnd = Wind.WindEffectSurface(wind, height, altitude, vspeed, ref wp, wnd);
            wnd = fieldRenderer.RegisterField(altitude, FieldRenderer.Name.WindAltitude, wnd);
            wnd = fieldRenderer.RegisterField(wind, FieldRenderer.Name.WindSurface, wnd);

            // Export to texture
            dlj = fieldRenderer.RegisterField(dlf, FieldRenderer.Name.DirectLighting, dlj);
            aej = fieldRenderer.RegisterField(aef, FieldRenderer.Name.AmbientLighting, aej);
            ilj = fieldRenderer.RegisterField(ilf, FieldRenderer.Name.IndirectLighting, ilj);

            JobHandle illum = JobHandle.CombineDependencies(dlj, aej, ilj);
            illum = new CombineFields(dlf, aef, ilf, temperature).Schedule(temperature.Length, illum);
            illum = new NormalizeTemperature { temperature = temperature }.Schedule(illum);
            illum = new ComputeTemperature(height, temperature).Schedule(temperature.Length, illum);

            illum = fieldRenderer.RegisterField(temperature, FieldRenderer.Name.CombinedLighting, illum);

            wnd = altitude.GenerateMesh(out Mesh.MeshDataArray mda, wnd);

            dlf.Dispose(illum);
            aef.Dispose(illum);
            ilf.Dispose(illum);
            //altitude.Dispose(wnd);
            vspeed.Dispose(wnd);

            illum = JobHandle.CombineDependencies(illum, wnd);
            await illum.WaitForComplete();
            illum.Complete();
            
            enabled = true;
            Debug.Log("Complete");
        }

        private void Update()
        {
            _parameters.TempBase = _weather.Temperature;
            _parameters.SnowfallMinHeight = _weather.Altitude0C;
            var (ev, dt) = events.Step(Time.deltaTime * timeMultiplier);
            JobHandle jh = default;
            switch (ev)
            {
                case null:
                    return;
                case Events.Name.MeltStep:
                    //Debug.Log($"Melt at {Time.frameCount}");
                    jh = Snow.Melt(snow, temperature, height, dt.Value, ref _parameters, jh);
                    break;
                case Events.Name.TransportStep:
                    break;
                case Events.Name.DifussionStep:
                    //Debug.Log($"Diffusion at {Time.frameCount}");
                    jh = Snow.Diffusion(snow, height, dt.Value, ref _parameters, jh);
                    break;
                case Events.Name.SnowfallStep:
                    //Debug.Log($"Snowfall at {Time.frameCount}");
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
            temperature.Dispose();
            snow.Dispose();
        }

        [BurstCompile]
        private struct AsembleInstances : IJobFor
        {
            [ReadOnly] public doubleF height;
            public NativeArray<Matrix4x4> instances;
            
            public void Execute(int index)
            {
                var cell = height.cell(index);
                var h = (float)height[index];
                var cs = (float2)height.cellSize;
                instances[index] = Matrix4x4.TRS(
                    float3(cell *cs + cs / 2, h / 2).xzy,
                    Quaternion.identity,
                    float3(cs, h).xzy
                );
            }
        }
        
        [BurstCompile]
        private struct CombineFields : IJobFor
        {
            [ReadOnly] public doubleF a;
            [ReadOnly] public doubleF b;
            [ReadOnly] public doubleF c;
            public doubleF result;

            public CombineFields(doubleF a, doubleF b, doubleF c, doubleF result)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                this.result = result;
            }
            
            public void Execute(int index)
            {
                result[index] = a[index] + b[index] + c[index];
            }
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
                temperature[index] = heightfield[index] * 1000 * kt + light * ki;
            }
        }

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
                DifussionStep,
                SnowfallStep,
                SnowfallStart,
                SnowfallEnd,
            }
            
            private float meltStep, transportStep, snowfallStep, snowfallStart, snowfallEnd, diffusionStep;
            private float meltStepDt, transportStepDt, snowfallStepDt, snowfallStartDt, snowfallEndDt, diffusionStepDt;
            
            private Random rng;
            
            private const float meltStepLambda = 0.5f;
            private const float snowfallStepLambda = 1f;
            private const float snowfallStartLambda = 7f;
            private const float snowfallEndLambda = 3f;
            private const float diffusionStepLambda = 0.5f;

            public Events(uint seed)
            {
                rng = new Random(seed);
                meltStep = meltStepDt = Q(rng.NextFloat(), meltStepLambda);
                transportStep = transportStepDt = float.PositiveInfinity;
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
                    transportStep = transportStepDt = float.PositiveInfinity;
                    return (Name.TransportStep, transportStepDt);
                }

                if (diffusionStep <= 0)
                {
                    diffusionStep = diffusionStepDt = Q(rng.NextFloat(), diffusionStepLambda);
                    return (Name.DifussionStep, diffusionStepDt);
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