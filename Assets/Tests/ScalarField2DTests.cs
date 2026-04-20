using NUnit.Framework;
using Sim.Structs;
using Unity.Collections;
using UnityEngine;

public class ScalarField2DTests
{
    // A Test behaves as an ordinary method
    [Test]
    public void IndexAndCellAccessors()
    {
        int size = 128;
        var field = new ScalarField2D(size, default, Allocator.Persistent);
        var index = Random.Range(0, size);

        var cell = field.cell(index);
        var computedIndex = field.index(cell);
        
        Assert.That(computedIndex, Is.EqualTo(index));

        field.Dispose();
    }
}
