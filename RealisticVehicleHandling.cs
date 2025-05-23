using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO; // Required for file operations
using System.Text; // Required for StringBuilder

public class RealisticVehicleHandling : Script
{
    private Dictionary<string, Dictionary<string, object>> vehicleDataStore;
    private Vehicle? lastVehicle;

    // Configuration and Logging
    private static readonly string settingsDirectory = "scripts";
    private static readonly string settingsFile = Path.Combine(settingsDirectory, "RealisticVehicleHandling.ini");
    private static readonly string logFile = Path.Combine(settingsDirectory, "RealisticVehicleHandling.log");
    private static bool scriptEnabled = true;

    public RealisticVehicleHandling()
    {
        EnsureScriptsDirectoryExists();
        LoadSettings();

        vehicleDataStore = new Dictionary<string, Dictionary<string, object>>();
        LoadVehicleData(); // Load vehicle data after settings

        Tick += OnTick;

        if (scriptEnabled)
        {
            Notification.PostTicker("RealisticVehicleHandling script loaded and enabled.", true);
            LogMessage("RealisticVehicleHandling script initialized and enabled.");
        }
        else
        {
            Notification.PostTicker("RealisticVehicleHandling script loaded but DISABLED in settings.", true);
            LogMessage("RealisticVehicleHandling script initialized but DISABLED in settings.");
        }
    }

    private static void EnsureScriptsDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }
        }
        catch (Exception ex)
        {
            // Use GTA.UI.Screen.ShowSubtitle for this specific startup error as LogMessage might also fail if dir creation failed
            GTA.UI.Screen.ShowSubtitle($"Error creating scripts directory: {settingsDirectory}. Logging may fail. Details: {ex.Message}", 8000);
        }
    }

    public static void LogMessage(string message)
    {
        try
        {
            EnsureScriptsDirectoryExists(); // Ensure directory exists before logging
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, logEntry);
        }
        catch (Exception ex)
        {
            GTA.UI.Screen.ShowSubtitle($"Failed to write to log file: {logFile}. Error: {ex.Message}", 8000);
        }
    }

    private static void LoadSettings()
    {
        EnsureScriptsDirectoryExists();
        if (File.Exists(settingsFile))
        {
            try
            {
                string[] lines = File.ReadAllLines(settingsFile);
                foreach (string line in lines)
                {
                    if (line.StartsWith("Enabled=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (bool.TryParse(line.Substring("Enabled=".Length).Trim(), out bool enabledValue))
                        {
                            scriptEnabled = enabledValue;
                            // LogMessage($"Loaded setting: Enabled = {scriptEnabled}"); // Avoid logging before log file is confirmed ready
                        }
                        else
                        {
                            LogMessage($"Error parsing 'Enabled' value: '{line.Substring("Enabled=".Length)}'. Using default.");
                            scriptEnabled = true; // Default to true on parsing error
                        }
                        return; // Found the setting
                    }
                }
            }
            catch (IOException ex)
            {
                LogMessage($"Error reading settings file '{settingsFile}': {ex.ToString()}. Using default settings.");
                SaveDefaultSettings(); // Attempt to save defaults if read failed
                return;
            }
        }
        else
        {
            LogMessage($"Settings file '{settingsFile}' not found. Creating default settings.");
            SaveDefaultSettings();
        }
    }

    private static void SaveDefaultSettings()
    {
        EnsureScriptsDirectoryExists();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[Settings]");
        sb.AppendLine("Enabled=true");
        try
        {
            File.WriteAllText(settingsFile, sb.ToString());
            scriptEnabled = true; // Ensure scriptEnabled reflects the saved default
            LogMessage($"Saved default settings to '{settingsFile}'.");
        }
        catch (IOException ex)
        {
            LogMessage($"Error saving default settings to '{settingsFile}': {ex.ToString()}");
            GTA.UI.Screen.ShowSubtitle($"CRITICAL: Failed to save default settings to '{settingsFile}'. Check permissions.", 10000);
        }
    }

    void LoadVehicleData()
    {
        if (!scriptEnabled) return;

        // Example data for BANSHEE
        var bansheeData = new Dictionary<string, object>
        {
            { "Mass", 1530f },
            { "SteeringLock", 35.0f },      // Valeur réaliste
            { "TractionBiasFront", 0.50f },
            { "vecCentreOfMassOffset", new Vector3(0f, 0f, -0.15f) } // recule pas le CG pour le moment
        };
        vehicleDataStore.Add("BANSHEE", bansheeData);
        LogMessage("Vehicle handling data loaded for BANSHEE.");
        LogMessage("Updated BANSHEE handling data: Mass set to 1530.0f, TractionBiasFront to 0.35f, SteeringLock to 25.0f.");
    }

    void OnTick(object sender, EventArgs e)
    {
        if (!scriptEnabled) return;

        Ped playerPed = Game.Player.Character;
        if (playerPed.IsInVehicle())
        {
            Vehicle currentVehicle = playerPed.CurrentVehicle;
            if (currentVehicle != null && currentVehicle.Exists() && currentVehicle != lastVehicle)
            {
                LogMessage($"Player entered new vehicle: {currentVehicle.DisplayName} (Model: {currentVehicle.DisplayName}). Applying handling.");
                GTA.UI.Screen.ShowSubtitle($"Entered {currentVehicle.DisplayName}. Applying custom handling...", 3000);
                ApplyHandling(currentVehicle);
                LogMessage($"SteeringLock (rad) in memory = {currentVehicle.HandlingData.SteeringLock}");
                lastVehicle = currentVehicle;
            }
        }
        else
        {
            if (lastVehicle != null)
            {
                LogMessage("Player exited vehicle.");
                lastVehicle = null;
            }
        }
    }

    void ApplyHandling(Vehicle vehicle)
    {
        if (!scriptEnabled || vehicle == null || !vehicle.Exists())
        {
            LogMessage("ApplyHandling called but script is disabled or vehicle is invalid. Aborting.");
            return;
        }

        string modelName = vehicle.DisplayName.ToUpper();
        if (vehicleDataStore.ContainsKey(modelName))
        {
            var handlingData = vehicleDataStore[modelName];
            LogMessage($"Applying handling data to {modelName}. Vehicle Handle: {vehicle.Handle}.");

            foreach (var propertyEntry in handlingData)
            {
                string propertyName = propertyEntry.Key;
                object value = propertyEntry.Value;

                try
                {
                    // Use HandlingData property instead of raw native calls
                    switch (propertyName)
                    {
                        case "Mass":
                            vehicle.HandlingData.Mass = Convert.ToSingle(value);
                            break;
                        case "InitialDriveGears":
                            // Use a different approach for gears since there's no direct native
                            // Modify top speed as a proxy for gears
                            vehicle.EnginePowerMultiplier = Convert.ToSingle(value);
                            break;
                        case "TractionBiasFront":
                            vehicle.HandlingData.TractionBiasFront = Convert.ToSingle(value);
                            break;
                        case "InitialDriveForce":
                            vehicle.HandlingData.InitialDriveForce = Convert.ToSingle(value);
                            break;
                        case "DriveInertia":
                            vehicle.HandlingData.DriveInertia = Convert.ToSingle(value);
                            break;
                        case "BrakeForce":
                            vehicle.HandlingData.BrakeForce = Convert.ToSingle(value);
                            break;
                        case "SteeringLock":
                            //   On lit un nombre EN DEGRÉS dans le dictionnaire
                            float deg = Convert.ToSingle(value);
                            vehicle.HandlingData.SteeringLock = deg * (float)(Math.PI / 180f); // → radians
                            break;                       //       ^ conversion indispensable
                        case "SuspensionForce":
                            vehicle.HandlingData.SuspensionForce = Convert.ToSingle(value);
                            break;
                        case "SuspensionCompressionDamping":
                            // Skip this property as it's not directly accessible
                            LogMessage($"Property '{propertyName}' not directly accessible in this version of SHVDN. Skipping.");
                            break;
                        case "SuspensionReboundDamping":
                            // Skip this property as it's not directly accessible
                            LogMessage($"Property '{propertyName}' not directly accessible in this version of SHVDN. Skipping.");
                            break;
                        case "SuspensionUpperLimit":
                            vehicle.HandlingData.SuspensionUpperLimit = Convert.ToSingle(value);
                            break;
                        case "SuspensionLowerLimit":
                            vehicle.HandlingData.SuspensionLowerLimit = Convert.ToSingle(value);
                            break;
                        case "SuspensionBiasFront":
                            vehicle.HandlingData.SuspensionBiasFront = Convert.ToSingle(value);
                            break;
                        case "AntiRollBarForce":
                            vehicle.HandlingData.AntiRollBarForce = Convert.ToSingle(value);
                            break;
                        case "RollCentreHeightFront":
                            // Skip this property as it's not directly accessible
                            LogMessage($"Property '{propertyName}' not directly accessible in this version of SHVDN. Skipping.");
                            break;
                        case "RollCentreHeightRear":
                            // Skip this property as it's not directly accessible
                            LogMessage($"Property '{propertyName}' not directly accessible in this version of SHVDN. Skipping.");
                            break;
                        case "PetrolTankVolume":
                            vehicle.HandlingData.PetrolTankVolume = Convert.ToSingle(value);
                            break;
                        case "OilVolume":
                            vehicle.HandlingData.OilVolume = Convert.ToSingle(value);
                            break;
                        case "vecCentreOfMassOffset":
                            if (value is Vector3 com)
                            {
                                // Set center of mass offset using individual components
                                vehicle.HandlingData.CenterOfMassOffset = com;
                            }
                            break;
                        default:
                            LogMessage($"Property '{propertyName}' not mapped to a specific handling property or value type is incorrect. Value: {value}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error setting {propertyName} to {value} for vehicle {modelName} (Handle: {vehicle.Handle}). Exception: {ex.ToString()}");
                    GTA.UI.Screen.ShowSubtitle($"Error applying handling for {modelName}. Check log.", 5000);
                }
            }
            GTA.UI.Screen.ShowSubtitle($"Realistic handling applied to {modelName}.", 3000);
            LogMessage($"All handling data successfully processed for {modelName}.");
        }
        else
        {
            GTA.UI.Screen.ShowSubtitle($"No custom handling data found for {modelName}.", 3000);
            LogMessage($"No handling data found for {modelName}.");
        }
    }

    // SetHandlingProperty is kept for potential future use with custom objects,
    // but its direct use for Vehicle.HandlingData is limited due to read-only properties.
    // Logging within this method should also use LogMessage.
    void SetHandlingProperty(object handlingObject, string propertyName, object value)
    {
        if (!scriptEnabled) return;
        try
        {
            string[] parts = propertyName.Split('.');
            PropertyInfo? propertyInfo = null;
            object currentObject = handlingObject;

            for (int i = 0; i < parts.Length; i++)
            {
                propertyInfo = currentObject.GetType().GetProperty(parts[i]);
                if (propertyInfo == null)
                {
                    LogMessage($"Reflection: Property {parts[i]} not found on {currentObject.GetType().Name}");
                    return;
                }

                if (i < parts.Length - 1)
                {
                    currentObject = propertyInfo.GetValue(currentObject, null) ?? throw new NullReferenceException($"Property {parts[i]} returned null");
                }
            }

            if (propertyInfo!.CanWrite)
            {
                Type propertyType = propertyInfo.PropertyType;
                object convertedValue = Convert.ChangeType(value, propertyType);
                propertyInfo.SetValue(currentObject, convertedValue, null);
                LogMessage($"Reflection: Set {propertyName} to {value}");
            }
            else
            {
                LogMessage($"Reflection: Property {propertyName} is read-only. Cannot set via reflection.");
                // Removed reference to VehicleHandling class since we're using native calls instead
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Reflection Error setting {propertyName}: {ex.ToString()}");
        }
    }
}