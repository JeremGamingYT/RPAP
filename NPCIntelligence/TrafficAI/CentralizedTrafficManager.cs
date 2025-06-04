using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using REALIS.Common;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Version centralisée et robuste du gestionnaire de trafic.
    /// Utilise le système d'événements central pour éviter les conflits.
    /// </summary>
    public class CentralizedTrafficManager : Script, IEventHandler
    {
        private readonly string MANAGER_ID = "CentralizedTrafficManager";
        private readonly Dictionary<int, TrafficVehicleInfo> _trackedVehicles = new();
        private readonly Dictionary<int, DateTime> _lastProcessTime = new();
        
        // Configuration optimisée
        private const float SCAN_RADIUS = 35f;
        private const float PROCESSING_INTERVAL = 2f; // secondes
        private const float SPEED_THRESHOLD = 1.2f;
        private const float BLOCKED_TIME_THRESHOLD = 4f;
        private const float HONK_COOLDOWN = 8f;
        private const float BYPASS_COOLDOWN = 12f;
        private const int MAX_CONCURRENT_PROCESSING = 6;
        private const float PLAYER_SAFE_ZONE = 6f;
        
        private DateTime _lastFullScan = DateTime.MinValue;
        private int _processedThisTick = 0;

        private bool _isRegistered = false;

        public CentralizedTrafficManager()
        {
            Tick += OnTick;
            Interval = 1000; // 1 seconde - plus conservateur
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Vérifie que le gestionnaire central est disponible
                if (CentralEventManager.Instance == null)
                    return;
                
                // S'enregistre auprès du gestionnaire central (une seule fois)
                if (!_isRegistered)
                {
                    CentralEventManager.Instance.RegisterHandler(REALISEventType.TrafficBlock, this);
                    CentralEventManager.Instance.RegisterHandler(REALISEventType.VehicleStuck, this);
                    _isRegistered = true;
                }

                // Continue avec la logique normale
                ProcessTrafficAI();
            }
            catch (Exception ex)
            {
                SafeLogError($"Traffic AI error: {ex.Message}");
            }
        }

        private void ProcessTrafficAI()
        {
            try
            {
                if (!ShouldProcess()) return;
                
                _processedThisTick = 0;
                ProcessTrafficIntelligence();
                CleanupStaleEntries();
            }
            catch (Exception ex)
            {
                SafeLogError($"Traffic processing error: {ex.Message}");
            }
        }

        private bool ShouldProcess()
        {
            var player = Game.Player.Character;
            if (player?.CurrentVehicle == null || !player.Exists()) return false;
            
            var playerVehicle = player.CurrentVehicle;
            if (playerVehicle.Speed < 0.3f) return false; // Joueur immobile
            
            // Limite le traitement à intervalles réguliers
            if ((DateTime.Now - _lastFullScan).TotalSeconds < PROCESSING_INTERVAL) return false;
            
            return true;
        }

        private void ProcessTrafficIntelligence()
        {
            var player = Game.Player.Character;
            var playerVehicle = player.CurrentVehicle;
            
            // Utilise le gestionnaire central pour obtenir les véhicules
            var nearbyVehicles = GetSafeNearbyVehicles(player.Position, SCAN_RADIUS);
            
            var relevantVehicles = nearbyVehicles
                .Where(IsVehicleRelevantForProcessing)
                .OrderBy(v => v.Position.DistanceTo(player.Position))
                .Take(MAX_CONCURRENT_PROCESSING)
                .ToList();

            foreach (var vehicle in relevantVehicles)
            {
                if (_processedThisTick >= MAX_CONCURRENT_PROCESSING) break;
                
                ProcessSingleVehicle(vehicle, playerVehicle);
                _processedThisTick++;
            }
            
            _lastFullScan = DateTime.Now;
        }

        private Vehicle[] GetSafeNearbyVehicles(Vector3 position, float radius)
        {
            try
            {
                return World.GetNearbyVehicles(position, radius)
                    .Where(v => v != null && v.Exists() && !v.IsDead)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<Vehicle>();
            }
        }

        private bool IsVehicleRelevantForProcessing(Vehicle vehicle)
        {
            return CanProcessVehicle(vehicle);
        }

        private void ProcessSingleVehicle(Vehicle vehicle, Vehicle playerVehicle)
        {
            try
            {
                // Utilise la nouvelle méthode avec gestionnaire central
                if (!TryStartProcessing(vehicle))
                    return;

                var info = _trackedVehicles[vehicle.Handle];
                UpdateVehicleAnalysis(info, playerVehicle);
            }
            catch (Exception ex)
            {
                SafeLogError($"Vehicle processing error for {vehicle.Handle}: {ex.Message}");
            }
            finally
            {
                ReleaseVehicle(vehicle.Handle);
            }
        }

        private void UpdateVehicleAnalysis(TrafficVehicleInfo info, Vehicle playerVehicle)
        {
            var vehicle = info.Vehicle;
            if (!vehicle.Exists()) return;

            // Reset si le véhicule bouge
            if (vehicle.Speed > SPEED_THRESHOLD)
            {
                ResetVehicleBlockedState(info);
                return;
            }

            // Analyse de blocage
            var blockageInfo = AnalyzeTrafficSituation(vehicle, playerVehicle);
            
            if (!blockageInfo.IsBlocked)
            {
                ResetVehicleBlockedState(info);
                return;
            }

            // Met à jour le temps de blocage
            info.BlockedDuration += PROCESSING_INTERVAL;
            
            // Actions intelligentes basées sur la situation
            if (ShouldTakeAction(info, blockageInfo))
            {
                TakeIntelligentAction(info, blockageInfo);
            }
        }

        private TrafficBlockageInfo AnalyzeTrafficSituation(Vehicle vehicle, Vehicle playerVehicle)
        {
            var info = new TrafficBlockageInfo();
            
            try
            {
                var distanceToPlayer = vehicle.Position.DistanceTo(playerVehicle.Position);
                var directionToPlayer = (playerVehicle.Position - vehicle.Position).Normalized;
                var vehicleForward = vehicle.ForwardVector;
                var angleToPlayer = Vector3.Dot(vehicleForward, directionToPlayer);

                // Détection plus intelligente du blocage par le joueur
                info.IsPlayerBlocking = IsPlayerActuallyBlocking(vehicle, playerVehicle, distanceToPlayer, angleToPlayer);
                info.DistanceToPlayer = distanceToPlayer;
                
                // Détection de blocage plus tolérante
                info.IsBlocked = IsPathBlockedIntelligent(vehicle);
                
                // Analyse des directions de contournement avec plus de tests
                info.CanMoveLeft = CanMoveInDirection(vehicle, -vehicle.RightVector, 8f);
                info.CanMoveRight = CanMoveInDirection(vehicle, vehicle.RightVector, 8f);
                info.CanReverse = CanMoveInDirection(vehicle, -vehicle.ForwardVector, 6f);
                
                info.TrafficDensity = CountNearbySlowVehicles(vehicle);
                info.IsInTrafficJam = info.TrafficDensity > 4; // Plus tolérant
            }
            catch
            {
                // En cas d'erreur, être moins agressif
                info.IsBlocked = false;
                info.IsPlayerBlocking = false;
            }
            
            return info;
        }

        private bool IsPlayerActuallyBlocking(Vehicle vehicle, Vehicle playerVehicle, float distance, float angle)
        {
            try
            {
                // Plus restrictif : le joueur doit vraiment être dans le chemin
                if (distance > 15f) return false; // Distance augmentée
                if (angle < 0.6f) return false;   // Angle plus strict
                
                // Vérifier si le joueur est vraiment stationnaire
                if (playerVehicle.Speed > 2f) return false; // Le joueur bouge
                
                // Vérifier si le véhicule peut facilement contourner
                var leftClear = CanMoveInDirection(vehicle, -vehicle.RightVector, 4f);
                var rightClear = CanMoveInDirection(vehicle, vehicle.RightVector, 4f);
                
                // Si une voie de contournement est libre, ne pas considérer comme bloqué
                if (leftClear || rightClear) return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsPathBlockedIntelligent(Vehicle vehicle)
        {
            try
            {
                // Test plus intelligent avec plusieurs points de contrôle
                var testDirections = new[]
                {
                    vehicle.ForwardVector,
                    vehicle.ForwardVector + vehicle.RightVector * 0.2f,  // Légèrement à droite
                    vehicle.ForwardVector - vehicle.RightVector * 0.2f   // Légèrement à gauche
                };

                int blockedDirections = 0;
                foreach (var direction in testDirections)
                {
                    if (IsPathBlocked(vehicle, direction, 12f)) // Distance augmentée
                    {
                        blockedDirections++;
                    }
                }

                // Il faut que la majorité des directions soient bloquées
                return blockedDirections >= 2;
            }
            catch
            {
                return false;
            }
        }

        private bool CanMoveInDirection(Vehicle vehicle, Vector3 direction, float distance)
        {
            try
            {
                // Test multiple points pour une meilleure précision
                var basePosition = vehicle.Position + Vector3.WorldUp * 1.0f;
                
                // Test à différentes hauteurs pour éviter les faux positifs
                var testHeights = new[] { 0.5f, 1.0f, 1.5f };
                
                foreach (var height in testHeights)
                {
                    var start = basePosition + Vector3.WorldUp * height;
                    var end = start + direction * distance;
                    
                    var hit = World.Raycast(start, end,
                        IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles,
                        vehicle);
                    
                    if (hit.DidHit)
                    {
                        // Calculer la distance manuellement
                        var hitDistance = start.DistanceTo(hit.HitPosition);
                        if (hitDistance < distance * 0.8f) // 80% de la distance
                        {
                            return false; // Direction bloquée
                        }
                    }
                }
                
                return true; // Direction libre
            }
            catch
            {
                return false; // En cas d'erreur, considérer comme bloqué
            }
        }

        private bool ShouldTakeAction(TrafficVehicleInfo info, TrafficBlockageInfo blockageInfo)
        {
            if (info.BlockedDuration < BLOCKED_TIME_THRESHOLD * 1.5f) return false; // Plus patient
            
            var now = DateTime.Now;
            
            // Plus de tolérance avant d'agir
            if (!info.HasHonked && (now - info.LastActionTime).TotalSeconds > HONK_COOLDOWN * 1.2f)
                return true;
                
            // Contournement seulement si vraiment nécessaire et possible
            if (info.HasHonked && 
                (now - info.LastActionTime).TotalSeconds > BYPASS_COOLDOWN * 1.5f && 
                info.BypassAttempts < 1 && // Réduit à 1 tentative
                (blockageInfo.CanMoveLeft || blockageInfo.CanMoveRight))
                return true;
                
            return false;
        }
        
        private void TakeIntelligentAction(TrafficVehicleInfo info, TrafficBlockageInfo blockageInfo)
        {
            var now = DateTime.Now;
            
            try
            {
                // Klaxon intelligent
                if (!info.HasHonked)
                {
                    PerformSmartHonk(info.Vehicle, blockageInfo);
                    info.HasHonked = true;
                    info.LastActionTime = now;
                    
                    // Déclenche un événement pour informer les autres systèmes
                    FireTrafficEvent(info, blockageInfo);
                }
                // Contournement intelligent
                else if ((now - info.LastActionTime).TotalSeconds > BYPASS_COOLDOWN * 1.5f && 
                         info.BypassAttempts < 1)
                {
                    PerformSmartBypass(info, blockageInfo);
                    info.BypassAttempts++;
                    info.LastActionTime = now;
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Action execution error: {ex.Message}");
            }
        }

        private void PerformSmartHonk(Vehicle vehicle, TrafficBlockageInfo blockageInfo)
        {
            try
            {
                if (vehicle?.Driver == null || !vehicle.Exists()) return;
                
                // Klaxon adaptatif selon la situation
                var honkDuration = blockageInfo.IsPlayerBlocking ? 1000 : 500;
                vehicle.SoundHorn(honkDuration);
                
                // Animation du conducteur
                var driver = vehicle.Driver;
                if (driver.Exists() && driver.IsAlive)
                {
                    driver.Task.LookAt(Game.Player.Character, 2000);
                }
            }
            catch
            {
                // Ignore les erreurs de klaxon
            }
        }

        private void PerformSmartBypass(TrafficVehicleInfo info, TrafficBlockageInfo blockageInfo)
        {
            try
            {
                var vehicle = info.Vehicle;
                var driver = vehicle.Driver;
                
                if (driver == null || !driver.Exists() || !driver.IsAlive) return;
                
                // Choisit la meilleure direction de contournement
                Vector3? bypassTarget = CalculateBypassTarget(vehicle, blockageInfo);
                
                if (bypassTarget.HasValue)
                {
                    // Nettoie les tâches existantes
                    driver.Task.ClearAll();
                    Script.Wait(100);
                    
                    // Lance le contournement
                    driver.Task.DriveTo(vehicle, bypassTarget.Value, 5f, VehicleDrivingFlags.SwerveAroundAllVehicles | VehicleDrivingFlags.SteerAroundObjects, 15f);
                    
                    // Programme le retour à la route normale après quelques secondes
                    var returnTarget = vehicle.Position + vehicle.ForwardVector * 25f;
                    Script.Wait(3000);
                    
                    if (driver.Exists() && driver.IsAlive)
                    {
                        driver.Task.DriveTo(vehicle, returnTarget, 8f, VehicleDrivingFlags.StopForVehicles, 25f);
                    }
                }
            }
            catch
            {
                // Ignore les erreurs de contournement
            }
        }

        private Vector3? CalculateBypassTarget(Vehicle vehicle, TrafficBlockageInfo blockageInfo)
        {
            try
            {
                var basePosition = vehicle.Position;
                var forward = vehicle.ForwardVector;
                var right = vehicle.RightVector;
                
                // Priorité : droite > gauche > marche arrière
                if (blockageInfo.CanMoveRight)
                {
                    return basePosition + right * 4f + forward * 8f;
                }
                else if (blockageInfo.CanMoveLeft)
                {
                    return basePosition - right * 4f + forward * 8f;
                }
                else if (blockageInfo.CanReverse)
                {
                    return basePosition - forward * 6f + right * 2f;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void FireTrafficEvent(TrafficVehicleInfo info, TrafficBlockageInfo blockageInfo)
        {
            if (CentralEventManager.Instance == null)
                return;
                
            var trafficEvent = new TrafficBlockEvent
            {
                VehicleHandle = info.Vehicle.Handle,
                DriverHandle = info.Vehicle.Driver?.Handle ?? 0,
                Position = info.Vehicle.Position,
                BlockedDuration = info.BlockedDuration,
                BlockingPosition = info.Vehicle.Position
            };
            
            trafficEvent.Data["Reason"] = "TrafficBlockage";
            trafficEvent.Data["TrafficDensity"] = GetTrafficDensity(info.Vehicle.Position);
            trafficEvent.Data["IsPlayerBlocking"] = blockageInfo.IsPlayerBlocking;
            trafficEvent.Data["CanBypass"] = blockageInfo.CanMoveLeft || blockageInfo.CanMoveRight;
            
            CentralEventManager.Instance.FireEvent(trafficEvent);
        }

        private int GetTrafficDensity(Vector3 position)
        {
            try
            {
                return World.GetNearbyVehicles(position, 20f)
                    .Count(v => v != null && v.Exists() && v.Speed < SPEED_THRESHOLD);
            }
            catch
            {
                return 0;
            }
        }

        private void ResetVehicleBlockedState(TrafficVehicleInfo info)
        {
            info.BlockedDuration = 0f;
            info.HasHonked = false;
            info.BypassAttempts = 0;
        }

        private void CleanupStaleEntries()
        {
            var cutoff = DateTime.Now.AddSeconds(-30);
            var staleBatch = _trackedVehicles
                .Where(kvp => kvp.Value.LastSeen < cutoff || !kvp.Value.Vehicle.Exists())
                .Select(kvp => kvp.Key)
                .Take(5) // Nettoie par petits lots
                .ToList();

            foreach (var handle in staleBatch)
            {
                _trackedVehicles.Remove(handle);
                _lastProcessTime.Remove(handle);
            }
        }

        private void SafeLogError(string message)
        {
            try
            {
                GTA.UI.Notification.PostTicker($"~o~[Traffic] {message}", false);
            }
            catch
            {
                // Ignore les erreurs de logging
            }
        }

        #region IEventHandler Implementation

        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent is TrafficBlockEvent || gameEvent is VehicleEvent;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                if (gameEvent is TrafficBlockEvent trafficEvent)
                {
                    // Peut réagir aux événements de trafic d'autres systèmes
                    HandleTrafficBlockEvent(trafficEvent);
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Event handling error: {ex.Message}");
            }
        }

        private void HandleTrafficBlockEvent(TrafficBlockEvent trafficEvent)
        {
            // Logique pour réagir aux événements de trafic externes
            // Par exemple, éviter de traiter un véhicule déjà géré par un autre système
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            try
            {
                if (_isRegistered && CentralEventManager.Instance != null)
                {
                    CentralEventManager.Instance.UnregisterHandler(REALISEventType.TrafficBlock, this);
                    CentralEventManager.Instance.UnregisterHandler(REALISEventType.VehicleStuck, this);
                }
                
                _trackedVehicles.Clear();
                _lastProcessTime.Clear();
            }
            catch (Exception ex)
            {
                SafeLogError($"Disposal error: {ex.Message}");
            }
        }

        #endregion

        private bool CanProcessVehicle(Vehicle vehicle)
        {
            if (vehicle?.Driver == null || !vehicle.Exists() || !vehicle.Driver.Exists())
                return false;

            if (IsPlayerOrPlayerVehicle(vehicle))
                return false;

            // Vérifie les verrous via le gestionnaire central
            if (CentralEventManager.Instance?.IsVehicleLocked(vehicle.Handle) == true) 
                return false;

            return !_lastProcessTime.TryGetValue(vehicle.Handle, out var lastTime) ||
                   (DateTime.Now - lastTime).TotalSeconds >= PROCESSING_INTERVAL;
        }

        private bool IsPlayerOrPlayerVehicle(Vehicle vehicle)
        {
            try
            {
                var player = Game.Player.Character;
                
                // Vérifie si c'est le véhicule du joueur
                if (player.CurrentVehicle != null && vehicle.Handle == player.CurrentVehicle.Handle)
                    return true;
                
                // Vérifie si le conducteur est le joueur
                if (vehicle.Driver != null && vehicle.Driver.Handle == player.Handle)
                    return true;
                
                // Vérifie la distance pour éviter d'affecter les véhicules trop proches du joueur
                var distanceToPlayer = vehicle.Position.DistanceTo(player.Position);
                if (distanceToPlayer < PLAYER_SAFE_ZONE)
                    return true;
                
                return false;
            }
            catch
            {
                return true; // En cas d'erreur, considère comme joueur pour éviter les actions
            }
        }

        private bool TryStartProcessing(Vehicle vehicle)
        {
            if (CentralEventManager.Instance == null)
                return false;
                
            if (!CentralEventManager.Instance.TryLockVehicle(vehicle.Handle, MANAGER_ID))
                return false;

            _lastProcessTime[vehicle.Handle] = DateTime.Now;
            _trackedVehicles[vehicle.Handle] = new TrafficVehicleInfo(vehicle);
            return true;
        }

        private void ReleaseVehicle(int vehicleHandle)
        {
            _trackedVehicles.Remove(vehicleHandle);
            
            if (CentralEventManager.Instance != null)
                CentralEventManager.Instance.UnlockVehicle(vehicleHandle);
        }

        private bool IsPathBlocked(Vehicle vehicle, Vector3 direction, float distance)
        {
            try
            {
                var start = vehicle.Position + Vector3.WorldUp * 1.2f;
                var end = start + direction * distance;
                
                var hit = World.Raycast(start, end,
                    IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Vehicles,
                    vehicle);
                
                return hit.DidHit;
            }
            catch
            {
                return true;
            }
        }

        private int CountNearbySlowVehicles(Vehicle vehicle)
        {
            try
            {
                return World.GetNearbyVehicles(vehicle.Position, 20f)
                    .Count(v => v != null && v.Exists() && v != vehicle && v.Speed < SPEED_THRESHOLD);
            }
            catch
            {
                return 0;
            }
        }
    }

    #region Supporting Classes

    internal class TrafficVehicleInfo
    {
        public Vehicle Vehicle { get; }
        public float BlockedDuration { get; set; }
        public bool HasHonked { get; set; }
        public int BypassAttempts { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastActionTime { get; set; }

        public TrafficVehicleInfo(Vehicle vehicle)
        {
            Vehicle = vehicle;
            LastSeen = DateTime.Now;
            LastActionTime = DateTime.MinValue;
        }
    }

    internal class TrafficBlockageInfo
    {
        public bool IsBlocked { get; set; }
        public bool IsPlayerBlocking { get; set; }
        public float DistanceToPlayer { get; set; }
        public bool CanMoveLeft { get; set; }
        public bool CanMoveRight { get; set; }
        public bool CanReverse { get; set; }
        public int TrafficDensity { get; set; }
        public bool IsInTrafficJam { get; set; }
    }

    #endregion
} 