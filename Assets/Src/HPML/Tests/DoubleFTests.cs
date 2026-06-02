using HPML;
using HPML.Serialization;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Serialization.Binary;
using UnityEngine;
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
    
    [Test]
    public unsafe void Serialization()
    {
        var adapter = new doubleFAdapter(Allocator.Temp);
        BinarySerialization.AddGlobalAdapter(adapter);
        
        var srcField = new doubleF(4, default, Allocator.Temp);
        var buffer = new UnsafeAppendBuffer(512, 4, Allocator.Temp);
        BinarySerialization.ToBinary(&buffer, srcField);
        
        var reader = buffer.AsReader();
        var dstField = BinarySerialization.FromBinary<doubleF>(&reader);
        
        Assert.That(srcField.dimension, Is.EqualTo(dstField.dimension));
        Assert.That(srcField.size, Is.EqualTo(dstField.size));
        Assert.That(srcField.Length, Is.EqualTo(dstField.Length));
        Assert.That(UnsafeUtility.MemCmp(srcField.field.GetUnsafePtr(), dstField.field.GetUnsafePtr(), srcField.Length * sizeof(double)), Is.EqualTo(0));

        srcField.Dispose();
        buffer.Dispose();
        dstField.Dispose();
    }
    
    [Test]
    public unsafe void ScalarFieldSerialization()
    {
        var adapter = new ScalarField2DAdapter(Allocator.Temp);
        BinarySerialization.AddGlobalAdapter(adapter);
        
        var srcField = new ScalarField2D(2, 3, default, Allocator.Temp);
        var buffer = new UnsafeAppendBuffer(512, 4, Allocator.Temp);
        BinarySerialization.ToBinary(&buffer, srcField);
        Debug.Log($"Capacity: {buffer.Capacity}, Lenght: {buffer.Length}");
        
        var reader = buffer.AsReader();
        var dstField = BinarySerialization.FromBinary<ScalarField2D>(&reader);
        
        Assert.That(srcField.dimension, Is.EqualTo(dstField.dimension));
        Assert.That(srcField.size, Is.EqualTo(dstField.size));
        Assert.That(srcField.Length, Is.EqualTo(dstField.Length));
        Assert.That(UnsafeUtility.MemCmp(srcField.array.GetUnsafePtr(), dstField.array.GetUnsafePtr(), srcField.Length * sizeof(double)), Is.EqualTo(0));

        srcField.Dispose();
        buffer.Dispose();
        dstField.Dispose();
    }
}
