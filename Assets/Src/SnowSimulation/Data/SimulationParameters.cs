using System;
using TFM.Simulation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace TFM.SnowSimulation.Data
{
    [CreateAssetMenu(fileName = "SimParameters", menuName = "Simulation Parameters", order = 0)]
    public class SimulationParameters : ScriptableObject
    {
        [SerializeField] public float4 initialSnowValue = new (0);
        [SerializeField] public bool loadFromCache = true;
        [SerializeField] public string cacheLocation = "cache";
        [SerializeField] public uint seed = 1337;
        [Header("Illumination")] 
        [SerializeField] public double directIntensity = Lighting.Parameters.Default.IntensityDirect;
        [SerializeField] public double ambientIntensity = Lighting.Parameters.Default.IntensityAmbient;
        [SerializeField] public double indirectIntensity = Lighting.Parameters.Default.IntensityIndirect;

        [SerializeField] public float latitude = 45;
        [SerializeField] public int2 fromDate = new (1, 1);
        [SerializeField] public int2 toDate = new (31, 12);
        [SerializeField][Range(1, 365)] public int daysBetweenSamples = Lighting.Parameters.Default.DirectDaysBetweenSamples;
        [SerializeField][Range(1, 24)] public int hoursBetweenSamples = Lighting.Parameters.Default.DirectHoursBetweenSamples;
        
        [SerializeField][Range(1, 32)] public int angularSamples = Lighting.Parameters.Default.IndirectAngularSamples;
        [SerializeField][Range(1, 100)] public int distanceSamples = Lighting.Parameters.Default.IndirectDistanceSamples;
        [SerializeField][Range(1, 500)] public double maxDistance = Lighting.Parameters.Default.IndirectMaxDistance;

        [Header("Wind")]
        [SerializeField][Range(0, 359)] public double windHeading = 0;
        [SerializeField] public double venturiIntensity = Wind.Parameters.Default.VenturiIntensity;
        [SerializeField] public double deflectionIntensity = Wind.Parameters.Default.DeflectionIntensity;
        [SerializeField] public double surfaceFalloff = Wind.Parameters.Default.SurfaceFalloff;
        [SerializeField] public int surfaceMaxIterations = Wind.Parameters.Default.SurfaceMaxIterations;
        [SerializeField] public int surfaceSamples = Wind.Parameters.Default.SurfaceSamples;
        [SerializeField] public double surfaceSpeedIncrement = Wind.Parameters.Default.SurfaceSpeedIncrement;
        [SerializeField] public int gaussianKernelSize = Wind.Parameters.Default.GaussianKernelSize;

        public Lighting.Parameters LightingParameters => new()
        {
            IntensityDirect = directIntensity,
            IntensityAmbient = ambientIntensity,
            IntensityIndirect = indirectIntensity,
            DirectLatitude = latitude,
            DirectStartingDay = Days(fromDate),
            DirectEndDay = Days(toDate),
            DirectDaysBetweenSamples = daysBetweenSamples,
            DirectHoursBetweenSamples = hoursBetweenSamples,
            IndirectAngularSamples = angularSamples,
            IndirectDistanceSamples = distanceSamples,
            IndirectMaxDistance = maxDistance,
            TemperatureIncreasePerMetre = -0.01,
            TemperatureIncreasePerSunlight = 10,
        };
        
        public Wind.Parameters WindParameters => new()
        {
            WindDirection = new double2(math.cos(math.radians(windHeading)), math.sin(math.radians(windHeading))),
            VenturiIntensity = venturiIntensity,
            DeflectionIntensity = deflectionIntensity,
            SurfaceFalloff = surfaceFalloff,
            SurfaceMaxIterations = surfaceMaxIterations,
            SurfaceSamples = surfaceSamples,
            SurfaceSpeedIncrement = surfaceSpeedIncrement,
            GaussianKernelSize = gaussianKernelSize,
        };
        
        private int Days(int2 x) => new DateTime(2000, x.y, x.x).DayOfYear;
    }
}