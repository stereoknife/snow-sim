using Sim.Structs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Sim.Mathematics
{
    [BurstCompile]
    public static class sfmath
    {
        public static void add(in ScalarField2D a, in ScalarField2D b, ScalarField2D result)
        {
            Debug.Assert(all(a.dimension == b.dimension) && all(b.dimension == a.dimension));
            unsafe
            {
                sfmUnsafe.add((double*)a.field.GetUnsafeReadOnlyPtr(), (double*)b.field.GetUnsafeReadOnlyPtr(), (double*)result.field.GetUnsafePtr(), result.field.Length);
            }
        }
        
        public static void add(in ScalarField2D a, ScalarField2D to)
        {
            Debug.Assert(all(a.dimension == to.dimension));
            unsafe
            {
                sfmUnsafe.add((double*)a.field.GetUnsafeReadOnlyPtr(), (double*)to.field.GetUnsafeReadOnlyPtr(), (double*)to.field.GetUnsafePtr(), to.field.Length);
            }
        }
        
        public static void normalize(in ScalarField2D a, ScalarField2D result)
        {
            Debug.Assert(all(a.dimension == result.dimension));
            unsafe
            {
                var mm = minmax(a);
                sfmUnsafe.normalize((double*)a.field.GetUnsafeReadOnlyPtr(),  (double*)result.field.GetUnsafePtr(), mm.x, mm.y, result.field.Length);
            }
        }
        
        public static void normalize(in ScalarField2D a)
        {
            unsafe
            {
                var mm = minmax(a);
                sfmUnsafe.normalize((double*)a.field.GetUnsafeReadOnlyPtr(),  (double*)a.field.GetUnsafePtr(), mm.x, mm.y, a.field.Length);
            }
        }
        
        public static double2 minmax(in ScalarField2D a)
        {
            sfmUnsafe.minmax(in a.field, out double2 result);
            return result;
        }

        public static double2 gradient(in ScalarField2D a, int i) => gradient(in a, a.cell(i));
        public static double2 gradient(in ScalarField2D a, int2 i)
        {
            int2 up   = min(i + 1, a.dimension - 1);
            int2 down = max(i - 1, 0);
            return new double2(
                a[up.x, i.y] - a[down.x, i.y],
                a[i.x, up.y] - a[i.x, down.y]
            ) * a.iCellSize;
        }
        
        public static double3 normal(in ScalarField2D a, int i) => normal(a, a.cell(i));
        public static double3 normal(in ScalarField2D a, int2 i)
        {
            double2 gradient = sfmath.gradient(in a, i);
            double3 tanx = new(1.0, gradient.x, 0.0);
            double3 tanz = new(0.0, gradient.y, 1.0);
            return math.normalize(cross(tanz, tanx));
        }

        public static double k(in ScalarField2D a)
        {
            double k = 0;
            for (int i = 0; i < a.Length; i++)
            {
                k = max(k, lengthsq(gradient(a, i)));
            }
            return sqrt(k);
        }

        public static void normal_map(in ScalarField2D a, VectorField2D result)
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = normal(a, i);
            }
        }
    }
}