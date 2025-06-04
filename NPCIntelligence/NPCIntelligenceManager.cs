using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;

namespace REALIS.NPCIntelligence
{
    /// <summary>
    /// Simple intelligence layer for ambient NPCs.
    /// NPCs react to threats near the player and may call the police.
    /// </summary>
    public class NPCIntelligenceManager : Script
    {
        private readonly Dictionary<int, NPCStatusInfo> _statuses = new();

        private const float CheckRadius = 40f;
        private const float ThreatRadius = 12f;

        public NPCIntelligenceManager()
        {
            Tick += OnTick;
            Interval = 0;
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsDead)
                return;

            bool playerShooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player);

            foreach (Ped ped in World.GetNearbyPeds(player, CheckRadius))
            {
                if (ped == null || !ped.Exists() || ped.IsDead || ped == player)
                    continue;

                if (!_statuses.TryGetValue(ped.Handle, out var info))
                {
                    info = new NPCStatusInfo(ped);
                    _statuses[ped.Handle] = info;
                }

                UpdatePed(ped, player, info, playerShooting);
                UpdatePed(ped, player, info);
            }

            CleanupStatuses();
        }

        private void UpdatePed(Ped ped, Ped player, NPCStatusInfo info, bool playerShooting)
        {
            bool beingAimedAt = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, ped);
            bool closeThreat = player.Position.DistanceTo(ped.Position) < ThreatRadius;

            if ((beingAimedAt && closeThreat) || (playerShooting && closeThreat) || ped.HasBeenDamagedBy(player))
        private void UpdatePed(Ped ped, Ped player, NPCStatusInfo info)
        {
            bool beingAimedAt = Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, Game.Player, ped);
            bool playerShooting = player.IsShooting;
            bool playerShooting = player.IsShooting || player.IsFiringWeapon;
            bool closeThreat = player.Position.DistanceTo(ped.Position) < ThreatRadius;

            if ((beingAimedAt && closeThreat) || ped.HasBeenDamagedBy(player))
            {
                if (!info.Reacted)
                {
                    ped.Task.ReactAndFlee(player);
                    info.Reacted = true;
                    info.LastThreatTime = Game.GameTime;
                }
            }
            else if (info.Reacted && Game.GameTime - info.LastThreatTime > 5000)
            {
                info.Reacted = false;
            }

            if (info.Reacted && !info.CalledPolice && Game.GameTime - info.LastThreatTime > 2000)
            {
                CallPolice(ped);
                info.CalledPolice = true;
            }
        }

        private void CallPolice(Ped caller)
        {
            GTA.Wanted wanted = Game.Player.Wanted;
            if (wanted.WantedLevel < 2)
            {
                wanted.SetWantedLevel(2, false);
                wanted.ApplyWantedLevelChangeNow(false);
            }
            if (Game.Player.WantedLevel < 2)
                Game.Player.WantedLevel = 2;
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Cell_Call_To", "Phone_SoundSet", false);
        }

        private void CleanupStatuses()
        {
            var invalid = new List<int>();
            foreach (var pair in _statuses)
            {
                if (!pair.Value.Ped.Exists())
                    invalid.Add(pair.Key);
            }
            foreach (var key in invalid)
                _statuses.Remove(key);
        }
    }

    internal class NPCStatusInfo
    {
        public Ped Ped { get; }
        public bool Reacted { get; set; }
        public bool CalledPolice { get; set; }
        public int LastThreatTime { get; set; }

        public NPCStatusInfo(Ped ped)
        {
            Ped = ped;
            Reacted = false;
            CalledPolice = false;
            LastThreatTime = 0;
        }
    }
}
