using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.IO;
using System.Text;
using RealHandlingLib;
using System.Collections.Generic;

public class RealisticVehicleHandling : Script
{
    private Vehicle? lastVehicle;
    private readonly Dictionary<int, float> lastFrameSpeeds = new();

    private const string Dir = "scripts";
    private static readonly string IniPath = Path.Combine(Dir, "RealisticVehicleHandling.ini");
    private static readonly string LogPath = Path.Combine(Dir, "RealisticVehicleHandling.log");

    private static bool enabled = true;

    public RealisticVehicleHandling()
    {
        Directory.CreateDirectory(Dir);
        LoadSettings();

        Tick += OnTick;

        Notification.PostTicker(
            enabled ? "Realistic Vehicle Handling ✔️" : "RVH chargé mais désactivé ❌",
            true
        );
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (!enabled) return;

        Ped p = Game.Player.Character;
        if (!p.IsInVehicle())
        {
            lastVehicle = null;
            return;
        }

        Vehicle v = p.CurrentVehicle;
        if (!v.Exists()) return;

        string model = v.DisplayName.ToUpperInvariant();

        if (v != lastVehicle)
        {
            ApplyStaticHandling(v, model);
            lastVehicle = v;
        }

        if (!RealisticVehicleData.Specs.TryGetValue(model, out var spec)) return;

        float kmh = v.Speed * 3.6f;

        float prev = lastFrameSpeeds.TryGetValue(v.Handle, out var sp) ? sp : kmh;
        float deltaKmh = Math.Abs(prev - kmh);
        lastFrameSpeeds[v.Handle] = kmh;

        float speedFactor = 0.05f + (float)Math.Pow(kmh / 40f, 2.2); // ~E = ½ m v²
        speedFactor = Math.Max(0.05f, Math.Min(speedFactor, 20f));

        bool justCollided = Function.Call<bool>(Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, v.Handle);

        if (justCollided && deltaKmh > 5f)
        {
            float extra = 1f + (float)Math.Pow(deltaKmh / 40f, 2.0);
            speedFactor *= extra;
            speedFactor = Math.Min(speedFactor, 25f);
        }

        var h = v.HandlingData;
        h.DeformationDamageMultiplier = spec.DeformationMult * speedFactor;
        h.CollisionDamageMultiplier = spec.CollisionMult * speedFactor;
    }

    private void ApplyStaticHandling(Vehicle v, string model)
    {
        if (!RealisticVehicleData.Specs.TryGetValue(model, out var spec))
        {
            Screen.ShowSubtitle($"Pas de data réaliste pour {model}", 2500);
            return;
        }

        var h = v.HandlingData;

        h.Mass = spec.Mass;
        h.SteeringLock = spec.SteeringDeg * (float)(Math.PI / 180f);
        h.TractionBiasFront = spec.TractionBiasFront;
        h.CenterOfMassOffset = spec.CenterOfMass;
        h.InitialDriveForce = spec.InitialDriveForce;
        h.BrakeForce = spec.BrakeForce;

        h.DeformationDamageMultiplier = spec.DeformationMult;
        h.CollisionDamageMultiplier = spec.CollisionMult;

        Screen.ShowSubtitle($"Handling réaliste appliqué : {model}", 2000);
        Log($"Applied {model}");
    }

    private static void LoadSettings()
    {
        if (!File.Exists(IniPath))
            File.WriteAllText(IniPath, "[Settings]\nEnabled=true");

        foreach (var raw in File.ReadAllLines(IniPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("Enabled=", StringComparison.OrdinalIgnoreCase))
            {
                string value = line.Substring("Enabled=".Length).Trim();
                bool.TryParse(value, out enabled);
            }
        }
    }

    private static void Log(string msg)
    {
        File.AppendAllText(
            LogPath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}"
        );
    }
}