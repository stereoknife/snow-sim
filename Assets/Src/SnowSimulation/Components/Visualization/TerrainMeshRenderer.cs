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
        private Dictionary<TextureId, Texture2D> _textures = new();
        
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
            CombinedIllumination
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

        public void Apply()
        {
            foreach (var tex in _textures.Values)
            {
                tex.Apply(false, true);
            }
        }
    }
}