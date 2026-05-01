using System;
using System.Collections;
using System.Collections.Generic;
using HPML;
using Unity.Jobs;
using UnityEngine;
using Utils;

namespace TFM.Components
{
    public class FieldRenderer : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Gradient gradient;
        
        [Header("Draw operation")]
        [SerializeField] private Name textureA;
        [SerializeField] private Name textureB;
        [SerializeField] private Operation operation;
        [SerializeField] private bool update = false;
        
        public static Dictionary<Name, Texture2D> textures = new();
        public static Texture2D render;

        public enum Operation
        {
            Difference,
            Add,
            Subtract,
            AsIs,
            Normalize
        }
        
        public enum Name
        {
            Heightmap,
            DirectLighting,
            AmbientLighting,
            IndirectLighting,
            CombinedLighting,
            VenturiWind,
            DeflectedWind,
            WindSurface,
            WindAltitude,
        }

        public JobHandle RegisterField(doubleF field, Name name, JobHandle dependsOn)
        {
            var texture = new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false);
            textures.Add(name, texture);
            dependsOn = name switch
            {
                Name.DirectLighting => field.ToTextureRGBA(texture, Color.white, dependsOn),
                Name.CombinedLighting => field.ToTextureRGBA(texture, Color.red, Color.cyan, dependsOn),
                _ => field.ToTextureRGBA(texture, Color.white, dependsOn),
            };
            StartCoroutine(Apply(texture, dependsOn));
            return dependsOn;
        }
        
        public JobHandle RegisterField(double3F field, Name name, JobHandle dependsOn)
        {
            var texture = new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false);
            textures.Add(name, texture);
            dependsOn = field.ToTextureRGBA(texture, false, dependsOn);
            StartCoroutine(Apply(texture, dependsOn));
            return dependsOn;
        }
        
        public JobHandle RegisterField(doubleF field, Name name, bool negative, JobHandle dependsOn)
        {
            var texture = new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false);
            textures.Add(name, texture);
            dependsOn = field.ToTextureRGBA(texture, Color.red, Color.cyan, dependsOn);
            StartCoroutine(Apply(texture, dependsOn));
            return dependsOn;
        }

        public IEnumerator Apply(Texture2D texture, JobHandle dependsOn)
        {
            yield return new WaitForJobCompletion(dependsOn);
            texture.Apply();
        }

        private void Awake()
        {
            render = new Texture2D(505, 505, TextureFormat.RGBA32, false);
            render.filterMode = FilterMode.Point;
            meshRenderer.material.mainTexture = render;
        }

        private void Difference(Texture2D tex_a, Texture2D tex_b)
        {
            var r = render.GetRawTextureData<Color32>();
            var a = tex_a.GetRawTextureData<Color32>();
            var b = tex_b.GetRawTextureData<Color32>();

            for (int i = 0; i < r.Length; i++)
            {
                var c = r[i];
                var va = a[i];
                var vb = b[i];
                var diff = (a[i].r - b[i].r) * 2;
                c.r = (byte)(a[i].r - diff);
                c.g = b[i].r;
                c.b = a[i].r;
                r[i] = c;
            }
        }

        private void OnValidate()
        {
            if (!update) return;
            switch (operation)
            {
                case Operation.Difference:
                    var a = textures[textureA];
                    var b = textures[textureB];
                    if (a == null || b == null) break;
                    Difference(a, b);
                    render.Apply();
                    break;
                default:
                    break;
            }

            update = false;
        }
    }
}