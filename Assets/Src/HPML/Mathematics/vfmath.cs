using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace HPML
{
    public static class vfmath
    {
        // This is not very good but we'll live with it
        public static double divergence_xz(double3F field, int2 at)
        {
            int2 up = min(at + 1, field.dimension - 1);
            int2 down = max(at - 1, 0);
            double2 norm = field.iCellSize / (up - down);
            return csum(new double2(
                field[up.x, at.y].x - field[down.x, at.y].x,
                field[at.x, up.y].z - field[at.x, down.y].z
            ) * norm);
        }

        public static double2 minmax(in double3F field)
        {
            double maxl = double.MinValue, minl = double.MaxValue;
            for (int i = 0; i < field.Length; i++)
            {
                maxl = max(lengthsq(field[i]), maxl);
                minl = min(lengthsq(field[i]), minl);
            }
            
            return double2(minl, maxl);
        }
    }
}