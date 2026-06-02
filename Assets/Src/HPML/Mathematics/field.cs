// ReSharper disable InconsistentNaming

using System;
using HPML.Unsafe;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace HPML
{   
    /// <summary>
    /// Static class containing mathematical operations performed on 2D scalar and vector fields.
    /// </summary>
    public static class field
    {
        /// <summary>
        /// Add two fields together and store the result in a third one. All fields must be the same dimension.
        /// Non-destructive.
        /// </summary>
        /// <param name="a">Lhs field</param>
        /// <param name="b">Rhs field</param>
        /// <param name="result">Field to store the result in</param>
        /// <exception cref="IndexOutOfRangeException">If collections checks are enabled the exception will be thrown
        /// if the fields are not of the same dimension.</exception>
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
        
        /// <summary>
        /// Add two fields together and store the result in the second one. All fields must be the same dimension.
        /// </summary>
        /// <param name="a">Field to add to b</param>
        /// <param name="b">Field where a is added to</param>
        /// <exception cref="IndexOutOfRangeException">If collections checks are enabled the exception will be thrown
        /// if the fields are not of the same dimension.</exception>
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
        
        public static unsafe void add(in doubleF a, double b)
        {
            scalarfield.mul((double*)a.field.GetUnsafeReadOnlyPtr(), b, (double*)a.field.GetUnsafePtr(), a.field.Length);
        }
        
        public static unsafe void add(in double2F a, double b)
        {
            scalarfield.mul((double*)a.array.GetUnsafeReadOnlyPtr(), b, (double*)a.array.GetUnsafePtr(), a.array.Length * 2);
        }

        public static unsafe void mul(in doubleF a, double b)
        {
            scalarfield.mul((double*)a.field.GetUnsafeReadOnlyPtr(), b, (double*)a.field.GetUnsafePtr(), a.field.Length);
        }
        
        public static unsafe void mul(in double2F a, double b)
        {
            scalarfield.mul((double*)a.array.GetUnsafeReadOnlyPtr(), b, (double*)a.array.GetUnsafePtr(), a.array.Length * 2);
        }
        
        /// <summary>
        /// Scale the values in a field to all be contained within the [0, 1] interval. Non-destructive.
        /// </summary>
        /// <param name="a">Field to normalize</param>
        /// <param name="result">Field to store the normalized result to</param>
        public static void normalize(in doubleF a, doubleF result)
        {
            Debug.Assert(all(a.dimension == result.dimension));
            unsafe
            {
                var mm = minmax(a);
                scalarfield.normalize((double*)a.field.GetUnsafeReadOnlyPtr(),  (double*)result.field.GetUnsafePtr(), mm.x, mm.y, result.field.Length);
            }
        }
        
        /// <summary>
        /// Scale the values in a field to all be contained within the [0, 1] interval.
        /// </summary>
        /// <param name="a">Field to normalize</param>
        public static void normalize(in doubleF a)
        {
            unsafe
            {
                var mm = minmax(a);
                scalarfield.normalize((double*)a.field.GetUnsafeReadOnlyPtr(),  (double*)a.field.GetUnsafePtr(), mm.x, mm.y, a.field.Length);
            }
        }
        
        /// <summary>
        /// Get the minimum and maximum values of a field.
        /// </summary>
        /// <param name="a">Field to get the values from</param>
        /// <returns>A double2 containing the minimum value in the x position and the maximum value in the y position.</returns>
        public static double2 minmax(in doubleF a)
        {
            scalarfield.minmax(in a.field, out double2 result);
            return result;
        }
        
        /// <summary>
        /// Get the minimum and maximum magnitudes of a field.
        /// </summary>
        /// <param name="a">Field to get the magnitudes from</param>
        /// <returns>A double2 containing the minimum magnitude in the x position and the maximum magnitude in the y position.</returns>
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

        
        /// <summary>
        /// Get the first derivative at a point on a field, computed using both neighbors if possible.
        /// </summary>
        /// <param name="a">Field to get the gradient of</param>
        /// <param name="at">Cell at which to get the gradient</param>
        /// <returns>A double2 with the gradient of the field</returns>
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
        
        public static double2x4 cgradient(in double4F a, int at) => cgradient(in a, a.cell(at));
        public static double2x4 cgradient(in double4F a, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            double2 norm = a.iCellSize / (up - down);
            
            return transpose(double4x2(
                (a[up.x, at.y] - a[down.x, at.y]) * norm.x,
                (a[at.x, up.y] - a[at.x, down.y]) * norm.y
            ));
        }
        
        public static double2x3 cgradient(in double3F a, int at) => cgradient(in a, a.cell(at));
        public static double2x3 cgradient(in double3F a, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            double2 norm = a.iCellSize / (up - down);
            
            return transpose(double3x2(
                (a[up.x, at.y] - a[down.x, at.y]) * norm.x,
                (a[at.x, up.y] - a[at.x, down.y]) * norm.y
            ));
        }
        
        public static double2x2 cgradient(in double2F a, int at) => cgradient(in a, a.cell(at));
        public static double2x2 cgradient(in double2F a, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            double2 norm = a.iCellSize / (up - down);
            
            return transpose(double2x2(
                (a[up.x, at.y] - a[down.x, at.y]) * norm.x,
                (a[at.x, up.y] - a[at.x, down.y]) * norm.y
            ));
        }

        /// <summary>
        /// Get the second derivative at a point on a field.
        /// </summary>
        /// <param name="a">Field to get the 2nd derivative of.</param>
        /// <param name="at">Cell at which to get the 2nd derivative.</param>
        /// <returns>A double2 with the 2nd derivative of the field.</returns>
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
        
        /// <summary>
        /// Get the slope of each orthogonal direction at a point on a field.
        /// </summary>
        /// <param name="a">Field to get the slopes from.</param>
        /// <param name="at">Point to get the slopes at.</param>
        /// <returns>A double4 with the slopes in order +x, -x, +y, -y.</returns>
        /// <remarks>Slope is 0 if there is no neighbor in that direction.</remarks>
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

            return (h - n) / a.iCellSize.xxyy;
        }
        
        /// <summary>
        /// Get the normal at a point on a field, computed using both neighbors if possible.
        /// </summary>
        /// <param name="a">Field to get the normal of</param>
        /// <param name="at">Cell at which to get the normal</param>
        /// <returns>A double3 with the normal of the field.</returns>
        public static double3 normal(in doubleF a, int at) => normal(a, a.cell(at));
        public static double3 normal(in doubleF a, int2 at)
        {
            double2 gradient = field.gradient(in a, at);
            double3 tanx = new(1.0, gradient.x, 0.0);
            double3 tanz = new(0.0, gradient.y, 1.0);
            return math.normalize(cross(tanz, tanx));
        }

        /// <summary>
        /// Get the Lipschitz coefficient of a field.
        /// </summary>
        /// <param name="a">Scalar field</param>
        /// <returns>Lipschitz coefficient</returns>
        public static double lipschitz(in doubleF a)
        {
            double k = 0;
            for (int i = 0; i < a.Length; i++)
            {
                k = max(k, lengthsq(gradient(a, i)));
            }
            return sqrt(k);
        }
        
        // TODO: Find a better solution
        public static double4 slope(doubleF a, double4F b, int at) => slope(a, b, a.cell(at));
        public static double4 slope(doubleF a, double4F b, int2 at)
        {
            int2 up   = min(at + 1, b.dimension - 1);
            int2 down = max(at - 1, 0);
            double h = a[at] + csum(b[at]);
            double4 n = double4(
                a[at] + csum(b[up.x, at.y]),
                a[at] + csum(b[down.x, at.y]),
                a[at] + csum(b[at.x, up.y]),
                a[at] + csum(b[at.x, down.y])
            );
            return (n - h) / b.iCellSize.xxyy;
        }
        
        public static double2 gradient2(doubleF a, double4F b, int at) => gradient2(a, b, a.cell(at));
        /*
        public static double2 gradient2(doubleF a, double4F b, int2 at)
        {
            int2 up   = min(at + 1, b.dimension - 1);
            int2 down = max(at - 1, 0);
            double h = a[at] + csum(b[at]);
            return new double2(
                (a[up.x] + csum(b[up.x]) + a[down.x] + csum(b[up.x])) * 0.5 - h,
                (a[up.y] + csum(b[up.x]) + a[down.y] + csum(b[up.y])) * 0.5 - h
            ) * a.iCellSize * 0.667;
        }
        /*/
        public static double2 gradient2(doubleF a, double4F b, int2 at)
        {
            int2 up   = min(at + 1, b.dimension - 1);
            int2 down = max(at - 1, 0);
            double h = a[at] + csum(b[at]);
            return new double2(
                a[up.x] + csum(b[up.x]) + a[down.x] + csum(b[up.x]) - 2*h,
                a[up.y] + csum(b[up.x]) + a[down.y] + csum(b[up.y]) - 2*h
            ) * a.iCellSize * a.iCellSize;
        }
        //*/
        
        public static double2 gradient(in doubleF a, in double4F b, int at) => gradient(in a, in b, a.cell(at));
        public static double2 gradient(in doubleF a, in double4F b, int2 at)
        {
            int2 up   = min(at + 1, a.dimension - 1);
            int2 down = max(at - 1, 0);
            double2 norm = a.iCellSize / (up - down);
            
            return new double2(
                a[up.x, at.y] + csum(b[up.x, at.y]) - a[down.x, at.y] - csum(b[down.x, at.y]),
                a[at.x, up.y] + csum(b[at.x, up.y]) - a[at.x, down.y] - csum(b[at.x, down.y])
            ) * norm;
        }

        public static double3x3 quadric(in doubleF field, int2 at, int w)
        {
            var n = w / 2;
            var z0 = field[at];
            double m00 = 0, m01 = 0, m10 = 0, m11 = 0, m22 = 0, m33 = 0, m44 = 0, r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0;

            for (int i = -n; i <= n; i++) {
                for (int j = -n; j <= n; j++)
                {
                    var ij = int2(i, j) + at;
                    var xy = int2(i, j) * field.cellSize;
                    var z = any(ij < 0 | ij >= field.dimension) ? z0 : field[ij];
                    z -= z0;

                    var xy2 = xy * xy;

                    m00 += xy2.x * xy2.x;
                    m11 += xy2.y * xy2.y;
                    m01 += xy2.x * xy2.y;
                    m10 += xy2.x * xy2.y;
                    m22 += xy2.x * xy2.y;
                    m33 += xy2.x;
                    m44 += xy2.y;
                    m01 += xy2.y;

                    r0 += z * xy2.x;
                    r1 += z * xy2.y;
                    r2 += z * xy.x * xy.y;
                    r3 += z * xy.x;
                    r4 += z * xy.y;
                }
            }

            var det = m00 * m11 - m01 * m10;
            var a = (m11 * r0 - m01 * r1) / det;
            var b = (m00 * r1 - m10 * r0) / det;
            var c = r2 / m22;
            var d = r3 / m33;
            var e = r4 / m44;
            var f = 0d;

            return double3x3(
                f, e, b,
                d, c, 0,
                a, 0, 0
            );
        }

        public static double2 curv(doubleF field, int index, int w) => curv(field, field.cell(index), w);
        public static double2 curv(doubleF field, int2 at, int w)
        {
            var q = quadric(field, at, w);
            var a = q.c0.z;
            var b = q.c2.x;
            var c = q.c1.y;
            var d = q.c0.x;
            var e = q.c1.x;

            var profile = 2.0 * (a * d * d + b * e * e + c * e * d) / ((e * e + d * d) * sqrt(pow(1.0 + d * d + e * e, 3)));
            var contour = 2.0 * (b * d * d + a * e * e - c * d * e) / sqrt(pow(e * e + d * d, 3));

            return double2(profile, contour);
        }
    }
}