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
        [Header("Illumination")] 
        [SerializeField] private double directIntensity = Lighting.Parameters.Default.IntensityDirect;
        [SerializeField] private double ambientIntensity = Lighting.Parameters.Default.IntensityAmbient;
        [SerializeField] private double indirectIntensity = Lighting.Parameters.Default.IntensityIndirect;

        [SerializeField] private float latitude = 45;
        [SerializeField] private int2 fromDate = new (1, 1);
        [SerializeField] private int2 toDate = new (31, 12);
        [SerializeField][Range(1, 365)] private int daysBetweenSamples = Lighting.Parameters.Default.DirectDaysBetweenSamples;
        [SerializeField][Range(1, 24)] private int hoursBetweenSamples = Lighting.Parameters.Default.DirectHoursBetweenSamples;
        
        [SerializeField][Range(1, 32)] private int angularSamples = Lighting.Parameters.Default.IndirectAngularSamples;
        [SerializeField][Range(1, 50)] private int distanceSamples = Lighting.Parameters.Default.IndirectDistanceSamples;
        [SerializeField][Range(1, 500)] private double maxDistance = Lighting.Parameters.Default.IndirectMaxDistance;

        [Header("Wind")]
        [SerializeField] private double venturiIntensity = Wind.Parameters.Default.VenturiIntensity;
        [SerializeField] private double deflectionIntensity = Wind.Parameters.Default.DeflectionIntensity;
        [SerializeField] private double surfaceFalloff = Wind.Parameters.Default.SurfaceFalloff;
        [SerializeField] private int surfaceMaxIterations = Wind.Parameters.Default.SurfaceMaxIterations;
        [SerializeField] private int surfaceSamples = Wind.Parameters.Default.SurfaceSamples;
        [SerializeField] private double surfaceSpeedIncrement = Wind.Parameters.Default.SurfaceSpeedIncrement;

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
            VenturiIntensity = venturiIntensity,
            DeflectionIntensity = deflectionIntensity,
            SurfaceFalloff = surfaceFalloff,
            SurfaceMaxIterations = surfaceMaxIterations,
            SurfaceSamples = surfaceSamples,
            SurfaceSpeedIncrement = surfaceSpeedIncrement
        };
        
        private int Days(int2 x) => new DateTime(2000, x.y, x.x).DayOfYear;
    }
}