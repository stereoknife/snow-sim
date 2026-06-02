using System.Collections;
using System.Runtime.InteropServices;
using HPML;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;
using static Unity.Mathematics.math;

namespace TFM.Utils
{
    public static class FieldGenerateMeshExtensions
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct vtx
        {
            public float3 position, normal;
            public float2 uv0, uv1;
        }
        
        public static JobHandle GenerateMesh(this doubleF field, out Mesh.MeshDataArray mda, JobHandle dependsOn, double4F snow = default)
        {
            mda = Mesh.AllocateWritableMeshData(1);
            var md = mda[0];

            var vad = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
            vad[0] = new VertexAttributeDescriptor(VertexAttribute.Position, dimension: 3, stream: 0);
            vad[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3, stream: 0);
            vad[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 0);
            vad[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, dimension: 2, stream: 0);

            var idx_ct = vec.area(field.dimension - 1) * 6;
            md.SetVertexBufferParams(field.Length, vad);
            vad.Dispose();
            md.SetIndexBufferParams(idx_ct, IndexFormat.UInt32);

            var verts = md.GetVertexData<vtx>();
            var idx = md.GetIndexData<uint>();

            if (snow.IsCreated)
            {
                var vjob = new VerticesJob2 { height = field, vertices = verts, snow = snow };
                dependsOn = vjob.Schedule(field.Length, dependsOn);
            }
            else
            {
                var vjob = new VerticesJob { height = field, vertices = verts };
                dependsOn = vjob.Schedule(field.Length, dependsOn);
            }

            var ijob = new IndicesJob()
            {
                height = field,
                indices = idx
            };
            
            dependsOn = ijob.Schedule(field.Length, dependsOn);
            dependsOn.Complete();
            
            md.subMeshCount = 1;
            md.SetSubMesh(0, new SubMeshDescriptor(0, idx_ct));
            
            return dependsOn;
        }
        
        [BurstCompile]
        private struct VerticesJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            //[ReadOnly] public double4F snow;
            public NativeArray<vtx> vertices;
            
            public void Execute(int index)
            {
                var ij = height.cell(index);
                var xz = height.cellSize * ij;
                var v = (float3)double3(xz, height[index]).xzy;
                var n = (float3)field.normal(height, index);
                var uv0 = (float2)height.cell(index) / (float2)(height.dimension - 1);
                var uv1 = uv0;
                //if (snow.IsCreated) uv1 = float2((float)csum(snow[index]));
                vertices[index] = new vtx
                {
                    position = v,
                    normal = n,
                    uv0 = uv0,
                    uv1 = uv1
                };
            }
        }
        
        [BurstCompile]
        private struct VerticesJob2 : IJobFor
        {
            [ReadOnly] public doubleF height;
            [ReadOnly] public double4F snow;
            public NativeArray<vtx> vertices;
            
            public void Execute(int index)
            {
                var ij = height.cell(index);
                var xz = height.cellSize * ij;
                var v = (float3)double3(xz, height[index]).xzy;
                var n = (float3)field.normal(height, index);
                var uv0 = (float2)height.cell(index) / (float2)(height.dimension - 1);
                var uv1 = float2((float)csum(snow[index]));
                vertices[index] = new vtx
                {
                    position = v,
                    normal = n,
                    uv0 = uv0,
                    uv1 = uv1
                };
            }
        }
        
        [BurstCompile]
        private struct IndicesJob : IJobFor
        {
            [ReadOnly] public doubleF height;
            [NativeDisableContainerSafetyRestriction] public NativeArray<uint> indices;
            
            public void Execute(int index)
            {
                var ij = height.cell(index);
                if (any(ij >= height.dimension - 1)) return;

                var i00 = (uint)index;
                var i01 = (uint)height.index(ij.x, ij.y + 1);
                var i10 = (uint)height.index(ij.x + 1, ij.y);
                var i11 = (uint)height.index(ij + 1);

                var i = (index - ij.y) * 6;
                indices[i] = i00;
                indices[i + 1] = i01;
                indices[i + 2] = i10;
                indices[i + 3] = i10;
                indices[i + 4] = i01;
                indices[i + 5] = i11;
            }
        }
    }
}