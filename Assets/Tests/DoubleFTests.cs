using HPML;
using NUnit.Framework;
using Unity.Collections;
using Random = UnityEngine.Random;

public class DoubleFTests
{
    // A Test behaves as an ordinary method
    [Test]
    public void IndexAndCellAccessors()
    {
        int size = 128;
        var field = new doubleF(size, default, Allocator.Persistent);
        var index = Random.Range(0, size);

        var cell = field.cell(index);
        var computedIndex = field.index(cell);
        
        Assert.That(computedIndex, Is.EqualTo(index));

        field.Dispose();
    }
}
