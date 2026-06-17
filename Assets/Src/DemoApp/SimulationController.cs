using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EasyButtons;
using HPML;
using ImGuiNET;
using TFM.Solvers;
using TFM.Components.Visualization;
using TFM.Simulation;
using Unity.Collections;
using UnityEngine;
using static Unity.Mathematics.math;
using TFM.SnowSimulation.Data;
using UImGui;
using Unity.Mathematics;
using UnityEngine.Serialization;
using noise = Unity.Mathematics.noise;

namespace DemoApp
{
    public class SimulationController : MonoBehaviour, IRenderTerrain
    {
        [SerializeField] private GameObject ui;
        [SerializeField] private SimulationTerrain[] terrains;
        
        public NativeHashSet<int> SelectedPoints { get; private set; }
        public NativeHashSet<int> HighlightedPoints { get; private set; }
        
        private StochasticSimulation _simulation;
        private Wind.Parameters _windParams;
        private Lighting.Parameters _lightParams;
        
        public doubleF Heightfield => _simulation.Height;
        public double4F Snowfield => _simulation.SnowLayers;
        public ref Wind.Parameters WindParams => ref _windParams;
        public ref Lighting.Parameters LightParams => ref _lightParams;
        public StochasticSimulation Simulation => _simulation;
        public bool IsRunning { get; private set; }
        
        // UI VARIABLES -----
        private string _terrainNames;
        private bool _useTempTimeline, _useWindTimeline, _useCloudTimeline, _useSnowTimeline;
        private bool _evMelt, _evStability, _evTransport, _evSnow, _evAvalanche, _evDiffusion;

        private int _selectedTerrain;
        private float _timeLimit;

        private TimelineEditorUI _tl;
        private TerrainMeshRenderer _meshRenderer;
        // END
        
        private void Awake()
        {
            _tl = ui.GetComponent<TimelineEditorUI>();
            _meshRenderer = GetComponent<TerrainMeshRenderer>();
            
            SelectedPoints = new NativeHashSet<int>(10, Allocator.Persistent);
            HighlightedPoints = new NativeHashSet<int>(10, Allocator.Persistent);
            _terrainNames = (from t in terrains select t.name).Aggregate((acc, next) => $"{acc}\0{next}");
            
            _simulation = new StochasticSimulation();
            _simulation.Init(terrains[_selectedTerrain]);

            _windParams = Wind.Parameters.Default;
            _lightParams = Lighting.Parameters.Default;
        }

        private void Start()
        {
            _meshRenderer.SetTerrain(_simulation.Height, _simulation.SnowLayers);
        }

        private void OnEnable()
        {
            UImGuiUtility.Layout += OnLayout;
            //UImGuiUtility.OnInitialize += OnInitialize;
            //UImGuiUtility.OnDeinitialize += OnDeinitialize;
        }
        
        private void OnDisable()
        {
            UImGuiUtility.Layout -= OnLayout;
            //UImGuiUtility.OnInitialize -= OnInitialize;
            //UImGuiUtility.OnDeinitialize -= OnDeinitialize;
        }

        private void Update()
        {
            if (!IsRunning) return;
            _simulation.Step();
            IsRunning = _simulation.simulationTime < _timeLimit;
        }

        private void OnLayout(UImGui.UImGui obj)
        {
            if (!ImGui.Begin("Simulation settings", ImGuiWindowFlags.MenuBar))
            {
                ImGui.End();
                return;
            }

            if (ImGui.BeginMenuBar())
            {
                if(ImGui.BeginMenu("View"))
                {
                    if (ImGui.MenuItem("Timeline editor")) _tl.enabled = true;
                    ImGui.MenuItem("Simulation parameters");
                    ImGui.MenuItem("Performance analysis");
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
            
            if (!_simulation.IsReady) ImGui.BeginDisabled();
            var w = ImGui.GetContentRegionAvail().x;
            if (ImGui.Button("Start", new Vector2(w, 19))) IsRunning = true;
            if (ImGui.Button("Pause", new Vector2(w, 19))) IsRunning = false;
            if (!_simulation.IsReady) ImGui.EndDisabled();

            var time = _simulation.simulationTime;
            var hour = frac(time) * 24f;
            var minute = frac(hour) * 60f;
            var second = frac(minute) * 60f;
            ImGui.Text($"H {hour}:{minute}:{second}");
            ImGui.Text($"Day {(int)floor(time)} {(int)floor(hour):d2}:{(int)floor(minute):d2}:{(int)floor(second):d2}");

            if (ImGui.Combo("Terrain", ref _selectedTerrain, _terrainNames))
            {
                _simulation.Init(terrains[_selectedTerrain]);
                _meshRenderer.SetTerrain(_simulation.Height, _simulation.SnowLayers);
            }
            
            if (!_simulation.IsInit) ImGui.BeginDisabled();
            if (ImGui.Button("Generate environment")) _simulation.Generate(_windParams, _lightParams, true, 1337);
            if (!_simulation.IsInit) ImGui.EndDisabled();
            
            ImGui.SeparatorText("Timeline");
            ImGui.BeginTable("Events", 2);
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Temperature", ref _useTempTimeline)) _simulation.UseTempTimeline = _useTempTimeline;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Wind speed", ref _useWindTimeline)) _simulation.UseWindTimeline = _useWindTimeline;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Precipitation", ref _useSnowTimeline)) _simulation.UsePrecipTimeline = _useSnowTimeline;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Cloud cover", ref _useCloudTimeline)) _simulation.UseCloudTimeline = _useCloudTimeline;
            ImGui.EndTable();
            if (ImGui.Button("Timeline editor")) _tl.enabled = true;

            ImGui.SeparatorText("Events");
            ImGui.BeginTable("Events", 2);
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Melt", ref _evMelt))
                _simulation.Enabled[StochasticSimulation.EventId.MeltStep] = _evMelt;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Stability", ref _evStability))
                _simulation.Enabled[StochasticSimulation.EventId.StabilityStep] = _evStability;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Wind Transport", ref _evTransport))
                _simulation.Enabled[StochasticSimulation.EventId.TransportStep] = _evTransport;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Diffusion", ref _evDiffusion))
                _simulation.Enabled[StochasticSimulation.EventId.DiffusionStep] = _evDiffusion;
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Precipitation", ref _evSnow))
            {
                _simulation.Enabled[StochasticSimulation.EventId.SnowfallStart] = _evSnow;
                _simulation.Enabled[StochasticSimulation.EventId.SnowfallStep] = _evSnow;
                _simulation.Enabled[StochasticSimulation.EventId.SnowfallEnd] = _evSnow;
            }
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Avalanche", ref _evAvalanche))
            {
                _simulation.Enabled[StochasticSimulation.EventId.AvalancheStart] = _evAvalanche;
                _simulation.Enabled[StochasticSimulation.EventId.AvalancheStep] = _evAvalanche;
            }
            ImGui.EndTable();
            ImGui.Button("Set parameters");
            
            ImGui.SeparatorText("Visualisation");
            //ImGui.Selectable();
                
            ImGui.End();
        }
    }
}