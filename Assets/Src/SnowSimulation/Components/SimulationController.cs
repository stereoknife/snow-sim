using System.Collections;
using System.IO;
using EasyButtons;
using HPML;
using TFM.Solvers;
using TFM.Components.Visualization;
using TFM.Simulation;
using Unity.Collections;
using UnityEngine;
using static Unity.Mathematics.math;
using TFM.SnowSimulation.Data;
using Unity.Mathematics;
using noise = Unity.Mathematics.noise;

namespace TFM.Components
{
    public class SimulationController : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private SimulationTerrain terrain;
        [SerializeField] private SimulationParameters parameters;
        [SerializeField] private double4 initialSnowValue;
        [Header("Events")]
        [SerializeField] private bool snowfall;
        [SerializeField] private bool windTransport;
        [SerializeField] private bool melting;
        [SerializeField] private bool stability;
        [SerializeField] private bool powderDiffusion;
        [SerializeField] private bool avalanche;
        [SerializeField] private bool simpleMelt;
        [Header("Timelines")]
        [SerializeField] private bool temperatureTimeline;
        [SerializeField] private bool precipitationTimeline;
        [SerializeField] private bool windTimeline;
        [SerializeField] private bool cloudCoverTimeline;
        [Header("Profiling")]
        [SerializeField] private bool enableProfiling;
        [SerializeField] private string outputFile;
        [SerializeField][Min(1)] private int bufferSize;
        [Header("Run parameters")]
        [SerializeField] private int frames;
        [SerializeField] protected float time;

        private NativeList<double> _tempTimeline;
        private NativeList<double> _cloudTimeline;
        private NativeList<double> _windTimeline;
        private NativeList<double> _precipTimeline;

        private NativeHashSet<int> _selectedPoints, _highlightedPoints;

        public doubleF Heightfield => _simulation.Height;
        public double4F Snowfield => _simulation.SnowLayers;
        public NativeHashSet<int> SelectedPoints => _selectedPoints;
        public NativeHashSet<int> HighlightedPoints => _highlightedPoints;
        public ScalarField2D WindAltitude => _simulation.WindAltitude;

        protected StochasticSimulation _simulation;
        private bool runSim = false;

        private TerrainMeshRenderer _renderer;
        protected int minDay;

        [Button]
        private void StartSimulation()
        {
            runSim = true;
            //_simulation.Reset();
            StartCoroutine(RunSimulationCR());
        }

        [Button]
        private void PauseSimulation()
        {
            runSim = false;
        }
        
        [Button]
        private void RunFrames()
        {
            runSim = true;
            //_simulation.Reset();
            StartCoroutine(RunFramesCR());
        }
        
        [Button]
        protected void RunTime()
        {
            runSim = true;
            //_simulation.Reset();
            StartCoroutine(RunTimeCR());
        }

        [Button]
        protected void Reset()
        {
            var snow = Snowfield;
            for (int i = 0; i < snow.Length; i++)
            {
                snow[i] = initialSnowValue;
            }
            _simulation.Reset();
        }
        
        [Button]
        private void ExportProfilingData()
        {
            _simulation.ExportProfilingData(Path.Combine(Application.dataPath, "results", $"{outputFile}.csv"));
            Debug.Log($"File written at {outputFile}.csv");
        }

        [Button]
        private void ForceAvalanche()
        {
            if (_selectedPoints.IsEmpty)
            {
                _simulation.TriggerAvalanche();
            }
            else
            {
                foreach (var point in _selectedPoints)
                {
                    _simulation.TriggerAvalanche(point);
                }
            }
        }

        private IEnumerator RunSimulationCR()
        {
            while (runSim)
            {
                _simulation.Step();
                yield return null;
            }
        }

        private IEnumerator RunFramesCR()
        {
            for (int i = 0; i < frames && runSim; i++)
            {
                _simulation.Step();
                yield return null;
            }
            Debug.Log("Done");
        }
        
        private IEnumerator RunTimeCR()
        {
            while(runSim && _simulation.simulationTime < time)
            {
                _simulation.Step();
                yield return null;
            }
            Debug.Log("Done");
        }

        public void HighlightPoint(int index, bool clearHighlights = true)
        {
            if (clearHighlights) _highlightedPoints.Clear();
            if (0 <= index && index < Heightfield.Length) _highlightedPoints.Add(index);
        }
        
        public void SelectPoint(int index, bool clearSelection = true)
        {
            if (clearSelection) _selectedPoints.Clear();
            if (0 <= index && index < Heightfield.Length) _selectedPoints.Add(index);
        }

        private void Awake()
        {
            _renderer = GetComponent<TerrainMeshRenderer>();
            
            _selectedPoints = new NativeHashSet<int>(10, Allocator.Persistent);
            _highlightedPoints = new NativeHashSet<int>(10, Allocator.Persistent);

            _simulation = new StochasticSimulation
            {
                UseCloudTimeline = cloudCoverTimeline,
                UsePrecipTimeline = precipitationTimeline,
                UseTempTimeline = temperatureTimeline,
                UseWindTimeline = windTimeline,
                Enabled =
                {
                    [StochasticSimulation.EventId.MeltStep] = melting,
                    [StochasticSimulation.EventId.TransportStep] = windTransport,
                    [StochasticSimulation.EventId.DiffusionStep] = powderDiffusion,
                    [StochasticSimulation.EventId.SnowfallStep] = snowfall,
                    [StochasticSimulation.EventId.SnowfallStart] = snowfall,
                    [StochasticSimulation.EventId.SnowfallEnd] = snowfall,
                    [StochasticSimulation.EventId.AvalancheStart] = avalanche,
                    [StochasticSimulation.EventId.AvalancheStep] = avalanche,
                    [StochasticSimulation.EventId.StabilityStep] = stability
                },
                UseSimpleMelt = simpleMelt
            };
            
            _simulation.Init(terrain, parameters);
            
            _tempTimeline = _simulation.TempTimeline;
            _cloudTimeline = _simulation.CloudTimeline;
            _windTimeline = _simulation.WindTimeline;
            _precipTimeline = _simulation.PrecipTimeline;
            
            var startDay = parameters.LightingParameters.DirectStartingDay;
            var endDay = parameters.LightingParameters.DirectEndDay;
            if (startDay > endDay) endDay += 365;
            var tlLength = endDay - startDay;
            
            _tempTimeline.ResizeUninitialized(tlLength);
            _cloudTimeline.ResizeUninitialized(tlLength);
            _windTimeline.ResizeUninitialized(tlLength);
            _precipTimeline.ResizeUninitialized(tlLength);

            time = tlLength;
            
            GenerateTemperatureTimeline();
            GenerateCloudPrecipTimelines();
            GenerateWindTimeline();

            if (enableProfiling)
                _simulation.EnableProfiling(bufferSize);
            else
                _simulation.DisableProfiling();
        }
        
        private void SetSimulationParams()
        {
            _simulation.Enabled[StochasticSimulation.EventId.MeltStep] = melting;
            _simulation.Enabled[StochasticSimulation.EventId.TransportStep] = windTransport;
            _simulation.Enabled[StochasticSimulation.EventId.DiffusionStep] = powderDiffusion;
            _simulation.Enabled[StochasticSimulation.EventId.SnowfallStep] = snowfall;
            _simulation.Enabled[StochasticSimulation.EventId.SnowfallStart] = snowfall;
            _simulation.Enabled[StochasticSimulation.EventId.SnowfallEnd] = snowfall;
            _simulation.Enabled[StochasticSimulation.EventId.AvalancheStart] = avalanche;
            _simulation.Enabled[StochasticSimulation.EventId.AvalancheStep] = avalanche;
            _simulation.UseCloudTimeline = cloudCoverTimeline;
            _simulation.UsePrecipTimeline = precipitationTimeline;
            _simulation.UseTempTimeline = temperatureTimeline;
            _simulation.UseWindTimeline = windTimeline;
            
            if (enableProfiling)
                _simulation.EnableProfiling(bufferSize);
            else
                _simulation.DisableProfiling();
        }

        private void OnValidate()
        {
            if (_simulation != null) SetSimulationParams();
        }

        private void GenerateTemperatureTimeline()
        {
            // Temperature follows a sin curve starting between spring equinox and summer solstice
            // a * sin(b * (x - c)) + d
            var maxTemp = 30f;
            var minTemp = -3;
            
            var a = (maxTemp - minTemp) / 2f;
            var b = PI2 / 365f;
            var c = -10f;
            var d = (maxTemp + minTemp) / 2f;

            var startDay = parameters.LightingParameters.DirectStartingDay;
            minDay = 0;
            for (int i = 0; i < _tempTimeline.Length; i++)
            {
                _tempTimeline[i] = a * sin(b * (i + startDay - c) - PI * 5f/8f) + d;
                if (i > 0 && _tempTimeline[i] < _tempTimeline[i - 1]) minDay = i;
            }
            Debug.Log($"min day {minDay}");
        }

        private void GenerateCloudPrecipTimelines()
        {
            for (int i = 0; i < _cloudTimeline.Length; i++)
            {
                _cloudTimeline[i] = noise.cnoise(float2(-420f, i / 5f)) / 2f + 0.5f;
                _precipTimeline[i] = saturate(unlerp(0.7, 1, _cloudTimeline[i])) * 0.005f;
            }
        }

        private void GenerateWindTimeline()
        {
            var maxWindSpeed = parameters.WindParameters.SurfaceSpeedIncrement *
                               parameters.WindParameters.SurfaceSamples;
            for (int i = 0; i < _windTimeline.Length; i++)
            {
                _windTimeline[i] = 8f;//(noise.cnoise(float2(i / 2f, -20f)) + 1f) / 2f * maxWindSpeed;
            }
        }
        

        private void OnGUI()
        {
            var t = _simulation.simulationTime;
            var style = new GUIStyle { fontSize = 20 };
            GUILayout.Label($"Simulation time: {t}", style);
            GUILayout.Label($"Temperature: {_simulation.Parameters.TempBase}", style);
            GUILayout.Label($"Cloud cover: {_simulation.Parameters.CloudCover}", style);
            GUILayout.Label($"Precipitation: {_simulation.Parameters.SnowfallIntensity * _simulation.Parameters.SnowfallStrength}", style);
            GUILayout.Label($"Wind speed: {_simulation.Parameters.WindSpeed}", style);
        }

        private void OnDestroy()
        {
            runSim = false;
            _selectedPoints.Dispose();
            _highlightedPoints.Dispose();
            _simulation.Dispose();
        }
    }
}