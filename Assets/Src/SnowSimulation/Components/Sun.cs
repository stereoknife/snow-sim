using System;
using HPML;
using TFM.Simulation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace TFM.Components
{
    public class Sun : MonoBehaviour
    {
        [SerializeField][Range(-90, 90)] private double latitude = 45;
        
        [Header("Effect intensity")] 
        [SerializeField] private double directIntensity = 1;
        [SerializeField] private double ambientIntensity = 1;
        [SerializeField] private double indirectIntensity = 1;
        
        [Header("Direct lighting parameters")]
        [SerializeField]private int2 fromDate = new (1, 1);
        [SerializeField]private int2 toDate = new (31, 12);
        [SerializeField][Range(1, 365)] private int daysBetweenSamples = 7;
        [SerializeField][Range(1, 24)] private int hoursBetweenSamples = 2;
        
        [Header("Indirect lighting parameters")] 
        [SerializeField][Range(1, 32)] private int angularSamples = 16;
        [SerializeField][Range(1, 50)] private int distanceSamples = 20;
        [SerializeField][Range(1, 500)] private double maxDistance = 50;

        public void Temperature(in doubleF heightfield, doubleF output)
        {
            uint seed = 1;
            doubleF tmp = new doubleF(heightfield, Allocator.Persistent);
            DirectLighting(heightfield, tmp);
            field.add(tmp, to: output);
            IndirectLighting(heightfield, output, tmp, new Random(seed));
            field.add(tmp, to: output);
            AmbientLighting(heightfield, tmp);
            field.add(tmp, to: output);
            field.normalize(output, output);
        }
        
        public JobHandle Temperature(in doubleF heightfield, doubleF output, Allocator allocator, JobHandle dependsOn)
        {
            doubleF tmp = new doubleF(heightfield, allocator);
            dependsOn = DirectLighting(heightfield, tmp, dependsOn);
            dependsOn = Add(tmp, output, dependsOn);
            dependsOn = IndirectLighting(heightfield, output, tmp, dependsOn);
            dependsOn = Add(tmp, output, dependsOn);
            dependsOn = AmbientLighting(heightfield, tmp, dependsOn);
            dependsOn = Add(tmp, output, dependsOn);
            return dependsOn;
        }

        private int Days(int2 x) => new DateTime(2000, x.y, x.x).DayOfYear;
        
        public void DirectLighting(in doubleF heightfield, doubleF output)
            => Lighting.DirectLighting(in heightfield, output, latitude, daysBetweenSamples, hoursBetweenSamples, Days(fromDate), Days(toDate));
        
        public JobHandle DirectLighting(in doubleF heightfield, doubleF output, JobHandle dependsOn)
            => Lighting.DirectLighting(in heightfield, output, latitude, daysBetweenSamples, hoursBetweenSamples, dependsOn, Days(fromDate), Days(toDate));
        
        public void AmbientLighting(in doubleF heightfield, doubleF output)
            => Lighting.AmbientLighting(in heightfield, output);
        
        public JobHandle AmbientLighting(in doubleF heightfield, doubleF output, JobHandle dependsOn)
            => Lighting.AmbientLighting(in heightfield, output, dependsOn);
        
        public void IndirectLighting(in doubleF heightfield, in doubleF directLighting, doubleF output, Random rng) 
            => Lighting.IndirectLighting(in heightfield, in directLighting, output, distanceSamples, angularSamples, maxDistance, rng);
        
        public JobHandle IndirectLighting(in doubleF heightfield, in doubleF directLighting, doubleF output, JobHandle dependsOn) 
            => Lighting.IndirectLighting(in heightfield, in directLighting, output, distanceSamples, angularSamples, maxDistance, dependsOn);
        
        public JobHandle IndirectLightingParallel(in doubleF heightfield, in doubleF directLighting, doubleF output, JobHandle dependsOn) 
            => Lighting.IndirectLightingParallel(in heightfield, in directLighting, output, distanceSamples, angularSamples, maxDistance, dependsOn);


        private JobHandle Add(in doubleF x, doubleF to, JobHandle dependsOn)
            => new AddJob { x = x, to = to }.Schedule(dependsOn);

        struct AddJob : IJob
        {
            [ReadOnly] public doubleF x;
            public doubleF to;

            public void Execute()
            {
                field.add(x, to);
            }
        }
    }
}