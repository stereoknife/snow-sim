using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Sim
{
    public static class geometry
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int area(int2 value) => value.x * value.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float area(float2 value) => value.x * value.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double area(double2 value) => value.x * value.y;
    }
}