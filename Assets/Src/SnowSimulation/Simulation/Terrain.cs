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
    public static class Terrain
    {
        public static JobHandle Hazard(doubleF height, doubleF hazard, JobHandle dependsOn)
        {
            var curvs = new NativeArray<int>(hazard.Length, Allocator.TempJob);
            var grads = new doubleF(hazard, Allocator.TempJob);
            var roughness = new doubleF(hazard, Allocator.TempJob);
            
            var windowSize = 3;
            Debug.Log(windowSize);

            var cj = new CurvatureJob
            {
                curvatures = curvs,
                height = height,
                windowSize = windowSize,
                threshold = 0.2
            };

            var gj = new GradientJob
            {
                gradient = grads,
                height = height
            };

            var rj = new RoughnessJob
            {
                height = height,
                roughness = roughness,
                window = windowSize,
            };

            var ch = cj.ScheduleParallel(height.Length, 64, dependsOn);
            var gh = gj.ScheduleParallel(height.Length, 64, dependsOn);
            var rh = rj.ScheduleParallel(height.Length, 64, dependsOn);

            JobHandle.CombineDependencies(ch, gh, rh).Complete();
            
            var hj = new CombineJob
            {
                curvatures = curvs,
                gradients = grads,
                roughness = roughness,
                hazard = hazard
            };

            return hj.Schedule(JobHandle.CombineDependencies(ch, gh, rh));
        }
        
        public static JobHandle Curvature(doubleF height, NativeArray<int> curvature, int windowSize, JobHandle dependsOn)
        {
            //var windowSize = 5;
            var cj = new CurvatureJob
            {
                curvatures = curvature,
                height = height,
                windowSize = windowSize,
                threshold = 0.2
            };

            return cj.ScheduleParallel(height.Length, 64, dependsOn);
        }
        
        public static JobHandle Gradient(doubleF height, doubleF gradient, JobHandle dependsOn)
        {
            var gj = new GradientJob
            {
                gradient = gradient,
                height = height
            };

            return gj.ScheduleParallel(height.Length, 64, dependsOn);
        }
        
        public static JobHandle Roughness(doubleF height, doubleF roughness, int windowSize, JobHandle dependsOn)
        {
            //var windowSize = 3;//(int)round(100 * height.iCellSize.x);
            var rj = new RoughnessJob
            {
                height = height,
                roughness = roughness,
                window = windowSize,
            };

            return rj.ScheduleParallel(height.Length, 64, dependsOn);
        }

        public static JobHandle GradientDist(doubleF array, JobHandle dependsOn)
        {
            var job = new NormalDistJob
            {
                array = array,
                mean = 39,
                stdv = 3.7,
            };
            
            return job.ScheduleParallel(array.Length, 64, dependsOn);
        }
        
        public static JobHandle RoughnessDist(doubleF array, JobHandle dependsOn)
        {
            var job = new LogNormalDistJob
            {
                array = array,
                mean = -5.68397985,
                stdv = 0.53062825,
            };
            
            return job.ScheduleParallel(array.Length, 64, dependsOn);
        }
        
        public static JobHandle Hazard(doubleF hazard, doubleF gradient, doubleF roughness, NativeArray<int> curvature, JobHandle dependsOn)
        {
            var cj = new CombineJob
            {
                curvatures = curvature,
                gradients = gradient,
                roughness = roughness,
                hazard = hazard
            };

            return cj.Schedule(dependsOn);
        }
        
        [BurstCompile]
        private struct CurvatureJob : IJobFor
        {
            [WriteOnly] public NativeArray<int> curvatures;
            [ReadOnly] public doubleF height;
            public int windowSize;
            public double threshold;
            
            public void Execute(int index)
            {
                var curv = field.curv(height, index, windowSize);
                var category = (int2)step(curv, -threshold) + (int2)step(curv, threshold);
                //var category = select(0, select(int2(1), 2, curv >= threshold), curv < -threshold);
                curvatures[index] = category.x + category.y * 3;
                //curvatures[index] = category.x;
            }
        }

        [BurstCompile]
        private struct GradientJob : IJobFor
        {
            [WriteOnly] public doubleF gradient;
            [ReadOnly] public doubleF height;

            public void Execute(int index)
            {
                var mean = 35; // should be 39
                var stdv = 5.6; // should be 3.7
                var g = abs(field.gradient(height, index));
                var gl = dot(g, normalizesafe(g, 0));
                var ga = degrees(atan(gl));
                gradient[index] = ga; //select(ga, 0, gl < 0.01);
            }
        }
        
        [BurstCompile]
        private struct RoughnessJob : IJobFor
        {
            [WriteOnly] public doubleF roughness;
            [ReadOnly] public doubleF height;
            public int window;

            public void Execute(int index)
            {
                // These values are weird bc they are logs!!!
                var mean = -5.68397985;
                var stdv = 0.53062825;

                var ij = height.cell(index);
                var start = max(0, ij - window / 2);
                var end = min(height.dimension - 1, ij + window / 2);

                var norms = double3(0);
                for (int i = start.x; i < end.x; i++)
                {
                    for (int j = start.y; j < end.y; j++)
                    {
                        norms += field.normal(height, int2(i, j));
                    }
                }

                var r = 1 - length(norms) / vec.area(end - start);
                //var prob = lnd(mean, stdv, r);
                roughness[index] = r;
            }
        }
        
        [BurstCompile]
        private struct NormalDistJob : IJobFor
        {
            public doubleF array;
            public double mean, stdv;
            
            public void Execute(int index)
            {
                array[index] = nd(mean, stdv, array[index]);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private double nd(double mean, double stdv, double x)
                => 1 / sqrt(PI2_DBL * stdv * stdv) * exp(-pow(x - mean, 2) / (2 * stdv * stdv));
        }
        
        [BurstCompile]
        private struct LogNormalDistJob : IJobFor
        {
            public doubleF array;
            public double mean, stdv;
            
            public void Execute(int index)
            {
                array[index] = select(lnd(mean, stdv, array[index]), 0, array[index] < 0.0000000001);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private double lnd(double mean, double stdv, double x)
                => 1 / (x * stdv * sqrt(PI2_DBL)) * exp(-pow(log(x) - mean, 2) / (2 * stdv * stdv));
        }

        //[BurstCompile]
        private struct CombineJob : IJob
        {
            [DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> curvatures;
            [DeallocateOnJobCompletion][ReadOnly] public doubleF gradients;
            [DeallocateOnJobCompletion][ReadOnly] public doubleF roughness;
            [WriteOnly] public doubleF hazard;
            
            public void Execute()
            {
                Span<double> lut = stackalloc double[] {
                    0.0350877192982456,
                    0.210526315789474,
                    0.236842105263158,
                    0.0175438596491228,
                    0.280701754385965,
                    0.105263157894737,
                    0.0350877192982456,
                    0.0526315789473684,
                    0.0263157894736842
                };

                var h = 0.0;
                for (int i = 0; i < hazard.Length; i++)
                {
                    h += gradients[i] * roughness[i] * lut[curvatures[i]];
                    hazard[i] = h;
                }
            }
        }
    }
}