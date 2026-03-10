using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SnowSim : MonoBehaviour
{
    private Terrain terrain;
    private NativeArray<ushort> snowHeightmap;

    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        snowHeightmap = new NativeArray<ushort>(terrain.Xsize * terrain.Zsize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        snowHeightmap[(terrain.Xsize/2) * (terrain.Zsize/2)] = 1 << 12;
    }

    private void Update()
    {
        var job = new SnowSimJob
        {
            inputHeightmap =  new NativeArray<ushort>(snowHeightmap, Allocator.TempJob),
            outputHeightmap = snowHeightmap,
            width = terrain.Xsize,
        };

        var handle = job.ScheduleParallel(snowHeightmap.Length, 32, new JobHandle());
        handle.Complete();
    }
    
    private struct SnowSimJob : IJobFor
    {
        [ReadOnly] public NativeArray<ushort> inputHeightmap;
        public NativeArray<ushort> outputHeightmap;
        public int width;
    
        public void Execute(int index)
        {
            int4 nidx = new(index + 1, index - 1, index + width, index - width);
            int4 nval = new();
            ushort thisval = inputHeightmap[index];

            bool4 nbounds = nidx > 0 & nidx < inputHeightmap.Length & nidx % width != 0 & nidx % width != width - 1;
        
            if (nbounds.x) nval.x = inputHeightmap[nidx.x];
            if (nbounds.y) nval.y = inputHeightmap[nidx.y];
            if (nbounds.z) nval.z = inputHeightmap[nidx.z];
            if (nbounds.w) nval.w = inputHeightmap[nidx.w];
        
            bool4 exchange = nval < thisval;
        
            nval = math.select(nval, nval + 1, exchange);

            if (exchange.x) thisval--;
            if (exchange.y) thisval--;
            if (exchange.z) thisval--;
            if (exchange.w) thisval--;

            outputHeightmap[index] = thisval;
            outputHeightmap[nidx.x] = (ushort)nval.x;
            outputHeightmap[nidx.y] = (ushort)nval.y;
            outputHeightmap[nidx.z] = (ushort)nval.z;
            outputHeightmap[nidx.w] = (ushort)nval.w;
        }
    }
}