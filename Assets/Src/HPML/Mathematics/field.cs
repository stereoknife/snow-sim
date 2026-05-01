using System;
using HPML.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

// ReSharper disable InconsistentNaming

namespace HPML
{   
    public static class field
    {
        public static void add(in doubleF a, in doubleF b, doubleF result)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (a.Length != b.Length) throw new IndexOutOfRangeException("Attempted to add two fields of different size");
            if (b.Length != result.Length) throw new IndexOutOfRangeException("Attempted to add two larger fields into a smaller one");
#endif
            Debug.Assert(all(a.dimension == b.dimension) && all(b.dimension == a.dimension));
            unsafe
            {
                scalarfield.add((double*)a.field.GetUnsafeReadOnlyPtr(), (double*)b.field.GetUnsafeReadOnlyPtr(), (double*)result.field.GetUnsafePtr(), result.field.Length);
            }
        }
        
        public static void add(in doubleF a, doubleF to)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (a.Length != to.Length) throw new IndexOutOfRangeException("Attempted to add two fields of different size");
#endif
            Debug.Assert(all(a.dimension == to.dimension));
            unsafe
            {
                scalarfield.add((double*)a.field.GetUnsafeReadOnlyPtr(), (double*)to.field.GetUnsafeReadOnlyPtr(), (double*)to.field.GetUnsafePtr(), to.field.Length);
            }
        }
        
        public static void normalize(in doubleF a, doubleF result)
        {
            Debug.Assert(all(a.dimension == result.dimension));
            unsafe
            {
                var mm = minmax(a);
                scalarfield.normalize((double*)a.field.GetUnsafeReadOnlyPtr(),  (double*)result.field.GetUnsafePtr(), mm.x, mm.y, result.field.Length);
            }
        }
        
        public static void normalize(in doubleF a)
        {
            unsafe
            {
                var mm = minmax(a);
                scalarfield.normalize((double*)a.field.GetUnsafeReadOnlyPtr(),  (double*)a.field.GetUnsafePtr(), mm.x, mm.y, a.field.Length);
            }
        }
        
        public static double2 minmax(in doubleF a)
        {
            scalarfield.minmax(in a.field, out double2 result);
            return result;
        }
        
        public static double2 minmax(in double3F field)
        {
            double maxl = double.MinValue, minl = double.MaxValue;
            for (int i = 0; i < field.Length; i++)
            {
                maxl = max(lengthsq(field[i]), maxl);
                minl = min(lengthsq(field[i]), minl);
            }
            
            return double2(minl, maxl);
        }

        public static double2 gradient(in doubleF a, int at) => gradient(in a, a.cell(at));
        public static double2 gradient(in doubleF a, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            double2 norm = a.iCellSize / (up - down);
            
            return new double2(
                a[up.x, at.y] - a[down.x, at.y],
                a[at.x, up.y] - a[at.x, down.y]
            ) * norm;
        }

        public static double2 gradient2(in doubleF a, int at) => gradient2(in a, a.cell(at));
        public static double2 gradient2(in doubleF a, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            return new double2(
                (a[up.x] + a[down.x]) * 0.5 - a[at],
                (a[up.y] + a[down.y]) * 0.5 - a[at]
            ) * a.iCellSize;
        }
        
        public static double4 slope(in doubleF a, int at) => slope(in a, a.cell(at));
        public static double4 slope(in doubleF a, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            double h = a[at];
            double4 n = double4(
                a[up.x, at.y],
                a[down.x, at.y],
                a[at.x, up.y],
                a[at.x, down.y]
            );

            return (n - h) / a.iCellSize.xxyy;
        }
        
        public static double3 normal(in doubleF a, int at) => normal(a, a.cell(at));
        public static double3 normal(in doubleF a, int2 at)
        {
            double2 gradient = field.gradient(in a, at);
            double3 tanx = new(1.0, gradient.x, 0.0);
            double3 tanz = new(0.0, gradient.y, 1.0);
            return Unity.Mathematics.math.normalize(cross(tanz, tanx));
        }

        public static double lipschitz(in doubleF a)
        {
            double k = 0;
            for (int i = 0; i < a.Length; i++)
            {
                k = max(k, lengthsq(gradient(a, i)));
            }
            return sqrt(k);
        }

        // TODO: Remove
        [Obsolete]
        public static void normal_map(in doubleF a, double3F result)
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = normal(a, i);
            }
        }
    }
}