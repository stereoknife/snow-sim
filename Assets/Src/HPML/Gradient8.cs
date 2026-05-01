using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace HPML.Utils
{
    public unsafe struct Gradient8
    {
        private fixed float c[8 * 3];
        private fixed float t[8];

        public Gradient8(Gradient gradient)
        {
            var k = gradient.colorKeys;
            for (int i = 0; i < k.Length; i++)
            {
                var col = k[i].color;
                c[i * 3] = col.r;
                c[i * 3 + 1] = col.g;
                c[i * 3 + 2] = col.b;
                t[i] = k[i].time;
            }
        }

        public Color Eval(float time)
        {
            time = clamp(time, 0, 1);
            Color ca = default, cb = default;
            float ta = 0, tb = 0;
            for (int i = 0; i < 8; i++)
            {
                if (t[i] < time)
                {
                    tb = t[i];
                    cb = GetColor(i);
                }
                else if (t[i] > time)
                {
                    ta = t[i];
                    ca = GetColor(i);
                    break;
                }
                else
                {
                    return GetColor(i);
                }
            }
            
            time = unlerp(tb, ta, time);
            return Color.Lerp(cb, ca, time);
        }

        public Color EvalMult(float time)
        {
            time = clamp(time, 0, 7);
            var ca = GetColor((int)ceil(time));
            var cb = GetColor((int)floor(time));
            return Color.Lerp(cb, ca, frac(time));
        }

        public Color GetColor(int i) => new Color(c[i*3], c[i*3 + 1], c[i*3 + 2], 1);
    }
}