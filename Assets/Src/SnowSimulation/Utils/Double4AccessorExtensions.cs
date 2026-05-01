using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TFM.Utils
{
    // Utility class to keep a consistent mapping between snow layers and double4 values
    public static class Double4AccessorExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double c(this double4 f) => f.x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double s(this double4 f) => f.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double u(this double4 f) => f.z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double p(this double4 f) => f.w;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double c(this double4 f, double x) => f.x = x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double s(this double4 f, double x) => f.y = x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double u(this double4 f, double x) => f.z = x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double p(this double4 f, double x) => f.w = x;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double compacted(this double4 f) => f.x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double stable   (this double4 f) => f.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double unstable (this double4 f) => f.z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double powder   (this double4 f) => f.w;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double compacted(this double4 f, double x) => f.x = x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double stable   (this double4 f, double x) => f.y = x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double unstable (this double4 f, double x) => f.z = x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static double powder   (this double4 f, double x) => f.w = x;
    }
}