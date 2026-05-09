using System.Runtime.CompilerServices;
using HPML;
using TFM.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
            public double SnowfallMinHeight; //------- m
            public double SnowfallStrength; //-------- day⁻¹
            public double SnowfallMax; //------------- m
            public double SnowfallPowderRatio; //----- scalar
            public double SnowfallUnstableRatio; //--- scalar
            public double CriticalSlopeMin; //-------- ratio
            public double CriticalSlopeTempFactor; //- °C⁻¹
            public double CriticalSlopeMaxTemp; //---- °C
            
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
            
            // Avalanche
            public double AvalancheSnowDensity;                  // g/cm^-3
            public double AvalancheSnowViscosity;               // s^-2
            public double AvalancheRestSlope;
            public double AvalancheGravity;
            public double AvalancheTemp;

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
                AvalancheSnowDensity = 0.5,
                AvalancheRestSlope = tan(radians(30)),
                AvalancheGravity = 9.81,
                AvalancheSnowViscosity = 0.0005,
                AvalancheTemp = 0.5,
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
            public double4F snow;
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
                    stability = remap(stabStableTemp, stabFreezeTemp, stabMedium, stabFreeze, temperature);
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

        public static void Transport(double4F snow, double3F wind, doubleF windAltitude, doubleF height, double step, ref Parameters P)
        {
            var job = new TransportJob(snow, wind, windAltitude, height, step, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                job.Run(snow.Length);
            }
        }
        
        public static JobHandle Transport(double4F snow, double3F wind, doubleF windAltitude, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TransportJob(snow, wind, windAltitude, height, step, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                dependsOn = job.ScheduleParallel(snow.Length, 256, dependsOn);
            }

            return dependsOn;
        }

        private struct TransportJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public double4F snow;
            [ReadOnly] public double3F wind;
            [ReadOnly] public doubleF windAltitude;
            [ReadOnly] public doubleF height;
            private double stabMinSlope, unstableFactor, step;

            // Add parameter
            private double windPlates;
            public int stage;

            public TransportJob(double4F snow, double3F wind, doubleF windAltitude, doubleF height, double step, ref Parameters P)
            {
                this.snow = snow;
                this.wind = wind;
                this.height = height;
                this.step = step;
                this.windAltitude = windAltitude;
                stabMinSlope = P.StabilityMinSlope;
                unstableFactor = P.SnowfallUnstableRatio;
                windPlates = P.WindPlates;
                stage = 0;
            }
            
            public void Execute(int index)
            {
                int2 ij = snow.cell(index);
                if ((ij.y * 2 + ij.x) % 5 != stage) return;
                
                var grad2 = -field.gradient2(height, snow, index) * 0.01;
                var w = abs(wind[index].xz);
                var curv = dot(w, grad2);
                var d = snow[index];
                var snowAmt = csum(d);
                
                var erosion = clamp(curv, 0, min(snowAmt, 1));
                erosion = clamp(height[index] + snowAmt - windAltitude[index], 0, erosion);
                snowAmt -= erosion;

                var windDir = (int2)sign(wind[index].xz);
                var windNeighbour = clamp(ij + windDir, 0, snow.dimension - 1);
                
                if (csum(w) > 0.001)
                {
                    var windNorm = w / csum(w);
                    var nd = snow[windNeighbour.x, ij.y];
                    nd.y += erosion * windNorm.x;
                    snow[windNeighbour.x, ij.y] = nd;
                    nd = snow[ij.x, windNeighbour.y];
                    nd.y += erosion * windNorm.y;
                    snow[ij.x, windNeighbour.y] = nd;
                }
                
                // Remaining snow turns into powder
                
                snowAmt -= erosion;
                d.w = min(d.w, snowAmt);
                d.z = min(d.z, snowAmt - d.w);
                var stable = max(0, snowAmt - d.w - d.z);
                
                var slope = cmax(field.slope(height, snow, ij));
                var xuns = max(0, slope - stabMinSlope) * unstableFactor;

                var windTerrain = dot(wind[index].xz, field.cgradient(wind, index).c1);
                var unstability = min(stable, xuns * max(0, windTerrain) * windPlates);
                d.z += unstability;
                d.x = min(d.x, stable - unstability);
                d.y = stable - unstability - d.x;
                snow[index] = d;
            }
        }

        #endregion


        #region Avalanche

        public static void Avalanche(double4F snow, double4F flow, doubleF height, NativeBitArray moving, double dt, ref Parameters P)
        {
            //new AvalancheFlowJob(snow, flow, flow, height, moving, dt, ref P).Run(snow.Length);
            //new AvalancheScaleJob(snow, flow, flow, moving, dt).Run(snow.Length);
            //new AvalancheJob(snow, flow, moving, dt).Run(snow.Length);
        }
        
        public static JobHandle Avalanche(double4F snow, double4F flow, doubleF height, NativeBitArray moving, double dt, ref Parameters P, JobHandle dependsOn)
        {
            /*
            var flow2 = new double4F(flow, Allocator.TempJob);
            dependsOn = new AvalancheFlowJob(snow, flow, flow2, height, moving, dt, ref P)
                .Schedule(snow.Length, dependsOn);
            dependsOn = new AvalancheScaleJob(snow, flow2, flow, moving, dt)
                .Schedule(snow.Length, dependsOn);
            dependsOn = new AvalancheJob(snow, flow, moving, dt)
                .Schedule(snow.Length, dependsOn);
            flow2.Dispose(dependsOn);
            */
            return dependsOn;
        }
        
        public static JobHandle AvalancheParallel(double4F snow, NativeArray<double> flow, doubleF height, NativeArray<bool> moving, double dt, ref Parameters P, JobHandle dependsOn)
        {
            dependsOn = new AvalancheJobUpdateMoving
                {
                    Height = height,
                    Snow = snow,
                    Flow = flow,
                    Density = P.AvalancheSnowDensity,
                    Gravity = P.AvalancheGravity,
                    Dt = dt,
                    Moving = moving,
                    RestSlope = P.AvalancheRestSlope,
                    KViscosity = P.AvalancheSnowViscosity,
                    Temp = P.AvalancheTemp
                }
                .ScheduleParallel(height.Length, 64, dependsOn);

            dependsOn = new AvalancheJobUpdateMoving2
                {
                    Snow = snow,
                    Flow = flow,
                    Dt = dt,
                    Moving = moving,
                }
                .ScheduleParallel(height.Length, 64, dependsOn);

            return dependsOn;
        }
        
        private static readonly int2x4 Dirs = new int2x4(int2(0, 1), int2(-1, 1), int2(0, 1), int2(1, 1));
        
        // TODO: Reduce size of flow if possible, clean up branches and stuff
        [BurstCompile]
        private struct AvalancheJobUpdateMoving : IJobFor
        {
            [ReadOnly] public doubleF Height;
            [ReadOnly] public double4F Snow;
            [NativeDisableParallelForRestriction] public NativeArray<double> Flow;
            [ReadOnly] public NativeArray<bool> Moving;
            public double Density, Gravity, Dt, RestSlope, KViscosity, Temp;
            
            public void Execute(int index)
            {
                var ij = Height.cell(index);
                var h = Height[index];
                var s = Snow[index];
                h += csum(s);

                var start = ij - 1;
                var end = ij + 2;

                var c = square(Height.cellSize.x);
                var moving = Moving[index];

                var of = 0d;
                for (int i = start.x, k = 0; i < end.x; i++)
                {
                    for (int j = start.y; j < end.y; j++, k++)
                    {
                        if (k == 4) continue;
                        var ii = i < 0
                            ? -i
                            : i > Height.dimension.x - 1
                                ? 2 * (Height.dimension.x - 1) - i
                                : i;
                        var jj = j < 0
                            ? -j
                            : j > Height.dimension.y - 1
                                ? 2 * (Height.dimension.y - 1) - j
                                : j;
                        
                        var nh = Height[ii, jj];
                        var ns = Snow[ii, jj];
                        nh += csum(ns);
                        
                        var pressure = Density * Gravity * (h - nh);
                        var dist = i == 0 || j == 0 ? Height.iCellSize.x : Height.iCellSize.x * SQRT2_DBL;
                        var acc = pressure / Density * dist;
                        var flow = Flow[index * 9 + k];
                        flow += acc * c * Dt;
                        
                        var friction = Gravity * RestSlope * c * Dt * Temp;
                        friction = min(abs(flow), friction);
                        flow += friction * -sign(flow);
                        
                        var viscosity = -KViscosity * flow * c * Dt * (1 - Temp);
                        flow += select(viscosity, -flow, abs(viscosity) > abs(flow));

                        var nmoving = Moving[Snow.index(ii, jj)];
                        if (flow < 0 && !nmoving) flow = 0;
                        moving |= flow < 0 && nmoving;
                        
                        of += max(0, flow);
                        Flow[index * 9 + k] = flow;
                    }
                }

                if (!moving)
                {
                    for (int k = 0; k < 9; k++)
                    {
                        Flow[index * 9 + k] = 0;
                    }
                    of = 0;
                }

                var scale = square(Height.cellSize.x) * Snow[index].z;
                scale = select(1, scale / of, of > scale);
                Flow[index * 9 + 4] = scale;
            }
        }
        
        [BurstCompile]
        private struct AvalancheJobUpdateMoving2 : IJobFor
        {
            public double4F Snow;
            [NativeDisableParallelForRestriction] public NativeArray<double> Flow;
            public double Dt;
            [WriteOnly] public NativeArray<bool> Moving;
            
            public void Execute(int index)
            {
                var ij = Snow.cell(index);
                var start = ij - 1;
                var end = ij + 2;
                
                var tf = 0d;
                var scale = Flow[index * 9 + 4];

                var moving = false;
                for (int i = start.x, k = 0; i < end.x; i++)
                {
                    for (int j = start.y; j < end.y; j++, k++)
                    {
                        if (k == 4) continue;
                        
                        var ii = i < 0
                            ? -i
                            : i > Snow.dimension.x - 1
                                ? 2 * (Snow.dimension.x - 1) - i
                                : i;
                        var jj = j < 0
                            ? -j
                            : j > Snow.dimension.y - 1
                                ? 2 * (Snow.dimension.y - 1) - j
                                : j;
                        
                        var ni = Snow.index(ii, jj);
                        var flow = Flow[index * 9 + k];
                        flow *= select(Flow[ni * 9 + 4], scale, flow > 0);
                        tf += Flow[index * 9 + k] = flow;
                        moving |= abs(flow) > 1.0e-6f;
                    }
                }

                var s = Snow[index];
                s.z -= Dt * square(Snow.iCellSize.x) * tf;
                s.z = max(s.z, 0);
                Snow[index] = s;
                Moving[index] = moving;
            }
        }

        #endregion
    }
}