using System;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

namespace HPML
{
    public static class FieldParallelExtensions
    {
        public static double InterlockedAdd(this doubleF field, int index, double value)
        {
            unsafe
            {
                var ptr = (double*)field.field.GetUnsafePtr();
                var cmp = field[index];
                while (true)
                {
                    var old = Interlocked.CompareExchange(ref ptr[index], cmp + value, cmp);
                    if (cmp.Equals(old)) return cmp + value;
                    cmp = old;
                }
            }
        }
    }
}