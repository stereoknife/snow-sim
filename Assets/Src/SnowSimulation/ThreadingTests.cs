using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace TFM.Tests
{
    public class ThreadingTests : MonoBehaviour
    {
        private Barrier barrier;
        private JobHandle jobHandle;
        private float timer = 0;
        
        private void Start()
        {
            var job = new WaitJob()
            {

            };
            jobHandle = job.Schedule(10,default);
            jobHandle.Complete();
            Debug.Break();
        }

        private struct WaitJob : IJobFor
        {
            
            public void Execute(int index)
            {

            }
        }

    }
}