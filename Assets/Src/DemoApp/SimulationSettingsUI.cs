using System;
using ImGuiNET;
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

    private void OnLayout(UImGui.UImGui obj)
    {
        if (!ImGui.Begin("Simulation settings"))
        {
            ImGui.End();
            return;
        }
        
        //ImGui.PushItemWidth(-0);
        var w = ImGui.GetContentRegionAvail().x;
        ImGui.Button("Start", new Vector2(w, 19));
        ImGui.Button("Pause", new Vector2(w, 19));
        ImGui.Checkbox("Use timeline", ref useTimeline);

        if (ImGui.CollapsingHeader("Simulation events"))
        {
            //ImGui.LabelText("Period", "Enabled");
            ImGui.Checkbox("###mt", ref evMelt);
            ImGui.SameLine();
            ImGui.DragFloat("Melt", ref evMeltPeriod);
            
            ImGui.Checkbox("###tp", ref evTransport);
            ImGui.SameLine();
            ImGui.DragFloat("Transport", ref evTransportPeriod);
            
            ImGui.Checkbox("###df", ref evDiffusion);
            ImGui.SameLine();
            ImGui.DragFloat("Diffusion", ref evDiffusionPeriod);
            
            ImGui.Checkbox("###sf", ref evSnowfall);
            ImGui.SameLine();
            ImGui.DragFloat("Snowfall", ref evSnowfallPeriod);
            ImGui.BeginDisabled();
            ImGui.Checkbox("###sf2", ref evSnowfall);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.DragFloat("Snowfall start", ref evSnowfallStartPeriod);
            ImGui.BeginDisabled();
            ImGui.Checkbox("###sf3", ref evSnowfall);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.DragFloat("Snowfall end", ref evSnowfallEndPeriod);
            
            ImGui.Checkbox("###av", ref evAvalanche);
            ImGui.SameLine();
            ImGui.DragFloat("Avalanche", ref evAvalanchePeriod);
            ImGui.BeginDisabled();
            ImGui.Checkbox("###av2", ref evAvalanche);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.DragFloat("Avalanche start", ref evAvalancheStartPeriod);
        }
        
        ImGui.CollapsingHeader("Simulation parameters");
            
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
