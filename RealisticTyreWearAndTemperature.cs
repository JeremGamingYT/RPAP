// RealisticTyreWearAndTemperature.cs
// v1.4 – grip fixe & chauffe réaliste

using GTA;
using GTA.Native;
using GTA.UI;
using RealHandlingLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.IO; // Added for file operations
using Newtonsoft.Json; // Added for JSON serialization

public class VehicleTyreTempData
{
    public float[] TyreWear { get; set; } = new float[4];
    public float[] TyrePressure { get; set; } = new float[4];
    public bool[] TyreBlownOut { get; set; } = new bool[4];
    public float EngineTemp { get; set; }
    public float CoolantLevel { get; set; }
}

public class RealisticTyreWearAndTemperature : Script
{
    private readonly string tyreDataSavePath;
    private int lastSaveTime = 0; 
    private const int SaveIntervalMs = 60000; // 60 seconds

    // —————————————————— réglages pneus
    private const float TyreLifetimeKm      = 350f;
    private const float WearGripLossFactor  = 0.6f;     // slick = –60 % grip
    private const float WearBrakeBonus      = 0.0002f;  // freinage violent
    private const float WearBurnoutBonus    = 0.0015f;  // burn-out

    // —————————————————— réglages température
    private const float AmbientT            = 25f;
    private const float NormalT             = 95f;
    private const float OverheatT           = 110f;
    private const float CriticalT           = 125f;
    private const float IdleHeatPerSec      = 0.05f;    // ralenti
    private const float HeatAccelPerSec     = 0.20f;    // plein gaz
    private const float HeatSpeedPerKmh     = 0.002f;   // charge aéro
    private const float CoolSpeedPerKmh     = 0.0012f;  // flux radiateur
    private const float CoolEngineOff       = 0.18f;    // moteur coupé
    private const float OverheatDmgPerSec   = 2f;
    private const float CriticalTimeToFire  = 6f;

    // —————————————————— état
    private readonly Dictionary<int,float[]> tyreWear   = new(); // Changed to float[] for individual tyre wear
    private readonly Dictionary<int,float[]> tyrePressure = new(); // Added for tyre pressure
    private readonly Dictionary<int,float> engineTemp = new();
    private readonly Dictionary<int,float> overTimer  = new();
    private readonly Dictionary<int,float> lastBrake  = new();
    private int lastCollisionCheckTime = 0;
    private readonly Dictionary<int, bool[]> tyreBlownOut = new();
    private readonly Dictionary<int, float> coolantLevel = new();
    private readonly Dictionary<int, float> previousEngineHealth = new();

    // Dictionary for surface wear multipliers
    // NOTE: The material hashes used here are EXAMPLES. Actual hash values need to be researched and verified from GTA V game data.
    private static readonly Dictionary<uint, float> surfaceWearMultipliers = new Dictionary<uint, float>
    {
        { 0x98D99308, 1.0f },    // Example for Asphalt/Concrete
        { 0x7A86343E, 1.1f },    // Example for Damaged Asphalt
        { 0x4E6A4754, 1.75f },   // Example for Dirt/Loose Soil (e.g. SAND_LOOSE_DRY)
        { 0x3B2481A6, 1.3f },    // Example for Grass (e.g. GRASS_LONG_DRY)
        { 0x4E8A2C1F, 1.5f },    // Example for Gravel (e.g. GRAVEL_SMALL_DRY)
        // Add more verified mappings as needed based on research of GTA V material hashes
    };

    private class Baseline
    {
        public float TractionMax;
        public float TractionMin;
        public float? LowSpeedLoss;
    }
    private readonly Dictionary<int,Baseline> baselines = new();
    private static readonly PropertyInfo? LowSpeedProp =
        typeof(HandlingData).GetProperty("LowSpeedTractionLossMult",
                                         BindingFlags.Public | BindingFlags.Instance);

    public RealisticTyreWearAndTemperature()
    {
        Interval = 0;
        Tick    += OnTick;
        Aborted += (_,__) => { tyreWear.Clear(); baselines.Clear(); SaveTyreAndTempData(); }; // Added SaveTyreAndTempData to Aborted
        Notification.PostTicker("Realistic Tyres & Temp ✔️", true);

        string scriptFolder;
        try
        {
            scriptFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            scriptFolder = "."; // Fallback
        }
        tyreDataSavePath = Path.Combine(scriptFolder, "RealisticTyreAndTempData.json");
        lastSaveTime = Game.GameTime;
        LoadTyreAndTempData(); // Load data on startup
    }

    private void OnTick(object? s, EventArgs e)
    {
        Ped ped = Game.Player.Character;
        if (!ped.IsInVehicle()) return;
        Vehicle v = ped.CurrentVehicle;
        if (!v.Exists()) return;

        int id = v.Handle;
        float dt  = Game.LastFrameTime;
        float kmh = v.Speed * 3.6f;
        float m   = v.Speed * dt;

        // ——— baseline (si handling changé par un autre script)
        var hdl = v.HandlingData;
        if (!baselines.ContainsKey(id) ||
            Math.Abs(hdl.TractionCurveMax - baselines[id].TractionMax) > 0.01f)
        {
            baselines[id] = new Baseline {
                TractionMax = hdl.TractionCurveMax,
                TractionMin = hdl.TractionCurveMin,
                LowSpeedLoss = LowSpeedProp is null ? null
                              : (float?)LowSpeedProp.GetValue(hdl)
            };
            // on repart de zéro pour éviter le cumul
            if (tyreWear.ContainsKey(id)) tyreWear[id] = new float[] { 0f, 0f, 0f, 0f };
            if (tyrePressure.ContainsKey(id)) tyrePressure[id] = new float[] { 1f, 1f, 1f, 1f };
            if (tyreBlownOut.ContainsKey(id)) tyreBlownOut[id] = new bool[4] { false, false, false, false };
            coolantLevel[id] = 1.0f; // Initialize coolant level
            previousEngineHealth[id] = v.EngineHealth; // Initialize previous engine health
        }
        var baseH = baselines[id];

        // Determine Drivetrain
        float driveBiasFront = hdl.DriveBiasFront;
        bool isFWD = driveBiasFront > 0.7f;
        bool isRWD = driveBiasFront < 0.3f;
        bool isAWD = driveBiasFront >= 0.3f && driveBiasFront <= 0.7f;

        // Tyre Pressure Loss from Impacts
        if (v.HasCollided)
        {
            float collisionMagnitude = v.Speed; 
            if (Game.GameTime - lastCollisionCheckTime > 500) 
            {
                 if (collisionMagnitude > 5.0f) 
                 {
                    Random random = new Random();
                    int affectedTyre1 = random.Next(0, 4);
                    float pressureLoss1 = (collisionMagnitude / 100f) * (float)random.NextDouble();
                    if(tyrePressure.ContainsKey(id)) // Ensure key exists before access
                         tyrePressure[id][affectedTyre1] = Math.Max(0.1f, tyrePressure[id][affectedTyre1] - pressureLoss1);
                    GTA.UI.Screen.ShowSubtitle($"Collision! Tyre {affectedTyre1} pressure reduced.", 2000);

                    if (random.NextDouble() < 0.3) 
                    {
                        int affectedTyre2 = random.Next(0, 4);
                        if (affectedTyre2 != affectedTyre1)
                        {
                           float pressureLoss2 = (collisionMagnitude / 150f) * (float)random.NextDouble();
                           if(tyrePressure.ContainsKey(id)) // Ensure key exists before access
                                tyrePressure[id][affectedTyre2] = Math.Max(0.1f, tyrePressure[id][affectedTyre2] - pressureLoss2);
                           GTA.UI.Screen.ShowSubtitle($"Collision! Tyre {affectedTyre2} pressure also reduced.", 2000);
                        }
                    }
                 }
                 lastCollisionCheckTime = Game.GameTime;
            }
        }

        // ——— usure pneus (inactive à l’arrêt pour éviter “glisse fantôme”)
        if (!tyreWear.ContainsKey(id)) tyreWear[id] = new float[] { 0f, 0f, 0f, 0f };
        if (!tyrePressure.ContainsKey(id)) tyrePressure[id] = new float[] { 1f, 1f, 1f, 1f }; // Initialize tyre pressure
        if (!tyreBlownOut.ContainsKey(id)) tyreBlownOut[id] = new bool[4] { false, false, false, false }; // Initialize blowout state
        if (!coolantLevel.ContainsKey(id)) coolantLevel[id] = 1.0f; // Initialize coolant level if not present
        if (!previousEngineHealth.ContainsKey(id)) previousEngineHealth[id] = v.EngineHealth; // Initialize previous engine health if not present

        // Temperature influence on wear
        float currentEngineTemp = engineTemp.ContainsKey(id) ? engineTemp[id] : AmbientT;
        float temperatureWearBonus = 0.0f;
        if (currentEngineTemp > NormalT)
        {
            if (currentEngineTemp > OverheatT)
            {
                temperatureWearBonus = (currentEngineTemp - NormalT) / 20000f; // Higher wear when overheating
            }
            else
            {
                temperatureWearBonus = (currentEngineTemp - NormalT) / 50000f; // Moderate wear increase when hot
            }
        }

        // Base wear from distance and temperature (calculated once per tick)
        float baseWearThisTick = (kmh > 1f ? m / (TyreLifetimeKm * 1000f) : 0f) + temperatureWearBonus;

        float brakeInput = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 72); // GTA.Control.VehicleBrake
        float accelInput = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 71); // GTA.Control.VehicleAccelerate
        float currentSteeringAngle = v.SteeringAngle; // For steering wear

        // Use deltaBrake as calculated before for consistency, or recalculate if needed
        // Assuming deltaBrake is already calculated based on lastBrake state
        float actualDeltaBrake = Math.Max(0f, brakeInput - (lastBrake.TryGetValue(id, out var prevBrake) ? prevBrake : 0f));
        lastBrake[id] = brakeInput;


        for (int i = 0; i < 4; i++) // Tyre indices: 0=FL, 1=FR, 2=RL, 3=RR
        {
            // Skip wear calculation and blowout check if already blown
            if (tyreBlownOut.ContainsKey(id) && tyreBlownOut[id][i])
            {
                // Ensure wear and pressure stay at blown values
                if (tyreWear.ContainsKey(id)) tyreWear[id][i] = 1.0f;
                if (tyrePressure.ContainsKey(id)) tyrePressure[id][i] = 0.0f;
                continue;
            }

            float tyreSpecificWearBonus = 0f;

            // Braking Wear (more on front)
            if (actualDeltaBrake > 0.01f)
            {
                if (i < 2) // Front tyres (0, 1)
                    tyreSpecificWearBonus += (actualDeltaBrake * WearBrakeBonus * dt) * 1.5f;
                else // Rear tyres (2, 3)
                    tyreSpecificWearBonus += (actualDeltaBrake * WearBrakeBonus * dt) * 0.5f;
            }

            // Acceleration/Burnout Wear
            if (accelInput > 0.5f)
            {
                float accelBonus = (accelInput - 0.5f) * WearBurnoutBonus * dt;
                if (isFWD && i < 2) tyreSpecificWearBonus += accelBonus;
                else if (isRWD && i >= 2) tyreSpecificWearBonus += accelBonus;
                else if (isAWD) tyreSpecificWearBonus += accelBonus / 2f; // Distribute AWD wear, can be refined with DriveBiasFront
            }
            
            // Steering Wear (front tyres)
            if (i < 2 && Math.Abs(currentSteeringAngle) > 15.0f && kmh > 10f)
            {
                tyreSpecificWearBonus += 0.00001f * dt * (Math.Abs(currentSteeringAngle) / 30f);
            }

            // Low Pressure Wear Bonus
            float lowPressureWearMultiplier = 1.0f;
            if (tyrePressure.ContainsKey(id) && tyrePressure[id][i] < 0.6f)
            {
                lowPressureWearMultiplier = 1.0f + (0.6f - tyrePressure[id][i]) * 0.5f; // Up to +25% wear at 0.1 pressure (0.6-0.1 = 0.5 * 0.5 = 0.25)
            }
            
            float actualWearStepForTyre = (baseWearThisTick + tyreSpecificWearBonus) * GetSurfaceModifier(v, i) * lowPressureWearMultiplier;
            if(tyreWear.ContainsKey(id)) // Ensure key exists before access
                 tyreWear[id][i] = Math.Min(1f, tyreWear[id][i] + actualWearStepForTyre);
        }

        // Blowout Checks & Effects
        for (int i = 0; i < 4; i++)
        {
            if (tyreBlownOut.ContainsKey(id) && tyreBlownOut[id][i]) continue; // Already blown

            bool isBlown = false;
            if (tyreWear.ContainsKey(id) && tyreWear[id][i] >= 0.99f) isBlown = true;
            if (tyrePressure.ContainsKey(id) && tyrePressure[id][i] <= 0.1f) isBlown = true;

            if (isBlown)
            {
                tyreBlownOut[id][i] = true;
                if (tyreWear.ContainsKey(id)) tyreWear[id][i] = 1.0f;
                if (tyrePressure.ContainsKey(id)) tyrePressure[id][i] = 0.0f;

                Function.Call(Hash.SET_TYRE_BURST, v.Handle, i, true);
                Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "Tyre_Burst", v.Handle, "GTAO_Damage_Soundset", false, 0);
                GTA.UI.Screen.ShowSubtitle($"Tyre {i} has blown out!", 3000);

                // Attempt Spark Effects - Experimental
                string[] wheelBoneNames = { "wheel_lf", "wheel_rf", "wheel_lr", "wheel_rr" };
                if (v.Bones.Contains(wheelBoneNames[i]))
                {
                    int boneIndex = v.Bones[wheelBoneNames[i]].Index;
                    Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");
                    // It's good practice to wait until the asset is loaded, but for a quick test:
                    // if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "core")) //This check should be in a loop if used properly
                    // {
                        // Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, "core"); // Deprecated in some contexts, direct call often works
                        Function.Call(Hash.SET_PARTICLE_FX_LOOPED_ON_ENTITY_BONE, 
                           "veh_tyre_rim_spark",  // Particle effect name - NEEDS VERIFICATION
                           v.Handle,             // Vehicle
                           0.0f, 0.0f, 0.0f,     // Offset from bone
                           0.0f, 0.0f, 0.0f,     // Rotation
                           boneIndex,            // Bone index
                           0.2f,                 // Scale (adjust as needed)
                           false, false, false); 
                    // }
                }
            }
        }

        float averageWear = 0f;
        if(tyreWear.ContainsKey(id)) // Ensure key exists
             averageWear = (tyreWear[id][0] + tyreWear[id][1] + tyreWear[id][2] + tyreWear[id][3]) / 4f;
        
        float wearGripFactor = 1f - averageWear * WearGripLossFactor;

        // Calculate Pressure Grip Modifier
        float totalPressureGripEffect = 0f;
        int nonBlownTyres = 0; // Count non-blown tyres for averaging grip
        for (int i = 0; i < 4; i++) // Changed loop variable to 'i' for consistency
        {
            if (tyreBlownOut.ContainsKey(id) && tyreBlownOut[id][i])
            {
                totalPressureGripEffect += 0.05f; // Blown tyre offers almost no grip
                                                  // Do not increment nonBlownTyres here, as its grip is fixed at 0.05
                                                  // We will average over 4 tyres anyway, but this indicates it's not contributing normally.
            }
            else
            {
                float pressure = tyrePressure.ContainsKey(id) ? tyrePressure[id][i] : 1.0f;
                if (pressure < 0.3f) totalPressureGripEffect += 0.3f;      // Very low pressure = significantly bad grip (e.g. 70% loss)
                else if (pressure < 0.6f) totalPressureGripEffect += 0.7f + (pressure - 0.3f) * 1f; // Grip from 0.7 to 1.0 for pressure 0.3 to 0.6
                else if (pressure > 1.3f) totalPressureGripEffect += 0.8f; // Overinflated = somewhat bad grip (e.g. 20% loss)
                else totalPressureGripEffect += 1.0f;                     // Optimal pressure range (around 1.0)
                nonBlownTyres++; // This tyre contributes normally to grip calculation
            }
        }
        // If all tyres are blown, averagePressureGripModifier will be 0.05.
        // If some are blown, their contribution is 0.05, others normal. The average is still over 4.
        float averagePressureGripModifier = (nonBlownTyres > 0 || (tyreBlownOut.ContainsKey(id) && Array.Exists(tyreBlownOut[id], blown => blown))) 
                                          ? totalPressureGripEffect / 4f 
                                          : 1.0f; // Default to 1.0f if no pressure/blowout data yet (e.g. first tick)
        
        hdl.TractionCurveMax = baseH.TractionMax * wearGripFactor * averagePressureGripModifier;
        hdl.TractionCurveMin = baseH.TractionMin * wearGripFactor * averagePressureGripModifier;
        if (LowSpeedProp is not null && baseH.LowSpeedLoss is float origLoss)
        {
            // Apply wear factor, and inverse of pressure modifier (bad pressure = more loss)
            // Ensure averagePressureGripModifier is not zero to avoid division by zero
            float pressureEffectOnLowSpeed = (averagePressureGripModifier > 0.01f) ? (1f / averagePressureGripModifier) : 100f; // Cap extreme effect
            LowSpeedProp.SetValue(hdl, origLoss * (1f + averageWear * 0.6f) * pressureEffectOnLowSpeed);
        }

        // ——— température moteur (modèle simplifié réaliste)

        // Coolant Loss on Damage
        float currentVehicleEngineHealth = v.EngineHealth;
        if (currentVehicleEngineHealth < previousEngineHealth.GetValueOrDefault(id, 1000f) - 10f)
        {
            float healthDrop = previousEngineHealth.GetValueOrDefault(id, currentVehicleEngineHealth) - currentVehicleEngineHealth;
            float coolantLossAmount = healthDrop / 500f; 
            if (coolantLevel.ContainsKey(id))
            {
                coolantLevel[id] = Math.Max(0f, coolantLevel[id] - coolantLossAmount);
                if (coolantLossAmount > 0.05f) GTA.UI.Screen.ShowSubtitle("Significant coolant leak detected!", 2000);
            }
        }
        // Slow leak if engine health is generally low
        if (currentVehicleEngineHealth < 500 && coolantLevel.ContainsKey(id) && coolantLevel[id] > 0)
        {
            coolantLevel[id] -= 0.0005f * dt;
        }


        // Weather Influence
        Weather currentWorldWeather = World.Weather;
        float weatherIdleHeatMultiplier = 1.0f;
        float weatherCoolingMultiplier = 1.0f;

        if (currentWorldWeather == Weather.Raining || currentWorldWeather == Weather.ThunderStorm)
        {
            weatherCoolingMultiplier = 1.3f; // Rain provides better cooling
        }
        else if (currentWorldWeather == Weather.ExtraSunny)
        {
            weatherIdleHeatMultiplier = 1.2f; // Extra sunny makes idle heating worse
            weatherCoolingMultiplier = 0.9f; // Extra sunny slightly reduces cooling efficiency
        }

        // Coolant Level Influence
        float currentCoolantLevel = coolantLevel.TryGetValue(id, out float cl) ? cl : 1.0f;
        float coolantEfficiencyFactor = Math.Max(0.1f, currentCoolantLevel); // Minimum 10% efficiency

        if (!engineTemp.ContainsKey(id)) engineTemp[id] = AmbientT + 5f;
        float T = engineTemp[id];

        if (v.IsEngineRunning)
        {
            float effectiveIdleHeat = IdleHeatPerSec * weatherIdleHeatMultiplier;
            float coolingFromSpeed = kmh * CoolSpeedPerKmh * coolantEfficiencyFactor * weatherCoolingMultiplier;
            // Note: accelInput was defined in tyre wear section, ensure it's still in scope or redefine if needed.
            // For this block, assuming 'accel' refers to the same accelInput.
            float accel = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 71); // Re-fetch if not in scope

            float heat =
                effectiveIdleHeat +
                accel * HeatAccelPerSec + // Assuming accel is the current acceleration input
                kmh * HeatSpeedPerKmh -
                coolingFromSpeed;
            T += heat * dt;
        }
        else
        {
            T -= CoolEngineOff * coolantEfficiencyFactor * weatherCoolingMultiplier * dt;
        }
        T = Math.Max(AmbientT, T);
        engineTemp[id] = T;

        if (T > OverheatT) // Overheat damage logic
            v.EngineHealth = Math.Max(0, v.EngineHealth - (OverheatDmgPerSec * (T - OverheatT) / 10f * dt));
        
        previousEngineHealth[id] = currentVehicleEngineHealth; // Update previous engine health for next tick

        if (T >= CriticalT)
        {
            overTimer[id] = overTimer.TryGetValue(id,out var t) ? t + dt : dt;
            if (overTimer[id] >= CriticalTimeToFire && !v.IsOnFire)
                Function.Call(Hash.START_ENTITY_FIRE, v);
        }
        else if (overTimer.ContainsKey(id)) overTimer[id] = 0f;

        // ——— HUD
        // Define Bar Parameters
        // float minimapRightX = 0.175f; // Example if bars are to the right of minimap
        float barAreaLeftEdge = 0.02f; // Distance from very left of screen
        float barWidth = 0.01f;
        float barMaxHeight = 0.04f;
        float barSpacingY = 0.008f; // Vertical spacing between bars
        float initialYPos = 0.65f; // Starting Y for the first bar (from top of screen)

        // Tyre Health Bar
        float[] currentTyreWear = tyreWear.TryGetValue(id, out var tw) ? tw : new float[]{0f,0f,0f,0f};
        float averageWear = (currentTyreWear[0] + currentTyreWear[1] + currentTyreWear[2] + currentTyreWear[3]) / 4f;
        float avgTyreHealth = (1.0f - averageWear) * 100f;
        Color tyreColor = avgTyreHealth <= 20 ? Color.Red : avgTyreHealth <= 50 ? Color.Orange : Color.LightGreen;
        DrawVerticalBar("PNEUS", avgTyreHealth, 100f, barAreaLeftEdge + barWidth / 2f, initialYPos, barWidth, barMaxHeight, tyreColor, Color.White, true);

        // Engine Temperature Bar
        float currentEngineTempVal = engineTemp.TryGetValue(id, out float temp) ? temp : AmbientT;
        float maxDisplayTemp = CriticalT + 5f; // Max value for the temp bar
        Color tempColor = currentEngineTempVal <= NormalT ? Color.LightBlue : currentEngineTempVal <= OverheatT ? Color.Orange : Color.Red;
        float engineTempY = initialYPos + barMaxHeight + barSpacingY;
        DrawVerticalBar("MOTEUR", currentEngineTempVal, maxDisplayTemp, barAreaLeftEdge + barWidth / 2f, engineTempY, barWidth, barMaxHeight, tempColor, Color.White, false);

        // Periodic Save Data
        if (Game.GameTime - lastSaveTime >= SaveIntervalMs)
        {
            SaveTyreAndTempData();
            lastSaveTime = Game.GameTime;
        }
    }

    private void LoadTyreAndTempData()
    {
        if (File.Exists(tyreDataSavePath))
        {
            try
            {
                string json = File.ReadAllText(tyreDataSavePath);
                var loadedData = JsonConvert.DeserializeObject<Dictionary<string, VehicleTyreTempData>>(json);

                if (loadedData != null)
                {
                    foreach (var entry in loadedData)
                    {
                        if (int.TryParse(entry.Key, out int vehicleId))
                        {
                            VehicleTyreTempData data = entry.Value;
                            tyreWear[vehicleId] = data.TyreWear ?? new float[4];
                            tyrePressure[vehicleId] = data.TyrePressure ?? new float[] { 1f, 1f, 1f, 1f };
                            tyreBlownOut[vehicleId] = data.TyreBlownOut ?? new bool[4];
                            engineTemp[vehicleId] = data.EngineTemp;
                            coolantLevel[vehicleId] = data.CoolantLevel;
                            // Optional: Initialize previousEngineHealth based on current health or a default.
                            // Vehicle currentVehicle = World.GetVehicle(vehicleId);
                            // if(currentVehicle != null && currentVehicle.Exists()) previousEngineHealth[vehicleId] = currentVehicle.EngineHealth;
                        }
                    }
                    Notification.PostTicker("RealisticTyreAndTemp: Data loaded.", true);
                }
            }
            catch (Exception ex)
            {
                Notification.PostTicker($"RealisticTyreAndTemp: Error loading data: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}", true);
            }
        }
    }

    private void SaveTyreAndTempData()
    {
        try
        {
            Dictionary<string, VehicleTyreTempData> dataToSave = new Dictionary<string, VehicleTyreTempData>();
            List<int> vehicleIds = new List<int>(engineTemp.Keys); 

            foreach (int vehicleId in vehicleIds)
            {
                // Check if vehicle still exists and is valid before saving.
                // Vehicle v = World.GetVehicle(vehicleId); // This might be too slow here.
                // For simplicity, we save all data we have in dictionaries. If a vehicle is gone, its data might persist until next load.
                // Consider adding a cleanup mechanism if save files grow too large with orphaned data.

                VehicleTyreTempData data = new VehicleTyreTempData();
                if (tyreWear.TryGetValue(vehicleId, out float[] tw)) data.TyreWear = tw; else continue; 
                if (tyrePressure.TryGetValue(vehicleId, out float[] tp)) data.TyrePressure = tp; else data.TyrePressure = new float[] {1f,1f,1f,1f};
                if (tyreBlownOut.TryGetValue(vehicleId, out bool[] tbo)) data.TyreBlownOut = tbo; else data.TyreBlownOut = new bool[4];
                if (engineTemp.TryGetValue(vehicleId, out float et)) data.EngineTemp = et; else continue; 
                if (coolantLevel.TryGetValue(vehicleId, out float cl)) data.CoolantLevel = cl; else data.CoolantLevel = 1f; 
                
                dataToSave[vehicleId.ToString()] = data;
            }

            string json = JsonConvert.SerializeObject(dataToSave, Formatting.Indented);
            File.WriteAllText(tyreDataSavePath, json);
            // Notification.PostTicker("RealisticTyreAndTemp: Data saved.", true); // Optional for debugging
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"RealisticTyreAndTemp: Error saving data: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}", true);
        }
    }

    // Method to get surface modifier based on material under the wheel
    private float GetSurfaceModifier(Vehicle vehicle, int tyreIndex)
    {
        // Common bone names for wheels: Front Left, Front Right, Rear Left, Rear Right
        string[] wheelBoneNames = { "wheel_lf", "wheel_rf", "wheel_lr", "wheel_rr" }; 
        if (tyreIndex < 0 || tyreIndex >= wheelBoneNames.Length) return 1.0f; // Safety check

        VehicleBone bone = vehicle.Bones[wheelBoneNames[tyreIndex]];
        
        // Fallback if specific wheel bone isn't found (e.g. trailers, different vehicle structures)
        if (bone == null || !bone.Exists) 
        {
            // Try common alternative bone names or a general chassis bone as a last resort
            string alternativeBoneName = "";
            if (tyreIndex == 0) alternativeBoneName = "wheel_f"; // Generic front for some models
            else if (tyreIndex == 1 && vehicle.WheelCount >=2) alternativeBoneName = vehicle.Bones[wheelBoneNames[0]] != null ? "wheel_f" : "wheel_r"; // if rf fails, try f or r
            else if (tyreIndex == 2 && vehicle.WheelCount >=3) alternativeBoneName = "wheel_lm"; // Left middle for some trucks
            else if (tyreIndex == 3 && vehicle.WheelCount >=4) alternativeBoneName = "wheel_rm"; // Right middle for some trucks
            
            if (!string.IsNullOrEmpty(alternativeBoneName)) bone = vehicle.Bones[alternativeBoneName];

            if (bone == null || !bone.Exists) bone = vehicle.Bones.Core; // Fallback to vehicle's core bone position
            if (bone == null || !bone.Exists) return 1.0f; // If no valid bone found, default to 1.0f
        }

        GTA.Math.Vector3 bonePos = bone.Position;
        // Raycast downwards from slightly above the bone's position to find the ground material
        GTA.Math.Vector3 rayStart = bonePos + new GTA.Math.Vector3(0, 0, 0.3f); // Start ray slightly above the bone
        GTA.Math.Vector3 rayEnd = bonePos - new GTA.Math.Vector3(0, 0, 1.0f);   // End ray below the bone

        RaycastResult rayResult = World.Raycast(rayStart, rayEnd, IntersectFlags.Map | IntersectFlags.Vehicles, vehicle); // Ignore the vehicle itself

        uint materialHash = 0;
        if (rayResult.DidHit)
        {
            materialHash = rayResult.MaterialHash;
        }
        // Uncomment for debugging material hashes:
        // if (materialHash != 0) GTA.UI.Screen.ShowSubtitle($"Tyre {tyreIndex} on material: {materialHash.ToString("X")}", 100);


        if (surfaceWearMultipliers.TryGetValue(materialHash, out float multiplier))
        {
            return multiplier;
        }
        return 1.0f; // Default multiplier if material hash not in dictionary or raycast failed
    }

    // ————————— affichage
    public static void DrawVerticalBar(string label, float currentValue, float maxValue, float xPosition, float yPosition, float barWidth, float barMaxHeight, Color barColor, Color labelColor, bool drawPercentage = false, float labelSize = 0.30f, int font = 0, bool outline = true)
    {
        currentValue = Math.Min(currentValue, maxValue); // Cap current value at max for drawing
        currentValue = Math.Max(currentValue, 0f);     // Ensure non-negative
        float barCurrentHeight = (currentValue / maxValue) * barMaxHeight;

        // Background Rect
        // Y position for DRAW_RECT is center, so adjust yPosition (which is top of bar area) to center of BG.
        float bgRectCenterY = yPosition + barMaxHeight / 2.0f;
        Function.Call(Hash.DRAW_RECT, xPosition, bgRectCenterY, barWidth, barMaxHeight, 30, 30, 30, 180); // Dark semi-transparent BG

        // Foreground Rect (Actual Bar)
        // Y position for DRAW_RECT is center, so adjust yPosition (top of bar area) for the current bar height.
        // The bar grows upwards from the bottom of the bar area.
        float fgRectCenterY = (yPosition + barMaxHeight) - barCurrentHeight / 2.0f; 
        Function.Call(Hash.DRAW_RECT, xPosition, fgRectCenterY, barWidth, barCurrentHeight, barColor.R, barColor.G, barColor.B, 220);

        // Label Text
        Function.Call(Hash.SET_TEXT_FONT, font);
        Function.Call(Hash.SET_TEXT_SCALE, labelSize, labelSize);
        Function.Call(Hash.SET_TEXT_COLOUR, labelColor.R, labelColor.G, labelColor.B, 255);
        if (outline) Function.Call(Hash.SET_TEXT_OUTLINE); else Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 0, 0, 0, 255);
        Function.Call(Hash.SET_TEXT_JUSTIFICATION, 0); // Left Justify
        Function.Call(Hash.SET_TEXT_WRAP, xPosition, xPosition + 0.2f); // Increased wrap width for longer labels
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        string displayText = label;
        if (drawPercentage) displayText += $": {(currentValue / maxValue) * 100f:F0}%"; else displayText += $": {currentValue:F0}/{maxValue:F0}";
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, displayText);
        // Position text to the right of the bar, using yPosition as the top alignment for text.
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, xPosition + barWidth / 1.5f + 0.002f, yPosition + barMaxHeight / 2f - (labelSize * 1.5f / 2f) ); // Adjusted text Y for better centering
    }

    // Old DrawText method, can be removed if DrawVerticalBar handles all text needs or if we ensure it's not used elsewhere.
    // For now, keeping it as DrawVerticalBar is specific.
    // Old DrawText method is still present as it was not explicitly part of this subtask to remove if unused by DrawVerticalBar.
    // DrawTyreHUD and DrawTempHUD have been effectively replaced by calls to DrawVerticalBar in OnTick.
    private static void DrawText(string txt,float x,float y,Color c)
    {
        Function.Call(Hash.SET_TEXT_FONT,4);
        Function.Call(Hash.SET_TEXT_SCALE,0.40f,0.40f);
        Function.Call(Hash.SET_TEXT_COLOUR,c.R,c.G,c.B,255);
        Function.Call(Hash.SET_TEXT_OUTLINE);
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT,"STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,txt);
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT,x,y);
    }
}