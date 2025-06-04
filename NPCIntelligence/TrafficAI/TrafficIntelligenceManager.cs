using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Gestionnaire avancé de comportements pour les PNJ bloqués dans la circulation.
    /// Système intelligent avec détection multi-directionnelle, limitation des ressources,
    /// et algorithmes de contournement adaptatifs.
    /// </summary>
    public class TrafficIntelligenceManager : Script
    {
        private readonly Dictionary<int, BlockedVehicleInfo> _tracked = new();
        private readonly HashSet<int> _processingVehicles = new(); // Prévient les doubles traitements
        private readonly Dictionary<int, DateTime> _lastActionTime = new(); // Cooldown par véhicule

        // Gestion du nettoyage des entrées trop anciennes
        private const float TrackingTimeout = 30f; // secondes
        
        // Configuration avancée
        private const float CheckRadius = 40f;
        private const float SpeedThreshold = 0.8f; 
        private const float HonkDelay = 3f;       
        private const float BypassDelay = 6f;     
        private const int MaxBypassAttempts = 2;  
        private const int MaxSimultaneousProcessing = 8; // Limite pour éviter les surcharges
        private const float MinCooldownSeconds = 5f; // Cooldown minimum entre actions
        private const float PlayerSafeZone = 8f; // Zone de sécurité autour du joueur
        
        // Compteurs pour debug et optimisation
        private int _processedThisTick = 0;
        private DateTime _lastCleanup = DateTime.Now;

        public TrafficIntelligenceManager()
        {
            Tick += OnTick;
            Interval = 1500; // Un peu plus rapide pour plus de réactivité
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                _processedThisTick = 0;
                
                Ped player = Game.Player.Character;
                if (player?.CurrentVehicle == null || !player.Exists()) return;

                Vehicle playerVehicle = player.CurrentVehicle;
                if (playerVehicle.Speed < 0.5f) return; // Le joueur ne bouge pas

                var nearby = World.GetNearbyVehicles(player.Position, CheckRadius);
                if (nearby == null || nearby.Length == 0) return;

                // Traite seulement les véhicules les plus pertinents
                var relevantVehicles = nearby
                    .Where(IsVehicleRelevant)
                    .OrderBy(v => v.Position.DistanceTo(player.Position))
                    .Take(MaxSimultaneousProcessing)
                    .ToList();

                foreach (var veh in relevantVehicles)
                {
                    if (_processedThisTick >= MaxSimultaneousProcessing) break;
                    
                    ProcessVehicle(veh, playerVehicle);
                    _processedThisTick++;
                }

                // Nettoyage périodique plus efficace
                if ((DateTime.Now - _lastCleanup).TotalSeconds > 10)
                {
                    CleanupInvalidEntries();
                    CleanupStaleEntries();
                    _lastCleanup = DateTime.Now;
                }
            }
            catch (Exception)
            {
                // Log l'erreur mais continue à fonctionner
                // Dans un mod de production, on loggerait dans un fichier
            }
        }

        private bool IsVehicleRelevant(Vehicle veh)
        {
            if (veh == null || !veh.Exists() || veh.Driver == null || !veh.Driver.IsAlive) 
                return false;

            // Ignore le véhicule du joueur
            if (veh.Driver == Game.Player.Character) 
                return false;

            // Ignore les véhicules trop proches (zone de sécurité)
            float distance = veh.Position.DistanceTo(Game.Player.Character.Position);
            if (distance < PlayerSafeZone || distance > CheckRadius) 
                return false;

            // Ignore les véhicules en mouvement rapide
            if (veh.Speed > SpeedThreshold * 3) 
                return false;

            // Ignore les véhicules déjà en traitement
            if (_processingVehicles.Contains(veh.Handle)) 
                return false;

            return true;
        }

        private void ProcessVehicle(Vehicle veh, Vehicle playerVehicle)
        {
            try
            {
                _processingVehicles.Add(veh.Handle);

                if (!_tracked.TryGetValue(veh.Handle, out var info))
                {
                    info = new BlockedVehicleInfo(veh.Driver, veh);
                    _tracked[veh.Handle] = info;
                }

                info.LastSeen = DateTime.Now;

                UpdateVehicleIntelligently(info, playerVehicle);
            }
            catch
            {
                // Supprime de la liste en cas d'erreur
                _processingVehicles.Remove(veh.Handle);
                _tracked.Remove(veh.Handle);
            }
            finally
            {
                _processingVehicles.Remove(veh.Handle);
            }
        }

        private void UpdateVehicleIntelligently(BlockedVehicleInfo info, Vehicle playerVehicle)
        {
            Vehicle veh = info.Vehicle;
            Ped driver = info.Driver;

            if (veh == null || !veh.Exists() || driver == null || !driver.Exists()) return;

            // Respect du cooldown
            if (_lastActionTime.TryGetValue(veh.Handle, out var lastAction))
            {
                if ((DateTime.Now - lastAction).TotalSeconds < MinCooldownSeconds)
                    return;
            }

            // Reset si le véhicule bouge
            if (veh.Speed > SpeedThreshold)
            {
                ResetVehicleState(info);
                return;
            }

            // Détection avancée de blocage
            var blockageInfo = AnalyzeBlockage(veh, playerVehicle);
            if (!blockageInfo.IsBlocked)
            {
                ResetVehicleState(info);
                return;
            }

            info.BlockedTime += 1.5f;

            // Klaxon intelligent basé sur la distance et la situation
            if (ShouldHonk(info, blockageInfo))
            {
                PerformIntelligentHonk(veh, blockageInfo);
                info.Honked = true;
                _lastActionTime[veh.Handle] = DateTime.Now;
            }

            // Contournement adaptatif
            if (ShouldAttemptBypass(info, blockageInfo))
            {
                if (PerformIntelligentBypass(driver, veh, blockageInfo))
                {
                    info.BypassAttempts++;
                    info.BlockedTime = 0f;
                    _lastActionTime[veh.Handle] = DateTime.Now;
                }
            }
        }

        private BlockageAnalysis AnalyzeBlockage(Vehicle veh, Vehicle playerVehicle)
        {
            var analysis = new BlockageAnalysis();
            
            // Vérifie si c'est spécifiquement le joueur qui bloque
            float distanceToPlayer = veh.Position.DistanceTo(playerVehicle.Position);
            Vector3 directionToPlayer = (playerVehicle.Position - veh.Position).Normalized;
            float angleToPlayer = Vector3.Dot(veh.ForwardVector, directionToPlayer);

            analysis.IsPlayerBlocking = distanceToPlayer < 15f && angleToPlayer > 0.3f;
            analysis.DistanceToObstacle = distanceToPlayer;

            // Raycast multi-directionnel pour détecter les obstacles
            analysis.IsBlocked = IsPathBlocked(veh, veh.ForwardVector, 12f);

            // Compte les véhicules lents ou à l'arrêt devant pour détecter un embouteillage
            analysis.IsInTrafficJam = CountFrontVehicles(veh) > 2;
            
            if (analysis.IsBlocked)
            {
                // Analyse des directions possibles
                analysis.CanGoLeft = !IsPathBlocked(veh, -veh.RightVector, 8f);
                analysis.CanGoRight = !IsPathBlocked(veh, veh.RightVector, 8f);
                analysis.CanReverse = !IsPathBlocked(veh, -veh.ForwardVector, 6f);
                
                // Préfère la direction la plus sûre
                analysis.PreferredDirection = DetermineOptimalDirection(veh, analysis);
            }

            return analysis;
        }

        private bool IsPathBlocked(Vehicle veh, Vector3 direction, float distance)
        {
            try
            {
                Vector3 start = veh.Position + Vector3.WorldUp * 1.5f;
                Vector3 end = start + direction * distance;
                
                var hit = World.Raycast(start, end, 
                    IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles | IntersectFlags.Peds, 
                    veh);
                
                return hit.DidHit;
            }
            catch
            {
                return true; // Assume bloqué en cas d'erreur
            }
        }

        private int CountFrontVehicles(Vehicle veh)
        {
            try
            {
                var nearby = World.GetNearbyVehicles(veh.Position, 12f);
                if (nearby == null) return 0;

                int count = 0;
                foreach (var other in nearby)
                {
                    if (other == veh || other.Driver == null || !other.Driver.IsAlive)
                        continue;

                    Vector3 dir = (other.Position - veh.Position).Normalized;
                    if (Vector3.Dot(veh.ForwardVector, dir) > 0.5f && other.Speed < 2f)
                        count++;
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private BypassDirection DetermineOptimalDirection(Vehicle veh, BlockageAnalysis analysis)
        {
            // Logique avancée pour choisir la meilleure direction
            if (analysis.IsPlayerBlocking)
            {
                // Si c'est le joueur qui bloque, préfère la gauche (plus naturel)
                if (analysis.CanGoLeft) return BypassDirection.Left;
                if (analysis.CanGoRight) return BypassDirection.Right;
            }
            else
            {
                // Pour les autres obstacles, préfère la droite
                if (analysis.CanGoRight) return BypassDirection.Right;
                if (analysis.CanGoLeft) return BypassDirection.Left;
            }

            if (analysis.IsInTrafficJam && analysis.CanReverse)
                return BypassDirection.Reverse;

            if (analysis.CanReverse)
                return BypassDirection.Reverse;

            return BypassDirection.None;
        }

        private bool ShouldHonk(BlockedVehicleInfo info, BlockageAnalysis analysis)
        {
            return info.BlockedTime > HonkDelay && 
                   !info.Honked && 
                   analysis.IsPlayerBlocking &&
                   analysis.DistanceToObstacle < 12f;
        }

        private void PerformIntelligentHonk(Vehicle veh, BlockageAnalysis analysis)
        {
            try
            {
                // Durée du klaxon basée sur la distance et l'urgence
                int duration = analysis.IsPlayerBlocking ? 800 : 400;
                Function.Call(Hash.START_VEHICLE_HORN, veh, duration, 0, false);
            }
            catch { /* Ignore */ }
        }

        private bool ShouldAttemptBypass(BlockedVehicleInfo info, BlockageAnalysis analysis)
        {
            return info.BlockedTime > BypassDelay && 
                   info.BypassAttempts < MaxBypassAttempts &&
                   analysis.PreferredDirection != BypassDirection.None;
        }

        private bool PerformIntelligentBypass(Ped driver, Vehicle veh, BlockageAnalysis analysis)
        {
            try
            {
                Vector3 targetPosition = CalculateBypassTarget(veh, analysis);

                if (targetPosition == Vector3.Zero) return false;

                // Vérifie qu'il n'y a pas d'obstacle majeur vers la cible
                if (IsPathBlocked(veh, (targetPosition - veh.Position).Normalized,
                                  targetPosition.DistanceTo(veh.Position)))
                    return false;

                // Utilise une approche plus douce avec plusieurs flags
                var drivingFlags = VehicleDrivingFlags.StopForVehicles |
                                 VehicleDrivingFlags.SwerveAroundAllVehicles |
                                 VehicleDrivingFlags.SteerAroundObjects |
                                 VehicleDrivingFlags.SteerAroundPeds;

                // Vitesse adaptée à la manœuvre
                float speed = analysis.PreferredDirection == BypassDirection.Reverse ? 3f : 6f;

                driver.Task.DriveTo(veh, targetPosition, 3f, drivingFlags, speed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Vector3 CalculateBypassTarget(Vehicle veh, BlockageAnalysis analysis)
        {
            Vector3 baseTarget = Vector3.Zero;
            float lateralDistance = analysis.IsInTrafficJam ? 7f : 5f;
            float forwardDistance = analysis.IsInTrafficJam ? 15f : 12f;

            switch (analysis.PreferredDirection)
            {
                case BypassDirection.Left:
                    baseTarget = veh.Position + (-veh.RightVector * lateralDistance) + (veh.ForwardVector * forwardDistance);
                    break;
                case BypassDirection.Right:
                    baseTarget = veh.Position + (veh.RightVector * lateralDistance) + (veh.ForwardVector * forwardDistance);
                    break;
                case BypassDirection.Reverse:
                    baseTarget = veh.Position + (-veh.ForwardVector * 8f);
                    break;
                default:
                    return Vector3.Zero;
            }

            // Ajuste la hauteur pour qu'elle soit sur la route (utilise la hauteur du véhicule actuel)
            return new Vector3(baseTarget.X, baseTarget.Y, veh.Position.Z);
        }

        private void ResetVehicleState(BlockedVehicleInfo info)
        {
            info.BlockedTime = 0f;
            info.Honked = false;
            // Ne reset pas BypassAttempts immédiatement pour éviter les boucles

            // Retire l'entrée de suivi si le véhicule roule de nouveau
            Vehicle veh = info.Vehicle;
            if (veh != null && veh.Exists() && veh.Speed > SpeedThreshold)
            {
                _tracked.Remove(veh.Handle);
                _lastActionTime.Remove(veh.Handle);
                _processingVehicles.Remove(veh.Handle);
            }
        }

        private void CleanupInvalidEntries()
        {
            var toRemove = new List<int>();
            
            foreach (var kvp in _tracked)
            {
                if (kvp.Value?.Vehicle == null || !kvp.Value.Vehicle.Exists() || 
                    kvp.Value.Vehicle.Driver == null || !kvp.Value.Vehicle.Driver.Exists())
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _tracked.Remove(key);
                _lastActionTime.Remove(key);
                _processingVehicles.Remove(key);
            }
        }

        private void CleanupStaleEntries()
        {
            var now = DateTime.Now;
            var toRemove = new List<int>();

            foreach (var kvp in _tracked)
            {
                if ((now - kvp.Value.LastSeen).TotalSeconds > TrackingTimeout)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _tracked.Remove(key);
                _lastActionTime.Remove(key);
                _processingVehicles.Remove(key);
            }
        }
    }

    // Classes auxiliaires pour une meilleure organisation
    public class BlockageAnalysis
    {
        public bool IsBlocked { get; set; }
        public bool IsPlayerBlocking { get; set; }
        public float DistanceToObstacle { get; set; }
        public bool IsInTrafficJam { get; set; }
        public bool CanGoLeft { get; set; }
        public bool CanGoRight { get; set; }
        public bool CanReverse { get; set; }
        public BypassDirection PreferredDirection { get; set; }
    }

    public enum BypassDirection
    {
        None,
        Left,
        Right,
        Reverse
    }
}
