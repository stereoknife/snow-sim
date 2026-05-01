using System.Runtime.CompilerServices;
using HPML.Utils;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace HPML
{
    public static class FieldTextureExportExtensions
    {
        public static Texture2D ToTextureR(this doubleF field, bool makeNoLongerReadable = true) =>
            field.ToTextureR(new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.R8, false), makeNoLongerReadable);

        public static Texture2D ToTextureR(this doubleF field, Texture2D texture, bool makeNoLongerReadable = false)
        {
            double2 minMax = HPML.field.minmax(field);
            var array = texture.GetRawTextureData<byte>();
            for (int i = 0; i < array.Length; i++)
            {
                var val = (field[i] + minMax.x) / (minMax.y + minMax.x);
                array[i] = (byte)(val * byte.MaxValue);
            }
            texture.Apply(false, makeNoLongerReadable);
            return texture;
        }
        
        public static Texture2D ToTextureRGBA(this doubleF field, Color color, bool makeNoLongerReadable = true) =>
            field.ToTextureRGBA(new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false), color, makeNoLongerReadable);
        
        public static Texture2D ToTextureRGBA(this doubleF field, Texture2D texture, Color color, bool makeNoLongerReadable = true)
        {
            double2 minMax = HPML.field.minmax(field);
            var array = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = ValToColor((float)((field[i] + minMax.x) / (minMax.y + minMax.x)), color);
            }
            texture.Apply(false, makeNoLongerReadable);
            return texture;
        }
        
        public static Texture2D ToTextureRGBA(this doubleF field, Color positiveColor, Color negativeColor, bool makeNoLongerReadable = true) =>
            field.ToTextureRGBA(new Texture2D(field.dimension.x, field.dimension.y, TextureFormat.RGBA32, false), positiveColor, negativeColor, makeNoLongerReadable);
        
        public static Texture2D ToTextureRGBA(this doubleF field, Texture2D texture, Color positiveColor, Color negativeColor, bool makeNoLongerReadable = true)
        {
            double2 minMax = HPML.field.minmax(field);
            var array = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = ValToColor((float)(field[i] / cmax(abs(minMax))), positiveColor, negativeColor);
            }
            texture.Apply(false, makeNoLongerReadable);
            return texture;
        }
        
        public static JobHandle ToTextureRGBA(this doubleF field, Texture2D texture, Color color,
            JobHandle dependsOn, bool makeNoLongerReadable = true)
            => field.ToTextureRGBAJob(texture, color, default, false, makeNoLongerReadable, dependsOn);

        public static JobHandle ToTextureRGBA(this doubleF field, Texture2D texture, Color positiveColor,
            Color negativeColor, JobHandle dependsOn, bool makeNoLongerReadable = true)
            => field.ToTextureRGBAJob(texture, positiveColor, negativeColor, true, makeNoLongerReadable, dependsOn);
        
        public static JobHandle ToTextureRGBA(this doubleF field, Texture2D texture, Gradient gradient, JobHandle dependsOn, bool makeNoLongerReadable = true)
            => field.ToTextureRGBAJob(texture, gradient, makeNoLongerReadable, dependsOn);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static JobHandle ToTextureRGBAJob(this doubleF field, Texture2D texture, Color positiveColor, Color negativeColor, bool divergent, bool makeNoLongerReadable, JobHandle dependsOn)
        {
            var job = new CopyTextureJob()
            {
                field = field,
                array = texture.GetRawTextureData<Color32>(),
                positiveColor = positiveColor,
                negativeColor = negativeColor,
                divergent = divergent,
                useGradient = false
            };
            return job.Schedule(dependsOn);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static JobHandle ToTextureRGBAJob(this doubleF field, Texture2D texture, Gradient gradient, bool makeNoLongerReadable, JobHandle dependsOn)
        {
            var job = new CopyTextureJob()
            {
                field = field,
                array = texture.GetRawTextureData<Color32>(),
                gradient = new Gradient8(gradient),
                useGradient = true,
            };
            return job.Schedule(dependsOn);
        }

        private struct CopyTextureJob : IJob
        {
            [ReadOnly] public doubleF field;
            public NativeArray<Color32> array;
            public Color positiveColor, negativeColor;
            public Gradient8 gradient;
            public bool divergent, useGradient;
            
            public void Execute()
            {
                double2 minMax = HPML.field.minmax(field);
                var a = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    if (useGradient)
                        array[i] = ValToColor((float)field[i], gradient, true);
                    else if (divergent)
                        array[i] = ValToColor((float)(field[i] / cmax(abs(minMax))), positiveColor, negativeColor);
                    else
                        array[i] = ValToColor((float)((field[i] + minMax.x) / (minMax.y + minMax.x)), positiveColor);
                }
            }
        }
        
        public static JobHandle ToTextureRGBA(this double3F field, Texture2D texture, bool makeNoLongerReadable, JobHandle dependsOn)
        {
            var job = new CopyTextureJob3()
            {
                field = field,
                array = texture.GetRawTextureData<Color32>(),
            };
            return job.Schedule(dependsOn);
        }
        
        private struct CopyTextureJob3 : IJob
        {
            [ReadOnly] public double3F field;
            public NativeArray<Color32> array;
            
            public void Execute()
            {
                var a = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    var val = (float3)normalize(field[i]);
                    array[i] = new Color(val.x, val.z, val.y, 1);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color ValToColor(float val, Color color)
        {
            color = Color.Lerp(Color.black, color, val);
            return color;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color ValToColor(float val, Color positiveColor, Color negativeColor)
        {
            var color = Color.Lerp(Color.black, val > 0 ? positiveColor : negativeColor, abs(val));
            return color.gamma;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color ValToColor(float val, Gradient8 gradient, bool timeIs01)
        {
            var color = timeIs01 ? gradient.Eval(val) : gradient.EvalMult(val);
            return color.gamma;
        }
    }
}