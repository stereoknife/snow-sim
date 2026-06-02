using DemoApp.UI;
using ImGuiNET;
using UImGui;
using UnityEngine;

namespace DemoApp
{
    public class TimelineEditorUI : MonoBehaviour
    {
        private bool[] hoverEnter = new bool[366];
        private float[] precip = new float[366];
        private float[] cloud = new float[366];
        private float[] wind = new float[366];

        private bool showValues;
        
        private bool useGaussian;
        private int gaussianSize;

        private float monthlyPrecip;

        private void OnLayout(UImGui.UImGui obj)
        {
            if (!ImGui.Begin("Timeline editor")) return;

            ImGui.Checkbox("Show values", ref showValues);
            ImGui.PushItemWidth(ImGui.GetItemRectSize().x);
            ImGui.Checkbox("Use Gaussian", ref useGaussian);
            ImGui.SameLine();
            ImGui.InputInt("Gaussian size", ref gaussianSize);
            ImGui.PopItemWidth();
            
            ImGui.NewLine();
            
            var values = precip;
            if (ImGui.BeginTabBar("MyTabBar"))
            {
                if (ImGui.BeginTabItem("Precipitation"))
                {
                    values = precip;
                    ImGui.DragFloat("Monthly precipitation", ref monthlyPrecip);
                    ImGui.SameLine();
                    ImGui.Dummy(new Vector2(19, 19));
                    ImGui.Button("Generate");
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Cloud cover"))
                {
                    values = cloud;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Wind speed"))
                {
                    values = wind;
                    ImGui.EndTabItem();
                }
            }
            ImGui.NewLine();
            var spacing = 14f;
            var hMonths = (int)(ImGui.GetContentRegionAvail().x / (Calendar.Width + spacing / 2));
            hMonths = Mathf.Max(1, hMonths);
            for (int i = 0; i < 5; i++)
            {
                if (i % hMonths != 0)
                {
                    ImGui.SameLine();
                    ImGui.Dummy(new (spacing, spacing));
                    ImGui.SameLine();
                }
                Calendar.Month(values, hoverEnter, i + 1, 2000, showValues);
            }
            ImGui.EndTabBar();
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
        
        private void OnEnable()
        {
            UImGuiUtility.Layout += OnLayout;
            UImGuiUtility.OnInitialize += OnInitialize;
            UImGuiUtility.OnDeinitialize += OnDeinitialize;
        }

        private void OnDisable()
        {
            UImGuiUtility.Layout -= OnLayout;
            UImGuiUtility.OnInitialize -= OnInitialize;
            UImGuiUtility.OnDeinitialize -= OnDeinitialize;
        }
    }
}