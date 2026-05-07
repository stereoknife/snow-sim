using System.Runtime.CompilerServices;
using HPML;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace TFM.Simulation
{
    public static class Wind
    {
        public struct Parameters
        {
            public double VenturiIntensity;     // m^-1
            public double DeflectionIntensity;  //
            public double SurfaceFalloff;       // m/s^2
            public int SurfaceMaxIterations;    // 

            public static Parameters Default => new()
            {
                VenturiIntensity = 0.001,
                DeflectionIntensity = 0.5,
                SurfaceFalloff = 0.7,
                SurfaceMaxIterations = 500,
            };
        }
        
        #region Venturi
        
        public static void Venturi(double3F wind, doubleF height, ref Parameters P)
        {
            var job = new VenturiJob(wind, height, ref P);
            job.Run(wind.Length);
        }

        public static JobHandle Venturi(double3F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new VenturiJob(wind, height, ref P);
            return job.Schedule(wind.Length, dependsOn);
        }
        
        public static JobHandle VenturiParallel(in double3F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new VenturiJob(wind, height, ref P);
            return job.ScheduleParallel(wind.Length, 256, dependsOn);
        }

        private struct VenturiJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            [NativeDisableParallelForRestriction] public double3F wind;
            public double intensity;

            public VenturiJob(double3F wind, doubleF height, ref Parameters P)
            {
                this.wind = wind;
                this.height = height;
                intensity = P.VenturiIntensity;
            }
            
            public void Execute(int index)
            {
                wind[index] *= 1 + intensity * height[index];
            }
        }
        
        #endregion
        
        #region Terrain Deflection

        public static void TerrainDeflection(double3F wind, doubleF height, ref Parameters P)
        {
            var job = new TerrainDeflectionJob(wind, height, ref P);
            job.Run(height.Length);
        }

        public static JobHandle TerrainDeflection(double3F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TerrainDeflectionJob(wind, height, ref P);
            return job.Schedule(height.Length, dependsOn);
        }
        
        public static JobHandle TerrainDeflectionParallel(double3F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TerrainDeflectionJob(wind, height, ref P);
            return job.ScheduleParallel(height.Length, 256, dependsOn);
        }
        
        private struct TerrainDeflectionJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            [NativeDisableParallelForRestriction] public double3F wind;
            public double intensity;

            public TerrainDeflectionJob(double3F wind, doubleF height, ref Parameters P)
            {
                this.wind = wind;
                this.height = height;
                intensity = P.DeflectionIntensity;
            }
            
            public void Execute(int index)
            {
                var normal = field.normal(height, index);
                var nxz = normal.xz;
                var cw = vec.cw(nxz);
                var ccw = vec.ccw(nxz);
                var nxzt = select(ccw, cw, dot(cw, wind[index].xz) > 0);
                wind[index] = wind[index] * (1 - length(nxz)) + intensity * length(wind[index]) * double3(nxzt, 0).xzy;
            }
        }
        
        #endregion

        #region Wind Effect Surface
        
        public static void WindEffectSurface(double3F wind, doubleF height, doubleF altitude, doubleF vspeed, ref Parameters P)
        {
            for (int i = 0; i < wind.Length; i++)
            {
                altitude[i] = height[i];
                vspeed[i] = dot(wind[i].xz, field.gradient(height, i));
            }

            var job = new WindEffectSurfaceJob(wind, height, height, altitude, vspeed, 0, ref P);
            for (int i = 0; i < P.SurfaceMaxIterations * 2; i++)
            {
                job.iteration = i;
                job.Run(wind.Length);
            }

            for (int i = 0; i < wind.Length; i++)
            {
                var w = wind[i];
                w.y = vspeed[i];
                wind[i] = w;
            }
        }

        public static JobHandle WindEffectSurface(double3F wind, doubleF height, doubleF altitude,
            doubleF vspeed, ref Parameters P, JobHandle dependsOn)
        {
            var init = new InitializeWESValuesJob(wind, height, altitude, vspeed);
            dependsOn = init.Schedule(wind.Length, dependsOn);
            
            var job = new WindEffectSurfaceJob(wind, height, height, altitude, vspeed, 0, ref P);
            for (int i = 0; i < P.SurfaceMaxIterations * 2; i++)
            {
                job.iteration = i;
                dependsOn = job.ScheduleParallel(wind.Length, 64, dependsOn);
            }

            var finalize = new FinalizeWESJob(wind, vspeed);
            dependsOn = finalize.Schedule(wind.Length, dependsOn);

            return dependsOn;
        }
        
        [BurstCompile]
        public struct InitializeWESValuesJob : IJobFor
        {
            [ReadOnly] public double3F wind;
            [ReadOnly] public doubleF height, gaussian;
            public doubleF altitude, vspeed;

            public InitializeWESValuesJob(double3F wind, doubleF height, doubleF altitude, doubleF vspeed)
            {
                this.wind = wind;
                this.height = height;
                this.altitude = altitude;
                this.vspeed = vspeed;
                gaussian = height;
            }
            
            public void Execute(int index)
            {
                altitude[index] = height[index];
                vspeed[index] = dot(wind[index].xz, field.gradient(gaussian, index));
                if (isnan(vspeed[index]))
                {
                    var a = 0;
                }
            }
        }
        
        [BurstCompile]
        public struct FinalizeWESJob : IJobFor
        {
            public double3F wind;
            [ReadOnly] public doubleF vspeed;

            public FinalizeWESJob(double3F wind, doubleF vspeed)
            {
                this.wind = wind;
                this.vspeed = vspeed;
            }
            
            public void Execute(int index)
            {
                var w = wind[index];
                w.y = vspeed[index];
                wind[index] = w;
            }
        }
        
        [BurstCompile]
        public struct WindEffectSurfaceJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public double3F wind;
            [NativeDisableParallelForRestriction] public doubleF altitude;
            [NativeDisableParallelForRestriction] public doubleF vspeed;
            [ReadOnly] public doubleF height, gaussian;
            public double falloff;
            public int iteration;

            public WindEffectSurfaceJob(double3F wind, doubleF height, doubleF gaussian, doubleF altitude,
                doubleF vspeed, int iteration, ref Parameters P)
            {
                this.wind = wind;
                this.height = height;
                this.gaussian = gaussian;
                this.altitude = altitude;
                this.vspeed = vspeed;
                this.iteration = iteration;
                falloff = P.SurfaceFalloff;
            }
            
            public void Execute(int index)
            {
                if (((index + iteration) & 1) == 0) return;
                
                var cell = height.cell(index);
                
                var w = wind[cell].xz;
                var n = cell - (int2)sign(w);
                w = abs(w);
                
                if (any(n < 0) || any(n > wind.dimension - 1)) return;

                var alt_n = double2(altitude[n.x, cell.y], altitude[cell.x, n.y]);
                var spd_n = double2(vspeed[n.x, cell.y], vspeed[cell.x, n.y]);

                var spd = mad(-falloff, wind.cellSize.x, dot(w, spd_n)) / csum(w);
                var alt = mad(spd, wind.cellSize.x, dot(w, alt_n)) / csum(w);

                if (csum(w) < 0.001)
                    alt = -999999;
                
                if (alt <= height[index])
                {
                    var grad = (gaussian[index] - double2(gaussian[n.x, cell.y], gaussian[cell.x, n.y])) * gaussian.iCellSize;
                    spd = dot(w, grad);
                    alt = height[index];
                }

                if (alt - height[index] > 100)
                {
                    var a = 0;
                } 
                altitude[index] = alt;
                vspeed[index] = spd;
            }
        }
    }
        
        #endregion
}