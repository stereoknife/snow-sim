using System;
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
using Random = Unity.Mathematics.Random;

namespace TFM.Simulation
{
    public static class Snow
    {
        public struct Parameters
        {
            public double SnowfallStrength; //-------- day⁻¹
            public double SnowfallMax; //------------- m
            public double SnowfallPowderRatio; //----- scalar
            public double SnowfallUnstableRatio; //--- scalar
            public double CriticalSlopeMin; //-------- ratio
            public double CriticalSlopeTempFactor; //- °C⁻¹
            public double CriticalSlopeMaxTemp; //---- °C
            
            // Melt
            public double MeltRate;                 // m / °C
            public double MeltTemp;                 // °C
            public double MeltVolumeFactor;
            
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
            public double WindErosionRate;
            public double WindSpeedPerLayer;
            public double WindMaxSpeed;
            
            // Avalanche
            public double AvalancheSnowDensity;                  // g/cm^-3
            public double AvalancheSnowViscosity;               // s^-2
            public double AvalancheRestSlope;
            public double AvalancheGravity;
            public double AvalancheTemp;
            
            // Temp
            public double TemperatureIncreasePerMetre;
            public double TemperatureIncreasePerSunlight;
            
            // Weather
            [HideInInspector] public double TempBase;                 // °C
            [HideInInspector] public double WindSpeed;
            [HideInInspector] public double CloudCover;
            [HideInInspector] public double SnowfallIntensity;

            public static Parameters Default => new()
            {
                TempBase = 0,
                WindSpeed = 10,
                CloudCover = 0,
                SnowfallIntensity = 1,
                
                TemperatureIncreasePerMetre = -0.01,
                TemperatureIncreasePerSunlight = 10,
                SnowfallStrength = 0.001,
                SnowfallMax = 1000,
                SnowfallPowderRatio = 5,
                SnowfallUnstableRatio = 1,
                CriticalSlopeMin = 0.5,
                CriticalSlopeTempFactor = 0.05,
                CriticalSlopeMaxTemp = -10,
                MeltRate = 0.01,
                MeltTemp = 0,
                MeltVolumeFactor = 0,
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
                WindErosionRate = 0.1,
                WindSpeedPerLayer = 1,
                WindMaxSpeed = 10,
                AvalancheSnowDensity = 0.5,
                AvalancheRestSlope = tan(radians(30)),
                AvalancheGravity = 9.81,
                AvalancheSnowViscosity = 0.0005,
                AvalancheTemp = 0.5,
            };
        }
        
        #region Snowfall

        public static void Snowfall(double4F snow, doubleF height, doubleF illumination, double step, ref Parameters P)
            => new SnowfallJob(snow, height, illumination, step, ref P)
                .Run(snow.Length);
        
        public static JobHandle Snowfall(double4F snow, doubleF height, doubleF illumination, double step, ref Parameters P, JobHandle dependsOn)
            => new SnowfallJob(snow, height, illumination, step, ref P)
                .ScheduleParallel(snow.Length, 512, dependsOn);

       // [BurstCompile]
        private struct SnowfallJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public double4F snow;
            [ReadOnly] public doubleF height, illumination;
            public double step;
            public double sfPerDay, sfMax, sfPowderRatio, sfUnstableRatio;
            public double csMin, csTempFactor, csMaxTemp;
            public double stabMinSlope, baseTemp, tempIncAltitude, tempIncSunlight;

            public SnowfallJob(double4F snow, doubleF height, doubleF illumination, double step, ref Parameters P)
            {
                this.snow = snow;
                this.height = height;
                this.illumination = illumination;
                this.step = step;
                sfPerDay = P.SnowfallStrength * P.SnowfallIntensity;
                sfMax = P.SnowfallMax;
                sfPowderRatio = P.SnowfallPowderRatio;
                sfUnstableRatio = P.SnowfallUnstableRatio;
                csMin = P.CriticalSlopeMin;
                csTempFactor = P.CriticalSlopeTempFactor;
                csMaxTemp = P.CriticalSlopeMaxTemp;
                stabMinSlope = P.StabilityMinSlope;
                baseTemp = P.TempBase;
                tempIncAltitude = P.TemperatureIncreasePerMetre;
                tempIncSunlight = P.TemperatureIncreasePerSunlight;
            }
            
            public void Execute(int index)
            {
                var slope = cmax(field.gradient(height, snow, index));
                
                // Get amount of snow that has fallen on this cell
                var airTemp = baseTemp + height[index] * tempIncAltitude;
                var dd = sfPerDay * clamp(-airTemp, 0d, sfMax);
                // Get critical slope
                var cellTemp = airTemp + illumination[index] * tempIncSunlight;
                var cs = csMin + csTempFactor * max(0d, csMaxTemp - cellTemp);
                
                // Ratio of powder snow
                var xpow = saturate(sfPowderRatio * (slope - cs));
                // Ratio of unstable snow
                var xuns = clamp((slope - stabMinSlope) * sfUnstableRatio, 0d, 1d - xpow);
                // Ratio of stable snow
                var xstb = 1d - xpow - xuns;
                
                snow[index] += double4(0d, xstb, xuns, xpow) * dd * step;
            }
        }

        #endregion


        #region Melt
        
        public static void MeltSimple(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P)
            => new MeltSimpleJob(snow, illumination, height, step, ref P)
                .Run(snow.Length);

        public static JobHandle MeltSimple(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
            => new MeltSimpleJob(snow, illumination, height, step, ref P)
                .ScheduleParallel(snow.Length, 256, dependsOn);
        
        [BurstCompile]
        private struct MeltSimpleJob : IJobFor
        {
            [NativeDisableParallelForRestriction] double4F snowF;
            [ReadOnly] doubleF illumination, heightF;
            private double step, tempBase, meltRate, meltTemp, meltCompactionEffect;
            private double tempIncAltitude, tempIncIllum, cloudCover, volFactor;

            public MeltSimpleJob(double4F snowF, doubleF illumination, doubleF heightF, double step, ref Parameters P)
            {
                this.snowF = snowF;
                this.illumination = illumination;
                this.heightF = heightF;
                this.step = step;
                tempBase = P.TempBase;
                meltRate = P.MeltRate;
                meltTemp = P.MeltTemp;
                meltCompactionEffect = 0;
                tempIncAltitude = P.TemperatureIncreasePerMetre;
                tempIncIllum = P.TemperatureIncreasePerSunlight;
                cloudCover = 0.8 - 0.5 * P.CloudCover;
                volFactor = P.MeltVolumeFactor;
            }
            
            public void Execute(int index)
            {
                var snow = snowF[index];
                var cellHeight = heightF[index] + csum(snow);
                
                var airTemp = tempBase + cellHeight * tempIncAltitude;
                var temperature = airTemp + illumination[index] * tempIncIllum * cloudCover;
                
                // Melting
                var melt = -step * meltRate * max(0, temperature - meltTemp) / max(1, volFactor * csum(snow));

                snow.w += melt; // powder
                snow.z += min(0, snow.w); // unstable
                snow.y += min(0, snow.z); // stable
                snow.x += min(0, snow.y);
                snow = select(snow, 0, snow < 0.001);

                snowF[index] = snow;
            }
        }
        
        public static void Melt(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P)
            => new MeltJob(snow, illumination, height, step, ref P)
                .Run(snow.Length);

        public static JobHandle Melt(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
            => new MeltJob(snow, illumination, height, step, ref P)
                .ScheduleParallel(snow.Length, 256, dependsOn);

        private struct MeltJob : IJobFor
        {
            private double4F snow;
            private doubleF illumination;
            private doubleF height;
            private double step;
            private double tempBase, tempIncAltitude;
            private double sunIntensity, cloudFiltering, albedo;
            private double heatCapacity, latentHeat, conductivity;
            private double testValue;

            public MeltJob(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P)
            {
                this.snow = snow;
                this.illumination = illumination;
                this.height = height;
                this.step = step * 3600 * 24;
                tempBase = P.TempBase;
                tempIncAltitude = P.TemperatureIncreasePerMetre;
                cloudFiltering = 0.8 - 0.5 * P.CloudCover;
                // Constants:
                albedo = 0.1;
                sunIntensity = 1367;
                heatCapacity = 2090 * 110;
                latentHeat = 333100 * 110;
                conductivity = 0.05;
                testValue = P.MeltVolumeFactor;
            }
            
            public void Execute(int index)
            {
                // We assume snow temperature is air temperature if below 0 or 0 if above.
                var s = snow[index];
                var snowThickness = csum(s);
                var airTemp = tempBase + (height[index] + snowThickness) * tempIncAltitude;
                var snowTemp = min(airTemp / testValue, 0);
                var airConduction = conductivity * max(airTemp, 0);
                var neededRadiation = -snowTemp * snowThickness * heatCapacity;
                var solarRadiation = sunIntensity * illumination[index] * cloudFiltering * albedo;
                var totalRadiation = solarRadiation + airConduction - neededRadiation;
                var meltRate = totalRadiation * step / (latentHeat * snowThickness);

                var melt = -saturate(meltRate) * snowThickness;
                //var compaction = select(snow.x / stable, 0, stable < 0.0001);
                s.w += melt; // powder
                s.z += min(0, s.w); // unstable
                s.y += min(0, s.z); // stable
                s.x += min(0, s.y);
                s = select(s, 0, s < 0.001);
                snow[index] = s;
            }
        }
        
        #endregion


        #region Stability
        
        public static JobHandle Stability(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
            => new StabilityJob(snow, illumination, height, step, ref P)
                .ScheduleParallel(snow.Length, 256, dependsOn);

        private struct StabilityJob : IJobFor
        {
            [NativeDisableParallelForRestriction] double4F snowF;
            [ReadOnly] doubleF illumination, heightF;
            private double tempBase, step;
            private double stabStableTemp, stabUnstableTemp, stabFreezeTemp, stabHot, stabMedium, stabFreeze,
                stabCompactionPressure, stabMinSlope, unstableFactor, tempIncAltitude, tempIncIllum, cloudCover;

            public StabilityJob(double4F snow, doubleF illumination, doubleF height, double step, ref Parameters P)
            {
                this.step = step;
                snowF = snow;
                this.illumination = illumination;
                heightF = height;
                tempBase = P.TempBase;
                stabStableTemp = P.StabilityStableTemp;
                stabUnstableTemp = P.StabilityUnstableTemp;
                stabFreezeTemp = P.StabilityFreezeTemp;
                stabHot = P.StabilityHot;
                stabMedium = P.StabilityMedium;
                stabFreeze = P.StabilityFreeze;
                stabCompactionPressure = P.StabilityCompactionPressure;
                stabMinSlope = P.StabilityMinSlope;
                unstableFactor = P.SnowfallUnstableRatio;
                tempIncAltitude = P.TemperatureIncreasePerMetre;
                tempIncIllum = P.TemperatureIncreasePerSunlight;
                cloudCover = 0.8 - 0.5 * P.CloudCover;
            }
            
            public void Execute(int index)
            {
                var snow = snowF[index];
                if (csum(snow) < 0.001) return;
                var temperature = tempBase + illumination[index] * tempIncIllum * cloudCover + (heightF[index] + csum(snow)) * tempIncAltitude;
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

        //[BurstCompile]
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
                
                var h = height[ij] + csum(snow[index]);
                var hn = double4(
                    height[ij.x, up.y] + csum(du), 
                    height[ij.x, down.y] + csum(dd), 
                    height[up.x, ij.y] + csum(dl), 
                    height[down.x, ij.y] + csum(dr)
                );
                var dn = height.cellSize.yyxx;

                var sd = (h - hn) / dn;
                var diffusion = select(
                    max(0, sd - diffRestSlope), 
                    clamp(sd + diffRestSlope, -npow, 0), 
                    sd < 0
                );
                diffusion *= step * diffRate;

                //var i = csum(min(0, diffusion));
                //var o = csum(max(0, diffusion));
                var o = max(0, diffusion);
                var pow = snow[ij].w;
                //var scale = min(1, (abs(i) + pow) / o);
                //diffusion = select(diffusion, diffusion * scale, sd > 0);
                
                npow = max(0, npow + o * min(1, pow / csum(o)));
                du.w = npow.x;
                dd.w = npow.y;
                dl.w = npow.z;
                dr.w = npow.w;
            
                snow[ij.x, up.y] = du;
                snow[ij.x, down.y] = dd;
                snow[up.x, ij.y] = dl;
                snow[down.x, ij.y] = dr;
                
                var s = snow[index];
                s.w = max(s.w - csum(o), 0);
                snow[index] = s;
            }
        }
        
        #endregion

        
        #region Transport

        public static void Transport(double4F snow, double2F wind, ScalarField2D windAltitude, ScalarField2D windTerrain, doubleF height, double step, ref Parameters P)
        {
            var job = new TransportJob(snow, wind, windAltitude, windTerrain, height, step, ref P);
            for (int stage = 0; stage < 5; stage++)
            {
                job.stage = stage;
                job.Run(snow.Length);
            }
        }
        
        public static JobHandle Transport(double4F snow, double2F wind, ScalarField2D windAltitude, ScalarField2D windTerrain, doubleF height, double step, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TransportJob(snow, wind, windAltitude, windTerrain, height, step, ref P);
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
            [ReadOnly] public double2F wind;
            [ReadOnly] public doubleF windAltLow, windAltHigh, windTerrainLow, windTerrainHigh;
            [ReadOnly] public doubleF height;
            private double stabMinSlope, unstableFactor, step, windErosion, windSpeed, windLerp;

            // Add parameter
            private double windPlates;
            public int stage;
            private bool lowLayerZero;

            public TransportJob(double4F snow, double2F wind, ScalarField2D windAltitude, ScalarField2D windTerrain, doubleF height, double step, ref Parameters P)
            {
                this.snow = snow;
                this.wind = wind;
                this.height = height;
                this.step = step;
                stabMinSlope = P.StabilityMinSlope;
                unstableFactor = P.SnowfallUnstableRatio;
                windPlates = P.WindPlates;
                windErosion = P.WindErosionRate;
                windSpeed = P.WindSpeed;
                stage = 0;
                
                var windLayer = P.WindSpeed / P.WindSpeedPerLayer - 1;
                lowLayerZero = windLayer < 0;
                windLayer = clamp(windLayer, 0, windAltitude.layers - 1);
                windAltLow = lowLayerZero ? height : windAltitude.Layer((int)floor(windLayer));
                windAltHigh = windAltitude.Layer((int)ceil(windLayer));
                windTerrainLow = windTerrain.Layer((int)floor(windLayer));
                windTerrainHigh = windTerrain.Layer((int)ceil(windLayer));
                windLerp = frac(windLayer);
            }
            
            public void Execute(int index)
            {
                int2 ij = snow.cell(index);
                if ((ij.y * 2 + ij.x) % 5 != stage) return;
                if (snow[index].w < 0.001) return;
                
                // 0.01 is wind_snow_terrain
                var snowGrad2 = -field.gradient2(height, snow, index) * 0.01;
                //var windGrad2 = -field.gradient2(windAltLow, windAltHigh, windLerp, index) * 0.01;
                var w = abs(wind[index]);
                var curv = max(dot(w, snowGrad2), 0);
                var d = snow[index];
                var snowAmt = csum(d);
                var windAltitude = lerp(windAltLow[index], windAltHigh[index], windLerp);

                curv = select(curv, 0, windAltitude > height[index] + snowAmt);
                
                // 0.1 is wind_snow_capacity
                var erosion = clamp(curv * 10, 0, min(snowAmt, windSpeed * windErosion));
                erosion = clamp(height[index] + snowAmt - windAltitude, 0, erosion);
                
                var windDir = (int2)sign(wind[index]);
                var windNeighbour = clamp(ij + windDir, 0, snow.dimension - 1);
                
                if (csum(w) > 0.001 && erosion > 0.001)
                {
                    erosion *= step;
                    var windNorm = w / csum(w);
                    var nd = snow[windNeighbour.x, ij.y];
                    nd.w += erosion * windNorm.x;
                    snow[windNeighbour.x, ij.y] = nd;
                    nd = snow[ij.x, windNeighbour.y];
                    nd.w += erosion * windNorm.y;
                    snow[ij.x, windNeighbour.y] = nd;
                    d.w = max(d.w - erosion, 0);
                }
                
                var stable = d.y + d.x;
                var slope = cmax(field.slope(height, snow, ij));
                var xuns = max(0, slope - stabMinSlope) * unstableFactor;

                var windTerrainMin = select(windTerrainLow[index], 0, lowLayerZero);
                var windTerrain = lerp(windTerrainMin, windTerrainHigh[index], windLerp);
                //windTerrain = 0;
                var unstability = min(stable, xuns * max(0, windTerrain) * windPlates);
                d.z += unstability;
                d.x = min(d.x, stable - unstability);
                d.y = stable - unstability - d.x;
                snow[index] = d;
            }
        }

        #endregion


        #region Avalanche
        
        /*
         *   0, 3, 5
         *   1,  , 6
         *   2, 4, 7
         */
        
        private const int kStride = 4;
        private const int kLoop = kStride;
        private const double kEpsilon = 1e-6;
        private static readonly int2x4 Dirs = int2x4(int2(-1, -1), int2(-1, 0), int2(-1, 1), int2(0, -1));
        private static readonly int4x2 DirsT = transpose(Dirs);
        private static readonly bool4 Perp = DirsT.c0 == 0 | DirsT.c1 == 0;

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
            dependsOn = new AvalancheFlowJob
                {
                    Height = height,
                    Snow = snow,
                    FlowR = flow.GetSubArray(0, snow.Length * kStride),
                    FlowW = flow.GetSubArray(snow.Length * kStride, snow.Length * kStride),
                    Density = P.AvalancheSnowDensity,
                    Gravity = P.AvalancheGravity,
                    Dt = dt,
                    Moving = moving,
                    RestSlope = P.AvalancheRestSlope,
                    KViscosity = P.AvalancheSnowViscosity,
                    Temp = P.AvalancheTemp
                }
                .ScheduleParallel(height.Length, 64, dependsOn);
            
            dependsOn = new AvalancheScaleJob
                {
                    Snow = snow,
                    FlowW = flow.GetSubArray(0, snow.Length * kStride),
                    FlowR = flow.GetSubArray(snow.Length * kStride, snow.Length * kStride),
                }
                .ScheduleParallel(height.Length, 64, dependsOn);

            dependsOn = new AvalancheSnowJob
                {
                    Snow = snow,
                    Flow = flow.GetSubArray(0, snow.Length * kStride),
                    Dt = dt,
                    Moving = moving,
                }
                .ScheduleParallel(height.Length, 64, dependsOn);

            return dependsOn;
        }
        
        [BurstCompile]
        private struct AvalancheFlowJob : IJobFor
        {
            [ReadOnly] public doubleF Height;
            [ReadOnly] public double4F Snow;
            [NativeDisableContainerSafetyRestriction][ReadOnly] public NativeArray<double> FlowR;
            [NativeDisableContainerSafetyRestriction][WriteOnly] public NativeArray<double> FlowW;
            [ReadOnly] public NativeArray<bool> Moving;
            public double Density, Gravity, Dt, RestSlope, KViscosity, Temp;
            
            public void Execute(int index)
            {
                var ij = Height.cell(index);
                var h = Height[index];
                var s = Snow[index];
                h += csum(s);

                var c = square(Height.cellSize.x);
                var moving = Moving[index];

                double4 nflow = 0, nh = 0, ns = 0;
                bool4 nmov = false;
                for (int k = 0; k < kLoop; k++)
                {
                    var nij = ij - Dirs[k];
                    nij = select(nij, -nij, nij < 0);
                    nij = select(nij, 2 * (Snow.dimension - 1) - nij, nij > Snow.dimension - 1);
                    nflow[k] = FlowR[Snow.index(nij) * kStride + k];
                    
                    nij = ij + Dirs[k];
                    nij = select(nij, -nij, nij < 0);
                    nij = select(nij, 2 * (Snow.dimension - 1) - nij, nij > Snow.dimension - 1);

                    nh[k] = Height[nij];
                    ns[k] = csum(Snow[nij]);
                    nmov[k] = Moving[Snow.index(nij)];
                }

                nh += ns;
                moving |= any(nflow > kEpsilon);
                var flow = FlowR.ReinterpretLoad<double4>(index * kStride);
                
                var pressure = Density * Gravity * (h - nh);
                var dist = select(Height.iCellSize.x * SQRT2_DBL, Height.iCellSize.x, Perp);
                var acc = pressure / Density * dist;
                flow += acc * c * Dt;
                        
                double4 friction = Gravity * RestSlope * c * Dt * Temp;
                friction = min(abs(flow), friction);
                flow += friction * -sign(flow);
                        
                var viscosity = -KViscosity * flow * c * Dt * (1 - Temp);
                flow += select(viscosity, -flow, abs(viscosity) > abs(flow));
                
                moving |= any(flow < 0 & nmov);
                flow = select(flow, 0, flow < 0 & !nmov);
                flow = select(0, flow, moving);
                
                FlowW.ReinterpretStore(index * kStride, select(0, flow, moving));
            }
        }
        
        [BurstCompile]
        private struct AvalancheScaleJob : IJobFor
        {
            [ReadOnly] public double4F Snow;
            [NativeDisableContainerSafetyRestriction][ReadOnly] public NativeArray<double> FlowR;
            [NativeDisableContainerSafetyRestriction][WriteOnly] public NativeArray<double> FlowW;
            
            public void Execute(int index)
            {
                var ij = Snow.cell(index);
                
                var flow = FlowR.ReinterpretLoad<double4>(index * kStride);
                double4 nflow = 0;
                var nix = int4(0, 1, 2, 3);
                
                for (int k = 0; k < kLoop; k++)
                {
                    var nij = ij - Dirs[k];
                    nij = select(nij, -nij, nij < 0);
                    nij = select(nij, 2 * (Snow.dimension - 1) - nij, nij > Snow.dimension - 1);
                    nix[k] += Snow.index(nij) * kStride;
                    nflow[k] = FlowR[nix[k]];
                }

                var of = csum(max(0, flow) - min(0, nflow));
                var scale = square(Snow.cellSize.x) * Snow[index].z;
                scale = select(1, scale / of, of > scale);

                flow *= scale;
                nflow *= scale;
                
                for (int k = 0; k < kLoop; k++)
                {
                    if (flow[k] > 0)
                    {
                        FlowW[index * kStride + k] = flow[k];
                    }
                    if (nflow[k] <= 0)
                    {
                        FlowW[nix[k]] = nflow[k];
                    }
                }
            }
        }

        [BurstCompile]
        private struct AvalancheSnowJob : IJobFor
        {
            public double4F Snow;
            [NativeDisableParallelForRestriction] public NativeArray<double> Flow;
            public double Dt;
            [WriteOnly] public NativeArray<bool> Moving;
            
            public void Execute(int index)
            {
                var ij = Snow.cell(index);
                var moving = false;

                var flow = Flow.ReinterpretLoad<double4>(index * kStride);
                double4 nflow = 0;
                
                for (int k = 0; k < kLoop; k++)
                {
                    var nij = ij - Dirs[k];
                    nij = select(nij, -nij, nij < 0);
                    nij = select(nij, 2 * (Snow.dimension - 1) - nij, nij > Snow.dimension - 1);
                    nflow[k] = -Flow[Snow.index(nij) * kStride + k];
                }

                flow += nflow;
                moving |= any(abs(flow) > kEpsilon);

                var s = Snow[index];
                s.z -= Dt * square(Snow.iCellSize.x) * csum(flow);
                s.z = max(s.z, 0);
                Snow[index] = s;
                Moving[index] = moving;
            }
        }

        #endregion
    }
}