using System;
using System.Runtime.CompilerServices;
using System.Threading;
using HPML;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using static HPML.field;
using Random = Unity.Mathematics.Random;

namespace TFM.Simulation
{
    public static class Lighting
    {
        public struct Parameters
        {
            // Lighting intensity
            public double IntensityDirect;
            public double IntensityAmbient;
            public double IntensityIndirect;
            
            // Direct lighting
            public float DirectLatitude;
            public int DirectStartingDay;
            public int DirectEndDay;
            public int DirectDaysBetweenSamples;
            public int DirectHoursBetweenSamples;
            
            // Indirect lighting
            public int IndirectAngularSamples;
            public int IndirectDistanceSamples;
            public double IndirectMaxDistance;
            
            // Temperature
            public double TemperatureIncreasePerMetre;
            public double TemperatureIncreasePerSunlight;

            public static Parameters Default => new()
            {
                IntensityDirect = 1,
                IntensityAmbient = 1,
                IntensityIndirect = 1,
                DirectLatitude = 0,
                DirectStartingDay = 0,
                DirectEndDay = 365,
                DirectDaysBetweenSamples = 7,
                DirectHoursBetweenSamples = 2,
                IndirectAngularSamples = 16,
                IndirectDistanceSamples = 20,
                IndirectMaxDistance = 50,
                TemperatureIncreasePerMetre = -0.01,
                TemperatureIncreasePerSunlight = 10,
            };
            
            public int Hash()
            {
                var hash = new HashCode();
                hash.Add(IntensityDirect);
                hash.Add(IntensityAmbient);
                hash.Add(IntensityIndirect);
                hash.Add(DirectLatitude);
                hash.Add(DirectStartingDay);
                hash.Add(DirectEndDay);
                hash.Add(DirectDaysBetweenSamples);
                hash.Add(DirectHoursBetweenSamples);
                hash.Add(IndirectAngularSamples);
                hash.Add(IndirectDistanceSamples);
                hash.Add(IndirectMaxDistance);
                hash.Add(TemperatureIncreasePerMetre);
                hash.Add(TemperatureIncreasePerSunlight);
                return hash.ToHashCode();
            }
        }
        
        #region Light Direction

        public static double3 LightDirection(double latitude, int day, double hour)
        {
            var (azim, altit) = Angles(latitude, day, hour);
            azim = 0.5 * PI_DBL - azim; // convert to CCW angle starting at 0 in east direction

            double2 east = double2(1.0, 0.0);
            double2 north = double2(0.0, 1.0);
            double2 xy = cos(azim) * east + sin(azim) * north;
            double3 light = double3(xy * cos(altit),sin(altit)).xzy;
            return light;
        }
        
        private static (double, double) Angles(double latitude, int day, double hour)
        {
            double azim, altit;
            double ha = PI2_DBL * hour / 24.0;
            double delta = asin(sin(0.39779 * cos(0.0172 * (day + 10) + 0.03341 * sin(0.39779 * (day - 2)))));
            double x = cos(ha) * cos(delta);
            double y = sin(ha) * cos(delta);
            double z = sin(delta);
            
            double rlatit = radians(latitude);
            double xhor = x * sin(rlatit) - z * cos(rlatit);
            double yhor = y;
            double zhor = x * cos(rlatit) + z * sin(rlatit);
            
            // Azimuth angle
            azim = atan2(yhor, xhor) + PI_DBL;
            azim %= PI2_DBL;

            // Altitude angle
            altit = asin(zhor);
            
            return (azim, altit);
        }

        #endregion
        
        
        #region Direct Lighting

        public static void DirectLighting(
            in doubleF heightfield,
            doubleF output,
            ref Parameters P
        )
        {
            var start = P.DirectStartingDay;
            if (start > P.DirectEndDay) start -= 365;
            var k = lipschitz(heightfield);
            var daySamples = 365 / P.DirectDaysBetweenSamples;
            var hourSamples = 24 / P.DirectHoursBetweenSamples;
            int n = daySamples * hourSamples;
            for (int d = start; d < P.DirectEndDay; d += daySamples)
            {
                for (int h = 0; h < 24; h += hourSamples)
                {
                    var light = LightDirection(P.DirectLatitude, d, h);
                    for (int i = 0; i < heightfield.Length; i++)
                    {
                        double shadow = ComputeShadowing(in heightfield, i, light, k);
                        output[i] += shadow * dot(light, normal(heightfield, i)) / n;
                    }
                }
            }
        }
        
        public static JobHandle DirectLighting(
            in doubleF heightfield,
            doubleF output,
            ref Parameters P,
            JobHandle dependsOn
        )
        {
            var k = lipschitz(heightfield);
            
            var job = new DirectLightingJob
            {
                heightfield = heightfield,
                output = output,
                k = k,
                light = default,
            };
            
            var start = P.DirectStartingDay;
            var end = P.DirectEndDay;
            var daySamples = 365 / P.DirectDaysBetweenSamples;
            var hourSamples = 24 / P.DirectHoursBetweenSamples;

            var jh = new JobHandle();
            if (start > end) start -= 365;
            int n = 0;
            for (int d = start; d < end; d += daySamples)
            {
                for (int h = 0; h < 24; h += hourSamples)
                {
                    var l = job.light = LightDirection(P.DirectLatitude, d, h);
                    if (l.y <= 0) continue;
                    Debug.DrawLine(Vector3.zero, (float3)l, Color.red, 60);
                    Debug.Log($"Light direction: {l.xz}");
                    var njh = job.Schedule(heightfield.field.Length, dependsOn);
                    jh = JobHandle.CombineDependencies(jh, njh);
                    n++;
                }
            }

            var njob = new NormalizeDirectLightingJob
            {
                output = output,
                factor = 1.0 / n
            };

            return njob.Schedule(heightfield.field.Length, jh);
        }
        
        [BurstCompile]
        private struct DirectLightingJob : IJobFor
        {
            [ReadOnly] public doubleF heightfield;
            [NativeDisableContainerSafetyRestriction] public doubleF output;
            public double k;
            public double3 light;
            
            public void Execute(int index)
            {
                var d = dot(light, normal(heightfield, index));
                double shadow = 0;
                if (d > 0) shadow = ComputeShadowing(in heightfield, index, light, k);
                var incidence = shadow * d;
                output.InterlockedAdd(index, incidence);
            }
        }
        
        [BurstCompile]
        private struct NormalizeDirectLightingJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public doubleF output;
            public double factor;
            
            public void Execute(int index)
            {
                output[index] *= factor;
            }
        }
        
        private static double ComputeShadowing(in doubleF heightfield, int i, double3 lightDirection, double k)
        {
            double3 s = double3(heightfield.cell(i) * heightfield.cellSize, heightfield[i]).xzy;
            double minStep = sqrt(2 * square(heightfield.cellSize.x) * lengthsq(lightDirection) / csum(abs(lightDirection.xz)));
            s += lightDirection * minStep;
            float iter = 0;
            while ( all(s < heightfield.size) && all(s > 0))
            {
                int2 ix = int2(s.xz * heightfield.iCellSize);
                double h = heightfield[ix];
                if (s.y < h + 0.0001) return 0.0;
                double d = (h - s.y) / k;
                s += max(d, minStep) * lightDirection;
                iter += 0.01f;
            }
            return 1.0;
        }
        
        #endregion
        
        
        #region Ambient Lighting

        public static void AmbientLighting(in doubleF heightfield, doubleF output)
        {
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    int2 direction = int2(i, j);
                    int2 start = select(heightfield.dimension - 1, 0, direction > 0);
                    int2 end = select(heightfield.dimension, 0, direction < 0);
                    for (int k = 0; k < cmax(heightfield.dimension); k++)
                    {
                        if (i != 0 && k < heightfield.dimension.x) ComputeAmbientExposureLine(in heightfield, direction, int2(start.x, k), end, output);
                        if (k == 0 && i != 0 && j != 0) continue; // We don't want to count the diagonal twice
                        if (j != 0 && k < heightfield.dimension.y) ComputeAmbientExposureLine(in heightfield, direction, int2(k, start.y), end, output);
                    }
                }
            }
        }
        
        public static JobHandle AmbientLighting(in doubleF heightfield, doubleF output, JobHandle dependsOn)
        {
            
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    int2 direction = int2(i, j);
                    int2 start = select(heightfield.dimension - 1, 0, direction > 0);
                    var job = new AmbientLightingJob
                    {
                        heightfield = heightfield,
                        output = output,
                        direction = int2(i, j),
                        startSide = start,
                        concurrent = false
                    };
                    int count = csum(heightfield.dimension * abs(direction));
                    dependsOn = job.ScheduleParallel(heightfield.Length, 128, dependsOn);
                }
            }

            return dependsOn;
        }
        
        [BurstCompile]
        private struct AmbientLightingJob : IJobFor
        {
            [ReadOnly]public doubleF heightfield;
            [NativeDisableParallelForRestriction] public doubleF output;
            public int2 direction, startSide;
            public bool concurrent;
            
            public void Execute(int index)
            {
                double sampleHeight = heightfield[index];
                double steepest = -heightfield.size.y / length(heightfield.cellSize * direction);
                int2 i = heightfield.cell(index);
                int2 start = i - cmin(abs(startSide - i)) * direction;
                
                for (int2 k = start; any(k != i); k += direction)
                {
                    var slope = (heightfield[k] - sampleHeight) * heightfield.size.y / length((i - k) * heightfield.cellSize);
                    steepest = max(slope, steepest);
                }

                double3 horizon = normalize(double3(normalize(double2(direction)), steepest).xzy);
                double3 normal = field.normal(heightfield, i);
                double3 rejection = dot(normal, horizon) * normal;
                // Since horizon is normalized and tangent is its projection, the norm of tangent is the cosine of the angle
                double3 tangent = horizon - rejection;
                double occlusion = select(1, lengthsq(tangent), rejection.y > 0) / 8;

                if (concurrent)
                {
                    unsafe
                    {
                        int ix = output.index(i);
                        double* a = (double*)output.field.GetUnsafePtr();
                        ConcurrentAdd(ref a[ix], occlusion);
                    }
                }
                else
                {
                    output[i] += occlusion;
                }
                
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ConcurrentAdd(ref double location, double value)
            {
                double v = location;
                while (true)
                {
                    double nv = v;
                    double updated = v + value;
                    v = Interlocked.CompareExchange(ref location, updated, v);
                    if (v.Equals(nv)) return;
                }
            }
        }
        
        private static void ComputeAmbientExposureLine(in doubleF heightfield, int2 direction, int2 start, int2 end, doubleF output)
        {
            int2 highestIndex = start;
            double highestValue = 0;
            
            for (int2 i = start; !any(i == end); i += direction)
            {
                double sampleHeight = heightfield[i];
                double steepest = -heightfield.size.y / length(heightfield.cellSize * direction);
                
                for (int2 k = highestIndex; any(k != i); k += direction)
                {
                    var height = heightfield[k];
                    if (height > highestValue)
                    {
                        highestValue = height;
                        highestIndex = k;
                    }
                    var slope = (heightfield[k] - sampleHeight) * heightfield.size.y / length((i - k) * heightfield.cellSize);
                    if (slope > steepest) steepest = slope;
                }

                double3 horizon = normalize(double3(normalize(double2(direction)), steepest).xzy);
                double3 normal = field.normal(heightfield, i);
                double3 rejection = dot(normal, horizon) * normal;
                // Since horizon is normalized and tangent is its projection, the norm of tangent is the cosine of the angle
                double3 tangent = horizon - rejection;
                double occlusion = select(1, lengthsq(tangent), rejection.y > 0);
                output[i] += occlusion / 8;
            }
        }
        
        #endregion
        
        
        #region Indirect Lighting

        public static void IndirectLighting(
            in doubleF heightfield,
            in doubleF directLighting,
            doubleF output,
            Random rng,
            ref Parameters P
        )
        {
            for (int receiver = 0; receiver < heightfield.field.Length; receiver++)
            {
                output[receiver] = ComputeIndirectLighting(in heightfield, in directLighting, receiver, P.IndirectDistanceSamples, P.IndirectAngularSamples, P.IndirectMaxDistance, rng);
            }
        }

        public static JobHandle IndirectLighting(
            in doubleF heightfield,
            in doubleF directLighting,
            doubleF output,
            ref Parameters P,
            JobHandle dependsOn
        )
        {
            var job = new IndirectLightingJob
            {
                heightfield = heightfield,
                directLighting = directLighting,
                output = output,
                distanceSamples = P.IndirectDistanceSamples,
                angularSamples = P.IndirectAngularSamples,
                maxDistance = P.IndirectMaxDistance,
            };
            
            return job.Schedule(heightfield.field.Length, dependsOn);
        }
        
        public static JobHandle IndirectLightingParallel(
            in doubleF heightfield,
            in doubleF directLighting,
            doubleF output,
            ref Parameters P,
            JobHandle dependsOn
        )
        {
            var job = new IndirectLightingJob
            {
                heightfield = heightfield,
                directLighting = directLighting,
                output = output,
                distanceSamples = P.IndirectDistanceSamples,
                angularSamples = P.IndirectAngularSamples,
                maxDistance = P.IndirectMaxDistance,
            };
            
            return job.ScheduleParallel(heightfield.field.Length, 128, dependsOn);
        }
        
        [BurstCompile]
        private struct IndirectLightingJob : IJobFor
        {
            [ReadOnly] public doubleF heightfield;
            [ReadOnly] public doubleF directLighting;
            [WriteOnly] public doubleF output;
            public int distanceSamples;
            public int angularSamples;
            public double maxDistance;
            
            public void Execute(int index)
            {
                output[index] = ComputeIndirectLighting(in heightfield, in directLighting, index, distanceSamples, angularSamples, maxDistance, new Random((uint)index + 1));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ComputeIndirectLighting(
            in doubleF heightfield, 
            in doubleF directLighting, 
            int receiver,
            int distanceSamples,
            int angularSamples,
            double maxDistance, 
            Random rng
        ) {
            double value = 0;
            Span<double> rs = stackalloc double[distanceSamples + 1];
            {
                double v = 0;
                rs[0] = 0;
                for (int i = 1; i <= distanceSamples; i++)
                {
                    rs[i] = v += rng.NextDouble();
                }
                double n = maxDistance / v;
                for (int i = 1; i <= distanceSamples; i++)
                {
                    rs[i] *= n;
                }
            }

            for (int i = 1; i <= distanceSamples; i++)
            {
                var r = rs[i];
                var rm = rs[i - 1] + r / 2;
                var rp = rs[min(distanceSamples, i + 1)] + r / 2;
                var area = PI2_DBL * (square(rp) - square(rm)) / angularSamples;
                for (int j = 0; j < angularSamples; j++)
                {
                    double2 theta = rng.NextDouble3Direction().xz;
                    
                    int2 rCell = heightfield.cell(receiver);
                    double3 rPos =  heightfield.coord(receiver);
                    double3 rNormal = normal(heightfield, receiver);
                    
                    int2 sender = int2(rCell + theta * r);
                    if (any(sender < 0) || any(sender >= heightfield.dimension)) continue;
                    
                    double3 sPos = heightfield.coord(sender);
                    double3 sNormal = normal(heightfield, sender);
                    
                    double3 sampleDirection = normalize(sPos - rPos);
                    double rPhi = max(0, dot(rNormal, sampleDirection));
                    double sPhi = max(0, dot(sNormal, -sampleDirection));
                    
                    double dsq = max(1, distancesq(heightfield.coord(receiver), heightfield.coord(sender)));
                    value += area * directLighting[sender] * rPhi * sPhi / dsq;
                }
            }

            return value;
        }
        
        #endregion


        #region Temperature
        
        public static void Temperature(doubleF output, doubleF direct, doubleF ambient, doubleF indirect, doubleF height, ref Parameters P)
        {
            var job = new TempAddJob
            {
                direct = direct,
                indirect = indirect,
                ambient = ambient,
                output = output,
                directFactor = P.IntensityDirect,
                indirectFactor = P.IntensityIndirect,
                ambientFactor = P.IntensityAmbient
            };

            job.Run(output.Length);
        }

        public static JobHandle Temperature(doubleF output, doubleF direct, doubleF ambient, doubleF indirect, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TempAddJob
            {
                direct = direct,
                indirect = indirect,
                ambient = ambient,
                output = output,
                directFactor = P.IntensityDirect,
                indirectFactor = P.IntensityIndirect,
                ambientFactor = P.IntensityAmbient
            };

            dependsOn = job.Schedule(output.Length, dependsOn);

            return dependsOn;
        }
        
        public static JobHandle TemperatureParallel(doubleF output, doubleF direct, doubleF ambient, doubleF indirect, doubleF height, ref Parameters P, JobHandle dependsOn)
        {
            var job = new TempAddJob
            {
                direct = direct,
                indirect = indirect,
                ambient = ambient,
                output = output,
                directFactor = P.IntensityDirect,
                indirectFactor = P.IntensityIndirect,
                ambientFactor = P.IntensityAmbient
            };

            dependsOn = job.ScheduleParallel(output.Length, 64, dependsOn);

            return dependsOn;
        }
        
        [BurstCompile]
        private struct TempAddJob : IJobFor
        {
            [ReadOnly] public doubleF direct, ambient, indirect;
            [WriteOnly] public doubleF output;
            public double directFactor, ambientFactor, indirectFactor;

            public void Execute(int index)
            {
                output[index] = direct[index] * directFactor + ambient[index] * ambientFactor + indirect[index] * indirectFactor;
            }
        }
        
        [BurstCompile]
        private struct ComputeTemperature : IJobFor
        {
            [ReadOnly] public doubleF Heightfield;
            public doubleF Temperature;
            public double Kt;
            public double Ki;

            public ComputeTemperature(doubleF heightfield, doubleF temperature, ref Parameters P)
            {
                Heightfield = heightfield;
                Temperature = temperature;
                Kt = P.TemperatureIncreasePerMetre;
                Ki = P.TemperatureIncreasePerSunlight;
            }
            
            public void Execute(int index)
            {
                var light = Temperature[index];
                Temperature[index] = Heightfield[index] * Kt + light * Ki;
            }
        }

        #endregion
    }
}