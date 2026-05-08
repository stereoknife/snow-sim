using HPML;

namespace TFM.Components.Solvers
{
    public interface ISnowSimulation
    {
        public doubleF Heightfield { get; }
        public double4F Snowfield { get; }
    }
}