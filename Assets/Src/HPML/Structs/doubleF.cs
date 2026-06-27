using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static HPML.vec;
using static Unity.Mathematics.math;
using int2 = Unity.Mathematics.int2;

namespace HPML
{
    
    public struct doubleF : INativeDisposable, IExportToTexture
    {
        public NativeArray<double> field;
        public int2 dimension;
        public double3 size;
        public double2 cellSize => size.xz / dimension;
        public double2 iCellSize => dimension / size.xz;
        public int Length => field.Length;

        public static doubleF FromTexture(Texture2D tex, double3 size, Allocator allocator)
        {
            var dim = new int2(tex.width, tex.height);
            var output = new doubleF(dim, size, allocator);
            var data = tex.GetRawTextureData<ushort>();

            for (int i = 0; i < output.field.Length; i++)
            {
                //var c = dim - 1 - output.cell(i);
                //output[c.y, c.x] = size.y * (double)data[i] / (double)ushort.MaxValue;
                output[i] = size.y * (double)data[i] / (double)ushort.MaxValue;
            }

            return output;
        }

        public doubleF(doubleF sf, Allocator allocator)
        {
            field = new NativeArray<double>(area(sf.dimension), allocator);
            dimension = sf.dimension;
            size = sf.size;
        }
        
        public doubleF(int2 dimension, double3 size, Allocator allocator)
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
        
        public JobHandle ToTexture2D(Texture2D tex, JobHandle dependsOn)
            => FieldTextureExport.DoubleFToTex(this, tex, dependsOn);

        public void ToTexture2D(Texture2D tex)
            => FieldTextureExport.DoubleFToTex(this, tex);
        
        public void ExportToFile(string filename)
        {
            var tex = new Texture2D(dimension.x, dimension.y);
            ToTexture2D(tex);
            tex.Apply();
            System.IO.File.WriteAllBytes($"{Application.persistentDataPath}/results/{filename}.png", tex.EncodeToPNG());
        }
    }
}