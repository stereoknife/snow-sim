using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct InstanceTransformJob : IJobFor
{
    public NativeArray<Matrix4x4> instanceData;
    [ReadOnly] public NativeArray<ushort> heightmap;
    public int resolution;
    public float height;
    
    public void Execute(int index)
    {
        var i = index / resolution;
        var j = index % resolution;
        var h = ((float)heightmap[index]/(float)ushort.MaxValue) * height;
        instanceData[index] = Matrix4x4.TRS(
            new Vector3(i, h * 0.5f, j),
            Quaternion.identity,
            new Vector3(1, h, 1)
        );
    }
}