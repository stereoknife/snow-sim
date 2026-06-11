using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HPML;
using TFM.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TFM.Components.Visualization
{

    public class TerrainMeshRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField] private Color terrain, cliff, snow;
        [SerializeField] [Range(0f, 1f)] private float overlayLerp;
        [SerializeField] private TextureId overlay;
    
        private doubleF _heightfield;
        private double4F _snowfield;
        public Dictionary<TextureId, Texture2D> _textures = new();
        
        private RenderParams _rp;
        private Mesh _mesh;
        
        private static readonly int TextureLerp = Shader.PropertyToID("_TextureLerp");

        private void Start()
        {
            var sim = GetComponent<SimulationController>();
            _heightfield = sim.Heightfield;
            _snowfield = sim.Snowfield;
            
            _rp = new RenderParams(material)
            {
                matProps = new MaterialPropertyBlock()
            };

            _mesh = new Mesh();
            
            GenerateFinalMesh();
            OnEnable();
        }

        private void OnEnable()
        {
            if (!_heightfield.IsCreated) return;
            RenderPipelineManager.beginContextRendering += RenderNonInstanced;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= RenderNonInstanced;
        }
        
        private void RenderNonInstanced(ScriptableRenderContext scriptableRenderContext, List<Camera> cameras)
        {
            GenerateFinalMesh();
            _rp.matProps.SetFloat(TextureLerp, overlayLerp);
            if (_textures.TryGetValue(overlay, out var texture))
                _rp.matProps.SetTexture("_Overlay", texture);
            Graphics.RenderMesh(_rp, _mesh, 0, Matrix4x4.Scale(math.float3(1f/100f)));
        }
        
        private void GenerateFinalMesh()
        {
            _heightfield.GenerateMesh(out Mesh.MeshDataArray mda, default, _snowfield).Complete();
            Mesh.ApplyAndDisposeWritableMeshData(mda, _mesh);
            _mesh.RecalculateBounds();
        }


        public enum TextureId
        {
            Height,
            DirectIllumination,
            IndirectIllumination,
            AmbientIllumination,
            CombinedIllumination,
            TerrainRoughness,
            TerrainGradient,
            TerrainCurvatureCategory,
            RoughnessHazard,
            GradientHazard,
            CurvatureHazard,
            AvalancheHazard
        }
        
        public void AddTexture(TextureId id, doubleF field)
        {
            if (!_textures.TryGetValue(id, out var texture))
                texture = new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false);
            _textures[id] = texture;
            field.ToTexture2D(texture);
        }
        
        public JobHandle AddTexture(TextureId id, doubleF field, JobHandle dependsOn)
        {
            if (!_textures.TryGetValue(id, out var texture))
                texture = new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false);
            _textures[id] = texture;
            return field.ToTexture2D(texture, dependsOn);
        }
        
        public JobHandle AddTexture(TextureId id, doubleF field, NativeArray<int> array, JobHandle dependsOn)
        {
            if (!_textures.TryGetValue(id, out var texture))
                texture = new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false);
            _textures[id] = texture;
            return new IntTexJob{ array = array, texture = texture.GetRawTextureData<Color32>() }.Schedule(array.Length, dependsOn);
        }
        
        private struct IntTexJob : IJobFor
        {
            [ReadOnly] public NativeArray<int> array;
            [WriteOnly] public NativeArray<Color32> texture;
            
            public void Execute(int index)
            {
                texture[index] = array[index] switch
                {
                    0 => new Color32(0x69, 0xc8, 0xcc, 0xFF),
                    1 => new Color32(0x54, 0x78, 0xb9, 0xFF),
                    2 => new Color32(0xdd, 0x84, 0xb4, 0xFF),
                    3 => new Color32(0x9d, 0xc8, 0x53, 0xFF),
                    4 => new Color32(0x74, 0xc1, 0x76, 0xFF),
                    5 => new Color32(0xb7, 0xdc, 0xd5, 0xFF),
                    6 => new Color32(0xF1, 0x60, 0x3F, 0xFF),
                    7 => new Color32(0xF3, 0x9c, 0x4c, 0xFF),
                    8 => new Color32(0xF5, 0xe7, 0x4a, 0xFF),
                    _ => new Color32(0x00, 0x00, 0x00, 0xFF),
                };
            }
        }

        public void Apply()
        {
            foreach (var tex in _textures.Values)
            {
                tex.Apply(false, true);
            }
        }
    }
}