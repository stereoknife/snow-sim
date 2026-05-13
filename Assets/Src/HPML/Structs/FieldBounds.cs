using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace HPML
{
    public struct FieldBounds
    {
        public int2 min, max;
        public int2 size;
        private int2 _dimension;

        public FieldBounds(doubleF field, int2 min, int2 max)
        {
            _dimension = field.dimension;
            this.min = clamp(min, 0, _dimension);
            this.max = clamp(max, 0, _dimension);
            size = this.max - this.min;
        }
        
        public FieldBounds(FieldBounds bounds, int2 min, int2 max)
        {
            _dimension = bounds._dimension;
            this.min = clamp(min, 0, _dimension);
            this.max = clamp(max, 0, _dimension);
            size = this.max - this.min;
        }

        public static FieldBounds operator ++(FieldBounds bounds)
        {
            bounds.min -= clamp(bounds.min - 1, 0, bounds._dimension);
            bounds.max -= clamp(bounds.max + 1, 0, bounds._dimension);
            bounds.size = bounds.max - bounds.min;
            return bounds;
        }
        
        public static FieldBounds operator --(FieldBounds bounds)
        {
            bounds.min -= clamp(bounds.min + 1, 0, bounds._dimension);
            bounds.max -= clamp(bounds.max - 1, 0, bounds._dimension);
            bounds.size = bounds.max - bounds.min;
            return bounds;
        }
        
        public static FieldBounds operator +(FieldBounds bounds, int n)
        {
            bounds.min -= clamp(bounds.min - n, 0, bounds._dimension);
            bounds.max -= clamp(bounds.max + n, 0, bounds._dimension);
            bounds.size = bounds.max - bounds.min;
            return bounds;
        }
        
        public static FieldBounds operator -(FieldBounds bounds, int n)
        {
            bounds.min -= clamp(bounds.min + n, 0, bounds._dimension);
            bounds.max -= clamp(bounds.max - n, 0, bounds._dimension);
            bounds.size = bounds.max - bounds.min;
            return bounds;
        }

        public int index(int2 cell)
        {
            cell -= min;
            return cell.y * size.x + cell.x;
        }
        
        public int2 cell(int index)
        {
            var c = int2(index % size.x, index / size.y);
            return c + min;
        }

        public int2 bound(int2 cell) => clamp(cell, min, max);

        public int2 reflect(int2 cell)
        {
            cell -= min;
            cell = select(cell, -cell, cell < 0);
            cell = select(cell, 2 * (size - 1) - cell, cell > size - 1);
            return cell + min;
        }
    }
}