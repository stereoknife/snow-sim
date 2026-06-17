using System;
using DemoApp;
using ImGuiNET;
using TFM.Components;
using UImGui;
using Unity.Mathematics;
using UnityEngine;

public class SimulationSettingsUI : MonoBehaviour
{
    private void OnEnable()
    {
        UImGuiUtility.Layout += OnLayout;
        UImGuiUtility.OnInitialize += OnInitialize;
        UImGuiUtility.OnDeinitialize += OnDeinitialize;
    }

    private bool[] hoverEnter = new bool[366];
    private float[] values = new float[366];

    private bool useTimeline;
    private bool runFrames;
    private float days;
    private int frames;

    private bool evSnowfall = true;
    private bool evMelt = true;
    private bool evTransport = true;
    private bool evDiffusion = true;
    private bool evAvalanche = true;

    private float evSnowfallPeriod = 0.5f;
    private float evSnowfallStartPeriod = 7f;
    private float evSnowfallEndPeriod = 7f;
    private float evMeltPeriod = 0.5f;
    private float evTransportPeriod = 0.5f;
    private float evDiffusionPeriod = 0.5f;
    private float evAvalanchePeriod = 0.5f;
    private float evAvalancheStartPeriod = 15f;

    private int2 seMonths = new int2(1, 12);
    private int2 seDays = new int2(1, 31);

    private float test;

    private TimelineEditorUI _tl;
    private AnalysisWindow _an;

    private int _selectedTerrain;

    private SimulationController _sim;

    private void Awake()
    {
        _tl = GetComponent<TimelineEditorUI>();
        _an = GetComponent<AnalysisWindow>();
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
        
        var w = ImGui.GetContentRegionAvail().x;
        ImGui.Button("Start", new Vector2(w, 19));
        ImGui.Button("Pause", new Vector2(w, 19));

        if (ImGui.Combo("Terrain", ref _selectedTerrain, "Canigó\0Núria\0Everest"))
        {
            
        }
        
        ImGui.SeparatorText("Timeline");
        ImGui.BeginTable("Events", 2);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Temperature", ref useTimeline);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Wind speed", ref useTimeline);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Precipitation", ref useTimeline);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Cloud cover", ref useTimeline);
        ImGui.EndTable();
        if (ImGui.Button("Timeline editor")) _tl.enabled = true;

        ImGui.SeparatorText("Events");
        ImGui.BeginTable("Events", 2);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Melt", ref evMelt);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Stability", ref evMelt);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Wind Transport", ref evTransport);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Diffusion", ref evDiffusion);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Precipitation", ref evSnowfall);
        ImGui.TableNextColumn();
        ImGui.Checkbox("Avalanche", ref evAvalanche);
        ImGui.EndTable();
        ImGui.Button("Set parameters");
            
        ImGui.End();
    }

    private void OnInitialize(UImGui.UImGui obj)
    {
        // Runs after UImGui.OnEnable();
    }

    private void OnDeinitialize(UImGui.UImGui obj)
    {
        // Runs after UImGui.OnDisable();
    }

    private void OnDisable()
    {
        UImGuiUtility.Layout -= OnLayout;
        UImGuiUtility.OnInitialize -= OnInitialize;
        UImGuiUtility.OnDeinitialize -= OnDeinitialize;
    }
}
