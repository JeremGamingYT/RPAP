using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using REALIS.Common;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Gestionnaire centralisé des incidents routiers.
    /// Remplace le système NPCRoadRage avec une architecture robuste et sans conflits.
    /// </summary>
    public class CentralizedIncidentManager : Script, IEventHandler
    {
        private readonly string MANAGER_ID = "CentralizedIncidentManager";
        private const int LOCK_PRIORITY = 5;
        
        // État des incidents actifs
        private readonly Dictionary<int, ActiveIncident> _activeIncidents = new();
        private readonly Dictionary<int, DateTime> _vehicleCooldowns = new();
        private readonly HashSet<int> _processedCollisions = new();
        
        // Configuration
        private const float COLLISION_DETECTION_RADIUS = 8f;
        private const float MIN_IMPACT_SPEED = 4f;
        private const float SIGNIFICANT_IMPACT_SPEED = 8f;
        private const float INCIDENT_COOLDOWN_SECONDS = 15f;
        private const float POLICE_RESPONSE_CHANCE = 0.3f; // 30%
        private const float AGGRESSIVE_NPC_CHANCE = 0.2f; // 20%
        
        private DateTime _lastCollisionCheck = DateTime.MinValue;
        private readonly Random _random = new Random();
        private bool _isRegistered = false;

        public CentralizedIncidentManager()
        {
            // DÉSACTIVÉ TEMPORAIREMENT - Cause des plantages lors des collisions
            // Le système sera réactivé une fois stabilisé
            
            // Tick += OnTick;
            // Interval = 500; // Vérifie toutes les 500ms
            
            GTA.UI.Notification.PostTicker("~y~[CentralizedIncidentManager] Temporairement désactivé pour éviter les plantages", false);
        }

        private void OnTick(object sender, EventArgs e)
        {
            // DÉSACTIVÉ - Évite les plantages lors des collisions
            /*
            try
            {
                // Vérifie que le gestionnaire central est disponible
                if (CentralEventManager.Instance == null)
                    return;
                
                // S'enregistre auprès du gestionnaire central (une seule fois)
                if (!_isRegistered)
                {
                    CentralEventManager.Instance.RegisterHandler(REALISEventType.Collision, this);
                    _isRegistered = true;
                }

                // Continue avec la logique normale
                ProcessIncidents();
            }
            catch (Exception ex)
            {
                SafeLogError($"Incident manager error: {ex.Message}");
            }
            */
        }

        private void ProcessIncidents()
        {
            try
            {
                // Détecte les nouvelles collisions
                DetectCollisions();
                
                // Traite les incidents actifs
                ProcessActiveIncidents();
                
                // Nettoie les anciens incidents
                CleanupOldIncidents();
            }
            catch (Exception ex)
            {
                SafeLogError($"Process incidents error: {ex.Message}");
            }
        }

        #region Collision Detection

        private void DetectCollisions()
        {
            // Limite la fréquence de détection
            if ((DateTime.Now - _lastCollisionCheck).TotalMilliseconds < 250) return;
            
            var player = Game.Player.Character;
            var playerVehicle = player?.CurrentVehicle;
            
            if (playerVehicle == null || !playerVehicle.Exists()) return;

            // Recherche sécurisée des véhicules proches
            var nearbyVehicles = GetSafeNearbyVehicles(playerVehicle.Position, COLLISION_DETECTION_RADIUS);
            
            foreach (var vehicle in nearbyVehicles)
            {
                if (ShouldCheckCollision(playerVehicle, vehicle))
                {
                    CheckForCollision(playerVehicle, vehicle);
                }
            }
            
            _lastCollisionCheck = DateTime.Now;
        }

        private bool ShouldCheckCollision(Vehicle playerVehicle, Vehicle otherVehicle)
        {
            try
            {
                if (otherVehicle?.Driver == null || !otherVehicle.Driver.IsAlive) return false;
                if (otherVehicle.Driver == Game.Player.Character) return false;
                
                // Vérifie le cooldown
                if (_vehicleCooldowns.TryGetValue(otherVehicle.Handle, out var lastCollision))
                {
                    if ((DateTime.Now - lastCollision).TotalSeconds < INCIDENT_COOLDOWN_SECONDS)
                        return false;
                }
                
                // Vérifie si déjà traité
                if (_processedCollisions.Contains(otherVehicle.Handle)) return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CheckForCollision(Vehicle playerVehicle, Vehicle otherVehicle)
        {
            try
            {
                var collisionInfo = AnalyzeCollision(playerVehicle, otherVehicle);
                
                if (collisionInfo.HasCollision)
                {
                    ProcessCollision(collisionInfo);
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Collision check error: {ex.Message}");
            }
        }

        private CollisionAnalysis AnalyzeCollision(Vehicle playerVehicle, Vehicle otherVehicle)
        {
            var analysis = new CollisionAnalysis
            {
                PlayerVehicle = playerVehicle,
                OtherVehicle = otherVehicle,
                PlayerSpeed = playerVehicle.Speed,
                OtherSpeed = otherVehicle.Speed,
                Distance = playerVehicle.Position.DistanceTo(otherVehicle.Position)
            };

            try
            {
                // Détection basée sur plusieurs critères
                analysis.IsTouching = playerVehicle.IsTouching(otherVehicle);
                analysis.HasDamage = playerVehicle.HasBeenDamagedBy(otherVehicle) || 
                                   otherVehicle.HasBeenDamagedBy(playerVehicle);
                
                // Analyse de l'impact
                var relativeSpeed = Math.Abs(analysis.PlayerSpeed - analysis.OtherSpeed);
                analysis.ImpactForce = relativeSpeed * (analysis.Distance < 2f ? 2f : 1f);
                
                // Détermine si c'est une collision significative
                analysis.HasCollision = (analysis.IsTouching || analysis.HasDamage) && 
                                      (analysis.PlayerSpeed > MIN_IMPACT_SPEED || analysis.ImpactForce > MIN_IMPACT_SPEED);
                
                if (analysis.HasCollision)
                {
                    analysis.Severity = DetermineSeverity(analysis);
                    analysis.IsPlayerAtFault = DeterminePlayerFault(analysis);
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Collision analysis error: {ex.Message}");
                analysis.HasCollision = false;
            }

            return analysis;
        }

        private CollisionSeverity DetermineSeverity(CollisionAnalysis analysis)
        {
            if (analysis.ImpactForce > SIGNIFICANT_IMPACT_SPEED * 1.5f) return CollisionSeverity.Severe;
            if (analysis.ImpactForce > SIGNIFICANT_IMPACT_SPEED) return CollisionSeverity.Moderate;
            return CollisionSeverity.Minor;
        }

        private bool DeterminePlayerFault(CollisionAnalysis analysis)
        {
            // Logique simplifiée de détermination de faute
            return analysis.PlayerSpeed > analysis.OtherSpeed * 1.2f;
        }

        #endregion

        #region Incident Processing

        private void ProcessCollision(CollisionAnalysis collisionInfo)
        {
            try
            {
                // Vérifie si les véhicules existent encore
                if (collisionInfo.OtherVehicle == null || !collisionInfo.OtherVehicle.Exists())
                    return;

                if (CentralEventManager.Instance == null)
                    return;

                // Vérifie si on peut acquérir le contrôle du véhicule
                if (!CentralEventManager.Instance.TryLockVehicle(collisionInfo.OtherVehicle.Handle, MANAGER_ID, LOCK_PRIORITY))
                    return;

                // Marque comme traité
                _processedCollisions.Add(collisionInfo.OtherVehicle.Handle);
                _vehicleCooldowns[collisionInfo.OtherVehicle.Handle] = DateTime.Now;

                // Crée un incident
                var incident = new ActiveIncident
                {
                    Id = Guid.NewGuid(),
                    PlayerVehicle = collisionInfo.PlayerVehicle,
                    OtherVehicle = collisionInfo.OtherVehicle,
                    OtherDriver = collisionInfo.OtherVehicle?.Driver,
                    Location = collisionInfo.PlayerVehicle?.Position ?? Vector3.Zero,
                    Severity = collisionInfo.Severity,
                    IsPlayerAtFault = collisionInfo.IsPlayerAtFault,
                    CreationTime = DateTime.Now,
                    State = IncidentState.Initial
                };

                if (incident.OtherVehicle != null)
                {
                    _activeIncidents[incident.OtherVehicle.Handle] = incident;

                    // Démarre la séquence d'incident
                    InitiateIncidentSequence(incident);

                    // Déclenche l'événement global
                    FireCollisionEvent(collisionInfo, incident);
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Collision processing error: {ex.Message}");
                
                // Libère le véhicule en cas d'erreur
                if (collisionInfo.OtherVehicle != null && CentralEventManager.Instance != null)
                {
                    CentralEventManager.Instance.UnlockVehicle(collisionInfo.OtherVehicle.Handle);
                }
            }
        }

        private void InitiateIncidentSequence(ActiveIncident incident)
        {
            try
            {
                // Arrête les véhicules impliqués
                if (incident.PlayerVehicle != null)
                    StopVehicleSafely(incident.PlayerVehicle);
                if (incident.OtherVehicle != null)
                    StopVehicleSafely(incident.OtherVehicle);

                // Fait sortir le conducteur NPC
                if (incident.OtherDriver != null && incident.OtherDriver.Exists() && incident.OtherVehicle != null)
                {
                    incident.OtherDriver.Task.LeaveVehicle();
                    incident.State = IncidentState.NPCExiting;
                }

                // Détermine la réaction du NPC
                incident.NPCReaction = DetermineNPCReaction(incident);
                
                // Démarre la timeline de l'incident
                incident.NextStateTime = DateTime.Now.AddSeconds(3); // 3 secondes pour sortir du véhicule
            }
            catch (Exception ex)
            {
                SafeLogError($"Incident initiation error: {ex.Message}");
            }
        }

        private NPCReactionType DetermineNPCReaction(ActiveIncident incident)
        {
            // Facteurs influençant la réaction
            var severityFactor = (int)incident.Severity;
            var faultFactor = incident.IsPlayerAtFault ? 1.5f : 1f;
            var randomFactor = _random.NextDouble();

            var aggressionChance = AGGRESSIVE_NPC_CHANCE * severityFactor * faultFactor;
            var policeChance = POLICE_RESPONSE_CHANCE * severityFactor;

            if (randomFactor < aggressionChance)
                return NPCReactionType.Aggressive;
            else if (randomFactor < policeChance)
                return NPCReactionType.CallPolice;
            else
                return NPCReactionType.Calm;
        }

        private void StopVehicleSafely(Vehicle vehicle)
        {
            try
            {
                if (vehicle?.Driver != null && vehicle.Driver.Exists())
                {
                    vehicle.Driver.Task.ClearAll();
                    // Utilise la fonction native pour le frein à main
                    Function.Call(Hash.SET_VEHICLE_HANDBRAKE, vehicle.Handle, true);
                }
            }
            catch
            {
                // Ignore les erreurs d'arrêt
            }
        }

        #endregion

        #region Incident State Management

        private void ProcessActiveIncidents()
        {
            var incidentsToRemove = new List<int>();

            foreach (var kvp in _activeIncidents.ToList())
            {
                var incident = kvp.Value;
                
                try
                {
                    if (!IsIncidentValid(incident))
                    {
                        incidentsToRemove.Add(kvp.Key);
                        continue;
                    }

                    UpdateIncidentState(incident);
                }
                catch (Exception ex)
                {
                    SafeLogError($"Incident update error: {ex.Message}");
                    incidentsToRemove.Add(kvp.Key);
                }
            }

            // Nettoie les incidents invalides
            foreach (var handle in incidentsToRemove)
            {
                CleanupIncident(handle);
            }
        }

        private bool IsIncidentValid(ActiveIncident incident)
        {
            // Vérifie si l'incident est encore valide
            if (incident.OtherVehicle == null || !incident.OtherVehicle.Exists()) return false;
            if (incident.OtherDriver == null || !incident.OtherDriver.Exists()) return false;
            
            // Timeout de l'incident
            if ((DateTime.Now - incident.CreationTime).TotalMinutes > 5) return false;
            
            return true;
        }

        private void UpdateIncidentState(ActiveIncident incident)
        {
            if (DateTime.Now < incident.NextStateTime) return;

            switch (incident.State)
            {
                case IncidentState.NPCExiting:
                    ProcessNPCExited(incident);
                    break;
                    
                case IncidentState.NPCReacting:
                    // La réaction est déjà gérée dans ProcessNPCExited
                    // Passe directement à la résolution
                    incident.State = IncidentState.Resolution;
                    incident.NextStateTime = DateTime.Now.AddSeconds(5);
                    break;
                    
                case IncidentState.PoliceResponse:
                    ProcessPoliceResponse(incident);
                    break;
                    
                case IncidentState.Resolution:
                    ProcessResolution(incident);
                    break;
            }
        }

        private void ProcessNPCExited(ActiveIncident incident)
        {
            try
            {
                if (incident.OtherDriver?.IsInVehicle() == true)
                {
                    // Encore dans le véhicule, attend plus
                    incident.NextStateTime = DateTime.Now.AddSeconds(2);
                    return;
                }

                // Le NPC est sorti, fait réagir selon le type
                incident.State = IncidentState.NPCReacting;
                
                switch (incident.NPCReaction)
                {
                    case NPCReactionType.Calm:
                        ProcessCalmReaction(incident);
                        break;
                        
                    case NPCReactionType.Aggressive:
                        ProcessAggressiveReaction(incident);
                        break;
                        
                    case NPCReactionType.CallPolice:
                        ProcessPoliceCall(incident);
                        break;
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"NPC exit processing error: {ex.Message}");
                incident.State = IncidentState.Resolution;
                incident.NextStateTime = DateTime.Now.AddSeconds(1);
            }
        }

        private void ProcessCalmReaction(ActiveIncident incident)
        {
            try
            {
                var driver = incident.OtherDriver;
                
                if (driver != null && driver.Exists() && incident.OtherVehicle != null)
                {
                    // Animation de vérification des dégâts
                    driver.Task.FollowNavMeshTo(incident.OtherVehicle.Position + incident.OtherVehicle.ForwardVector * 2f);
                }
                
                // Message au joueur
                var messages = new[]
                {
                    "~y~L'autre conducteur inspecte les dégâts calmement.",
                    "~g~Pas de gros dégâts, le conducteur semble compréhensif.",
                    "~b~L'autre conducteur vérifie son véhicule."
                };
                
                Notification.PostTicker(messages[_random.Next(messages.Length)], false);
                
                // Retourne au véhicule après quelques secondes
                incident.State = IncidentState.Resolution;
                incident.NextStateTime = DateTime.Now.AddSeconds(8);
            }
            catch (Exception ex)
            {
                SafeLogError($"Calm reaction error: {ex.Message}");
            }
        }

        private void ProcessAggressiveReaction(ActiveIncident incident)
        {
            try
            {
                var driver = incident.OtherDriver;
                var player = Game.Player.Character;
                
                if (driver != null && driver.Exists())
                {
                    // Le NPC s'approche du joueur de manière agressive
                    driver.Task.FollowNavMeshTo(player.Position);
                    
                    // Animation d'énervement
                    Function.Call(Hash.TASK_PLAY_ANIM, driver.Handle, "gestures@m@standing@casual", "gesture_damn", 8.0f, 4.0f, -1, 48, 0, false, false, false);
                }
                
                var aggressiveMessages = new[]
                {
                    "~r~Le conducteur semble très en colère !",
                    "~r~L'autre conducteur vous menace du poing !",
                    "~r~Le conducteur crie après vous !"
                };
                
                Notification.PostTicker(aggressiveMessages[_random.Next(aggressiveMessages.Length)], false);
                
                // Résolution après confrontation
                incident.State = IncidentState.Resolution;
                incident.NextStateTime = DateTime.Now.AddSeconds(12);
            }
            catch (Exception ex)
            {
                SafeLogError($"Aggressive reaction error: {ex.Message}");
            }
        }

        private void ProcessPoliceCall(ActiveIncident incident)
        {
            try
            {
                var driver = incident.OtherDriver;
                
                if (driver != null && driver.Exists())
                {
                    // Animation d'appel téléphonique
                    Function.Call(Hash.TASK_USE_MOBILE_PHONE_TIMED, driver.Handle, 8000);
                }
                
                Notification.PostTicker("~b~L'autre conducteur appelle la police...", false);
                
                // Transition vers la réponse policière
                incident.State = IncidentState.PoliceResponse;
                incident.NextStateTime = DateTime.Now.AddSeconds(10);
                incident.PoliceCallTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                SafeLogError($"Police call error: {ex.Message}");
            }
        }

        private void ProcessPoliceResponse(ActiveIncident incident)
        {
            // Simplifié - notifie juste le joueur
            try
            {
                if (!incident.PoliceNotified)
                {
                    Notification.PostTicker("~r~La police a été appelée ! Vous feriez mieux de partir...", false);
                    incident.PoliceNotified = true;
                    
                    // Déclenche un événement pour d'autres systèmes
                    if (CentralEventManager.Instance != null)
                    {
                        var policeEvent = new PoliceCallEvent
                        {
                            Position = incident.Location
                        };
                        policeEvent.Data["IncidentId"] = incident.Id;
                        policeEvent.Data["Severity"] = incident.Severity;
                        CentralEventManager.Instance.FireEvent(policeEvent);
                    }
                }
                
                // Résolution après notification
                incident.State = IncidentState.Resolution;
                incident.NextStateTime = DateTime.Now.AddSeconds(5);
            }
            catch (Exception ex)
            {
                SafeLogError($"Police response error: {ex.Message}");
            }
        }

        private void ProcessResolution(ActiveIncident incident)
        {
            try
            {
                var driver = incident.OtherDriver;
                
                if (driver != null && driver.Exists())
                {
                    // Le NPC retourne à son véhicule
                    if (!driver.IsInVehicle() && incident.OtherVehicle != null)
                    {
                        driver.Task.EnterVehicle(incident.OtherVehicle, VehicleSeat.Driver);
                    }
                    else if (incident.OtherVehicle != null)
                    {
                        // Le NPC repart
                        driver.Task.CruiseWithVehicle(incident.OtherVehicle, 25f, VehicleDrivingFlags.StopForVehicles);
                        
                        // Marque l'incident comme terminé
                        CleanupIncident(incident.OtherVehicle.Handle);
                    }
                }
                else
                {
                    // Driver n'existe plus, nettoie l'incident
                    if (incident.OtherVehicle != null)
                        CleanupIncident(incident.OtherVehicle.Handle);
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Resolution error: {ex.Message}");
                if (incident.OtherVehicle != null)
                    CleanupIncident(incident.OtherVehicle.Handle);
            }
        }

        #endregion

        #region Event Handling

        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent is CollisionEvent;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                if (gameEvent is CollisionEvent collision)
                {
                    // Peut traiter les événements de collision d'autres systèmes
                    HandleExternalCollisionEvent(collision);
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Event handling error: {ex.Message}");
            }
        }

        private void HandleExternalCollisionEvent(CollisionEvent collisionEvent)
        {
            // Logique pour traiter les collisions détectées par d'autres systèmes
        }

        private void FireCollisionEvent(CollisionAnalysis analysis, ActiveIncident incident)
        {
            if (analysis.PlayerVehicle == null || analysis.OtherVehicle == null || CentralEventManager.Instance == null) 
                return;
            
            var collisionEvent = new CollisionEvent
            {
                VehicleHandle = analysis.PlayerVehicle.Handle,
                OtherVehicleHandle = analysis.OtherVehicle.Handle,
                DriverHandle = analysis.OtherVehicle.Driver?.Handle ?? 0,
                Position = incident.Location,
                ImpactForce = analysis.ImpactForce,
                Severity = analysis.Severity
            };
            
            collisionEvent.Data["IncidentId"] = incident.Id;
            collisionEvent.Data["IsPlayerAtFault"] = analysis.IsPlayerAtFault;
            collisionEvent.Data["NPCReaction"] = incident.NPCReaction;
            
            CentralEventManager.Instance.FireEvent(collisionEvent);
        }

        #endregion

        #region Utilities

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

        private void CleanupIncident(int vehicleHandle)
        {
            try
            {
                _activeIncidents.Remove(vehicleHandle);
                
                if (CentralEventManager.Instance != null)
                    CentralEventManager.Instance.UnlockVehicle(vehicleHandle);
            }
            catch (Exception ex)
            {
                SafeLogError($"Cleanup error: {ex.Message}");
            }
        }

        private void CleanupOldIncidents()
        {
            var cutoff = DateTime.Now.AddMinutes(-10);
            var oldVehicles = _vehicleCooldowns
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .Take(5) // Nettoie par petits lots
                .ToList();

            foreach (var handle in oldVehicles)
            {
                _vehicleCooldowns.Remove(handle);
                _processedCollisions.Remove(handle);
            }
        }

        private void SafeLogError(string message)
        {
            try
            {
                Notification.PostTicker($"~r~[Incident] {message}", false);
            }
            catch
            {
                // Ignore les erreurs de logging
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            try
            {
                if (_isRegistered && CentralEventManager.Instance != null)
                {
                    CentralEventManager.Instance.UnregisterHandler(REALISEventType.Collision, this);
                }
                
                // Nettoie tous les incidents actifs
                foreach (var kvp in _activeIncidents)
                {
                    if (CentralEventManager.Instance != null)
                        CentralEventManager.Instance.UnlockVehicle(kvp.Key);
                }
                
                _activeIncidents.Clear();
                _vehicleCooldowns.Clear();
                _processedCollisions.Clear();
            }
            catch (Exception ex)
            {
                SafeLogError($"Disposal error: {ex.Message}");
            }
        }

        #endregion
    }

    #region Supporting Classes

    internal class ActiveIncident
    {
        public Guid Id { get; set; }
        public Vehicle? PlayerVehicle { get; set; }
        public Vehicle? OtherVehicle { get; set; }
        public Ped? OtherDriver { get; set; }
        public Vector3 Location { get; set; }
        public CollisionSeverity Severity { get; set; }
        public bool IsPlayerAtFault { get; set; }
        public DateTime CreationTime { get; set; }
        public IncidentState State { get; set; }
        public NPCReactionType NPCReaction { get; set; }
        public DateTime NextStateTime { get; set; }
        public DateTime? PoliceCallTime { get; set; }
        public bool PoliceNotified { get; set; }
    }

    internal class CollisionAnalysis
    {
        public Vehicle? PlayerVehicle { get; set; }
        public Vehicle? OtherVehicle { get; set; }
        public float PlayerSpeed { get; set; }
        public float OtherSpeed { get; set; }
        public float Distance { get; set; }
        public bool IsTouching { get; set; }
        public bool HasDamage { get; set; }
        public bool HasCollision { get; set; }
        public float ImpactForce { get; set; }
        public CollisionSeverity Severity { get; set; }
        public bool IsPlayerAtFault { get; set; }
    }

    internal enum IncidentState
    {
        Initial,
        NPCExiting,
        NPCReacting,
        PoliceResponse,
        Resolution
    }

    internal enum NPCReactionType
    {
        Calm,
        Aggressive,
        CallPolice
    }

    internal class PoliceCallEvent : GameEvent
    {
        public PoliceCallEvent()
        {
            Type = REALISEventType.PoliceCall;
        }
    }

    #endregion
} 