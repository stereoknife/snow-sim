using ImGuiNET;
using TFM.Components;
using TFM.Simulation;
using TFM.SnowSimulation.Data;
using UImGui;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;

namespace DemoApp
{
    public class ParamsWindow : MonoBehaviour
    {
        public bool show = true;
        
        [SerializeField] private SimulationController _control;
        
        // UI VARS
        private float _directIntensity;
        private float _ambientIntensity;
        private float _indirectIntensity;
        private float _indMaxDistance;
        
        private float _windHeading;
        private float _venturiIntensity;
        private float _deflectionIntensity;
        private float _surfaceFalloff;
        private float _maxWindSpeed;
        
        // Snowfall
        private float _snowfallStrength; //-------- day⁻¹
        private float _snowfallMax; //------------- m
        private float _snowfallPowderRatio; //----- scalar
        private float _snowfallUnstableRatio; //--- scalar
        private float _criticalSlopeMin; //-------- ratio
        private float _criticalSlopeTempFactor; //- °C⁻¹
        private float _criticalSlopeMaxTemp; //---- °C
        
        // Melt
        private float _meltRate;                 // m / °C
        private float _meltTemp;                 // °C
        private float _meltVolumeFactor;
        
        // Stability
        private float _stabilityStableTemp;          // °C
        private float _stabilityUnstableTemp;        // °C
        private float _stabilityFreezeTemp;          // °C
        private float _stabilityHot;                 // m/day
        private float _stabilityMedium;              // m/day
        private float _stabilityFreeze;              // m/day
        private float _stabilityCompactionPressure;  // ?
        private float _stabilityMinSlope;            // ratio
        
        // Diffusion
        private float _diffusionRestSlope;
        private float _diffusionRate;
        
        // Wind
        private float _windPlates;
        private float _windErosionRate;
        
        // Avalanche
        private float _avalancheSnowDensity;                  // g/cm^-3
        private float _avalancheSnowViscosity;               // s^-2
        private float _avalancheRestSlope;
        private float _avalancheGravity;
        private float _avalancheTemp;
        
        // Temp
        private float _temperatureIncreasePerMetre;
        private float _temperatureIncreasePerSunlight;
        
        private Vector3
            meshGround = new(0.30f, 0.20f, 0.08f),
            meshCliff = new(0.32f, 0.32f, 0.32f),
            meshSnow = new(0.68f, 0.71f, 0.73f);
        private Vector3
            layerBedrock = new(0.42f, 0.42f, 0.42f),
            layerCompacted = new(0.38f, 0.42f, 0.75f),
            layerStable = new(0.49f, 0.97f, 1f),
            layerUnstable = new(1, 0.26f, 0.26f),
            layerPowder = new(1f, 1f, 1f);

        // END

        private void OnLayout(UImGui.UImGui obj)
        {
            if (!ImGui.Begin("Settings")) return;

            ref Wind.Parameters wp = ref _control.WindParams;
            ref Lighting.Parameters lp = ref _control.LightParams;
            ref Snow.Parameters sp = ref _control.Simulation.Parameters;
            
            if (ImGui.BeginTabBar("Parameter selection"))
            {
                
                if (ImGui.BeginTabItem("Rendering"))
                {
                    ImGui.SeparatorText("Mesh renderer");
                    var cliffAngle = PI / 4;
                    ImGui.ColorEdit3("Ground colour", ref meshGround);
                    ImGui.ColorEdit3("Cliff colour", ref meshCliff);
                    ImGui.ColorEdit3("Snow colour", ref meshSnow);
                    ImGui.SliderAngle("Cliff angle", ref cliffAngle);
                    
                    ImGui.SeparatorText("Layers renderer");
                    ImGui.ColorEdit3("Ground colour", ref layerBedrock);
                    ImGui.ColorEdit3("Cliff colour", ref layerCompacted);
                    ImGui.ColorEdit3("Snow colour", ref layerStable);
                    ImGui.ColorEdit3("Cliff colour", ref layerUnstable);
                    ImGui.ColorEdit3("Snow colour", ref layerPowder);
                    ImGui.EndTabItem();
                }
                
                if (ImGui.BeginTabItem("Event timings"))
                {
                    var p = _control.Simulation.Periods;
                    Debug.Log(p.Length);
                    for (int i = 0; i < p.Length; i++)
                    {
                        var (k, v) = p[i];
                        var upd = ImGui.DragFloat(k.ToString(), ref v, 1, 0);
                        if (v <= 0)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
                            ImGui.Text("Value must be larger than 0");
                            ImGui.PopStyleColor(1);
                        }

                        if (upd)
                        {
                            _control.Simulation.SetEventPeriod(k, v);
                        }
                    }
                    ImGui.EndTabItem();
                }
                
                if (ImGui.BeginTabItem("Simulation"))
                {
                    ImGui.SeparatorText("Precipitation");
                    if (ImGui.DragFloat("Snow per day", ref _snowfallStrength, 1, 0))
                        sp.SnowfallStrength = _snowfallStrength;
                    if (ImGui.DragFloat("Maximum temperature multiplier", ref _snowfallMax, 1, 0))
                        sp.SnowfallMax = _snowfallMax;
                    if (ImGui.SliderFloat("Powder proportion", ref _snowfallPowderRatio, 0, 1))
                        sp.SnowfallPowderRatio = _snowfallPowderRatio;
                    if (ImGui.SliderFloat("Unstable proportion", ref _snowfallUnstableRatio, 0, 1))
                        sp.SnowfallUnstableRatio = _snowfallUnstableRatio;
                    if (ImGui.SliderAngle("Minimum critical slope", ref _criticalSlopeMin, 0, 90))
                        sp.CriticalSlopeMin = tan(_criticalSlopeMin);
                    if (ImGui.DragFloat("Critical slope temperature influence", ref _criticalSlopeTempFactor, 0.1f, 0))
                        sp.CriticalSlopeTempFactor = _criticalSlopeTempFactor;
                    if (ImGui.DragFloat("Maximum critical slope temperature", ref _criticalSlopeMaxTemp, 1, 0))
                        sp.CriticalSlopeMaxTemp = _criticalSlopeMaxTemp;
                    
                    ImGui.SeparatorText("Melt");
                    if (ImGui.DragFloat("MeltRate", ref _meltRate))
                        sp.MeltRate = _meltRate;
                    if (ImGui.DragFloat("MeltTemp", ref _meltTemp))
                        sp.MeltTemp = _meltTemp;
                    if (ImGui.DragFloat("MeltVolumeFactor", ref _meltVolumeFactor))
                        sp.MeltVolumeFactor = _meltVolumeFactor;
                    
                    ImGui.SeparatorText("Stability");
                    if (ImGui.DragFloat("Hot temperature", ref _stabilityStableTemp, 1, 1))
                        sp.StabilityStableTemp = _stabilityStableTemp;
                    if (ImGui.DragFloat("Cool temperature", ref _stabilityUnstableTemp, 1, float.MinValue, -1))
                        sp.StabilityUnstableTemp = _stabilityUnstableTemp;
                    if (ImGui.DragFloat("Freezing temperature", ref _stabilityFreezeTemp, 1, float.MinValue, _stabilityUnstableTemp))
                        sp.StabilityFreezeTemp = _stabilityFreezeTemp;
                    if (ImGui.DragFloat("Stability at hot temperature", ref _stabilityHot))
                        sp.StabilityHot = _stabilityHot;
                    if (ImGui.DragFloat("Stability at cool temperture", ref _stabilityMedium))
                        sp.StabilityMedium = _stabilityMedium;
                    if (ImGui.DragFloat("Stability at freezing temperature", ref _stabilityFreeze))
                        sp.StabilityFreeze = _stabilityFreeze;
                    if (ImGui.DragFloat("Compaction pressure", ref _stabilityCompactionPressure))
                        sp.StabilityCompactionPressure = _stabilityCompactionPressure;
                    if (ImGui.DragFloat("Minimum unstable slope", ref _stabilityMinSlope))
                        sp.StabilityMinSlope = _stabilityMinSlope;
                    
                    ImGui.SeparatorText("Diffusion");
                    if (ImGui.SliderAngle("Rest slope", ref _diffusionRestSlope, 0, 90))
                        sp.DiffusionRestSlope = tan(_diffusionRestSlope);
                    if (ImGui.DragFloat("Rate of diffusion", ref _diffusionRate))
                        sp.DiffusionRate = _diffusionRate;
                    
                    ImGui.SeparatorText("Wind");
                    if (ImGui.DragFloat("Unstability effect", ref _windPlates))
                        sp.WindPlates = _windPlates;
                    if (ImGui.DragFloat("Erosion rate", ref _windErosionRate))
                        sp.WindErosionRate = _windErosionRate;
                    
                    ImGui.SeparatorText("Avalanche");
                    if (ImGui.DragFloat("Density of snow", ref _avalancheSnowDensity))
                        sp.AvalancheSnowDensity = _avalancheSnowDensity;
                    if (ImGui.DragFloat("Viscosity of wet snow", ref _avalancheSnowViscosity))
                        sp.AvalancheSnowViscosity = _avalancheSnowViscosity;
                    if (ImGui.SliderAngle("Rest slope", ref _avalancheRestSlope, 0, 90))
                        sp.AvalancheRestSlope = tan(_avalancheRestSlope);
                    if (ImGui.DragFloat("Gravity", ref _avalancheGravity))
                        sp.AvalancheGravity = _avalancheGravity;
                    if (ImGui.DragFloat("Wet avalanche temperature", ref _avalancheTemp))
                        sp.AvalancheTemp = _avalancheTemp;
                    if (ImGui.DragFloat("Dry avalanche temperature", ref _avalancheTemp))
                        sp.AvalancheTemp = _avalancheTemp;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Precomputation"))
                {
                    ImGui.SeparatorText("Illumination");
                    ImGui.SliderAngle("Latitude", ref lp.DirectLatitude, -180, 180);
                    ImGui.NewLine();
                    ImGui.Text("Direct illumination");
                    if (ImGui.SliderFloat("Intensity##dir", ref _directIntensity, 0, 1))
                        lp.IntensityDirect = _directIntensity;
                    ImGui.SliderInt("Days between samples", ref lp.DirectDaysBetweenSamples, 1, 31);
                    ImGui.SliderInt("Hours between samples", ref lp.DirectHoursBetweenSamples, 1, 24);
                    ImGui.NewLine();
                    ImGui.Text("Indirect illumination");
                    if (ImGui.SliderFloat("Intentisty##ind", ref _indirectIntensity, 0, 1))
                        lp.IntensityIndirect = _indirectIntensity;
                    ImGui.DragInt("Angular samples", ref lp.IndirectAngularSamples, 1, 0);
                    ImGui.DragInt("Distance samples", ref lp.IndirectDistanceSamples, 1, 0);
                    if (ImGui.DragFloat("Max sampling distance", ref _indMaxDistance, 1, 0))
                        lp.IndirectMaxDistance = _indMaxDistance;
                    ImGui.NewLine();
                    ImGui.Text("Ambient illumination");
                    if (ImGui.SliderFloat("Intensity##amb", ref _ambientIntensity, 0, 1))
                        lp.IntensityAmbient = _ambientIntensity;
                    ImGui.NewLine();
                    ImGui.SeparatorText("Wind");
                    Help("Winds from north to south are at 0°, winds from east to west are at 90°");
                    if (ImGui.SliderAngle("Wind heading", ref _windHeading))
                        _control.WindParams.WindDirection = float2(-sin(_windHeading), -cos(_windHeading));
                    if (ImGui.SliderFloat("Venturi intensity", ref _venturiIntensity, 0, 0.1f))
                        wp.VenturiIntensity = _venturiIntensity;
                    if (ImGui.SliderFloat("Deflection intensity", ref _deflectionIntensity, 0, 1f))
                        wp.DeflectionIntensity = _deflectionIntensity;
                    if (ImGui.SliderFloat("Surface falloff", ref _surfaceFalloff, 0, 1f))
                        wp.SurfaceFalloff = _surfaceFalloff;
                    ImGui.DragInt("Smoothing size", ref wp.GaussianKernelSize, 1, 3);
                    ImGui.DragInt("Max iterations", ref wp.SurfaceMaxIterations, 1, 50);
                    if (ImGui.DragInt("Wind speed samples", ref wp.SurfaceSamples, 1, 1))
                        wp.SurfaceSpeedIncrement = _maxWindSpeed / wp.SurfaceSamples;
                    if (ImGui.SliderFloat("Max wind speed", ref _maxWindSpeed, 0, 1f))
                        wp.SurfaceSpeedIncrement = _maxWindSpeed / wp.SurfaceSamples;
                    
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }
        
        private void OnEnable()
        {
            UImGuiUtility.Layout += OnLayout;
            //UImGuiUtility.OnInitialize += OnInitialize;
            //UImGuiUtility.OnDeinitialize += OnDeinitialize;
            
            ref Wind.Parameters wp = ref _control.WindParams;
            ref Lighting.Parameters lp = ref _control.LightParams;
            ref Snow.Parameters sp = ref _control.Simulation.Parameters;
            
            _directIntensity = (float)lp.IntensityDirect;
            _ambientIntensity = (float)lp.IntensityAmbient;
            _indirectIntensity = (float)lp.IntensityIndirect;
            _indMaxDistance = (float)lp.IndirectMaxDistance;

            _windHeading = (float)atan2(-wp.WindDirection.x, -wp.WindDirection.y);
            _venturiIntensity = (float)wp.VenturiIntensity;
            _deflectionIntensity = (float)wp.DeflectionIntensity;
            _surfaceFalloff = (float)wp.SurfaceFalloff;
            _maxWindSpeed = (float)(wp.SurfaceSamples * wp.SurfaceSpeedIncrement);
            
            _snowfallStrength = (float)sp.SnowfallStrength;
            _snowfallMax = (float)sp.SnowfallMax;
            _snowfallPowderRatio = (float)sp.SnowfallPowderRatio;
            _snowfallUnstableRatio = (float)sp.SnowfallUnstableRatio;
            _criticalSlopeMin = (float)atan(sp.CriticalSlopeMin);
            _criticalSlopeTempFactor = (float)sp.CriticalSlopeTempFactor;
            _criticalSlopeMaxTemp = (float)sp.CriticalSlopeMaxTemp;
            
            _meltRate = (float)sp.MeltRate;
            _meltTemp = (float)sp.MeltTemp;
            _meltVolumeFactor = (float)sp.MeltVolumeFactor;
            
            _stabilityStableTemp = (float)sp.StabilityStableTemp;
            _stabilityUnstableTemp = (float)sp.StabilityUnstableTemp;
            _stabilityFreezeTemp = (float)sp.StabilityFreezeTemp;
            _stabilityHot = (float)sp.StabilityHot;
            _stabilityMedium = (float)sp.StabilityMedium;
            _stabilityFreeze = (float)sp.StabilityFreeze;
            _stabilityCompactionPressure = (float)sp.StabilityCompactionPressure;
            _stabilityMinSlope = (float)atan(sp.StabilityMinSlope);
            
            _diffusionRestSlope = (float)atan(sp.DiffusionRestSlope);
            _diffusionRate = (float)sp.DiffusionRate;
            
            _windPlates = (float)sp.WindPlates;
            _windErosionRate = (float)sp.WindErosionRate;
            
            _avalancheSnowDensity = (float)sp.AvalancheSnowDensity;
            _avalancheSnowViscosity = (float)sp.AvalancheSnowViscosity;
            _avalancheRestSlope = (float)atan(sp.AvalancheRestSlope);
            _avalancheGravity = (float)sp.AvalancheGravity;
            _avalancheTemp = (float)sp.AvalancheTemp;
        }

        private void OnDisable()
        {
            UImGuiUtility.Layout -= OnLayout;
            //UImGuiUtility.OnInitialize -= OnInitialize;
            //UImGuiUtility.OnDeinitialize -= OnDeinitialize;
        }

        private void Help(string desc)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(desc);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }
}