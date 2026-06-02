using System;
using ImGuiNET;
using UnityEngine;

namespace DemoApp.UI
{
    public static class Calendar
    {
        private const int kdayWidth = 30;
        private const int kdayHeight = 20;

        public static float Width => kdayWidth * 7 + ImGui.GetStyle().ItemSpacing.x * 6;
        
        public static void Month(float[] values, bool[] hoverEnter, int month, int year = 2000, bool printValues = false, bool fillPadding = false, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        {
            ImGui.BeginGroup();
            var day = new DateTime(year, month, 1);
            var firstDayAsIndex = day.DayOfYear - 1;
            ImGui.Text(day.ToString("MMM"));
            var dayOfWeek = day.DayOfWeek;
            while (day.DayOfWeek != firstDayOfWeek) day = day.AddDays(-1);

            var diff = firstDayOfWeek - day.DayOfWeek;

            string label;
            ImGui.BeginDisabled();
            for (; day.DayOfWeek != dayOfWeek; day = day.AddDays(1))
            {
                if (day.DayOfWeek != firstDayOfWeek) ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f,0f));
                label = fillPadding ? $"{day.Day}" : "";
                ImGui.Button($"{label}###m{month}d-{day.Day}", new Vector2(kdayWidth, kdayHeight));
            }
            ImGui.EndDisabled();
            
            for (; day.Month == month; day = day.AddDays(1))
            {
                var styles = 1;
                if (day.DayOfWeek != DayOfWeek.Monday) ImGui.SameLine();
                var i = day.DayOfYear;
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f, values[i]));
                if (printValues && values[i] > 0.499f)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 0f, 0f, 1f));
                    styles++;
                }
                label = printValues ? $"{values[i] * 100f}%" : $"{day.Day}";
                ImGui.Button($"{label}###m{month}d{day.Day}", new Vector2(30, 20));

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
                {
                    if (!hoverEnter[i])
                    {
                        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left)) values[i] = Mathf.Clamp01(values[i] + 0.1f);
                        if (ImGui.IsMouseDragging(ImGuiMouseButton.Right)) values[i] = Mathf.Clamp01(values[i] - 0.1f);
                    }
                    else
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) values[i] = Mathf.Clamp01(values[i] + 0.1f);
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) values[i] = Mathf.Clamp01(values[i] - 0.1f);
                    }
                    hoverEnter[i] = true;
                }
                else
                {
                    hoverEnter[i] = false;
                }
                
                ImGui.PopStyleColor(styles);
            }
            ImGui.PushID(month);
            ImGui.PlotHistogram("###hist", ref values[0], DateTime.DaysInMonth(2000, month), firstDayAsIndex, "", 0, 1, new Vector2(Width, 19));
            ImGui.PopID();
            ImGui.EndGroup();
        }
    }
}