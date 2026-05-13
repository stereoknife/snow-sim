using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static HPML.vec;
using static Unity.Mathematics.math;
using int2 = Unity.Mathematics.int2;

namespace HPML
{
    
    public struct doubleFL : INativeDisposable
    {
        public NativeArray<double> field;
        public int3 dimension;
        public double3 size;
        public double2 cellSize => size.xz / dimension.xz;
        public double2 iCellSize => dimension.xz / size.xz;
        public int Length => field.Length;
        
        public static doubleFL FromTexture(Texture2D tex, int layers, double3 size, Allocator allocator)
        {
            var output = new doubleFL(new(tex.width, layers, tex.height), size, allocator);
            var data = tex.GetRawTextureData<ushort>();
            
            // TODO: Move to job or something like that
            for (int i = 0; i < output.field.Length; i++)
            {
                output.field[i] = size.y * (double)data[i] / (double)ushort.MaxValue;
            }

            return output;
        }

        public doubleFL(doubleFL sf, Allocator allocator)
        {
            field = new NativeArray<double>(volume(sf.dimension), allocator);
            dimension = sf.dimension;
            size = sf.size;
        }
        public doubleFL(int3 dimension, double3 size, Allocator allocator)
        {
            field = new NativeArray<double>(volume(dimension), allocator);
            this.dimension = dimension;
            this.size = size;
        }
        
        // Indexing
        // ========
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 cell(int idx) => new(idx % dimension.x, idx / dimension.y, idx % area(dimension.xz));
        
        //[Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int2 cell(double2 coord) => int2(coord * iCellSize);
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int index(int3 cell) => cell.z * area(dimension.xz) + cell.y * dimension.x + cell.x;
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int index(int2 cell, int layer) => layer * area(dimension.xz) + cell.y * dimension.x + cell.x;
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int index(int i, int j, int k) => k * area(dimension.xz) * j * dimension.x + i;
        
        //[Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int index(double2 coord) => index(int2(coord * iCellSize));

        //[Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public double3 coord(int2 cell) => double3(cell * cellSize, this[cell]).xzy;
        
        //[Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public double3 coord(int i) => double3(cell(i) * cellSize, this[i]).xzy;
        
        public double this[int key]
        {
            get => field[key];
            set => field[key] = value;
        }

        public double this[int3 key]
        {
            get => field[index(key)];
            set => field[index(key)] = value;
        }

        public double this[int i, int j, int k]
        {
            get => field[index(i, j, k)];
            set => field[index(i, j, k)] = value;
        }
        
        public double this[int2 ij, int k]
        {
            get => field[index(ij, k)];
            set => field[index(ij, k)] = value;
        }
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(double2 coords) => all(coords >= 0) && all(coords < size.xz);


        // IDispose Implementation
        // =======================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => field.Dispose();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JobHandle Dispose(JobHandle inputDeps) => field.Dispose(inputDeps);
        
        public bool IsCreated => field.IsCreated;
    }
}