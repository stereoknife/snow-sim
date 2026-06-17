using System;
using DemoApp.UI;
using ImGuiNET;
using UImGui;
using UnityEngine;

namespace DemoApp
{
    public class TimelineEditorUI : MonoBehaviour
    {
        private bool showValues;
        
        private bool useGaussian;
        private int gaussianSize;
        private float monthlyPrecip;

        private DateTime _startDate = new (2000, 11, 1);
        private DateTime _endDate = new (2001, 4, 30);
        
        private Calendar _precipCal, _cloudCal, _windCal;

        private void Awake()
        {
            _precipCal = new (_startDate, _endDate);
            _cloudCal = new (_startDate, _endDate);
            _windCal = new (_startDate, _endDate);
        }

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
            
            Calendar cal = _precipCal;
            if (ImGui.BeginTabBar("MyTabBar"))
            {
                if (ImGui.BeginTabItem("Precipitation"))
                {
                    cal = _precipCal;
                    ImGui.DragFloat("Monthly precipitation", ref monthlyPrecip);
                    ImGui.SameLine();
                    ImGui.Dummy(new Vector2(19, 19));
                    ImGui.Button("Generate");
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Cloud cover"))
                {
                    cal = _cloudCal;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Wind speed"))
                {
                    cal = _windCal;
                    ImGui.EndTabItem();
                }
            }
            ImGui.NewLine();
            var spacing = 14f;
            var hMonths = (int)(ImGui.GetContentRegionAvail().x / (Calendar.Width + spacing / 2));
            hMonths = Mathf.Max(1, hMonths);

            var endMonth = 1 + _endDate.Month + (_endDate.Year - _startDate.Year) * 12 - _startDate.Month;
            for (int i = 0; i < endMonth; i++)
            {
                if (i % hMonths != 0)
                {
                    ImGui.SameLine();
                    ImGui.Dummy(new (spacing, spacing));
                    ImGui.SameLine();
                }
                cal.Month(i);
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