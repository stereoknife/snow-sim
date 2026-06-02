using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static HPML.vec;
using int2 = Unity.Mathematics.int2;

namespace HPML
{
    
    public struct ScalarField2D : INativeDisposable
    {
        public NativeArray<double> array;
        public int2 dimension;
        public int layers;
        public double2 size;

        public double2 cellSize => size / dimension;
        public double2 iCellSize => dimension / size;
        public int Length => array.Length;

        public ScalarField2D(ScalarField2D sf, Allocator allocator)
        {
            array = new NativeArray<double>(sf.Length, allocator);
            dimension = sf.dimension;
            size = sf.size;
            layers = sf.layers;
        }
        
        public ScalarField2D(int2 dimension, int layers, double2 size, Allocator allocator)
        {
            array = new NativeArray<double>(area(dimension) * layers, allocator);
            this.dimension = dimension;
            this.layers = layers;
            this.size = size;
        }
        
        // Indexing
        // ========
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int2 cell(int idx) => new(idx / layers % dimension.x, idx / layers / dimension.y);
        public int3 cell(int idx) => new(idx % dimension.x, idx / dimension.x % dimension.y, idx / area(dimension));
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int index(int2 cell) => (cell.y * dimension.x + cell.x) * layers;
        public int index(int3 cell) => cell.z * area(dimension) + cell.y * dimension.x + cell.x;
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int index(int i, int j) => (j * dimension.x + i) * layers;
        public int index(int i, int j, int k) => k * area(dimension) + j * dimension.x + i;
        
        public double this[int key]
        {
            get => array[key];
            set => array[key] = value;
        }

        public double this[int3 key]
        {
            get => array[index(key)];
            set => array[index(key)] = value;
        }

        public double this[int i, int j, int k]
        {
            get => array[index(i, j, k)];
            set => array[index(i, j, k)] = value;
        }

        public doubleF Layer(int i)
        {
            var layerLength = area(dimension);
            var slice = array.GetSubArray(layerLength * i, layerLength);
            return new doubleF
            {
                dimension = dimension,
                size = math.double3(size, 1).xzy,
                field = slice
            };
        }

        // IDispose Implementation
        // =======================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => array.Dispose();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JobHandle Dispose(JobHandle inputDeps) => array.Dispose(inputDeps);
        
        public bool IsCreated => array.IsCreated;
    }
}