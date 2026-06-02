using Unity.Mathematics;

namespace HPML.Utils
{
    public interface IScalarField
    {
        public int2 Dimension { get; }
        public double3 Size { get; }
        public double2 CellSize => Size.xz / Dimension;
        public double2 ICellSize => Dimension / Size.xz;
        public int Length { get; }
    }
}