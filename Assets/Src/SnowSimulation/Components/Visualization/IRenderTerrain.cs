using HPML;
using Unity.Collections;

namespace TFM.Components.Visualization
{
    public interface IRenderTerrain
    {
        public doubleF Heightfield { get; }
        public double4F Snowfield { get; }
        public NativeHashSet<int> SelectedPoints { get; }
        public NativeHashSet<int> HighlightedPoints { get; }
    }
}