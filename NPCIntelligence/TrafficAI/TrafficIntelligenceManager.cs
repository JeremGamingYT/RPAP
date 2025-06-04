using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Gestionnaire de comportements pour les PNJ bloqués dans la circulation.
    /// Les véhicules klaxonnent lorsqu'ils sont coincés et tentent de contourner
    /// l'obstacle après quelques secondes.
    /// </summary>
    public class TrafficIntelligenceManager : Script
    {
        private readonly Dictionary<int, BlockedVehicleInfo> _tracked = new();

        // Rayon de détection autour du joueur
        private const float CheckRadius = 60f;
        private const float SpeedThreshold = 0.5f; // vitesse minimale pour considérer le véhicule arrêté
        private const float HonkDelay = 3f;        // temps avant klaxon
        private const float BypassDelay = 6f;      // tentative de dépassement plus rapide

        public TrafficIntelligenceManager()
        {
            Tick += OnTick;
            Interval = 500; // Exécuter seulement 2 fois par seconde au lieu de 60+
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;
                if (player == null || !player.Exists()) return;

                var nearby = World.GetNearbyVehicles(player.Position, CheckRadius);
                if (nearby == null) return;

                foreach (var veh in nearby)
                {
                    if (veh == null || !veh.Exists() || veh.Driver == null) continue;
                    if (veh.Driver == player || !veh.Driver.IsAlive) continue;

                    if (!_tracked.TryGetValue(veh.Handle, out var info))
                    {
                        info = new BlockedVehicleInfo(veh.Driver, veh);
                        _tracked[veh.Handle] = info;
                    }

                    UpdateVehicle(info);
                }

                // nettoyage des entrées invalides
                var invalid = _tracked.Where(p => p.Value?.Vehicle == null || !p.Value.Vehicle.Exists() || p.Value.Vehicle.Driver == null).Select(p => p.Key).ToList();
                foreach (var key in invalid)
                    _tracked.Remove(key);
            }
            catch
            {
                // Log l'erreur silencieusement pour éviter le crash
                // Vous pouvez ajouter un système de log ici si nécessaire
            }
        }

        private void UpdateVehicle(BlockedVehicleInfo info)
        {
            try
            {
                Vehicle veh = info.Vehicle;
                Ped driver = info.Driver;

                if (veh == null || !veh.Exists() || driver == null || !driver.Exists()) return;

                if (veh.Speed > SpeedThreshold)
                {
                    info.BlockedTime = 0f;
                    info.Honked = false;
                    return;
                }

                if (!IsBlocked(veh))
                {
                    info.BlockedTime = 0f;
                    info.Honked = false;
                    return;
                }

                info.BlockedTime += 0.5f; // Incrémente de 0.5 car on exécute 2 fois par seconde

                if (info.BlockedTime > HonkDelay && !info.Honked)
                {
                    try
                    {
                        Function.Call(Hash.START_VEHICLE_HORN, veh, 1000, 0, false);
                        info.Honked = true;
                    }
                    catch { /* Ignore si le klaxon échoue */ }
                }

                if (info.BlockedTime > BypassDelay)
                {
                    YieldNearbyTraffic(veh);
                    AttemptBypass(driver, veh);
                    info.BlockedTime = 0f;
                    info.Honked = false;
                }
            }
            catch { /* Ignore les erreurs pour éviter le crash */ }
        }

        private bool IsBlocked(Vehicle veh)
        {
            try
            {
                if (veh == null || !veh.Exists()) return false;
                
                Vector3 start = veh.Position + veh.ForwardVector * 2f + Vector3.WorldUp;
                Vector3 end = start + veh.ForwardVector * 5f;
                var hit = World.Raycast(start, end, IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles | IntersectFlags.Peds, veh);
                return hit.DidHit && hit.HitEntity != null && hit.HitEntity.Handle != veh.Handle;
            }
            catch
            {
                return false;
            }
        }

        private void AttemptBypass(Ped driver, Vehicle veh)
        {
            try
            {
                if (driver == null || !driver.Exists() || veh == null || !veh.Exists()) return;

                float forwardOffset = 8f;
                float sideOffset = 4f;

                Vector3 forward = veh.Position + veh.ForwardVector * forwardOffset;
                Vector3 rightTarget = forward + veh.RightVector * sideOffset;
                Vector3 leftTarget = forward - veh.RightVector * sideOffset;

                Vector3? target = null;
                if (IsRouteClear(veh.Position, rightTarget, veh))
                    target = rightTarget;
                else if (IsRouteClear(veh.Position, leftTarget, veh))
                    target = leftTarget;

                if (target.HasValue)
                {
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, driver, veh,
                        target.Value.X, target.Value.Y, target.Value.Z, 12f, 786603, 7f);
                }
            }
            catch { /* Ignore les erreurs */ }
        }

        private bool IsRouteClear(Vector3 start, Vector3 end, Vehicle ignore)
        {
            try
            {
                var test = ShapeTest.StartTestCapsule(
                    start + Vector3.WorldUp,
                    end + Vector3.WorldUp,
                    2f,
                    IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles | IntersectFlags.Peds,
                    ignore);
                var result = test.GetResult();
                return !result.result.DidHit;
            }
            catch
            {
                return false;
            }
        }

        private void YieldNearbyTraffic(Vehicle blockedVeh)
        {
            try
            {
                if (blockedVeh == null || !blockedVeh.Exists()) return;

                var nearby = World.GetNearbyVehicles(blockedVeh.Position, 10f);
                if (nearby == null) return;

                foreach (var other in nearby)
                {
                    if (other == blockedVeh || other?.Driver == null || !other.Driver.IsAlive)
                        continue;

                    if (other.Speed > 0.1f && other.Position.DistanceTo(blockedVeh.Position) < 6f)
                    {
                        try
                        {
                            Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, other.Driver, other, 27, 1500);
                        }
                        catch { /* Ignore si la tâche échoue */ }
                    }
                }
            }
            catch { /* Ignore les erreurs */ }
        }
    }
}
