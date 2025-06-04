// RealisticTyreWearAndTemperature v2.1-HF – HUD révisé + fix SHVDN3
// © 2025 – Jerem + Atlas

using GTA;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

/// <summary>
/// Système réaliste d'usure et de température des pneus
/// </summary>
[ScriptAttributes(NoDefaultInstance = true)] // Géré par ScriptCoordinator
public sealed class RealisticTyreWearAndTemperature : Script
{
    /* ─────────── PARAMS & STATE ─────────── */
    private const float TyreLifetimeKm     = 200f;   // distance avant usure totale
    private const float WearBurnoutBonus   = 0.008f; // usure extra lors des burnouts
    private const float BlowoutThreshold   = 0.96f;
    private const float RandomBlowoutChance= 0.003f;

    private const float AmbientT           = 22f;
    private const float NormalT            = 95f;
    private const float OverheatT          = 110f;
    private const float CriticalT          = 125f;

    private readonly Dictionary<int,float[]> tyres   = new();
    private readonly Dictionary<int,float>   engineT = new();
    private readonly Dictionary<int,float>   overHot = new();
    private readonly Dictionary<int,float>   lastBrake = new();
    private readonly Random rng = new();

    private class Baseline
    {
        public float TractionMax, TractionMin;
        public float? LowSpeedLoss;
    }
    private readonly Dictionary<int,Baseline> baseH = new();
    private static readonly PropertyInfo? LowSpeedProp =
        typeof(HandlingData).GetProperty("LowSpeedTractionLossMult",
                                         BindingFlags.Public|BindingFlags.Instance);

    /* ─────────── CONSTRUCTOR ─────────── */
    public RealisticTyreWearAndTemperature()
    {
        Interval = 0;
        Tick    += OnTick;
        Aborted += (_, __) => { tyres.Clear(); baseH.Clear(); };
        Notification.PostTicker("Tyres & Temp 2.1-HF ✔️", true);
    }

    /* ─────────── MAIN LOOP ─────────── */
    private void OnTick(object sender, EventArgs e)
    {
        Ped p = Game.Player.Character;
        if (!p.IsInVehicle()) return;
        Vehicle v = p.CurrentVehicle;
        if (!v.Exists()) return;

        int id   = v.Handle;
        float dt = Game.LastFrameTime;
        float kmh= v.Speed * 3.6f;
        float m  = v.Speed * dt;

        /* ----- Baseline handling ----- */
        var h = v.HandlingData;
        if (!baseH.ContainsKey(id))
            baseH[id] = new Baseline {
                TractionMax  = h.TractionCurveMax,
                TractionMin  = h.TractionCurveMin,
                LowSpeedLoss = LowSpeedProp is null ? null
                              : (float?)LowSpeedProp.GetValue(h)
            };

        /* ----- Tyre wear ----- */
        if (!tyres.ContainsKey(id)) tyres[id] = new float[4];
        float[] wear = tyres[id];

        float brake = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 72);
        float accel = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 71);
        float deltaBrake = Math.Max(0f, brake - (lastBrake.TryGetValue(id, out var pb) ? pb : 0f));
        lastBrake[id] = brake;

        float wearStep = (kmh > 3f ? m / (TyreLifetimeKm * 1000f) : 0f)
                         + deltaBrake * 0.0004f * dt;
        if (accel > 0.8f && kmh < 25f) wearStep += WearBurnoutBonus * dt;

        int[] wheels = { 0, 1, 4, 5 };
        for (int i = 0; i < 4; i++)
        {
            bool burst = Function.Call<bool>(Hash.IS_VEHICLE_TYRE_BURST, v, wheels[i], false);
            if (burst) { wear[i] = 1f; continue; }

            wear[i] = Math.Min(1f, wear[i] + wearStep);

            if (wear[i] >= BlowoutThreshold &&
                rng.NextDouble() < RandomBlowoutChance * dt)
                Function.Call(Hash.SET_VEHICLE_TYRE_BURST, v, wheels[i], true, 1000f);
        }

        /* Grip loss */
        float avgWear = (wear[0] + wear[1] + wear[2] + wear[3]) * 0.25f;
        h.TractionCurveMax = baseH[id].TractionMax * (1f - avgWear * 0.75f);
        h.TractionCurveMin = baseH[id].TractionMin * (1f - avgWear * 0.75f);
        if (LowSpeedProp is not null && baseH[id].LowSpeedLoss is float ls)
            LowSpeedProp.SetValue(h, ls * (1f + avgWear * 0.7f));

        /* ----- Engine Temp ----- */
        if (!engineT.ContainsKey(id)) engineT[id] = AmbientT + 5f;
        float T = engineT[id];

        if (v.IsEngineRunning)
        {
            float heat = 0.06f + accel * 0.26f + kmh * 0.0024f - kmh * 0.0015f;
            T += heat * dt;
        }
        else
            T -= 0.22f * dt;

        T = Math.Max(AmbientT, T);
        engineT[id] = T;

        if (T > OverheatT)
            v.EngineHealth -= 3f * (T - OverheatT) / 10f * dt;

        if (T >= CriticalT)
        {
            overHot[id] = overHot.TryGetValue(id, out var t) ? t + dt : dt;
            if (overHot[id] > 5f && !v.IsOnFire)
                Function.Call(Hash.START_ENTITY_FIRE, v);
        }
        else if (overHot.ContainsKey(id)) overHot[id] = 0f;

        /* ----- HUD ----- */
        DrawTyreSquares(wear);
        DrawTempText(T);
    }

    /* ─────────── HUD HELPERS ─────────── */
    private static readonly float MiniX = 0.142f; // gauche mini-map
    private static readonly float BaseY = 0.90f;
    private static readonly float Sq    = 0.020f;
    private static readonly float Gap   = 0.004f;

    private static void DrawTyreSquares(float[] w)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = MiniX + i * (Sq + Gap);
            Color c;
            bool burst = w[i] >= 1f;
            if      (burst)      c = Color.Red;
            else if (w[i] >= .8) c = Color.Orange;
            else if (w[i] >= .5) c = Color.Yellow;
            else                 c = Color.Lime;

            Function.Call(Hash.DRAW_RECT, x + Sq * .5f, BaseY, Sq, Sq,
                          c.R, c.G, c.B, 220);
            Function.Call(Hash.DRAW_RECT, x + Sq * .5f, BaseY, Sq * .9f, Sq * .9f,
                          0, 0, 0, 40); // léger contour ombré
        }
    }

    private static readonly TextElement TempTxt =
        new("", new PointF(0, 0), 0.35f, Color.White,
            GTA.UI.Font.Pricedown, Alignment.Left);

    private static void DrawTempText(float T)
    {
        Color c = T switch
        {
            <= NormalT   => Color.Cyan,
            <= OverheatT => Color.Yellow,
            _            => Color.Red
        };
        TempTxt.Caption = $"ENG {T:0}°C";
        TempTxt.Color   = c;

        float x = 0.79f * Screen.Width;
        float y = 0.885f * Screen.Height;
        TempTxt.Position = new PointF(x, y);
        TempTxt.Draw();
    }
}
