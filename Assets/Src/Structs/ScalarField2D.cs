using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Sim.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using static Sim.geometry;
using int2 = Unity.Mathematics.int2;

namespace Sim.Structs
{
    
    public struct ScalarField2D : INativeDisposable
    {
        public NativeArray<double> field;
        public int2 dimension;
        public double3 size;
        public double2 cellSize => size.xz / dimension;
        public double2 iCellSize => dimension / size.xz;
        public int Length => field.Length;
        
        public static ScalarField2D FromTexture(Texture2D tex, double3 size, Allocator allocator)
        {
            var output = new ScalarField2D(new(tex.width, tex.height), size, allocator);
            var data = tex.GetRawTextureData<ushort>();
            
            // TODO: Move to job or something like that
            for (int i = 0; i < output.field.Length; i++)
            {
                output.field[i] = size.y * (double)data[i] / (double)ushort.MaxValue;
            }

            return output;
        }

        public ScalarField2D(ScalarField2D sf, Allocator allocator)
        {
            field = new NativeArray<double>(area(sf.dimension), allocator);
            dimension = sf.dimension;
            size = sf.size;
        }
        public ScalarField2D(int2 dimension, double3 size, Allocator allocator)
        {
            field = new NativeArray<double>(area(dimension), allocator);
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

        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double3 coord(int2 cell) => double3(cell * cellSize, this[cell]).xzy;
        
        [Pure] [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double3 coord(int i) => double3(cell(i) * cellSize, this[i]).xzy;
        
        public double this[int key]
        {
            get => field[key];
            set => field[key] = value;
        }

        public double this[int2 key]
        {
            get => field[index(key)];
            set => field[index(key)] = value;
        }

        public double this[int i, int j]
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
        
        public JobHandle Normalize(JobHandle dependsOn) => new NormalizeJob{ field = this }.Schedule(dependsOn);

        [BurstCompile]
        private struct NormalizeJob : IJob
        {
            public ScalarField2D field;
            public void Execute() =>  sfmath.normalize(in field, field);
        }
        
        
        // Exports
        // =======
        public JobHandle ToTextureRGBA(NativeArray<Color32> texture, JobHandle dependsOn = default) =>
            new ToTextureRGBAJob{ field = this, texture = texture}.Schedule(dependsOn);

        public Texture2D ToTextureR(bool makeNoLongerReadable = true) =>
            ToTextureR(new Texture2D(dimension.x, dimension.y, TextureFormat.R8, false), makeNoLongerReadable);
        public Texture2D ToTextureR(Texture2D texture, bool makeNoLongerReadable = true)
        {
            double2 minMax = sfmath.minmax(this);
            var array = texture.GetRawTextureData<byte>();
            for (int i = 0; i < array.Length; i++)
            {
                var val = (field[i] + minMax.x) / (minMax.y + minMax.x);
                array[i] = (byte)(val * byte.MaxValue);
            }
            texture.Apply(false, makeNoLongerReadable);
            return texture;
        }
        
        public Texture2D ToTextureRGBA(bool makeNoLongerReadable = true) =>
            ToTextureRGBA(new Texture2D(dimension.x, dimension.y, TextureFormat.RGBA32, false), makeNoLongerReadable);
        public Texture2D ToTextureRGBA(Texture2D texture, bool makeNoLongerReadable = true)
        {
            double2 minMax = sfmath.minmax(this);
            var array = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < array.Length; i++)
            {
                var b = (byte)(byte.MaxValue * (field[i] + minMax.x) / (minMax.y + minMax.x));
                array[i] = new Color32(b, b, b, byte.MaxValue);
            }
            texture.Apply(false, makeNoLongerReadable);
            return texture;
        }

        private struct ToTextureRGBAJob : IJob
        {
            [ReadOnly] public ScalarField2D field;
            public NativeArray<Color32> texture;
            
            public void Execute()
            {
                double2 minMax = sfmath.minmax(field);
                for (int i = 0; i < texture.Length; i++)
                {
                    var b = (byte)(byte.MaxValue * (field[i] + minMax.x) / (minMax.y + minMax.x));
                    texture[i] = new Color32(b, b, b, byte.MaxValue);
                }
            }
        }

        public Texture2D NormalMap(bool makeNoLongerReadable = true)
        {
            var output = new Texture2D(dimension.x, dimension.y, TextureFormat.RGBA32, false);
            var array = output.GetRawTextureData<Color32>();
            for (int i = 0; i < array.Length; i++)
            {
                var n = (sfmath.normal(this, i) * 0.5 + 0.5) * byte.MaxValue;
                array[i] = new Color32((byte)n.x, (byte)n.z, (byte)n.y, byte.MaxValue);
            }
            output.Apply(false, makeNoLongerReadable);
            return output;
        }
    }
}