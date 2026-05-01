using System.Runtime.CompilerServices;
using HPML;
using TFM.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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
            public double SnowfallPerDegree;        // m / °C
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

            public static Parameters Default => new()
            {
                TempBase = 0,
                SnowfallMinHeight = 0,
                
                SnowfallStrength = 0.01,
                SnowfallPerDegree = 0.1f,
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
                StabilityCompactionPressure = 1e-3,
                DiffusionRestSlope = 0.5,
                DiffusionRate = 0.5
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
            public double sfPerDay, sfPerDegree, sfMinHeight, sfMax, sfPowderRatio, sfUnstableRatio;
            public double csMin, csTempFactor, csMaxTemp;
            public double stabMinSlope;

            public SnowfallJob(double4F snow, doubleF height, doubleF temperature, double step, ref Parameters P)
            {
                this.snow = snow;
                this.height = height;
                this.temperature = temperature;
                this.step = step;
                sfPerDay = P.SnowfallStrength;
                sfPerDegree = P.SnowfallPerDegree;
                sfMinHeight = P.SnowfallMinHeight;
                sfMax = P.SnowfallMax;
                sfPowderRatio = P.SnowfallPowderRatio;
                sfUnstableRatio = P.SnowfallUnstableRatio;
                csMin = P.CriticalSlopeMin;
                csTempFactor = P.CriticalSlopeTempFactor;
                csMaxTemp = P.CriticalSlopeMaxTemp;
                stabMinSlope = P.StabilityMinSlope;
            }
            
            public void Execute(int index)
            {
                var slope = cmax(field.gradient(height, index));
                var dd = sfPerDay * sfPerDegree * clamp(max(0d, height[index] - sfMinHeight), 0d, sfMax);
                var cs = csMin + csTempFactor * max(0d, csMaxTemp - temperature[index]);
                var xpow = saturate(sfPowderRatio - cs);
                var xuns = clamp((slope - stabMinSlope) * sfUnstableRatio, 0d, (1d - xpow));
                var xstb = 1d - xpow - xuns;

                var x = double4(0d, xstb, xuns, xpow);
                snow[index] += x * dd * step;
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
            [NativeDisableParallelForRestriction] double4F snow;
            [ReadOnly] doubleF temperature, height;
            private double step, tempBase, meltRate, meltTemp, meltCompactionEffect;
            private double stabStableTemp, stabUnstableTemp, stabFreezeTemp, stabHot, stabMedium, stabFreeze, stabCompactionPressure;

            public MeltJob(double4F snow, doubleF temperature, doubleF height, double step, ref Parameters P)
            {
                this.snow = snow;
                this.temperature = temperature;
                this.height = height;
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
            }
            
            public void Execute(int index)
            {
                // Melting
                
                // TODO: Apparently there's this "melt compaction effect" which is not mentioned in the paper but appears
                //  in source. Should maybe handle it?
                var t = temperature[index] + tempBase;
                var d = snow[index];
                double pressure = csum(d.yzw);
                d.w += -step * meltRate * max(0, t - meltTemp); // powder
                d.z += min(0, d.w); // unstable

                double stable = d.s() + d.c();
                double compaction = select(d.c() / stable, 0, stable < 0.0001);
                
                d.y += min(0, d.z * (1 - compaction) * meltCompactionEffect); // stable
                d.x += min(0, d.y); // compacted
                
                // Stability
                
                double temp = temperature[index];
                double slope = cmax(field.slope(height, index));

                double4 hotStability = double4(stabUnstableTemp, stabStableTemp, stabHot, stabMedium);
                double4 coldStability = double4(stabStableTemp, stabFreezeTemp, stabMedium, stabFreeze);
                double4 stabValues = select(hotStability, coldStability, temp > stabStableTemp);

                double x = saturate(unlerp(stabValues.x, stabValues.y, temp));
                double stability = (1.0 - x) * stabValues.z + x * stabValues.w;

                stability *= select(1d, slope, stability < 0d || stability > 1d);
                
                double available = d.s() + d.c();
            
                stability = clamp(stability, -available, d.u());

                d.unstable(max(0d, d.u() - stability));
                d.compacted(
                    clamp(d.c() + pressure * stabCompactionPressure, 0, available - d.p() - d.u())
                );
                snow[index] = d;
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

                var h = height[ij];
                var hn = double4(
                    height[ij.x, up.y], 
                    height[ij.x, down.y], 
                    height[up.x, ij.y], 
                    height[down.x, ij.y]
                );
                var dn = double4(double2(height.cellSize.y), double2(height.cellSize.x));

                var sd = (h - hn) / dn;
                var md = select(max(0, sd - diffRestSlope), min(0, sd + diffRestSlope), sd < 0);
                md *= step * diffRate;

                var i = csum(min(0, md));
                var o = csum(max(0, md));
                var P = snow[ij].w;
                var smd = md * select(1, (i + P) / o, o > i + P);
                md = select(smd, md, md < 0);

                var du = snow[ij.x, up.y];
                var dd = snow[ij.x, down.y];
                var dl = snow[up.x, ij.y];
                var dr = snow[down.x, ij.y];
            
                var np = double4( du.w, dd.w, dl.w, dr.w);
                np += md;
                du.w = np.x;
                dd.w = np.y;
                dl.w = np.z;
                dr.w = np.w;
            
                snow[ij.x, up.y] = du;
                snow[ij.x, down.y] = dd;
                snow[up.x, ij.y] = dl;
                snow[down.x, ij.y] = dr;
            }
        }
        
        #endregion

        
        #region Transport

        public static void Transport(double4F snow, double3F wind, doubleF windAltitude, doubleF height)
        {
            
        }

        private struct TransportJob : IJobFor
        {
            public double4F snow;
            [ReadOnly] public double3F wind;
            [ReadOnly] public doubleF altitude, height;
            
            public void Execute(int index)
            {
                var grad2 = field.gradient2(height, index);
                var curv = dot(grad2, wind[index].xz);
                var d = csum(snow[index]) * 0.001;
                var totalHeight = height[index] + d;
                curv = clamp(curv, 0, totalHeight - altitude[index]);
                var erosion = max(curv, d);
            }
        }

        #endregion
    }
}