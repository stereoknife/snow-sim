using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace HPML.Unsafe
{
    [BurstCompile]
    public static class scalarfield
    {
        [BurstCompile]
        public static unsafe void add(double* a, double* b, double* result, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Unity.Burst.CompilerServices.Loop.ExpectVectorized();
                result[i] = a[i] + b[i];
            }
        }
        
        [BurstCompile]
        public static unsafe void normalize(double* a, double* result, double min, double max, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Unity.Burst.CompilerServices.Loop.ExpectVectorized();
                result[i] = (a[i] - min) / (max - min);
            }
        }
        
        [BurstCompile]
        public static void minmax(in NativeArray<double> a, out double2 minmax)
        {
            double4 max4 = double.MinValue, min4 = double.MaxValue;
            int i;
            int count = a.Length / 4 * 4;
            for (i = 0; i < count; i += 4)
            {
                var v = a.ReinterpretLoad<double4>(i);
                max4 = Unity.Mathematics.math.max(v, max4);
                min4 = Unity.Mathematics.math.min(v, min4);
            }
            
            double max = cmax(max4), min = cmin(min4);
            
            for (; i < a.Length; i++)
            {
                max = Unity.Mathematics.math.max(a[i], max);
                min = Unity.Mathematics.math.min(a[i], min);
            }
            
            minmax = double2(min, max);
        }
    }
}