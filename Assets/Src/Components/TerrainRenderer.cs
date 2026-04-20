using System;
using System.Collections;
using Sim.Simulation;
using Sim.Structs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Sim.geometry;

namespace Sim
{
    [RequireComponent(typeof(Terrain))]
    [RequireComponent(typeof(Sun))]

    public class TerrainRenderer : MonoBehaviour
    {
        [SerializeField] private Material material, snow;
        [SerializeField] private Mesh cube;
        [SerializeField] private MeshRenderer imageRenderer;
        [SerializeField] private TextureName texture;
        
        private Terrain terrain;
        private Sun sun;
        private NativeArray<Matrix4x4> instanceData;
        private Material imageMaterial;
        private Texture2D[] textures = new Texture2D[4];

        private enum TextureName
        {
            DirectLighting,
            AmbientExposure,
            IndirectLighting,
            Sum
        }

        private void Awake()
        {
            sun = GetComponent<Sun>();
            
            terrain = GetComponent<Terrain>();
            terrain.Init();
            
            instanceData = new NativeArray<Matrix4x4>(area(terrain.Heightfield.dimension), Allocator.Persistent);
            ScalarField2D dlf = new ScalarField2D(terrain.Heightfield, Allocator.Persistent);
            ScalarField2D aef = new ScalarField2D(terrain.Heightfield, Allocator.Persistent);
            ScalarField2D ilf = new ScalarField2D(terrain.Heightfield, Allocator.Persistent);
            int w = terrain.Heightfield.dimension.x, h = terrain.Heightfield.dimension.y;
            textures[0] = new Texture2D(w, h, TextureFormat.RGBA32, false);
            textures[1] = new Texture2D(w, h, TextureFormat.RGBA32, false);
            textures[2] = new Texture2D(w, h, TextureFormat.RGBA32, false);
            textures[3] = new Texture2D(w, h, TextureFormat.RGBA32, false);
            
            // Compute maps
            JobHandle dlj = sun.DirectLighting(terrain.Heightfield, dlf, default);
            JobHandle aej = sun.AmbientLighting(terrain.Heightfield, aef, default);
            JobHandle ilj = sun.IndirectLightingParallel(terrain.Heightfield, dlf, ilf, dlj);
            
            // Export to texture
            dlj = dlf.ToTextureRGBA(textures[0].GetRawTextureData<Color32>(), dlj);
            aej = aef.ToTextureRGBA(textures[1].GetRawTextureData<Color32>(), aej);
            ilj = ilf.ToTextureRGBA(textures[2].GetRawTextureData<Color32>(), ilj);
            
            JobHandle post = new CombineFields(dlf, aef, ilf)
                .Schedule(dlf.field.Length, JobHandle.CombineDependencies(dlj, aej, ilj));
            post = dlf.Normalize(post);
            
            post = dlf.ToTextureRGBA(textures[3].GetRawTextureData<Color32>(), post);
            
            StartCoroutine(ApplyTextures(post));
        }

        private struct CombineFields : IJobFor
        {
            public ScalarField2D field1;
            [ReadOnly] public ScalarField2D field2;
            [ReadOnly] public ScalarField2D field3;

            public CombineFields(ScalarField2D field1, ScalarField2D field2, ScalarField2D field3)
            {
                this.field1 = field1;
                this.field2 = field2;
                this.field3 = field3;
            }
            
            public void Execute(int index)
            {
                field1[index] += field2[index] + field3[index];
            }
        }

        private struct ExportTexture : IJobFor
        {
            [ReadOnly][DeallocateOnJobCompletion] public ScalarField2D field;
            public NativeArray<Color32> texture;

            public ExportTexture(ScalarField2D field, NativeArray<Color32> texture)
            {
                this.field = field;
                this.texture = texture;
            }
            
            public void Execute(int index)
            {
                var b = (byte)(byte.MaxValue * field[index]);
                texture[index] = new Color32(b, b, b, byte.MaxValue);
            }
        }

        private IEnumerator ApplyTextures(JobHandle jobHandle) {
            while (!jobHandle.IsCompleted) yield return null;
            Debug.Log("Complete!");
            foreach (var tex in textures) tex.Apply(false, true);
        }
        
        private void Update()
        {
            var job = new InstanceTransformJob
            {
                heightmap = terrain.Heightfield,
                instanceData = instanceData,
                transform = transform.localToWorldMatrix
            };

            var handle = job.ScheduleParallel(area(terrain.Heightfield.dimension), 32, new JobHandle());
            handle.Complete();

            var rp = new RenderParams(material);
            Graphics.RenderMeshInstanced(rp, cube, 0, instanceData);

            imageRenderer.material.mainTexture = textures[(int)texture];
        }

        private void OnDestroy()
        {
            instanceData.Dispose();
        }

        private struct InstanceTransformJob : IJobFor
        {
            public NativeArray<Matrix4x4> instanceData;
            [ReadOnly] public ScalarField2D heightmap;
            public Matrix4x4 transform;

            public void Execute(int index)
            {
                var i = heightmap.cell(index);
                var h = heightmap.field[index];
                instanceData[index] = transform * Matrix4x4.TRS(
                    new Vector3((float)(i.x * heightmap.cellSize.x), (float)h * 0.5f, (float)(i.y * heightmap.cellSize.y)),
                    Quaternion.identity,
                    new Vector3((float)heightmap.cellSize.x, (float)h, (float)heightmap.cellSize.y)
                );
            }
        }
    }
}