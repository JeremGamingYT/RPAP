using GTA;
using GTA.Math;
using GTA.Native;          // ← ajoute / garde cette ligne
using GTA.UI;
using System;
using System.IO;
using System.Text;
using RealHandlingLib;        // ← référence à la nouvelle lib
using System.Collections.Generic;   // ← ajoute ça

public class RealisticVehicleHandling : Script
{
    private Vehicle? lastVehicle;

    // --- Config & log ---
    private const string Dir = "scripts";
    private static readonly string IniPath  = Path.Combine(Dir, "RealisticVehicleHandling.ini");
    private static readonly string LogPath  = Path.Combine(Dir, "RealisticVehicleHandling.log");

    private readonly Dictionary<int, float> lastFrameSpeeds = new();
    
    private static bool enabled = true;

    public RealisticVehicleHandling()
    {
        Directory.CreateDirectory(Dir);
        LoadSettings();

        Tick += OnTick;

        Notification.PostTicker(
            enabled ? "Realistic Vehicle Handling ✔️"
                    : "RVH chargé mais désactivé ❌",
            true);
    }

    // ------------------------------------------------------------------
    private void OnTick(object sender, EventArgs e)
    {
        if (!enabled) return;

        Ped p = Game.Player.Character;
        if (!p.IsInVehicle()) { lastVehicle = null; return; }

        Vehicle v = p.CurrentVehicle;
        if (!v.Exists()) return;

        string model = v.DisplayName.ToUpper();

        // Appliquer les valeurs fixes au premier passage
        if (v != lastVehicle)
        {
            ApplyStaticHandling(v, model);
            lastVehicle = v;
        }

        // ───────── Ajuster la déformation dynamiquement ───────────────
        if (RealisticVehicleData.Specs.TryGetValue(model, out var spec))
        {
            float kmh = v.Speed * 3.6f;
            var h     = v.HandlingData;

            // Facteur vitesse (quadratique) plafonné
            float speedFactor = kmh < 25f
                                ? 0.05f
                                : (float)Math.Min(Math.Pow(kmh / 60f, 2.0), 12.0);

            // — Impact quand la voiture est immobile mais reçoit un choc —
            bool justCollided = Function.Call<bool>(Hash.HAS_ENTITY_COLLIDED_WITH_ANYTHING, v.Handle);
            if (justCollided && kmh < 10f)
            {
                // On estime l'énergie du choc à partir de la vitesse de l'autre engin
                // => on prend sa vitesse la plus élevée dans les 2 dernières frames :
                float otherSpeed = Function.Call<float>(Hash.GET_ENTITY_SPEED, v.Handle); // vitesse propre (≈0)
                if (lastFrameSpeeds.TryGetValue(v.Handle, out float prev))
                    otherSpeed = Math.Max(otherSpeed, prev);

                lastFrameSpeeds[v.Handle] = kmh;   // maj pour la frame suivante

                float extra = (float)Math.Min(Math.Pow(otherSpeed / 30f, 2.0), 8.0f); // ≤ ×8
                speedFactor = Math.Max(speedFactor, extra);
            }

            h.DeformationDamageMultiplier = spec.DeformationMult * speedFactor;
            h.CollisionDamageMultiplier   = spec.CollisionMult   * speedFactor;
        }
    }

    // ------------------------------------------------------------------
    private void ApplyStaticHandling(Vehicle v, string model)
    {
        if (!RealisticVehicleData.Specs.TryGetValue(model, out var spec))
        {
            Screen.ShowSubtitle($"Pas de data réaliste pour {model}", 2500);
            return;
        }

        var h = v.HandlingData;

        h.Mass               = spec.Mass;
        h.SteeringLock       = spec.SteeringDeg * (float)(Math.PI / 180f);
        h.TractionBiasFront  = spec.TractionBiasFront;
        h.CenterOfMassOffset = spec.CenterOfMass;
        h.InitialDriveForce  = spec.InitialDriveForce;
        h.BrakeForce         = spec.BrakeForce;

        h.DeformationDamageMultiplier = spec.DeformationMult;
        h.CollisionDamageMultiplier   = spec.CollisionMult;

        Screen.ShowSubtitle($"Handling réaliste appliqué : {model}", 2000);
        Log($"Applied {model}");
    }

    // ------------------------------------------------------------------
    private static void LoadSettings()
    {
        if (!File.Exists(IniPath))
            File.WriteAllText(IniPath, "[Settings]\nEnabled=true");

        foreach (var line in File.ReadAllLines(IniPath))
        {
            if (line.StartsWith("Enabled=", StringComparison.OrdinalIgnoreCase))
            {
                string txt = line.Substring("Enabled=".Length);
                bool.TryParse(txt, out enabled);
            }
        }
    }

    private static void Log(string msg)
    {
        File.AppendAllText(LogPath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
    }
}