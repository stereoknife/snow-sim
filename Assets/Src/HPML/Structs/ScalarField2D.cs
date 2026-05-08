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
    
    public struct ScalarField2D<T> : INativeDisposable where T : struct
    {
        public NativeArray<T> field;
        public int2 dimension;
        public double3 size;
        public double2 cellSize => size.xz / dimension;
        public double2 iCellSize => dimension / size.xz;
        public int Length => field.Length;
        
        /*
        public static doubleF FromTexture<T>(Texture2D tex, double3 size, Allocator allocator)
        {
            var output = new doubleF(new(tex.width, tex.height), size, allocator);
            var data = tex.GetRawTextureData<ushort>();
            
            // TODO: Move to job or something like that
            for (int i = 0; i < output.field.Length; i++)
            {
                output.field[i] = size.y * (T)data[i] / (double)ushort.MaxValue;
            }

            return output;
        }
        */

        public ScalarField2D(ScalarField2D<T> sf, Allocator allocator)
        {
            field = new NativeArray<T>(area(sf.dimension), allocator);
            dimension = sf.dimension;
            size = sf.size;
        }
        
        public ScalarField2D(int2 dimension, double3 size, Allocator allocator)
        {
            field = new NativeArray<T>(area(dimension), allocator);
            this.dimension = dimension;
            this.size = size;
        }
        
        // Indexing
        // ========
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
        
        public T this[int key]
        {
            get => field[key];
            set => field[key] = value;
        }

        public T this[int2 key]
        {
            get => field[index(key)];
            set => field[index(key)] = value;
        }

        public T this[int i, int j]
        {
            get => field[index(i, j)];
            set => field[index(i, j)] = value;
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