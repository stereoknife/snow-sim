using HPML;

namespace TFM.Components.Visualization
{
    public interface IRenderTerrain
    {
        public doubleF Heightfield { get; }
        public double4F Snowfield { get; }
    }
}