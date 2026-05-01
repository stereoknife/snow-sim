using System;
using System.Runtime.CompilerServices;
using HPML;
using Unity.Jobs;
using UnityEngine;

namespace Utils
{
    public class WaitForJobCompletion : CustomYieldInstruction
    {
        private JobHandle jobHandle;
        
        public override bool keepWaiting => !jobHandle.IsCompleted;
        
        public WaitForJobCompletion(JobHandle jobHandle) => this.jobHandle = jobHandle;
    }

    public static class JobWaiter
    {
        public static async Awaitable WaitForComplete(this JobHandle jobHandle)
        {
            while (!jobHandle.IsCompleted)
            {
                await Awaitable.NextFrameAsync();
            }
        }
    }
}