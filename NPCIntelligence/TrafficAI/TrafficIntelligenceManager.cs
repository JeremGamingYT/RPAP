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
        private const float CheckRadius = 80f;
        private const float SpeedThreshold = 0.8f; // vitesse minimale pour considérer le véhicule arrêté
        private const float HonkDelay = 2f;        // temps avant klaxon
        private const float BypassDelay = 4f;      // temps avant tentative de dépassement
        private const int MaxBypassAttempts = 3;   // nombre max de tentatives de dépassement
        private const float CourtesyDistance = 15f; // distance pour la courtoisie

        public TrafficIntelligenceManager()
        {
            Tick += OnTick;
            Interval = 800; // Exécuter environ 1.25 fois par seconde
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

                // Gestion de la courtoisie pour les voies libres
                HandleTrafficCourtesy(nearby);

                // nettoyage des entrées invalides
                var invalid = _tracked.Where(p => p.Value?.Vehicle == null || !p.Value.Vehicle.Exists() || p.Value.Vehicle.Driver == null).Select(p => p.Key).ToList();
                foreach (var key in invalid)
                    _tracked.Remove(key);
            }
            catch
            {
                // Log l'erreur silencieusement pour éviter le crash
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
                    info.BypassAttempts = 0;
                    return;
                }

                if (!IsBlocked(veh))
                {
                    info.BlockedTime = 0f;
                    info.Honked = false;
                    info.BypassAttempts = 0;
                    return;
                }

                info.BlockedTime += 0.8f; // Incrémente selon l'intervalle

                // Klaxon plus rapide et réaliste
                if (info.BlockedTime > HonkDelay && !info.Honked)
                {
                    try
                    {
                        // Klaxon plus court et réaliste
                        Function.Call(Hash.START_VEHICLE_HORN, veh, 800, 0, false);
                        info.Honked = true;
                    }
                    catch { /* Ignore si le klaxon échoue */ }
                }

                // Tentatives de contournement plus intelligentes
                if (info.BlockedTime > BypassDelay && info.BypassAttempts < MaxBypassAttempts)
                {
                    if (AttemptIntelligentBypass(driver, veh))
                    {
                        info.BypassAttempts++;
                        info.BlockedTime = 0f;
                        info.Honked = false;
                    }
                    else
                    {
                        // Si pas possible de contourner, recommence le cycle mais plus lentement
                        info.BlockedTime = BypassDelay - 1f;
                    }
                }
            }
            catch { /* Ignore les erreurs pour éviter le crash */ }
        }

        private bool IsBlocked(Vehicle veh)
        {
            try
            {
                if (veh == null || !veh.Exists()) return false;
                
                Vector3 start = veh.Position + veh.ForwardVector * 1.5f + Vector3.WorldUp;
                Vector3 end = start + veh.ForwardVector * 6f;
                var hit = World.Raycast(start, end, IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles | IntersectFlags.Peds, veh);
                return hit.DidHit && hit.HitEntity != null && hit.HitEntity.Handle != veh.Handle;
            }
            catch
            {
                return false;
            }
        }

        private bool AttemptIntelligentBypass(Ped driver, Vehicle veh)
        {
            try
            {
                if (driver == null || !driver.Exists() || veh == null || !veh.Exists()) return false;

                // Distances adaptées au type de véhicule
                float forwardOffset = GetForwardOffset(veh);
                float sideOffset = GetSideOffset(veh);

                Vector3 vehiclePos = veh.Position;
                Vector3 forward = vehiclePos + veh.ForwardVector * forwardOffset;
                
                // Test des directions possibles (droite prioritaire, puis gauche)
                Vector3 rightTarget = forward + veh.RightVector * sideOffset;
                Vector3 leftTarget = forward - veh.RightVector * sideOffset;
                
                Vector3? bestTarget = null;
                
                // Priorité à droite (comme en conduite réelle)
                if (IsRouteClearAndSafe(vehiclePos, rightTarget, veh))
                {
                    bestTarget = rightTarget;
                }
                else if (IsRouteClearAndSafe(vehiclePos, leftTarget, veh))
                {
                    bestTarget = leftTarget;
                }

                if (bestTarget.HasValue)
                {
                    // Signale le changement de voie aux autres véhicules
                    SignalLaneChange(veh, bestTarget.Value);
                    
                    // Assigne la tâche de contournement
                    Function.Call(Hash.CLEAR_PED_TASKS, driver);
                    Wait(50);
                    
                    // Utilise une vitesse adaptée à la situation
                    float speed = Math.Max(5f, Math.Min(15f, veh.Speed + 3f));
                    
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, driver, veh,
                        bestTarget.Value.X, bestTarget.Value.Y, bestTarget.Value.Z, 
                        speed, 0, Function.Call<int>(Hash.GET_ENTITY_MODEL, veh), 
                        787004, 3f, 0);
                        
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private float GetForwardOffset(Vehicle veh)
        {
            // Adapte la distance selon la taille du véhicule
            float baseOffset = 8f;
            if (veh.Model.IsBus) return baseOffset + 2f;
            if (IsLargeVehicle(veh)) return baseOffset + 1.5f;
            if (veh.Model.IsBike) return baseOffset - 2f;
            return baseOffset;
        }

        private float GetSideOffset(Vehicle veh)
        {
            // Adapte la distance latérale selon le véhicule
            float baseOffset = 3.5f;
            if (veh.Model.IsBus) return baseOffset + 1f;
            if (IsLargeVehicle(veh)) return baseOffset + 0.5f;
            if (veh.Model.IsBike) return baseOffset - 1f;
            return baseOffset;
        }

        private bool IsLargeVehicle(Vehicle veh)
        {
            try
            {
                if (veh == null || !veh.Exists()) return false;
                
                // Vérifie par classe de véhicule
                var vehicleClass = Function.Call<int>(Hash.GET_VEHICLE_CLASS, veh);
                
                // Classes pour véhicules lourds : Commercial (11), Industrial (12), Service (17), Emergency (18)
                return vehicleClass == 11 || vehicleClass == 12 || vehicleClass == 17 || vehicleClass == 18;
            }
            catch
            {
                return false;
            }
        }

        private bool IsRouteClearAndSafe(Vector3 start, Vector3 end, Vehicle ignore)
        {
            try
            {
                // Test de collision basique
                var test = ShapeTest.StartTestCapsule(
                    start + Vector3.WorldUp,
                    end + Vector3.WorldUp,
                    2f,
                    IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles,
                    ignore);
                var result = test.GetResult();
                
                if (result.result.DidHit) return false;

                // Vérification de sécurité : pas trop près d'autres véhicules
                var nearbyVehicles = World.GetNearbyVehicles(end, 8f);
                if (nearbyVehicles != null)
                {
                    foreach (var other in nearbyVehicles)
                    {
                        if (other == ignore || other == null || !other.Exists()) continue;
                        if (other.Position.DistanceTo(end) < 4f) return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SignalLaneChange(Vehicle changingVehicle, Vector3 targetPosition)
        {
            try
            {
                // Active les clignotants
                if (targetPosition.X > changingVehicle.Position.X)
                {
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, changingVehicle, 1, true); // Droite
                }
                else
                {
                    Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, changingVehicle, 0, true); // Gauche
                }
            }
            catch { }
        }

        private void HandleTrafficCourtesy(Vehicle[] nearbyVehicles)
        {
            try
            {
                if (nearbyVehicles == null) return;

                foreach (var veh in nearbyVehicles)
                {
                    if (veh?.Driver == null || !veh.Exists()) continue;
                    
                    // Vérifie s'il y a des véhicules bloqués à proximité qui ont besoin de passer
                    var blockedNearby = FindBlockedVehiclesNeedingSpace(veh, nearbyVehicles);
                    
                    if (blockedNearby.Any() && veh.Speed > 0.5f)
                    {
                        // Ce véhicule peut laisser passer les autres
                        OfferCourtesy(veh, blockedNearby);
                    }
                }
            }
            catch { }
        }

        private List<Vehicle> FindBlockedVehiclesNeedingSpace(Vehicle checkingVehicle, Vehicle[] allVehicles)
        {
            var needingSpace = new List<Vehicle>();
            
            try
            {
                foreach (var other in allVehicles)
                {
                    if (other == checkingVehicle || other?.Driver == null) continue;
                    
                    float distance = other.Position.DistanceTo(checkingVehicle.Position);
                    if (distance > CourtesyDistance) continue;
                    
                    // Vérifie si l'autre véhicule est bloqué et a essayé de changer de voie
                    if (_tracked.TryGetValue(other.Handle, out var info))
                    {
                        if (info.BlockedTime > 2f && info.BypassAttempts > 0)
                        {
                            needingSpace.Add(other);
                        }
                    }
                }
            }
            catch { }
            
            return needingSpace;
        }

        private void OfferCourtesy(Vehicle courteous, List<Vehicle> needingSpace)
        {
            try
            {
                if (!needingSpace.Any()) return;
                
                var driver = courteous.Driver;
                if (driver == null || !driver.Exists()) return;
                
                // Ralentit temporairement pour laisser passer
                Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, driver, courteous, 6, 2000); // Ralentir
                
                // Petite pause pour que l'autre puisse passer
                Wait(100);
            }
            catch { }
        }
    }
}
