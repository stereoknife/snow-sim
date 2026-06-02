using System;
using System.Runtime.CompilerServices;
using HPML;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static Unity.Mathematics.math;

namespace TFM.Simulation
{
    public static class Terrain
    {
        public static JobHandle Hazard(doubleF height, doubleF hazard, JobHandle dependsOn)
        {
            var curvs = new NativeArray<int>(hazard.Length, Allocator.TempJob);
            var grads = new NativeArray<double>(hazard.Length, Allocator.TempJob);
            var roughness = new NativeArray<double>(hazard.Length, Allocator.TempJob);
            
            var windowSize = 5;
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
                var category = select(0, select(int2(1), 2, curv >= threshold), curv > -threshold);
                curvatures[index] = category.x + category.y * 3;
            }
        }

        [BurstCompile]
        private struct GradientJob : IJobFor
        {
            [WriteOnly] public NativeArray<double> gradient;
            [ReadOnly] public doubleF height;

            public void Execute(int index)
            {
                var mean = 35; // should be 39
                var stdv = 5.6; // should be 3.7
                var g = abs(field.gradient(height, index));
                var gl = length(g);
                var gd = length(g / cmax(g) * height.cellSize);
                var ga = degrees(atan(gl / gd));
                var prob = nd(mean, stdv, ga);
                gradient[index] = select(prob, 0, gl < 0.01);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private double nd(double mean, double stdv, double x)
                => 1 / sqrt(PI2_DBL * stdv * stdv) * exp(-pow(x - mean, 2) / (2 * stdv * stdv));
        }
        
        [BurstCompile]
        private struct RoughnessJob : IJobFor
        {
            [WriteOnly] public NativeArray<double> roughness;
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

                var r = 1 - length(norms) / vec.area(window);
                var prob = lnd(mean, stdv, r);
                roughness[index] = prob;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private double lnd(double mean, double stdv, double x)
                => 1 / (x * stdv * sqrt(PI2_DBL)) * exp(-pow(log(x - mean), 2) / (2 * stdv * stdv));
        }

        [BurstCompile]
        private struct CombineJob : IJob
        {
            [DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> curvatures;
            [DeallocateOnJobCompletion][ReadOnly] public NativeArray<double> gradients;
            [DeallocateOnJobCompletion][ReadOnly] public NativeArray<double> roughness;
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