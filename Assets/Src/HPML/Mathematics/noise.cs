using Unity.Mathematics;

namespace HPML
{
    public static class noise
    {
        public static float fractal(float2 p, int octaves, int frequency, int amplitude, int lacunarity = 2, float persistence = 0.5f)
        {
            var val = 0;
            Unity.Mathematics.noise.cnoise(p);
            return 0;
        }
    }
}