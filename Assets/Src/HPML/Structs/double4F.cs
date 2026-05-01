using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace HPML
{
    public struct double4F : INativeDisposable
    {
        public int2 dimension;
        public double3 size;
        public double2 cellSize => size.xz / dimension;
        public double2 iCellSize => dimension / size.xz;
        public int Length => array.Length;
        
        public NativeArray<double4> array;
        
        public double4F(double4F sf, Allocator allocator)
        {
            array = new NativeArray<double4>(sf.array.Length, allocator);
            dimension = sf.dimension;
            size = sf.size;
        }
        
        public double4F(doubleF sf, Allocator allocator, double4 initialValue = default)
        {
            array = new NativeArray<double4>(sf.field.Length, allocator);
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = initialValue;
            }
            dimension = sf.dimension;
            size = sf.size;
        }
        
        public double4F(int2 dimension, double3 size, Allocator allocator)
        {
            array = new NativeArray<double4>(vec.area(dimension), allocator);
            this.dimension = dimension;
            this.size = size;
        }
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 cell(int idx) => new(idx % dimension.x, idx / dimension.y);
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 cell(double2 coord) => int2(coord * iCellSize);
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int index(int2 cell) => cell.y * dimension.x + cell.x;
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int index(int i, int j) => j * dimension.x + i;
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int index(double2 coord) => index(int2(coord * iCellSize));
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool valid(double2 coords) => all(coords >= 0) && all(coords < size.xz);
        
        public double4 this[int key]
        {
            get => array[key];
            set => array[key] = value;
        }

        public double4 this[int2 key]
        {
            get => array[index(key)];
            set => array[index(key)] = value;
        }

        public double4 this[int i, int j]
        {
            get => array[index(i, j)];
            set => array[index(i, j)] = value;
        }
        
        public void Dispose() =>  array.Dispose();
        public JobHandle Dispose(JobHandle dependsOn) =>  array.Dispose(dependsOn);
        public bool IsCreated => array.IsCreated;
    }
}