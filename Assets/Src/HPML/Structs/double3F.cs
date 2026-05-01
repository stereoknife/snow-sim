using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using static HPML.vfmath;

namespace HPML
{
    public struct double3F : INativeDisposable
    {
        public int2 dimension;
        public double3 size;
        public double2 cellSize => size.xz / dimension;
        public double2 iCellSize => dimension / size.xz;
        public int Length => array.Length;
        
        private NativeArray<double3> array;
        
        public double3F(double3F sf, Allocator allocator)
        {
            array = new NativeArray<double3>(sf.array.Length, allocator);
            dimension = sf.dimension;
            size = sf.size;
        }
        
        public double3F(doubleF sf, Allocator allocator, double3 initialValue = default)
        {
            array = new NativeArray<double3>(sf.field.Length, allocator);
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = initialValue;
            }
            dimension = sf.dimension;
            size = sf.size;
        }
        
        public double3F(int2 dimension, double3 size, Allocator allocator)
        {
            array = new NativeArray<double3>(vec.area(dimension), allocator);
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
        
        public double3 this[int key]
        {
            get => array[key];
            set => array[key] = value;
        }

        public double3 this[int2 key]
        {
            get => array[index(key)];
            set => array[index(key)] = value;
        }

        public double3 this[int i, int j]
        {
            get => array[index(i, j)];
            set => array[index(i, j)] = value;
        }
        
        public void Dispose() =>  array.Dispose();
        public JobHandle Dispose(JobHandle dependsOn) =>  array.Dispose(dependsOn);
        public bool IsCreated => array.IsCreated;
        
        
        public JobHandle ToTextureRGBA(NativeArray<Color32> texture, JobHandle dependsOn, bool normalize = false) =>
            new ToTextureRGBAJob{ field = this, texture = texture, normalize = normalize}.Schedule(dependsOn);
        
        public Texture2D ToTextureRGBA(bool makeNoLongerReadable = true) =>
            ToTextureRGBA(new Texture2D(dimension.x, dimension.y, TextureFormat.RGBA32, false), makeNoLongerReadable);
        public Texture2D ToTextureRGBA(Texture2D texture, bool makeNoLongerReadable = true)
        {
            double norm = minmax(this).y;
            var data = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < array.Length; i++)
            {
                var c = byte.MaxValue * (array[i] / norm);
                data[i] = new Color32((byte)c.x, (byte)c.y, (byte)c.z, byte.MaxValue);
            }
            texture.Apply(false, makeNoLongerReadable);
            return texture;
        }

        private struct ToTextureRGBAJob : IJob
        {
            [ReadOnly] public double3F field;
            public NativeArray<Color32> texture;
            public bool normalize;
            
            public void Execute()
            {
                double norm = 1;
                if (!normalize) norm /= minmax(field).y;
                for (int i = 0; i < field.Length; i++)
                {
                    var v = field[i] * norm;
                    if (normalize) v = (normalize(v) + 1) * 0.5;
                    var c = byte.MaxValue * v;
                    texture[i] = new Color32((byte)c.x, (byte)c.y, (byte)c.z, byte.MaxValue);
                }
            }
        }
    }
}