using System.Runtime.CompilerServices;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace HPML
{
    public static class vec
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int area(int2 value) => value.x * value.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float area(float2 value) => value.x * value.y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double area(double2 value) => value.x * value.y;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int volume(int3 value) => value.x * value.y * value.z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float volume(float3 value) => value.x * value.y * value.z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double volume(double3 value) => value.x * value.y * value.z;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 cw(float2 value) => float2(-value.y, value.x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 cw(double2 value) => double2(-value.y, value.x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 ccw(float2 value) => float2(value.y, -value.x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 ccw(double2 value) => double2(value.y, -value.x);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2x2 unzip(int2 a, int2 b) => transpose(int2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2x2 unzip(int2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4x2 unzip(int4 a, int4 b) => int4x2(int4(a.xz, b.xz), int4(a.yw, b.yw));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4x2 unzip(int4x2 x) => unzip(x.c0, x.c1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2x2 unzip(uint2 a, uint2 b) => transpose(uint2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2x2 unzip(uint2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint4x2 unzip(uint4 a, uint4 b) => uint4x2(uint4(a.xz, b.xz), uint4(a.yw, b.yw));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint4x2 unzip(uint4x2 x) => unzip(x.c0, x.c1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x2 unzip(float2 a, float2 b) => transpose(float2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x2 unzip(float2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x2 unzip(float4 a, float4 b) => float4x2(float4(a.xz, b.xz), float4(a.yw, b.yw));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x2 unzip(float4x2 x) => unzip(x.c0, x.c1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2x2 unzip(double2 a, double2 b) => transpose(double2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2x2 unzip(double2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4x2 unzip(double4 a, double4 b) => double4x2(double4(a.xz, b.xz), double4(a.yw, b.yw));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4x2 unzip(double4x2 x) => unzip(x.c0, x.c1);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2x2 zip(int2 a, int2 b) => transpose(int2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2x2 zip(int2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4x2 zip(int4 a, int4 b) => int4x2(int4(a.x, b.x, a.y, b.y), int4(a.z, b.z, a.w, b.w));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int4x2 zip(int4x2 x) => zip(x.c0, x.c1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2x2 zip(uint2 a, uint2 b) => transpose(uint2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2x2 zip(uint2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint4x2 zip(uint4 a, uint4 b) => uint4x2(uint4(a.x, b.x, a.y, b.y), uint4(a.z, b.z, a.w, b.w));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint4x2 zip(uint4x2 x) => zip(x.c0, x.c1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x2 zip(float2 a, float2 b) => transpose(float2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x2 zip(float2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x2 zip(float4 a, float4 b) => float4x2(float4(a.x, b.x, a.y, b.y), float4(a.z, b.z, a.w, b.w));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x2 zip(float4x2 x) => zip(x.c0, x.c1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2x2 zip(double2 a, double2 b) => transpose(double2x2(a, b));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2x2 zip(double2x2 x) => transpose(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4x2 zip(double4 a, double4 b) => double4x2(double4(a.x, b.x, a.y, b.y), double4(a.z, b.z, a.w, b.w));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double4x2 zip(double4x2 x) => zip(x.c0, x.c1);
        
    }
}