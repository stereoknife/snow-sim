using System;
using System.Runtime.CompilerServices;
using HPML;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace TFM.Simulation
{
    public static class Wind
    {
        public struct Parameters
        {
            public double2 WindDirection;
            public double VenturiIntensity;     // m^-1
            public double DeflectionIntensity;  //
            public double SurfaceFalloff;       // m/s^2
            public int SurfaceMaxIterations;    //
            
            public int GaussianKernelSize;

            public int SurfaceSamples;
            public double SurfaceSpeedIncrement;

            public static Parameters Default => new()
            {
                WindDirection = double2(1, 0),
                VenturiIntensity = 0.001,
                DeflectionIntensity = 0.5,
                SurfaceFalloff = 0.7,
                SurfaceMaxIterations = 500,
                SurfaceSamples = 1,
                SurfaceSpeedIncrement = 10,
                GaussianKernelSize = 10,
            };

            public int Hash()
            {
                var hash = new HashCode();
                hash.Add(VenturiIntensity);
                hash.Add(DeflectionIntensity);
                hash.Add(SurfaceFalloff);
                hash.Add(SurfaceMaxIterations);
                hash.Add(SurfaceSamples);
                hash.Add(SurfaceSpeedIncrement);
                return hash.ToHashCode();
            }
        }
        
        #region Venturi
        
        public static void Venturi(double2F wind, doubleF height, ref Parameters P)
        {
            var job = new VenturiJob(wind, height, ref P);
            job.Run(wind.Length);
        }

        public static JobHandle Venturi(double2F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new VenturiJob(wind, height, ref P);
            return job.Schedule(wind.Length, dependsOn);
        }
        
        public static JobHandle VenturiParallel(in double2F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new VenturiJob(wind, height, ref P);
            return job.ScheduleParallel(wind.Length, 256, dependsOn);
        }

        private struct VenturiJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            [NativeDisableParallelForRestriction] public double2F wind;
            public double intensity;

            public VenturiJob(double2F wind, doubleF height, ref Parameters P)
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

        public static void TerrainDeflection(double2F wind, doubleF height, ref Parameters P)
        {
            var job = new TerrainDeflectionJob(wind, height, ref P);
            job.Run(height.Length);
        }

        public static JobHandle TerrainDeflection(double2F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TerrainDeflectionJob(wind, height, ref P);
            return job.Schedule(height.Length, dependsOn);
        }
        
        public static JobHandle TerrainDeflectionParallel(double2F wind, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TerrainDeflectionJob(wind, height, ref P);
            return job.ScheduleParallel(height.Length, 256, dependsOn);
        }
        
        private struct TerrainDeflectionJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            [NativeDisableParallelForRestriction] public double2F wind;
            public double intensity;

            public TerrainDeflectionJob(double2F wind, doubleF height, ref Parameters P)
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
                var nxzt = select(ccw, cw, dot(cw, wind[index]) > 0);
                wind[index] = wind[index] * (1 - length(nxz)) + intensity * length(wind[index]) * nxzt;
            }
        }
        
        #endregion

        #region Wind Effect Surface
        
        public static void WindEffectSurface(double2F wind, doubleF vspeed, doubleF height, doubleF altitude, double windSpeed, ref Parameters P)
        {
            for (int i = 0; i < wind.Length; i++)
            {
                altitude[i] = height[i];
                var w = wind[i];
                w.y = dot(w * windSpeed, field.gradient(height, i));
                wind[i] = w;
            }

            var job = new WindEffectSurfaceJob(wind, vspeed, height, height, altitude, windSpeed, 0, ref P);
            for (int i = 0; i < P.SurfaceMaxIterations * 2; i++)
            {
                job.iteration = i;
                job.Run(wind.Length);
            }
        }

        public static JobHandle WindEffectSurface(double2F wind, doubleF vspeed, doubleF height, ScalarField2D altitude,
            ScalarField2D terrain, ref Parameters P, JobHandle dependsOn)
        {
            var gaussian = new doubleF(height, Allocator.TempJob);
            var gaj = new GaussianJob
            {
                height = height,
                gaussian = gaussian,
                kernel = P.GaussianKernelSize / 2,
            };
            dependsOn = gaj.ScheduleParallel(height.Length, 64, dependsOn);
            
            for (int j = 0; j < P.SurfaceSamples; j++)
            {
                var windSpeed = (j + 1) * P.SurfaceSpeedIncrement;
                var altitudeLayer = altitude.Layer(j);
                var terrainEffectLayer = terrain.Layer(j);
                
                var ivj = new InitializeWESValuesJob(wind, vspeed, height, gaussian, altitudeLayer, windSpeed);
                dependsOn = ivj.Schedule(wind.Length, dependsOn);
            
                var wsj = new WindEffectSurfaceJob(wind, vspeed, height, gaussian, altitudeLayer, windSpeed, 0, ref P);
                for (int i = 0; i < P.SurfaceMaxIterations * 2; i++)
                {
                    wsj.iteration = i;
                    dependsOn = wsj.ScheduleParallel(wind.Length, 64, dependsOn);
                }

                var tej = new TerrainEffectJob(wind, vspeed, terrainEffectLayer, windSpeed);
                dependsOn = tej.Schedule(wind.Length, dependsOn);
            }

            dependsOn = gaussian.Dispose(dependsOn);

            return dependsOn;
        }
        
        [BurstCompile]
        private struct GaussianJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            public doubleF gaussian;
            public int kernel;
            
            public void Execute(int index)
            {
                double smooth = 0.005;
                int2 cell = height.cell(index);
                int2 start = clamp(cell - kernel, 0, height.dimension);
                int2 end = clamp(cell + kernel + 1, 0, height.dimension);
                double norm = 0, r = 0;
                
                for (int j = start.x; j < end.x; j++)
                {
                    for (int k = start.y; k < end.y; k++)
                    {
                        double2 d = (double2(j, k) - cell) * height.cellSize;
                        double w = exp(-lengthsq(d) * smooth);
                        norm += w;
                        r += height[j, k] * w;
                    }
                }

                gaussian[index] = r / norm;
            }
        }
        
        [BurstCompile]
        public struct InitializeWESValuesJob : IJobFor
        {
            [ReadOnly] public double2F wind;
            [ReadOnly] public doubleF height, gaussian;
            [WriteOnly] public doubleF altitude;
            [WriteOnly] public doubleF vspeed;
            public double speedMultiplier;

            public InitializeWESValuesJob(double2F wind, doubleF vspeed, doubleF height, doubleF gaussian, doubleF altitude, double speedMultiplier)
            {
                this.wind = wind;
                this.vspeed = vspeed;
                this.height = height;
                this.altitude = altitude;
                this.speedMultiplier = speedMultiplier;
                this.gaussian = gaussian;
            }
            
            public void Execute(int index)
            {
                altitude[index] = height[index];
                vspeed[index] = dot(wind[index] * speedMultiplier, field.gradient(gaussian, index));
            }
        }
        
        [BurstCompile]
        public struct WindEffectSurfaceJob : IJobFor
        {
            [ReadOnly] public double2F wind;
            [NativeDisableParallelForRestriction] public doubleF altitude, vspeed;
            [ReadOnly] public doubleF height, gaussian;
            public double windSpeed;
            public double falloff;
            public int iteration;

            public WindEffectSurfaceJob(double2F wind, doubleF vspeed, doubleF height, doubleF gaussian, doubleF altitude,
                double windSpeed, int iteration, ref Parameters P)
            {
                this.wind = wind;
                this.vspeed = vspeed;
                this.height = height;
                this.gaussian = gaussian;
                this.altitude = altitude;
                this.iteration = iteration;
                this.windSpeed = windSpeed;
                falloff = P.SurfaceFalloff;
            }
            
            public void Execute(int index)
            {
                if (((index + iteration) & 1) == 0) return;
                
                var cell = height.cell(index);
                
                var wdir = wind[cell] * windSpeed;
                var n = cell - (int2)sign(wdir);
                wdir = abs(wdir);
                
                if (any(n < 0) || any(n > wind.dimension - 1)) return;

                var alt_n = double2(altitude[n.x, cell.y], altitude[cell.x, n.y]);
                var spd_n = double2(vspeed[n.x, cell.y], vspeed[cell.x, n.y]);

                var spd = mad(-falloff, wind.cellSize.x, dot(wdir, spd_n)) / csum(wdir);
                var alt = mad(spd, wind.cellSize.x, dot(wdir, alt_n)) / csum(wdir);

                if (csum(wdir) < 0.001)
                    alt = -999999;
                
                if (alt <= height[index])
                {
                    var grad = (gaussian[index] - double2(gaussian[n.x, cell.y], gaussian[cell.x, n.y])) * gaussian.iCellSize;
                    spd = dot(wdir, grad);
                    alt = height[index];
                }
                
                altitude[index] = alt;
                vspeed[index] = spd;
            }
        }
        
        [BurstCompile]
        public struct TerrainEffectJob : IJobFor
        {
            [ReadOnly] public double2F wind;
            [ReadOnly] public doubleF vspeed;
            [WriteOnly] public doubleF terrainEffect;
            public double speedMultiplier;

            public TerrainEffectJob(double2F wind, doubleF vspeed, doubleF terrainEffect, double speedMultiplier)
            {
                this.wind = wind;
                this.vspeed = vspeed;
                this.terrainEffect = terrainEffect;
                this.speedMultiplier = speedMultiplier;
            }
            
            public void Execute(int index)
            {
                terrainEffect[index] = dot(wind[index] * speedMultiplier, field.gradient(vspeed, index));
            }
        }
    }
        
        #endregion
}