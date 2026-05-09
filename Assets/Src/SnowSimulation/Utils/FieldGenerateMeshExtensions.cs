using System.Collections;
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
        public static JobHandle GenerateMesh(this doubleF field, out Mesh.MeshDataArray mda, JobHandle dependsOn)
        {
            mda = Mesh.AllocateWritableMeshData(1);
            var md = mda[0];

            var idx_ct = vec.area(field.dimension - 1) * 6;
            md.SetVertexBufferParams(field.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal)
            );
            md.SetIndexBufferParams(idx_ct, IndexFormat.UInt32);

            var verts = md.GetVertexData<float3x2>();
            var idx = md.GetIndexData<uint>();

            var vjob = new VerticesJob
            {
                height = field,
                vertices = verts,
            };

            dependsOn = vjob.Schedule(field.Length, dependsOn);

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
            public NativeArray<float3x2> vertices;
            
            public void Execute(int index)
            {
                var ij = height.cell(index);
                var xz = height.cellSize * ij;
                var v = (float3)double3(xz, height[index]).xzy;
                var n = (float3)field.normal(height, index);
                vertices[index] = float3x2(v, n);
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