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

public class RealisticTyreWearAndTemperature : Script
{
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
    private readonly Dictionary<int,float> tyreWear   = new();
    private readonly Dictionary<int,float> engineTemp = new();
    private readonly Dictionary<int,float> overTimer  = new();
    private readonly Dictionary<int,float> lastBrake  = new();

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
        Aborted += (_,__) => { tyreWear.Clear(); baselines.Clear(); };
        Notification.PostTicker("Realistic Tyres & Temp ✔️", true);
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
            if (tyreWear.ContainsKey(id)) tyreWear[id] = 0f;
        }
        var baseH = baselines[id];

        // ——— usure pneus (inactive à l’arrêt pour éviter “glisse fantôme”)
        if (!tyreWear.ContainsKey(id)) tyreWear[id] = 0f;
        float wear = tyreWear[id];
        float wearStep = (kmh > 2f) ? m / (TyreLifetimeKm * 1000f) : 0f;

        float brake = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 72);
        float accel = Function.Call<float>(Hash.GET_CONTROL_NORMAL, 0, 71);
        float deltaBrake = Math.Max(0f, brake - (lastBrake.TryGetValue(id, out var pb) ? pb : 0f));
        lastBrake[id] = brake;

        wearStep += deltaBrake * WearBrakeBonus * dt;
        if (accel > 0.8f && kmh < 15f) wearStep += WearBurnoutBonus * dt;

        wear = Math.Min(1f, wear + wearStep);
        tyreWear[id] = wear;

        float gripFactor = 1f - wear * WearGripLossFactor;
        hdl.TractionCurveMax = baseH.TractionMax * gripFactor;
        hdl.TractionCurveMin = baseH.TractionMin * gripFactor;
        if (LowSpeedProp is not null && baseH.LowSpeedLoss is float origLoss)
            LowSpeedProp.SetValue(hdl, origLoss * (1f + wear * 0.6f));

        // ——— température moteur (modèle simplifié réaliste)
        if (!engineTemp.ContainsKey(id)) engineTemp[id] = AmbientT + 5f;
        float T = engineTemp[id];

        if (v.IsEngineRunning)
        {
            float heat =
                IdleHeatPerSec +
                accel * HeatAccelPerSec +
                kmh * HeatSpeedPerKmh -
                kmh * CoolSpeedPerKmh;              // refroidissement dynamique
            T += heat * dt;
        }
        else
        {
            T -= CoolEngineOff * dt;
        }
        T = Math.Max(AmbientT, T);
        engineTemp[id] = T;

        if (T > OverheatT)
            v.EngineHealth -= OverheatDmgPerSec * (T - OverheatT) / 10f * dt;

        if (T >= CriticalT)
        {
            overTimer[id] = overTimer.TryGetValue(id,out var t) ? t + dt : dt;
            if (overTimer[id] >= CriticalTimeToFire && !v.IsOnFire)
                Function.Call(Hash.START_ENTITY_FIRE, v);
        }
        else if (overTimer.ContainsKey(id)) overTimer[id] = 0f;

        // ——— HUD
        DrawTyreHUD(wear);
        DrawTempHUD(T);
    }

    // ————————— affichage
    private static void DrawTyreHUD(float wear)
    {
        float pct = (1f - wear) * 100f;
        Color c   = pct switch { <=20=>Color.Red, <=50=>Color.Orange, _=>Color.White };
        DrawText($"Pneus : {pct:0} %", 0.18f, 0.795f, c);
    }
    private static void DrawTempHUD(float T)
    {
        Color c = T switch { <=NormalT=>Color.White, <=OverheatT=>Color.Orange, _=>Color.Red };
        DrawText($"Moteur : {T:0} °C", 0.18f, 0.885f, c);
    }
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
