using System.Runtime.CompilerServices;
using HPML.Utils;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace HPML
{
    public static class FieldTextureExport
    {
        internal static JobHandle DoubleFToTex(doubleF f, Texture2D texture, JobHandle dependsOn)
        {
            var fieldArray = f.field;
            var texArray = texture.GetRawTextureData<Color32>();
            var job = new CopyDoubleToTexture
            {
                texture = texArray,
                array = fieldArray,
                color0 = Color.black,
                color1 = Color.white,
            };
            return job.Schedule(dependsOn);
        }

        internal static void DoubleFToTex(doubleF f, Texture2D texture)
        {
            var fieldArray = f.field;
            var texArray = texture.GetRawTextureData<Color32>();
            var job = new CopyDoubleToTexture
            {
                texture = texArray,
                array = fieldArray,
                color0 = Color.black,
                color1 = Color.white,
            };
            job.Run();
        }
        
        internal struct CopyDoubleToTexture : IJob
        {
            [WriteOnly] public NativeArray<Color32> texture;
            [ReadOnly] public NativeArray<double> array;
            public Color color0, color1;
            
            public void Execute()
            {
                var mn = double.PositiveInfinity;
                var mx = double.NegativeInfinity;

                foreach (var d in array)
                {
                    mn = min(d, mn);
                    mx = max(d, mx);
                }
                
                for (int i = 0; i < texture.Length; i++)
                {
                    texture[i] = Color.Lerp(color0, color1, saturate((float)unlerp(mn, mx, array[i])));
                }
            }
        }
        
        internal struct CopyDouble3ToTexture : IJob
        {
            [WriteOnly] public NativeArray<Color32> texture;
            [ReadOnly] public NativeArray<double3> array;
            
            public void Execute()
            {
                var maxLen = 0.0;

                foreach (var d3 in array)
                {
                    maxLen = max(lengthsq(d3), maxLen);
                }

                maxLen = sqrt(maxLen);
                
                for (int i = 0; i < texture.Length; i++)
                {
                    var val = (float3)(array[i] / maxLen);
                    texture[i] = new Color(val.x, val.z, val.y);
                }
            }
        }
    }
}