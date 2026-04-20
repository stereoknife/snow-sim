using Sim.Simulation;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

namespace Sim.Structs
{
    public class Sun : MonoBehaviour
    {
        [SerializeField][Range(-90, 90)] private double latitude = 45;
        
        [Header("Effect intensity")] 
        [SerializeField] private double directIntensity = 1;
        [SerializeField] private double ambientIntensity = 1;
        [SerializeField] private double indirectIntensity = 1;
        
        [Header("Direct lighting parameters")] 
        [SerializeField][Range(1, 365)] private int daySamples = 8;
        [SerializeField][Range(1, 24)] private int hourSamples = 12;
        
        [Header("Indirect lighting parameters")] 
        [SerializeField][Range(1, 32)] private int angularSamples = 16;
        [SerializeField][Range(1, 50)] private int distanceSamples = 20;
        [SerializeField][Range(1, 500)] private double maxDistance = 50;
        
        public void DirectLighting(in ScalarField2D heightfield, ScalarField2D output)
            => Lighting.DirectLighting(in heightfield, output, latitude, daySamples, hourSamples);
        
        public JobHandle DirectLighting(in ScalarField2D heightfield, ScalarField2D output, JobHandle dependsOn)
            => Lighting.DirectLighting(in heightfield, output, latitude, daySamples, hourSamples, dependsOn);
        
        public void AmbientLighting(in ScalarField2D heightfield, ScalarField2D output)
            => Lighting.AmbientLighting(in heightfield, output);
        
        public JobHandle AmbientLighting(in ScalarField2D heightfield, ScalarField2D output, JobHandle dependsOn)
            => Lighting.AmbientLighting(in heightfield, output, dependsOn);
        
        public void IndirectLighting(in ScalarField2D heightfield, in ScalarField2D directLighting, ScalarField2D output, Random rng) 
            => Lighting.IndirectLighting(in heightfield, in directLighting, output, distanceSamples, angularSamples, maxDistance, rng);
        
        public JobHandle IndirectLighting(in ScalarField2D heightfield, in ScalarField2D directLighting, ScalarField2D output, JobHandle dependsOn) 
            => Lighting.IndirectLighting(in heightfield, in directLighting, output, distanceSamples, angularSamples, maxDistance, dependsOn);
        
        public JobHandle IndirectLightingParallel(in ScalarField2D heightfield, in ScalarField2D directLighting, ScalarField2D output, JobHandle dependsOn) 
            => Lighting.IndirectLightingParallel(in heightfield, in directLighting, output, distanceSamples, angularSamples, maxDistance, dependsOn);
        
        
    }
}