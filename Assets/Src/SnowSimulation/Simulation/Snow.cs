using System.Runtime.CompilerServices;
using HPML;
using TFM.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace TFM.Simulation
{
    public static class Snow
    {
        public struct Parameters
        {
            // Snowfall
            public double SnowfallMinHeight;        // m
            public double SnowfallStrength;         // day⁻¹
            public double SnowfallMax;              // m
            public double SnowfallPowderRatio;      // ratio
            public double SnowfallUnstableRatio;    // ratio
            public double CriticalSlopeMin;         // ratio
            public double CriticalSlopeTempFactor;  // °C⁻¹
            public double CriticalSlopeMaxTemp;     // °C
            
            // Temp
            public double TempBase;                 // °C
            
            // Melt
            public double MeltRate;                 // m / °C
            public double MeltTemp;                 // °C
            
            // Stability
            public double StabilityStableTemp;          // °C
            public double StabilityUnstableTemp;        // °C
            public double StabilityFreezeTemp;          // °C
            public double StabilityHot;                 // m/day
            public double StabilityMedium;              // m/day
            public double StabilityFreeze;              // m/day
            public double StabilityCompactionPressure;  // ?
            public double StabilityMinSlope;            // ratio
            
            // Diffusion
            public double DiffusionRestSlope;
            public double DiffusionRate;
            
            // Wind
            public double WindPlates;

            public static Parameters Default => new()
            {
                TempBase = 0,
                SnowfallMinHeight = 0,
                
                SnowfallStrength = 0.001,
                SnowfallMax = 1000,
                SnowfallPowderRatio = 5,
                SnowfallUnstableRatio = 1,
                CriticalSlopeMin = 0.5,
                CriticalSlopeTempFactor = 0.05,
                CriticalSlopeMaxTemp = -10,
                MeltRate = 0.01,
                MeltTemp = 0,
                StabilityMinSlope = 0.3,
                StabilityStableTemp = -5,
                StabilityUnstableTemp = 5,
                StabilityFreezeTemp = -20,
                StabilityHot = 0.0001,
                StabilityMedium = 0.0001,
                StabilityFreeze = 0,
                StabilityCompactionPressure = 0.001,
                DiffusionRestSlope = 0.5,
                DiffusionRate = 0.5,
                WindPlates = 0.1,
            };
        }
        
        #region Snowfall

        public static void Snowfall(double4F snow, doubleF height, doubleF temperature, double step, ref Parameters P)
            => new SnowfallJob(snow, height, temperature, step, ref P)
                .Run(snow.Length);
        
        public static JobHandle Snowfall(double4F snow, doubleF height, doubleF temperature, double step, ref Parameters P, JobHandle dependsOn)
            => new SnowfallJob(snow, height, temperature, step, ref P)
                .ScheduleParallel(snow.Length, 512, dependsOn);

        [BurstCompile]
        private struct SnowfallJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public double4F snow;
            [ReadOnly] public doubleF height, temperature;
            public double step;
            public double sfPerDay, sfMinHeight, sfMax, sfPowderRatio, sfUnstableRatio;
            public double csMin, csTempFactor, csMaxTemp;
            public double stabMinSlope, baseTemp;

            public SnowfallJob(double4F snow, doubleF height, doubleF temperature, double step, ref Parameters P)
            {
                this.snow = snow;
                this.height = height;
                this.temperature = temperature;
                this.step = step;
                sfPerDay = P.SnowfallStrength;
                sfMinHeight = P.SnowfallMinHeight;
                sfMax = P.SnowfallMax;
                sfPowderRatio = P.SnowfallPowderRatio;
                sfUnstableRatio = P.SnowfallUnstableRatio;
                csMin = P.CriticalSlopeMin;
                csTempFactor = P.CriticalSlopeTempFactor;
                csMaxTemp = P.CriticalSlopeMaxTemp;
                stabMinSlope = P.StabilityMinSlope;
                baseTemp = P.TempBase;
            }
            
            public void Execute(int index)
            {
                var slope = cmax(field.gradient(height, index));
                
                // Get amount of snow that has fallen on this cell
                var airTemp = (height[index] - sfMinHeight) * 0.01;
                var dd = sfPerDay * clamp(airTemp, 0d, sfMax);
                // Get critical slope
                var cs = csMin + csTempFactor * max(0d, csMaxTemp - temperature[index] - baseTemp);
                
                // Ratio of powder snow
                var xpow = saturate(sfPowderRatio - cs);
                // Ratio of unstable snow
                var xuns = clamp((slope - stabMinSlope) * sfUnstableRatio, 0d, 1d - xpow);
                // Ratio of stable snow
                var xstb = 1d - xpow - xuns;
                
                snow[index] += double4(0d, xstb, xuns, xpow) * dd * step;
            }
        }

        #endregion


        #region Melt
        
        public static void Melt(double4F snow, doubleF temperature, doubleF height, double step, ref Parameters P)
            => new MeltJob(snow, temperature, height, step, ref P)
                .Run(snow.Length);

        public static JobHandle Melt(double4F snow, doubleF temperature, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
            => new MeltJob(snow, temperature, height, step, ref P)
                .ScheduleParallel(snow.Length, 256, dependsOn);
        
        [BurstCompile]
        private struct MeltJob : IJobFor
        {
            [NativeDisableParallelForRestriction] double4F snowF;
            [ReadOnly] doubleF tempF, heightF;
            private double step, tempBase, meltRate, meltTemp, meltCompactionEffect;
            private double stabStableTemp, stabUnstableTemp, stabFreezeTemp, stabHot, stabMedium, stabFreeze,
                stabCompactionPressure, stabMinSlope, unstableFactor;

            public MeltJob(double4F snowF, doubleF tempF, doubleF heightF, double step, ref Parameters P)
            {
                this.snowF = snowF;
                this.tempF = tempF;
                this.heightF = heightF;
                this.step = step;
                tempBase = P.TempBase;
                meltRate = P.MeltRate;
                meltTemp = P.MeltTemp;
                stabStableTemp = P.StabilityStableTemp;
                stabUnstableTemp = P.StabilityUnstableTemp;
                stabFreezeTemp = P.StabilityFreezeTemp;
                stabHot = P.StabilityHot;
                stabMedium = P.StabilityMedium;
                stabFreeze = P.StabilityFreeze;
                stabCompactionPressure = P.StabilityCompactionPressure;
                meltCompactionEffect = 0;
                stabMinSlope = P.StabilityMinSlope;
                unstableFactor = P.SnowfallUnstableRatio;
            }
            
            public void Execute(int index)
            {
                var temperature = tempF[index] + tempBase;
                var snow = snowF[index];
                var pressure = csum(snow.yzw);
                var slope = cmax(field.slope(heightF, snowF, index));
                slope = max(0, slope - stabMinSlope) * unstableFactor;
                
                double stability;
                if (temperature > stabStableTemp)
                {
                    stability = remap(stabUnstableTemp, stabStableTemp, -stabHot, stabMedium, temperature);
                    stability = clamp(stability, -stabHot, stabMedium);
                }
                else
                {
                    stability = remap(stabStableTemp, stabFreeze, stabMedium, stabFreeze, temperature);
                    stability = clamp(stability, stabFreeze, stabMedium);
                }

                stability *= step;
                if (stability < 0)
                    stability *= slope;
                else if (slope > 1)
                    stability /= slope;

                // Melting
                var melt = -step * meltRate * max(0, temperature - meltTemp);
                var stable = csum(snow.xy);
                var compaction = select(snow.x / stable, 0, stable < 0.0001);
                snow.w += melt; // powder
                snow.z += min(0, snow.w); // unstable
                snow.y += min(0, snow.z /* * (1 - compaction) * meltCompactionEffect*/); // stable
                snow.x += min(0, snow.y);
                snow = select(snow, 0, snow < 0.001);
                
                // Stability
                
                // Stability will at most eliminate all stable or unstable snow
                stability = clamp(stability, -csum(snow.xy), snow.z);
                var total = csum(snow.xyz);
                
                snow.z -= stability;
                snow.x = clamp(snow.x + pressure * stabCompactionPressure, 0,total - snow.z);
                snow.y = total - snow.z - snow.x;
                snow = select(snow, 0, snow < 0.001);
                
                snowF[index] = snow;
            }
        }
        
        #endregion

        
        #region Diffusion
        
        public static void Diffusion(double4F snow, doubleF height, double step, ref Parameters P)
        {
            var job = new DiffusionJob(snow, height, step, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                job.Run(snow.Length);
            }
        }

        public static JobHandle Diffusion(double4F snow, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
        {
            var job = new DiffusionJob(snow, height, step, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                dependsOn = job.ScheduleParallel(snow.Length, 256, dependsOn);
            }

            return dependsOn;
        }

        [BurstCompile]
        private struct DiffusionJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public double4F snow;
            [ReadOnly] public doubleF height;
            public double step;
            public int stage;
            
            public double diffRestSlope;
            public double diffRate;

            public DiffusionJob(double4F snow, doubleF height, double step, ref Parameters P)
            {
                this.snow = snow;
                this.height = height;
                this.step = step;
                diffRestSlope = P.DiffusionRestSlope;
                diffRate = P.DiffusionRate;
                stage = 0;
            }
            
            public void Execute(int index)
            {
                int2 ij = snow.cell(index);
                if ((ij.y * 2 + ij.x) % 5 != stage) return;
                int2 up   = min(ij + 1, snow.dimension - 1);
                int2 down = max(ij - 1, 0);

                var du = snow[ij.x, up.y];
                var dd = snow[ij.x, down.y];
                var dl = snow[up.x, ij.y];
                var dr = snow[down.x, ij.y];
                var npow = double4( du.w, dd.w, dl.w, dr.w);
                
                var h = height[ij];
                var hn = double4(
                    height[ij.x, up.y] + csum(du), 
                    height[ij.x, down.y] + csum(dd), 
                    height[up.x, ij.y] + csum(dl), 
                    height[down.x, ij.y] + csum(dr)
                );
                var dn = height.cellSize.yyxx;

                var sd = (h - hn) / dn;
                var mdpos = max(0, sd - diffRestSlope);
                var mdneg = clamp(sd + diffRestSlope, 0, npow);
                var md = select(mdpos, mdneg, sd < 0);
                md *= step * diffRate;

                var i = csum(min(0, md));
                var o = csum(max(0, md));
                var pow = snow[ij].w;
                var smd = md * select(1, (i + pow) / o, o > i + pow);
                md = select(smd, md, md < 0);
                
                npow = max(0, npow + md);
                du.w = npow.x;
                dd.w = npow.y;
                dl.w = npow.z;
                dr.w = npow.w;
            
                snow[ij.x, up.y] = du;
                snow[ij.x, down.y] = dd;
                snow[up.x, ij.y] = dl;
                snow[down.x, ij.y] = dr;
                
                var s = snow[index];
                s.w -= min(csum(md), s.w);
                snow[index] = s;
            }
        }
        
        #endregion

        
        #region Transport

        public static void Transport(double4F snow, double3F wind, doubleF windAltitude, doubleF height, ref Parameters P)
        {
            var job = new TransportJob(snow, wind, windAltitude, height, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                job.Run(snow.Length);
            }
        }
        
        public static JobHandle Transport(double4F snow, double3F wind, doubleF windAltitude, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TransportJob(snow, wind, windAltitude, height, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                dependsOn = job.ScheduleParallel(snow.Length, 256, dependsOn);
            }

            return dependsOn;
        }

        private struct TransportJob : IJobFor
        {
            public double4F snow;
            [ReadOnly] public double3F wind;
            [ReadOnly] public doubleF altitude, height;
            private double stabMinSlope, unstableFactor;

            // Add parameter
            private double windPlates;
            public int stage;

            public TransportJob(double4F snow, double3F wind, doubleF altitude, doubleF height, ref Parameters P)
            {
                this.snow = snow;
                this.wind = wind;
                this.altitude = altitude;
                this.height = height;
                stabMinSlope = P.StabilityMinSlope;
                unstableFactor = P.SnowfallUnstableRatio;
                windPlates = P.WindPlates;
                stage = 0;
            }
            
            public void Execute(int index)
            {
                int2 ij = snow.cell(index);
                if ((ij.y * 2 + ij.x) % 5 != stage) return;
                
                var grad2 = field.gradient2(height, snow, index);
                var curv = dot(grad2, abs(wind[index].xz));
                var d = snow[index];
                var td = csum(d);
                var totalHeight = height[index] + td;
                curv = clamp(-curv, 0, totalHeight - altitude[index]);
                var erosion = max(curv, td);

                var shift = normalize(wind[index].xz);
                shift *= erosion;
                var ndir = (int2)sign(wind[index].xz);
                var n = clamp(ij + ndir, 0, snow.dimension - 1);
                var nd = snow[n.x, ij.y];
                nd.y += shift.x;
                snow[n.x, ij.y] = nd;
                nd = snow[ij.x, n.y];
                nd.y += shift.y;
                snow[ij.x, n.y] = nd;
                
                // Remaining snow turns into powder
                td -= erosion;
                d.w = min(d.w, td);
                d.z = min(d.z, td - d.w);
                var stable = max(0, td - d.w - d.z);
                
                var slope = cmax(field.slope(height, snow, ij));
                var xuns = max(0, slope - stabMinSlope) * unstableFactor;
                // TODO: This can be precomputed
                var windTerrain = dot(wind[index].xz, field.cgradient(wind, index).c1);
                var unstability = min(stable, xuns * max(0, windTerrain) * windPlates);
                d.z += unstability;
                d.x = min(d.x, stable - unstability);
                d.y = stable - unstability - d.x;
                snow[index] = d;
            }
        }

        #endregion
    }
}