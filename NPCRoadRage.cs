using GTA;
using GTA.Native; // Required for Hash and Function.Call
using GTA.Math;
using GTA.UI; 
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using System.Drawing;

public class NPCRoadRage : Script 
{
    // --- Configuration (easily updatable) ---
    private readonly TimeSpan _npcCollisionCooldownDuration = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _npcReactionTimeoutDuration = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _policeDispatchTimeoutDuration = TimeSpan.FromSeconds(300); // Augmenté de 90 à 300 secondes
    private readonly TimeSpan _policeExitVehicleTimeoutDuration = TimeSpan.FromSeconds(15); // Augmenté de 10 à 15
    private readonly TimeSpan _policeApproachTimeoutDuration = TimeSpan.FromSeconds(30); // Augmenté de 15 à 30
    private readonly TimeSpan _policeInteractionDialoguePauseDuration = TimeSpan.FromSeconds(3);
    private readonly TimeSpan _policeDecisionPauseDuration = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _policeArrestSequenceDuration = TimeSpan.FromSeconds(8); 
    private readonly TimeSpan _policeDepartureBoardingTimeoutDuration = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _policeDepartureDriveAwayDuration = TimeSpan.FromSeconds(15);
    private const int AggressiveNpcChancePercent = 20; 
    private const int NumberOfPoliceOfficers = 2; 
    private PedHash _policePedModel = PedHash.Cop01SMY;
    private VehicleHash _policeVehicleModel = VehicleHash.Police;
    private const float PoliceArrivalDistanceThreshold = 8.0f; // Réduit de 15.0f à 8.0f - plus proche
    private const float OfficerApproachDistanceThreshold = 3.0f; // Réduit de 5.0f à 3.0f - plus proche 
    private const float MaxInteractionDistance = 40.0f; 
    private const float PoliceDriveSpeed = 30.0f; 
    private const float PolicePatrolSpeed = 20.0f; 
    private const float OfficerApproachSpeed = 1.5f; // Walking/jog speed for approach

    // --- Collision Detection Configuration ---
    private const float MinSpeedForSignificantImpact = 5.0f; // Vitesse minimale pour considérer un impact avec contact comme significatif
    private const float MinSpeedForHighSpeedCollision = 8.0f; // Vitesse minimale pour une collision à haute vitesse
    private const float MaxDistanceForHighSpeedCollision = 1.5f; // Distance maximale pour détecter une collision à haute vitesse

    // --- Response Menu Configuration ---
    private readonly string[] _responseOptions = new string[]
    {
        "It was an accident, I'm sorry.",
        "They came out of nowhere!",
        "It was their fault, not mine!",
        "I have nothing to say.",
        "This is bullshit!"
    };
    private int _selectedResponseIndex = 0;
    private bool _showingResponseMenu = false;

    // --- State Fields ---
    private Dictionary<int, DateTime> _npcCollisionCooldowns = new Dictionary<int, DateTime>();
    private Ped? _collidedNpcPed = null;
    private Vehicle? _collidedNpcVehicle = null; 
    private Vector3 _incidentLocation = Vector3.Zero; 
    private bool _npcReacting = false;
    private enum NpcReactionType { None, Aggressive, CallPolice }
    private NpcReactionType _currentReaction = NpcReactionType.None;
    private DateTime _stateTimer; 

    public static bool PoliceCalled = false; 
    private bool _policeDispatched = false; 
    private bool _policeArrived = false; 
    private bool _policeInteractionActive = false; 
    private int _policeInteractionStage = 0; 
    private bool _makingPoliceDecision = false;
    private enum PoliceDecision { None, ArrestPlayer, LetPlayerGo, GiveFine }
    private PoliceDecision _currentPoliceDecision = PoliceDecision.None;

    private Random _random = new Random();
    private List<Ped> _respondingPolicePeds = new List<Ped>();
    private Vehicle? _policeVehicle = null;
    private string _playerResponse = string.Empty;

    // Ajout de variables pour éviter les répétitions
    private bool _playerControlDisabled = false;
    private bool _fineAlreadyGiven = false;
    private bool _decisionMessageShown = false;

    // Ajout de variables pour gérer les redirections
    private DateTime _lastPoliceRedirectionTime = DateTime.MinValue;
    private Vector3 _lastPolicePosition = Vector3.Zero;
    private DateTime _policeStuckStartTime = DateTime.MinValue;

    // --- Police Navigation Configuration (Made Less Aggressive) ---
    private const float PoliceSpawnMinDistance = 60.0f; // Réduit de 80 à 60m - plus proche
    private const float PoliceSpawnMaxDistance = 120.0f; // Réduit de 150 à 120m - plus proche
    private const float PoliceStuckSpeedThreshold = 0.3f; // Réduit de 0.5f à 0.3f - plus sensible aux blocages
    private const float PoliceProgressThreshold = 5.0f; // Augmenté de 3.0f à 5.0f - plus tolérant
    private const int MaxNavigationRetries = 2; // Réduit de 3 à 2 tentatives - plus rapide
    private const float ForceArrivalDistance = 50.0f; // Augmenté de 40.0f à 50.0f - force l'arrivée plus tôt

    // Additional state tracking for better navigation
    private float _lastRecordedDistance = float.MaxValue;
    private DateTime _lastProgressTime = DateTime.MinValue;
    private int _navigationRetryCount = 0;
    
    // Nouvelles variables pour réduire les comportements répétitifs
    private DateTime _lastOfficerTaskTime = DateTime.MinValue;
    private Dictionary<int, DateTime> _officerLastTaskedTime = new Dictionary<int, DateTime>();
    private DateTime _lastMenuDisplayTime = DateTime.MinValue;

    public NPCRoadRage()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown; 
        Log("NPCRoadRage script loaded. Version 1.23 - Fixed Police Navigation and Menu Display.");
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;

        if (Game.IsCutsceneActive || player == null || !player.Exists() || player.IsDead)
        {
            if(_policeDispatched || _npcReacting || _policeInteractionActive || _makingPoliceDecision) {
                Log("Game state changed (cutscene/player dead). Forcing cleanup of active incident.");
                FullResetOfScriptStates(); 
            }
            return;
        }

        if ((_policeInteractionActive || _makingPoliceDecision) && _policeVehicle != null && _policeVehicle.Exists()) {
            // Vérifier la distance par rapport au lieu de l'incident OU aux officiers, pas seulement au véhicule
            float distanceToIncident = player.Position.DistanceTo(_incidentLocation);
            float distanceToVehicle = player.Position.DistanceTo(_policeVehicle.Position);
            
            // Trouver l'officier le plus proche
            float distanceToNearestOfficer = float.MaxValue;
            foreach (Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive))
            {
                float officerDistance = player.Position.DistanceTo(officer.Position);
                if (officerDistance < distanceToNearestOfficer)
                {
                    distanceToNearestOfficer = officerDistance;
                }
            }
            
            // Le joueur a quitté la scène SEULEMENT s'il est loin de TOUT : incident, véhicule ET officiers
            bool tooFarFromIncident = distanceToIncident > MaxInteractionDistance * 1.5f; // Plus tolérant pour l'incident
            bool tooFarFromVehicle = distanceToVehicle > MaxInteractionDistance * 2.0f; // Beaucoup plus tolérant pour le véhicule
            bool tooFarFromOfficers = distanceToNearestOfficer > MaxInteractionDistance; // Distance normale pour les officiers
            
            if (tooFarFromIncident && tooFarFromVehicle && tooFarFromOfficers) {
                Log($"Player moved too far during police interaction. Distances - Incident: {distanceToIncident:F2}, Vehicle: {distanceToVehicle:F2}, Nearest Officer: {distanceToNearestOfficer:F2}. Aborting.");
                GTA.UI.Notification.PostTicker("You left the scene of the incident.", false, false);
                EndPoliceInteraction(false, true); 
                return;
            }
        }

        if (PoliceCalled && _collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive && !_npcReacting) {
            float npcDistanceToIncident = _collidedNpcPed.Position.DistanceTo(_incidentLocation);
            if (npcDistanceToIncident > 15f) {
                Log($"NPC {_collidedNpcPed.Handle} drifted too far from incident scene ({npcDistanceToIncident:F2}m). Bringing back.");
                _collidedNpcPed.Task.ClearAll();
                Script.Wait(50);
                _collidedNpcPed.BlockPermanentEvents = true;
                _collidedNpcPed.Task.FollowNavMeshTo(_incidentLocation);
            } else if (!_collidedNpcPed.IsWalking && !_collidedNpcPed.IsRunning && npcDistanceToIncident < 10f) {
                // S'assurer qu'il reste en place s'il est proche
                _collidedNpcPed.Task.GuardCurrentPosition();
            }
        }

        // Vérifier si le NPC est rentré dans son véhicule pour programmer son départ
        if (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcVehicle != null && _collidedNpcVehicle.Exists() &&
            !_npcReacting && !_policeInteractionActive && !_makingPoliceDecision && !PoliceCalled &&
            _collidedNpcPed.IsInVehicle(_collidedNpcVehicle))
        {
            Log($"NPC {_collidedNpcPed.Handle} has entered vehicle. Programming departure.");
            ProgramNpcDeparture();
            
            // Nettoyer les références après avoir programmé le départ
            _collidedNpcPed = null;
            _collidedNpcVehicle = null;
            _currentReaction = NpcReactionType.None;
        }

        if (PoliceCalled && !_policeDispatched && !_policeInteractionActive && !_makingPoliceDecision && !_policeArrived)
        {
            Log("PoliceCalled is true and police not yet dispatched. Initiating police response.");
            Vector3 locationForPolice = (_incidentLocation != Vector3.Zero) ? _incidentLocation : player.Position;
            DispatchPolice(locationForPolice);
        }
        else if (_policeDispatched && !_policeArrived && !_policeInteractionActive && !_makingPoliceDecision)
        {
            CheckPoliceArrival();
        }
        else if (_policeInteractionActive)
        {
            ProcessPoliceInteraction();
        }
        else if (_makingPoliceDecision)
        {
            ProcessPoliceDecisionOutcome();
        }
        else if (_npcReacting)
        {
            ProcessNpcReaction();
        }
        else 
        {
            if (_policeDispatched || _policeArrived) {
                 if((_policeDispatched || _policeArrived) && !PoliceCalled && !_makingPoliceDecision && !_policeInteractionActive) { 
                    Log("Anomaly detected: Police dispatched/arrived but no incident active. Resetting.");
                    FullResetOfScriptStates();
                 }
                 return;
            }

            Vehicle playerVeh = player.CurrentVehicle; 
            // Permettre la détection même si le joueur n'est plus dans le véhicule (après un accident récent)
            if (playerVeh == null && DateTime.Now > _stateTimer + TimeSpan.FromSeconds(30)) return;
            
            // Si le joueur vient de descendre de son véhicule récemment, ne pas chercher de nouveaux accidents
            if (playerVeh == null) return;

            Vehicle[] nearbyVehicles = World.GetNearbyVehicles(playerVeh.Position, 7.0f)
                                             .Where(v => v != null && v.Exists() && v != playerVeh)
                                             .ToArray();

            foreach (Vehicle npcVehicle in nearbyVehicles)
            {
                if (npcVehicle == null || !npcVehicle.Exists()) continue; 
                if (_npcCollisionCooldowns.ContainsKey(npcVehicle.Handle) && DateTime.Now < _npcCollisionCooldowns[npcVehicle.Handle]) continue;
                
                Ped npcDriver = npcVehicle.Driver;
                if (npcDriver != null && npcDriver.Exists() && npcDriver.IsHuman && !npcDriver.IsPlayer)
                {
                    bool isTouching = playerVeh.IsTouching(npcVehicle); 
                    bool playerDamagedNpc = playerVeh.HasBeenDamagedBy(npcVehicle); 
                    bool npcDamagedPlayer = npcVehicle.HasBeenDamagedBy(playerVeh);
                    
                    // Détection de collision STRICTE - seulement pour de vraies collisions avec impact
                    float distance = playerVeh.Position.DistanceTo(npcVehicle.Position);
                    float playerSpeed = playerVeh.Speed;
                    float npcSpeed = npcVehicle.Speed;
                    
                    // Collision significative : il faut AU MOINS une des conditions suivantes :
                    // 1. Dommages réels détectés (le plus fiable)
                    // 2. Contact physique + vitesse élevée (impact réel)
                    // 3. Distance très proche + vitesse très élevée (collision imminente/en cours)
                    
                    bool realDamage = playerDamagedNpc || npcDamagedPlayer;
                    bool significantImpact = isTouching && (playerSpeed > MinSpeedForSignificantImpact || npcSpeed > MinSpeedForSignificantImpact); // Vitesse plus élevée requise
                    bool highSpeedCollision = distance < MaxDistanceForHighSpeedCollision && playerSpeed > MinSpeedForHighSpeedCollision; // Distance très proche + vitesse très élevée
                    
                    // Une vraie collision nécessite des dommages OU un impact significatif
                    bool isRealCollision = realDamage || significantImpact || highSpeedCollision;

                    if (isRealCollision)
                    {
                        Log($"REAL Collision detected: PlayerVehicle ({playerVeh.Model.Hash}) with NPCDriver ({npcDriver.Model.Hash}) in NPCVehicle ({npcVehicle.Model.Hash}). Distance: {distance:F2}, PlayerSpeed: {playerSpeed:F2}, NPCSpeed: {npcSpeed:F2}, Touch: {isTouching}, RealDamage: {realDamage}, SigImpact: {significantImpact}, HighSpeed: {highSpeedCollision}");
                        
                        // Validate NPC is suitable for reaction
                        if (!npcDriver.IsInVehicle(npcVehicle) || npcDriver.IsDead || npcDriver.IsInjured) {
                             Log("NPC no longer valid for reaction (not in vehicle or dead/injured). Applying cooldown.");
                            _npcCollisionCooldowns[npcVehicle.Handle] = DateTime.Now + _npcCollisionCooldownDuration;
                            continue; 
                        }
                        
                        FullResetOfScriptStates(); 

                        _collidedNpcPed = npcDriver;
                        _collidedNpcVehicle = npcVehicle; 

                        _incidentLocation = npcVehicle.Position; 
                        _npcReacting = true;
                        _stateTimer = DateTime.Now; 

                        // Determine reaction type
                        int randomChance = _random.Next(0, 100);
                        if (randomChance < AggressiveNpcChancePercent) {
                            _currentReaction = NpcReactionType.Aggressive;
                        } else {
                            _currentReaction = NpcReactionType.CallPolice;
                        }
                        
                        Log($"NPC {_collidedNpcPed.Handle} will react with: {_currentReaction} (roll: {randomChance}) at {_incidentLocation}");
                        _npcCollisionCooldowns[npcVehicle.Handle] = DateTime.Now + _npcCollisionCooldownDuration;
                        GTA.UI.Notification.PostTicker($"NPC {npcDriver.Handle} is reacting to collision with type: {_currentReaction}!", false, false);
                        return; // Process reaction on next tick
                    }
                    else if (distance < 3.0f || isTouching)
                    {
                        // Log pour débogage : proximité détectée mais pas de collision valide
                        Log($"Near miss detected: PlayerVehicle with NPCVehicle ({npcVehicle.Model.Hash}). Distance: {distance:F2}, PlayerSpeed: {playerSpeed:F2}, NPCSpeed: {npcSpeed:F2}, Touch: {isTouching}, RealDamage: {realDamage} - NO collision triggered");
                    }
                }
            }
        }
    }
    
    private void ProcessNpcReaction()
    {
        if (_collidedNpcPed == null || !_collidedNpcPed.Exists() || _collidedNpcPed.IsDead) {
            Log("Collided NPC is no longer valid. Resetting NPC reaction state.");
            ResetNpcReactionState();
            return;
        }
        if (DateTime.Now > _stateTimer + _npcReactionTimeoutDuration) {
            Log($"NPC reaction for {_collidedNpcPed.Handle} timed out. Resetting.");
            ResetNpcReactionState();
            return;
        }

        // Check if NPC is still in vehicle and needs to exit
        if (_collidedNpcVehicle != null && _collidedNpcVehicle.Exists() && _collidedNpcPed.IsInVehicle(_collidedNpcVehicle))
        {
            // Force NPC to exit vehicle if not already doing so
            bool isLeavingVehicle = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, _collidedNpcPed.Handle, 2); // Task type 2 = exiting vehicle
            if (!isLeavingVehicle) 
            {
                Log($"Forcing NPC {_collidedNpcPed.Handle} to leave vehicle {_collidedNpcVehicle.Handle}.");
                _collidedNpcPed.Task.LeaveVehicle(_collidedNpcVehicle, false);
                return; // Wait for next tick
            }
            else
            {
                Log($"NPC {_collidedNpcPed.Handle} is already exiting vehicle, waiting...");
                return; // Still exiting, wait
            }
        }
        
        // NPC is now out of vehicle, execute the reaction
        switch (_currentReaction)
        {
            case NpcReactionType.Aggressive:
                Log($"NPC {_collidedNpcPed.Handle} is out of vehicle. Becoming aggressive towards player.");
                _collidedNpcPed.Task.ClearAll(); 
                Script.Wait(100); // Small delay to ensure task clear
                
                // Empêcher le NPC de remonter dans le véhicule
                _collidedNpcPed.BlockPermanentEvents = true;
                _collidedNpcPed.CanRagdoll = true;
                
                // Marquer le véhicule comme plus nécessaire pour éviter que le NPC y retourne
                if (_collidedNpcVehicle != null && _collidedNpcVehicle.Exists()) {
                    _collidedNpcVehicle.MarkAsNoLongerNeeded();
                }
                
                _collidedNpcPed.Task.Combat(Game.Player.Character);
                Log($"NPC {_collidedNpcPed.Handle} set to combat mode against player.");
                ResetNpcReactionState(); 
                break;
                
            case NpcReactionType.CallPolice:
                // Check if we haven't called police yet
                if (!PoliceCalled) 
                {
                    Log($"NPC {_collidedNpcPed.Handle} is calling the police!");
                    
                    // Make NPC look scared and call police
                    _collidedNpcPed.Task.ClearAll();
                    Script.Wait(100);
                    
                    // Empêcher le NPC de partir ou de remonter dans le véhicule
                    _collidedNpcPed.BlockPermanentEvents = true;
                    
                    // Marquer le véhicule comme plus nécessaire pour éviter que le NPC y retourne
                    if (_collidedNpcVehicle != null && _collidedNpcVehicle.Exists()) {
                        _collidedNpcVehicle.MarkAsNoLongerNeeded();
                    }
                    
                    // Use hands up animation and make NPC stay at location
                    _collidedNpcPed.Task.HandsUp(5000); // Hands up for 5 seconds
                    
                    // Set police called flag
                    PoliceCalled = true; 
                    _stateTimer = DateTime.Now; 
                    Log($"PoliceCalled flag set to true by NPC {_collidedNpcPed.Handle}. Incident at: {_incidentLocation}");
                    GTA.UI.Notification.PostTicker("An NPC is calling the police!", false, false);
                    
                    // Don't reset - keep the NPC around for police interaction
                    _npcReacting = false; // Stop the reaction loop but keep the NPC alive
                }
                else
                {
                    // Police already called, make NPC wait nearby and NOT flee
                    // Force NPC to stay at incident location permanently
                    _collidedNpcPed.BlockPermanentEvents = true;
                    
                    if (_collidedNpcPed.Position.DistanceTo(_incidentLocation) > 8f)
                    {
                        _collidedNpcPed.Task.ClearAll();
                        Script.Wait(50);
                        _collidedNpcPed.Task.FollowNavMeshTo(_incidentLocation);
                        Log($"NPC {_collidedNpcPed.Handle} too far from incident, bringing back to scene.");
                    }
                    else
                    {
                        // S'assurer que le NPC reste sur place et ne fuit pas
                        // Utiliser GuardCurrentPosition pour qu'il reste vraiment sur place
                        if (!_collidedNpcPed.IsWalking && !_collidedNpcPed.IsRunning) {
                            _collidedNpcPed.Task.ClearAll();
                            Script.Wait(50);
                            _collidedNpcPed.Task.GuardCurrentPosition();
                            Log($"NPC {_collidedNpcPed.Handle} set to guard current position.");
                        }
                    }
                }
                break;
        }
    }

    private void CheckPoliceArrival()
    {
        if (_policeVehicle == null || !_policeVehicle.Exists()) {
             Log("Police vehicle does not exist. Aborting police response.");
             CleanUpPolice(); 
             return;
        }
         if (_respondingPolicePeds.All(p => p == null || !p.Exists() || p.IsDead)) {
            Log("All responding police officers are gone or dead. Aborting police response.");
            CleanUpPolice();
            return;
        }

        // Check if player has fled the scene - vérification plus intelligente
        Ped player = Game.Player.Character;
        if (player != null && player.Exists()) 
        {
            float playerDistanceToIncident = player.Position.DistanceTo(_incidentLocation);
            
            // Le joueur a vraiment quitté la scène seulement s'il est très loin de l'incident
            // ET qu'il n'y a pas d'interaction active (pour éviter les faux positifs pendant l'interaction)
            bool playerHasFled = playerDistanceToIncident > 80f && !_policeInteractionActive && !_makingPoliceDecision; // Augmenté de 60f à 80f et ajouté conditions
            
            if (playerHasFled)
            {
                Log($"Player has truly fled the scene. Distance to incident: {playerDistanceToIncident:F2}m. Setting wanted level.");
                GTA.UI.Notification.PostTicker("You left the scene of an accident! Police are now searching for you.", false, false);
                Game.Player.Wanted.SetWantedLevel(1, false);
                CleanUpPolice();
                return;
            }
        }

        Ped driver = _policeVehicle.Driver;
        float distanceToIncident = _policeVehicle.Position.DistanceTo(_incidentLocation);
        
        // Navigation simplifiée - vérifier seulement toutes les 20 secondes
        bool needsNavigationCheck = _lastPoliceRedirectionTime == DateTime.MinValue || 
                                   DateTime.Now > _lastPoliceRedirectionTime + TimeSpan.FromSeconds(20);
        
        if (needsNavigationCheck && distanceToIncident > 25f) // Seulement si vraiment loin
        {
            _lastPoliceRedirectionTime = DateTime.Now;
            
            // Vérifier si la police progresse
            bool isProgressingTowardsTarget = false;
            bool isActuallyStuck = false;
            
            if (_lastRecordedDistance != float.MaxValue)
            {
                float distanceChange = _lastRecordedDistance - distanceToIncident;
                float timeSinceLastCheck = (float)(DateTime.Now - _lastProgressTime).TotalSeconds;
                
                // La police progresse si elle se rapproche ou si elle bouge à vitesse raisonnable
                isProgressingTowardsTarget = (distanceChange > 1f && timeSinceLastCheck > 0) || _policeVehicle.Speed > 2f;
                isActuallyStuck = _policeVehicle.Speed < PoliceStuckSpeedThreshold && distanceChange < 0.5f && timeSinceLastCheck > 15f;
                
                Log($"Police navigation check - Distance: {distanceToIncident:F2}, Speed: {_policeVehicle.Speed:F2}, DistanceChange: {distanceChange:F2}, Stuck: {isActuallyStuck}, Progressing: {isProgressingTowardsTarget}");
            }
            
            _lastRecordedDistance = distanceToIncident;
            _lastProgressTime = DateTime.Now;
            
            // Navigation correction seulement si vraiment bloqué
            bool needsNavigationCorrection = isActuallyStuck && distanceToIncident > 30f;
            
            if (needsNavigationCorrection && _navigationRetryCount < MaxNavigationRetries)
            {
                CorrectPoliceNavigation(distanceToIncident, false);
                return;
            }
            
            // Fallback rapide si vraiment bloqué longtemps
            if (_navigationRetryCount >= MaxNavigationRetries && distanceToIncident > 40f && 
                DateTime.Now > _stateTimer + TimeSpan.FromMinutes(1.5f)) // Réduit de 3 à 1.5 minutes
            {
                Log("Navigation failed. Implementing emergency teleportation.");
                EmergencyPoliceReposition();
                return;
            }
        }
        
        // Critères d'arrivée plus flexibles
        bool arrivedAtScene = distanceToIncident < PoliceArrivalDistanceThreshold || 
                             (distanceToIncident < ForceArrivalDistance && _policeVehicle.Speed < 5.0f) || // Plus tolérant sur la vitesse
                             (distanceToIncident < 15.0f); // Force l'arrivée si proche mais plus près qu'avant
        
        if (driver != null && driver.Exists() && !driver.IsDead && arrivedAtScene)
        {
            Log($"Police arrived at scene. Distance: {distanceToIncident:F2}, Speed: {_policeVehicle.Speed:F2}");
            ForcePoliceArrival();
        }
        else if (driver != null && driver.Exists() && driver.IsDead) 
        {
            Log("Police driver died en route. Aborting police response.");
            CleanUpPolice();
        }
        else if (DateTime.Now > _stateTimer + _policeDispatchTimeoutDuration) 
        {
            Log("Police dispatch timed out. Cleaning up.");
            CleanUpPolice(); 
        }
        else
        {
            // Log progress moins fréquemment
            if (DateTime.Now > _lastPoliceRedirectionTime + TimeSpan.FromSeconds(30))
            {
                Log($"Police en route - Distance: {distanceToIncident:F2}, Speed: {_policeVehicle.Speed:F2}, Retries: {_navigationRetryCount}");
            }
        }
    }
    
    private void CorrectPoliceNavigation(float currentDistance, bool movingAway)
    {
        if (_policeVehicle == null || !_policeVehicle.Exists()) return;
        
        Ped driver = _policeVehicle.Driver;
        if (driver == null || !driver.Exists() || driver.IsDead) return;
        
        _navigationRetryCount++;
        Log($"Correcting police navigation (attempt {_navigationRetryCount}/{MaxNavigationRetries})");
        
        // Clear current task
        driver.Task.ClearAll();
        Script.Wait(300); // Wait for task clearing
        
        Vector3 targetPosition = _incidentLocation;
        
        // Strategies simplifiées et plus efficaces
        switch (_navigationRetryCount)
        {
            case 1:
                // Première tentative: navigation directe avec flags agressifs
                Log("Navigation correction 1: Direct path with aggressive flags");
                driver.Task.DriveTo(_policeVehicle, targetPosition, 8.0f, 
                                  VehicleDrivingFlags.AllowGoingWrongWay | 
                                  VehicleDrivingFlags.ForceStraightLine | 
                                  VehicleDrivingFlags.UseShortCutLinks, 
                                  PoliceDriveSpeed + 20f);
                break;
                
            case 2:
                // Deuxième tentative: trouver le point le plus proche sur la route
                Vector3 nearestRoad = World.GetNextPositionOnStreet(_incidentLocation, true);
                if (nearestRoad == Vector3.Zero || nearestRoad.DistanceTo(_incidentLocation) > 30f)
                {
                    nearestRoad = _incidentLocation;
                }
                
                Log($"Navigation correction 2: Targeting nearest road at {nearestRoad}");
                driver.Task.DriveTo(_policeVehicle, nearestRoad, 10.0f, 
                                  VehicleDrivingFlags.AllowGoingWrongWay | 
                                  VehicleDrivingFlags.ForceStraightLine, 
                                  PoliceDriveSpeed + 30f);
                break;
                
            default:
                // Dernière tentative: utilisation des natives GTA directement
                Log("Navigation correction 3: Using native GTA driving task");
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, driver.Handle, _policeVehicle.Handle, 
                             targetPosition.X, targetPosition.Y, targetPosition.Z, 
                             PoliceDriveSpeed + 40f, 1, _policeVehicle.Model.Hash, 
                             (int)(VehicleDrivingFlags.AllowGoingWrongWay | VehicleDrivingFlags.ForceStraightLine), 
                             5.0f, -1);
                break;
        }
        
        // Reset progress tracking
        _lastProgressTime = DateTime.Now;
        _lastRecordedDistance = currentDistance;
    }
    
    private void EmergencyPoliceReposition()
    {
        if (_policeVehicle == null || !_policeVehicle.Exists())
        {
            Log("Emergency police reposition failed: Police vehicle does not exist");
            return;
        }
        
        Log("Emergency police reposition initiated");
        
        // Find a good position near the incident
        Vector3 emergencyPosition = FindEmergencyPolicePosition();
        
        if (emergencyPosition != Vector3.Zero)
        {
            Log($"Teleporting police vehicle to emergency position: {emergencyPosition}");
            
            // Stop the vehicle
            _policeVehicle.Speed = 0f;
            
            // Teleport vehicle
            _policeVehicle.Position = emergencyPosition;
            _policeVehicle.PlaceOnGround();
            _policeVehicle.Heading = Utility.GetHeadingFromVector(_incidentLocation - emergencyPosition);
            
            Script.Wait(100);
            
            // Resume driving to incident
            Ped driver = _policeVehicle.Driver;
            if (driver != null && driver.Exists() && !driver.IsDead)
            {
                driver.Task.DriveTo(_policeVehicle, _incidentLocation, 5.0f, 
                                  VehicleDrivingFlags.AllowGoingWrongWay, 
                                  PoliceDriveSpeed);
            }
            
            // Reset counters
            _navigationRetryCount = 0;
            _lastRecordedDistance = emergencyPosition.DistanceTo(_incidentLocation);
        }
        else
        {
            Log("Could not find suitable emergency position. Forcing arrival.");
            ForcePoliceArrival();
        }
    }
    
    private Vector3 FindEmergencyPolicePosition()
    {
        // Try multiple positions around the incident
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * (float)(Math.PI / 180f);
            float distance = 25f + (i * 5f); // 25m to 60m
            
            Vector3 offset = new Vector3(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance,
                0f
            );
            
            Vector3 candidatePos = _incidentLocation + offset;
            Vector3 roadPos = World.GetNextPositionOnStreet(candidatePos);
            
            if (roadPos != Vector3.Zero && roadPos.DistanceTo(_incidentLocation) < 80f)
            {
                Log($"Found emergency position {i}: {roadPos} (distance to incident: {roadPos.DistanceTo(_incidentLocation):F2}m)");
                return roadPos;
            }
        }
        
        Log("No suitable emergency position found");
        return Vector3.Zero;
    }
    
    private void ForcePoliceArrival()
    {
        if (_policeVehicle == null || !_policeVehicle.Exists())
        {
            Log("Force police arrival failed: Police vehicle does not exist");
            return;
        }
        
        Log("Forcing police arrival");
        
        // Stop vehicle completely
        _policeVehicle.Speed = 0f;
        _policeVehicle.IsSirenActive = false;
        _policeVehicle.IsEngineRunning = false;
        
        // Force all officers out immediately
        foreach (Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive))
        {
            if (officer.IsInVehicle(_policeVehicle))
            {
                Log($"Forcing officer {officer.Handle} to exit vehicle immediately");
                ForceOfficerOutOfVehicle(officer, _policeVehicle);
            }
        }
        
        // Set arrival state
        _policeArrived = true;
        _policeInteractionActive = true;
        _stateTimer = DateTime.Now;
        _policeInteractionStage = -1;
        
        // Reset navigation tracking
        ResetNavigationTracking();
        
        Log("Police arrival forced and interaction initiated");
    }
    
    private void ResetNavigationTracking()
    {
        _lastRecordedDistance = float.MaxValue;
        _lastProgressTime = DateTime.MinValue;
        _navigationRetryCount = 0;
        _lastPoliceRedirectionTime = DateTime.MinValue;
        _lastPolicePosition = Vector3.Zero;
        _policeStuckStartTime = DateTime.MinValue;
    }

    private void ProcessPoliceInteraction()
    {
        Ped player = Game.Player.Character;
        Ped leadOfficer = _respondingPolicePeds.FirstOrDefault(p => p != null && p.Exists() && p.IsAlive); 

        if (leadOfficer == null || player == null || !player.Exists()) { 
            Log("Lead officer or player is invalid. Ending police interaction.");
            EndPoliceInteraction(); return; 
        }

        // Gérer les contrôles du joueur de manière plus intelligente
        bool shouldDisableControls = _policeInteractionStage >= 0 && _policeInteractionStage != 1; // Permettre les contrôles pour le menu (stage 1)
        
        if (shouldDisableControls && !_playerControlDisabled) {
            Log("Disabling player control for active police interaction.");
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
            _playerControlDisabled = true;
        } else if (!shouldDisableControls && _playerControlDisabled) {
            Log("Re-enabling player control for menu interaction.");
            Function.Call(Hash.ENABLE_ALL_CONTROL_ACTIONS, 0);
            _playerControlDisabled = false;
        }
        
        // Ne pas forcer le regard si le menu est affiché
        if (_policeInteractionStage != 1 || !_showingResponseMenu) {
            player.Task.LookAt(leadOfficer);
        }
        
        uint taskGotoEntityHash = (uint)Function.Call<int>(Hash.GET_HASH_KEY, "TASK_GOTO_ENTITY"); // Used for native task checks
        uint taskGotoCoordHash = (uint)Function.Call<int>(Hash.GET_HASH_KEY, "TASK_GOTO_COORD_ANY_PED"); // Native task hash

        if (!leadOfficer.IsRagdoll && 
            (uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, leadOfficer.Handle, -1) != taskGotoEntityHash &&
            (uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, leadOfficer.Handle, -1) != taskGotoCoordHash 
            ) { 
            leadOfficer.Task.LookAt(player);
        }

        switch (_policeInteractionStage)
        {
            case -1: 
                Log($"Police interaction stage -1. Officers count: {_respondingPolicePeds.Count(o => o != null && o.Exists() && o.IsAlive)}");
                
                // Compte des policiers encore dans le véhicule
                int officersStillInVehicle = 0;
                int totalValidOfficers = 0;
                
                foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) 
                {
                    totalValidOfficers++;
                    if(_policeVehicle != null && _policeVehicle.Exists() && officer.IsInVehicle(_policeVehicle)) 
                    {
                        officersStillInVehicle++;
                        Log($"Officer {officer.Handle} still in vehicle, forcing exit immediately.");
                        
                        // Forcer immédiatement la sortie sans attendre
                        ForceOfficerOutOfVehicle(officer, _policeVehicle);
                    }
                }
                
                Log($"Officers status: {officersStillInVehicle}/{totalValidOfficers} still in vehicle");
                
                // Si TOUS les officiers sont sortis, passer à l'approche
                if (officersStillInVehicle == 0 && totalValidOfficers > 0) {
                    Log($"All officers confirmed out of vehicle. Starting approach phase.");
                    _policeInteractionStage = 0; // Passer directement à l'approche
                    _stateTimer = DateTime.Now;
                    break;
                }
                
                // Timeout beaucoup plus court pour forcer la sortie
                if (DateTime.Now > _stateTimer + TimeSpan.FromSeconds(5)) {
                    Log($"Timeout reached - forcing all officers out immediately regardless of status.");
                    
                    // Force absolue : téléporter tous les officiers hors du véhicule
                    foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                        if(_policeVehicle != null && _policeVehicle.Exists() && officer.IsInVehicle(_policeVehicle)) {
                            Log($"Emergency: Force teleporting officer {officer.Handle} out of vehicle.");
                            
                            // Méthode ultra-directe
                            Vector3 exitPos = _policeVehicle.Position + _policeVehicle.RightVector * (4.0f + _respondingPolicePeds.IndexOf(officer) * 2.0f);
                            exitPos.Z = _policeVehicle.Position.Z + 1.0f;
                            
                            // Téléportation immédiate
                            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, officer.Handle, exitPos.X, exitPos.Y, exitPos.Z, true, true, true);
                            Script.Wait(100);
                            
                            // S'assurer qu'il ne retourne pas dans le véhicule
                            officer.BlockPermanentEvents = true;
                            Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 429, true); // Can't enter vehicles
                        }
                    }
                    
                    // Forcer le passage à l'étape suivante
                    Log("Emergency timeout - forcing progression to approach stage.");
                    _policeInteractionStage = 0;
                    _stateTimer = DateTime.Now;
                }
                break;
                
            case 0: // Approche des officiers
                // Amélioration du positionnement des officiers - plus proche et devant le joueur
                bool allOfficersReady = true;
                
                foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                    float distanceToPlayer = officer.Position.DistanceTo(player.Position);
                    
                    if (distanceToPlayer > OfficerApproachDistanceThreshold) { // Utilise le nouveau seuil plus proche
                        // Positionner les officiers devant le joueur, plus près de l'incident
                        Vector3 playerForward = player.ForwardVector;
                        Vector3 incidentDirection = (_incidentLocation - player.Position).Normalized;
                        
                        // Mélanger la direction vers l'incident et vers l'avant du joueur
                        Vector3 approachDirection = (playerForward + incidentDirection * 1.5f).Normalized;
                        
                        // Position devant le joueur, plus proche
                        Vector3 approachPos = player.Position + approachDirection * 4.0f; // Réduit de 8.0f à 4.0f
                        
                        // Décaler légèrement sur les côtés pour éviter que les officiers se chevauchent
                        float sideOffset = ((_respondingPolicePeds.IndexOf(officer) % 2 == 0) ? -1.5f : 1.5f);
                        approachPos += player.RightVector * sideOffset;
                        
                        // S'assurer que la position finale est proche de l'incident
                        float distanceToIncident = approachPos.DistanceTo(_incidentLocation);
                        if (distanceToIncident > 10.0f) {
                            // Ajuster vers l'incident si trop loin
                            Vector3 toIncident = (_incidentLocation - approachPos).Normalized;
                            approachPos += toIncident * 3.0f;
                        }
                        
                        // Seulement tasker si nécessaire et limiter la fréquence
                        if (!_officerLastTaskedTime.ContainsKey(officer.Handle) || 
                            DateTime.Now > _officerLastTaskedTime[officer.Handle] + TimeSpan.FromSeconds(2))
                        {
                            if (!officer.IsWalking && !officer.IsRunning) {
                                officer.Task.FollowNavMeshTo(approachPos);
                                Log($"Officer {officer.Handle} tasked to approach player frontally (distance: {distanceToPlayer:F2}m).");
                                _officerLastTaskedTime[officer.Handle] = DateTime.Now;
                            }
                        }
                        allOfficersReady = false;
                    } else {
                        // Assez proche - arrêter et regarder le joueur, se positionner face à face
                        if (officer.IsWalking || officer.IsRunning) {
                            officer.Task.StandStill(-1);
                            Script.Wait(50);
                            
                            // S'orienter vers le joueur
                            Vector3 lookDirection = (player.Position - officer.Position).Normalized;
                            float heading = Utility.GetHeadingFromVector(lookDirection);
                            officer.Heading = heading;
                            
                            officer.Task.LookAt(player);
                        }
                    }
                }
                
                // Timeout d'approche plus court
                if (allOfficersReady || DateTime.Now > _stateTimer + TimeSpan.FromSeconds(8)) { // Réduit de 10 à 8 secondes
                    Log("Officers positioned in front of player. Starting dialogue.");
                    _policeInteractionStage = 1; // Passer au dialogue
                    _stateTimer = DateTime.Now;
                }
                break;
                
            case 1: 
                if (DateTime.Now < _stateTimer + TimeSpan.FromSeconds(1)) return; 
                GTA.UI.Notification.PostTicker("Officer: We received a report of a vehicle collision here.", false, false);
                Log("Interaction Stage 1: Officer statement 1.");
                _policeInteractionStage++; _stateTimer = DateTime.Now; 
                break;
            case 2: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                
                if (!_showingResponseMenu) {
                    GTA.UI.Notification.PostTicker("Officer: What's your side of the story?", false, false);
                    Log("Interaction Stage 2: Officer asks player - showing response menu.");
                    _showingResponseMenu = true;
                    _selectedResponseIndex = 0; // Reset selection to first option
                    
                    // S'assurer que les contrôles sont activés pour le menu
                    if (_playerControlDisabled) {
                        Function.Call(Hash.ENABLE_ALL_CONTROL_ACTIONS, 0);
                        _playerControlDisabled = false;
                        Log("Enabling controls for response menu.");
                    }
                    return;
                }
                
                // Display response menu
                DrawResponseMenu();
                
                // Don't proceed until player selects a response
                return;
                
            case 3: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                if (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive && _incidentLocation.DistanceTo(_collidedNpcPed.Position) < 25f) 
                {
                    if (leadOfficer != null && leadOfficer.Exists()) _collidedNpcPed.Task.LookAt(leadOfficer);
                    else if(player != null && player.Exists()) _collidedNpcPed.Task.LookAt(player);
                    string npcStatement = $"NPC ({_collidedNpcPed.Handle}): They crashed right into me! It was their fault!";
                    GTA.UI.Notification.PostTicker(npcStatement, false, false); 
                    Log($"Interaction Stage 3: NPC statement: {npcStatement}");
                } else {
                    Log("Interaction Stage 3: Original NPC not available or too far for statement.");
                    GTA.UI.Notification.PostTicker("Officer: The other party isn't present to give a statement.", false, false);
                }
                _policeInteractionStage++; _stateTimer = DateTime.Now;
                break;
            case 4: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                GTA.UI.Notification.PostTicker("Officer: Alright, let me assess the situation...", false, false);
                Log("Interaction Stage 4: Officer assessing.");
                _policeInteractionStage++; _stateTimer = DateTime.Now; 
                break;
            case 5: 
                if (DateTime.Now < _stateTimer + _policeDecisionPauseDuration) return;
                Log("Transitioning to police decision making.");
                _policeInteractionActive = false; _makingPoliceDecision = true;     
                _stateTimer = DateTime.Now; 
                DecidePoliceAction(); 
                break;
        }
    }
    
    private void DecidePoliceAction() {
        Log("Deciding police action...");
        bool playerCooperative = true;
        bool admitFault = false;
        string lowerPlayerResponse = _playerResponse.ToLower();

        if (lowerPlayerResponse.Contains("my fault") || lowerPlayerResponse.Contains("sorry") || lowerPlayerResponse.Contains("accidentally") || lowerPlayerResponse.Contains("i messed up")) {
            admitFault = true; Log("Player admitted fault or apologized.");
        }
        if (lowerPlayerResponse.Contains("fuck") || lowerPlayerResponse.Contains("asshole") || lowerPlayerResponse.Contains("piss off") || 
            lowerPlayerResponse.Contains("stupid") || lowerPlayerResponse.Contains("idiot") || lowerPlayerResponse.Contains("jerk") || 
            lowerPlayerResponse.Contains("shut up") || lowerPlayerResponse.Contains("damn cop")) {
            playerCooperative = false; Log("Player was uncooperative/aggressive in response.");
        }
        if (lowerPlayerResponse == "i have nothing to say." || string.IsNullOrWhiteSpace(_playerResponse)) {
            playerCooperative = false; Log("Player was evasive.");
        }

        bool npcAccusatory = (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive);
        Ped player = Game.Player.Character;

        if (player != null && player.Exists() && Game.Player.Wanted.WantedLevel > 0) { 
             _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (already wanted).");
        } else if (admitFault && !playerCooperative) {
            _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (admitted fault but aggressive).");
        } else if (!playerCooperative && npcAccusatory) {
            int chance = _random.Next(0, 3);
            if (chance == 0) { _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (uncooperative and NPC accuses, 33% chance)."); }
            else if (chance == 1) { _currentPoliceDecision = PoliceDecision.GiveFine; Log("Decision: Give Fine (uncooperative and NPC accuses, 33% chance)."); }
            else { _currentPoliceDecision = PoliceDecision.LetPlayerGo; Log("Decision: Let Player Go (uncooperative but lucky, 33% chance)."); }
        } else if (admitFault && playerCooperative) {
            _currentPoliceDecision = PoliceDecision.GiveFine; Log("Decision: Give Fine (admitted fault but cooperative).");
        } else if (!playerCooperative) {
             if (_random.Next(0,2) == 0) { _currentPoliceDecision = PoliceDecision.GiveFine; Log("Decision: Give Fine (uncooperative, 50% chance).");}
             else { _currentPoliceDecision = PoliceDecision.LetPlayerGo; Log("Decision: Let Player Go (uncooperative but lucky, 50% chance).");}
        } else {
            _currentPoliceDecision = PoliceDecision.LetPlayerGo; Log("Decision: Let Player Go (cooperative).");
        }
        _stateTimer = DateTime.Now; 
    }

    private void ProcessPoliceDecisionOutcome()
    {
        Ped player = Game.Player.Character;
        Ped arrestingOfficer = _respondingPolicePeds.FirstOrDefault(p => p != null && p.Exists() && p.IsAlive);
        
        if (player == null || !player.Exists()) { Log("Player is invalid, cannot process decision outcome."); EndPoliceInteraction(); return; }

        if (arrestingOfficer == null && _currentPoliceDecision == PoliceDecision.ArrestPlayer) {
            Log("No officer available to perform arrest. Letting player go by default.");
            _currentPoliceDecision = PoliceDecision.LetPlayerGo; 
            _stateTimer = DateTime.Now; 
        }

        switch (_currentPoliceDecision)
        {
            case PoliceDecision.ArrestPlayer:
                bool alreadyArrested = player.IsCuffed || Function.Call<bool>(Hash.IS_PED_BEING_ARRESTED, player.Handle);
                if (!alreadyArrested) {
                    GTA.UI.Notification.PostTicker("Officer: You're coming with us!", false, false);
                    if (arrestingOfficer != null && arrestingOfficer.Exists()) 
                    {
                        Log($"Officer {arrestingOfficer.Handle} is arresting player {player.Handle}.");
                        arrestingOfficer.Task.Arrest(player);
                    }
                }
                
                if (DateTime.Now > _stateTimer + _policeArrestSequenceDuration || player.IsCuffed || Function.Call<bool>(Hash.IS_PED_BEING_ARRESTED, player.Handle)) {
                    GTA.UI.Screen.ShowSubtitle("~r~BUSTED!", 5000);
                    Log("Player busted. Ending interaction.");
                    int currentWantedLevel = Game.Player.Wanted.WantedLevel; 
                    Game.Player.Wanted.SetWantedLevel(Math.Max(currentWantedLevel, 1), false);
                    EndPoliceInteraction(true); 
                }
                break;

            case PoliceDecision.GiveFine:
                if (!_fineAlreadyGiven && DateTime.Now >= _stateTimer + TimeSpan.FromSeconds(0.5)) { 
                     int fineAmount = _random.Next(200, 1000);
                     GTA.UI.Notification.PostTicker($"Officer: You're getting a ${fineAmount} fine for reckless driving.", false, false);
                     Log($"Police giving player fine of ${fineAmount}.");
                     
                     // Remove money from player (if they have it)
                     if (Game.Player.Money >= fineAmount) {
                         Game.Player.Money -= fineAmount;
                         GTA.UI.Notification.PostTicker($"~r~-${fineAmount} Fine paid", false, false);
                     } else {
                         GTA.UI.Notification.PostTicker("~r~Insufficient funds! You'll need to pay at the police station.", false, false);
                     }
                     
                     _fineAlreadyGiven = true; // Marquer l'amende comme déjà donnée
                }

                if (DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration)
                {
                    Log("Police finished with fine interaction. Sending police away naturally.");
                    _policeInteractionActive = false;
                    _makingPoliceDecision = false;
                    SendPoliceAway();
                    
                    // Faire retourner le NPC à son véhicule
                    if (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive) {
                        Log("Sending NPC back to vehicle after fine.");
                        _collidedNpcPed.BlockPermanentEvents = false;
                        Function.Call(Hash.SET_PED_CONFIG_FLAG, _collidedNpcPed.Handle, 429, false);
                        SendNpcBackToVehicle();
                        _collidedNpcPed = null;
                        _collidedNpcVehicle = null;
                        _currentReaction = NpcReactionType.None;
                    }
                }
                break;

            case PoliceDecision.LetPlayerGo:
                if (!_decisionMessageShown && DateTime.Now >= _stateTimer + TimeSpan.FromSeconds(0.5)) { 
                     GTA.UI.Notification.PostTicker("Officer: Alright, be more careful next time. You're free to go.", false, false);
                     Log("Police letting player go.");
                     _decisionMessageShown = true; // Marquer le message comme affiché
                }

                if (DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration)
                {
                    Log("Police finished letting player go. Sending police away naturally.");
                    _policeInteractionActive = false;
                    _makingPoliceDecision = false;
                    SendPoliceAway();
                    
                    // Faire retourner le NPC à son véhicule
                    if (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive) {
                        Log("Sending NPC back to vehicle after fine.");
                        _collidedNpcPed.BlockPermanentEvents = false;
                        Function.Call(Hash.SET_PED_CONFIG_FLAG, _collidedNpcPed.Handle, 429, false);
                        SendNpcBackToVehicle();
                        _collidedNpcPed = null;
                        _collidedNpcVehicle = null;
                        _currentReaction = NpcReactionType.None;
                    }
                }
                break;
        }
    }
    
    private void ResetNpcReactionState() {
        Log($"Resetting NPC reaction state for Handle: {(_collidedNpcPed?.Handle.ToString() ?? "N/A")}.");
        
        // Si c'est un NPC agressif, ne pas le nettoyer immédiatement - laisser le combat continuer
        if (_currentReaction == NpcReactionType.Aggressive && _collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive) {
            Log($"NPC {_collidedNpcPed.Handle} is aggressive, keeping for combat but not managing vehicle return.");
            // Ne pas marquer comme plus nécessaire pour permettre au combat de continuer
            // Mais s'assurer qu'il ne retourne pas au véhicule pendant le combat
            _collidedNpcPed.BlockPermanentEvents = true;
        } else if (_currentReaction == NpcReactionType.CallPolice && _collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive) {
            Log($"NPC {_collidedNpcPed.Handle} called police, keeping around for police interaction.");
            // Garder le NPC en vie pour l'interaction avec la police - il retournera à son véhicule après
            _collidedNpcPed.BlockPermanentEvents = true;
            _collidedNpcPed.Task.StandStill(-1); // Le faire rester sur place
        } else {
            // Pour les autres cas, envoyer le NPC retourner à son véhicule naturellement
            if (_collidedNpcPed != null && _collidedNpcPed.Exists()) {
                Log($"NPC {_collidedNpcPed.Handle} returning to normal behavior - going back to vehicle.");
                SendNpcBackToVehicle();
            }
        }
        
        // Réinitialiser les variables de suivi de réaction
        _npcReacting = false;
        
        // Ne réinitialiser _collidedNpcPed et _collidedNpcVehicle que si ce n'est pas un NPC qui a appelé la police
        if (_currentReaction != NpcReactionType.CallPolice) {
            // Les références seront nettoyées naturellement quand les entités seront marquées
            _collidedNpcPed = null;
            _collidedNpcVehicle = null;
            _currentReaction = NpcReactionType.None;
        }
    }

    public void CleanUpPolice(bool immediate = false) { 
        Log("Emergency cleanup of police units - immediate removal.");
        if (_policeVehicle != null && _policeVehicle.Exists()) {
            _policeVehicle.MarkAsNoLongerNeeded();
            if (immediate) _policeVehicle.Delete();
            _policeVehicle = null;
        }
        foreach (var officer in _respondingPolicePeds) {
            if (officer != null && officer.Exists()) {
                officer.MarkAsNoLongerNeeded();
                if (immediate) officer.Delete();
            }
        }
        _respondingPolicePeds.Clear();
        _policeDispatched = false; 
        _policeArrived = false;
        PoliceCalled = false; 
        _incidentLocation = Vector3.Zero;
        
        // Nettoyer les données de timing des officiers
        _officerLastTaskedTime.Clear();
        _lastOfficerTaskTime = DateTime.MinValue;
        
        ResetPoliceRedirectionStates();
        Log("Emergency police cleanup complete.");
    }

    private void EndPoliceInteraction(bool playerArrested = false, bool playerFled = false)
    {
        Log($"Ending police interaction. Player arrested: {playerArrested}, Player fled: {playerFled}");
        Ped player = Game.Player.Character;
        
        // S'assurer que les contrôles du joueur sont réactivés
        if (_playerControlDisabled) {
            Function.Call(Hash.ENABLE_ALL_CONTROL_ACTIONS, 0);
            _playerControlDisabled = false;
            Log("Player control re-enabled during interaction end.");
        }
        
        _policeInteractionActive = false;
        _makingPoliceDecision = false;
        _currentPoliceDecision = PoliceDecision.None;
        _policeInteractionStage = 0;
        _playerResponse = string.Empty;
        _showingResponseMenu = false;
        _selectedResponseIndex = 0;
        
        // Réinitialiser les nouvelles variables d'état
        _fineAlreadyGiven = false;
        _decisionMessageShown = false;
        
        // Gérer le départ de la police et du NPC de manière naturelle
        if (playerArrested || playerFled) {
            // En cas d'arrestation ou de fuite, nettoyage d'urgence
            CleanUpPolice(playerArrested); // Suppression immédiate seulement si arrestation
        } else {
            // Situation normale : faire partir la police naturellement
            SendPoliceAway();
        }
        
        // Faire retourner le NPC à son véhicule si l'interaction est terminée normalement
        if (!playerArrested && !playerFled && _collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive) {
            Log("Police interaction ended normally. Sending NPC back to vehicle.");
            // Débloquer les événements permanents pour permettre au NPC de retourner à son véhicule
            _collidedNpcPed.BlockPermanentEvents = false;
            Function.Call(Hash.SET_PED_CONFIG_FLAG, _collidedNpcPed.Handle, 429, false); // Permettre d'entrer dans les véhicules
            
            // Envoyer le NPC retourner à son véhicule
            SendNpcBackToVehicle();
            
            // Nettoyer les références après avoir lancé le processus de retour
            _collidedNpcPed = null;
            _collidedNpcVehicle = null;
            _currentReaction = NpcReactionType.None;
        }
    }

    private void FullResetOfScriptStates() {
        Log("Performing full reset of script states.");
        
        // S'assurer que les contrôles du joueur sont réactivés
        if (_playerControlDisabled) {
            Function.Call(Hash.ENABLE_ALL_CONTROL_ACTIONS, 0);
            _playerControlDisabled = false;
            Log("Player control re-enabled during full reset.");
        }
        
        ResetNpcReactionState();
        EndPoliceInteraction(false, false); 
        _npcCollisionCooldowns.Clear(); 
        _incidentLocation = Vector3.Zero;
        _playerResponse = string.Empty;
        _npcReacting = false;
        _policeDispatched = false;
        _policeArrived = false;
        _policeInteractionActive = false;
        _makingPoliceDecision = false;
        PoliceCalled = false;
        _showingResponseMenu = false;
        _selectedResponseIndex = 0;
        
        // Réinitialiser les nouvelles variables d'état
        _fineAlreadyGiven = false;
        _decisionMessageShown = false;
        
        // Nettoyer les dictionnaires de timing
        _officerLastTaskedTime.Clear();
        _lastOfficerTaskTime = DateTime.MinValue;
        _lastMenuDisplayTime = DateTime.MinValue;
        
        ResetPoliceRedirectionStates();
        ResetNavigationTracking(); // Add navigation tracking reset
        Log("Full reset complete.");
    }
   
    private void DispatchPolice(Vector3 incidentLocation) {
        if (_policeDispatched) return; 
        Log($"Dispatching police to: {incidentLocation}");
        _policeDispatched = true;
        _policeArrived = false; 
        _stateTimer = DateTime.Now;
        ResetNavigationTracking(); // Reset tracking for new dispatch

        Vector3 spawnPoint = FindOptimalPoliceSpawnPoint(incidentLocation);
        if (spawnPoint == Vector3.Zero) {
            Log("Could not find optimal spawn point. Aborting police dispatch.");
            _policeDispatched = false; 
            PoliceCalled = false; 
            return;
        }

        _policeVehicle = World.CreateVehicle(_policeVehicleModel, spawnPoint, Utility.GetHeadingFromVector(incidentLocation - spawnPoint)); 
        if (_policeVehicle == null || !_policeVehicle.Exists())
        {
            Log("Failed to spawn police vehicle.");
            _policeDispatched = false; PoliceCalled = false; return;
        }
        
        _policeVehicle.PlaceOnGround();
        _policeVehicle.IsSirenActive = true;
        _policeVehicle.IsEngineRunning = true;
        
        Log($"Police vehicle {_policeVehicle.Handle} spawned at {spawnPoint} (distance: {spawnPoint.DistanceTo(incidentLocation):F2}m)");

        _respondingPolicePeds.Clear(); 
        for (int i = 0; i < NumberOfPoliceOfficers; i++)
        {
            Vector3 officerSpawnPos = _policeVehicle.Position + _policeVehicle.RightVector * (i * 1.5f) - _policeVehicle.ForwardVector * (i * 0.5f);
            Ped officer = World.CreatePed(_policePedModel, officerSpawnPos);
            if (officer != null && officer.Exists())
            {
                _respondingPolicePeds.Add(officer);
                officer.Armor = 100;
                officer.Health = officer.MaxHealth;
                officer.Weapons.Give(WeaponHash.CombatPistol, 250, true, true);
                
                // Améliorer les capacités de conduite pour les officiers
                Function.Call(Hash.SET_DRIVER_ABILITY, officer.Handle, 1.0f);
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, officer.Handle, 0.8f);
                
                Log($"Spawned police officer {officer.Handle}");
                if (i == 0) officer.SetIntoVehicle(_policeVehicle, VehicleSeat.Driver);
                else officer.SetIntoVehicle(_policeVehicle, VehicleSeat.Passenger);
            } else Log($"Failed to spawn officer {i+1}.");
        }

        Ped driver = _policeVehicle.Driver;
        if (driver != null && driver.Exists() && _respondingPolicePeds.Contains(driver) && _policeVehicle != null && _policeVehicle.Exists()) 
        {
            Log($"Police driver {driver.Handle} is in vehicle. Tasking to drive to incident: {incidentLocation}");
            
            // Navigation initiale améliorée avec flags optimisés
            VehicleDrivingFlags initialFlags = VehicleDrivingFlags.AllowGoingWrongWay | 
                                              VehicleDrivingFlags.PreferNavmeshRoute | 
                                              VehicleDrivingFlags.UseShortCutLinks |
                                              VehicleDrivingFlags.SteerAroundObjects;
            
            // Utiliser une distance d'arrêt plus grande pour éviter les dépassements
            driver.Task.DriveTo(_policeVehicle, incidentLocation, 8.0f, initialFlags, PoliceDriveSpeed + 10f);
            
            // S'assurer que les sirènes sont actives
            _policeVehicle.IsSirenActive = true; 
        } else {
            Log("Police driver not found or not set in vehicle correctly. Cannot dispatch.");
            CleanUpPolice(); 
            return;
        }
        GTA.UI.Notification.PostTicker("Police have been dispatched to your location!", false, false);
    }
    
    private Vector3 FindOptimalPoliceSpawnPoint(Vector3 incidentLocation)
    {
        Log("Finding optimal police spawn point...");
        
        // Essayer d'abord des positions plus proches pour une navigation plus simple
        Vector3[] spawnAttempts = new Vector3[16];
        
        // Strategy 1: Cardinal directions à distance courte (nouveau)
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * (float)(Math.PI / 180f);
            float distance = 70f; // Plus proche que l'original
            Vector3 offset = new Vector3(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance,
                0f
            );
            spawnAttempts[i] = incidentLocation + offset;
        }
        
        // Strategy 2: Cardinal directions à distance moyenne
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * (float)(Math.PI / 180f);
            float distance = 90f; // Distance raisonnable
            Vector3 offset = new Vector3(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance,
                0f
            );
            spawnAttempts[i + 4] = incidentLocation + offset;
        }
        
        // Strategy 3: Positions derrière le joueur (plus susceptibles d'avoir des routes)
        Ped player = Game.Player.Character;
        if (player != null && player.Exists())
        {
            Vector3 behindPlayer1 = incidentLocation + (-player.ForwardVector * 80f);
            Vector3 behindPlayer2 = incidentLocation + (-player.ForwardVector * 100f);
            Vector3 leftOfPlayer = incidentLocation + (-player.RightVector * 80f);
            Vector3 rightOfPlayer = incidentLocation + (player.RightVector * 80f);
            
            spawnAttempts[8] = behindPlayer1;
            spawnAttempts[9] = behindPlayer2;
            spawnAttempts[10] = leftOfPlayer;
            spawnAttempts[11] = rightOfPlayer;
        }
        else
        {
            // Fallback si pas de joueur
            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f + 45f) * (float)(Math.PI / 180f);
                float distance = 80f;
                Vector3 offset = new Vector3(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance,
                    0f
                );
                spawnAttempts[i + 8] = incidentLocation + offset;
            }
        }
        
        // Strategy 4: Positions aléatoires comme backup
        for (int i = 0; i < 4; i++)
        {
            float angle = _random.Next(0, 360) * (float)(Math.PI / 180f);
            float distance = 70f + _random.Next(0, 40); // Entre 70 et 110m
            Vector3 offset = new Vector3(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance,
                0f
            );
            spawnAttempts[i + 12] = incidentLocation + offset;
        }
        
        // Évaluer chaque tentative de spawn
        for (int i = 0; i < spawnAttempts.Length; i++)
        {
            Vector3 candidatePos = spawnAttempts[i];
            Vector3 roadPos = World.GetNextPositionOnStreet(candidatePos, true);
            
            if (roadPos != Vector3.Zero)
            {
                float distanceToIncident = roadPos.DistanceTo(incidentLocation);
                
                // Critères assouplis pour un spawn plus fiable
                if (distanceToIncident >= PoliceSpawnMinDistance && 
                    distanceToIncident <= PoliceSpawnMaxDistance)
                {
                    Log($"Found suitable spawn point {i}: {roadPos} (distance: {distanceToIncident:F2}m)");
                    return roadPos;
                }
                else if (distanceToIncident >= 50f && distanceToIncident <= 140f) // Critères de fallback
                {
                    Log($"Found acceptable spawn point {i}: {roadPos} (distance: {distanceToIncident:F2}m) - using fallback criteria");
                    return roadPos;
                }
                else
                {
                    Log($"Spawn attempt {i} distance out of range: {distanceToIncident:F2}m");
                }
            }
            else
            {
                Log($"Spawn attempt {i} failed to find road position");
            }
        }
        
        // Dernier recours: position simple avec offset
        Vector3 fallbackSpawn = incidentLocation + new Vector3(80, 80, 0);
        Vector3 fallbackRoad = World.GetNextPositionOnStreet(fallbackSpawn, true);
        if (fallbackRoad != Vector3.Zero)
        {
            fallbackSpawn = fallbackRoad;
        }
        
        Log($"Using emergency fallback spawn position: {fallbackSpawn} (distance: {fallbackSpawn.DistanceTo(incidentLocation):F2}m)");
        return fallbackSpawn;
    }

    private void Log(string message) {
        try { 
            string logPath = Path.Combine("scripts", "NPCRoadRage.log");
            Directory.CreateDirectory("scripts"); // S'assurer que le dossier existe
            File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " : " + message + Environment.NewLine); 
            
            // Afficher aussi dans la console pour debug
            GTA.UI.Screen.ShowSubtitle($"LOG: {message}", 2000);
        }
        catch (Exception ex) { 
            GTA.UI.Notification.PostTicker($"Log Error: {ex.Message}", false, false); 
            GTA.UI.Screen.ShowSubtitle($"LOG ERROR: {ex.Message}", 5000);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        Ped player = Game.Player.Character;
        if (player == null || !player.Exists()) return;
        
        // Handle response menu navigation
        if (_showingResponseMenu && _policeInteractionActive)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    _selectedResponseIndex = (_selectedResponseIndex - 1 + _responseOptions.Length) % _responseOptions.Length;
                    Log($"Menu navigation: Selected option {_selectedResponseIndex + 1}");
                    break;
                    
                case Keys.Down:
                    _selectedResponseIndex = (_selectedResponseIndex + 1) % _responseOptions.Length;
                    Log($"Menu navigation: Selected option {_selectedResponseIndex + 1}");
                    break;
                    
                case Keys.Enter:
                    SelectResponse();
                    break;
                    
                case Keys.D1:
                case Keys.NumPad1:
                    _selectedResponseIndex = 0;
                    SelectResponse();
                    break;
                    
                case Keys.D2:
                case Keys.NumPad2:
                    _selectedResponseIndex = 1;
                    SelectResponse();
                    break;
                    
                case Keys.D3:
                case Keys.NumPad3:
                    _selectedResponseIndex = 2;
                    SelectResponse();
                    break;
                    
                case Keys.D4:
                case Keys.NumPad4:
                    _selectedResponseIndex = 3;
                    SelectResponse();
                    break;
                    
                case Keys.D5:
                case Keys.NumPad5:
                    _selectedResponseIndex = 4;
                    SelectResponse();
                    break;
            }
            return; // Don't process other keys when menu is active
        }

        // Existing debug keys
        if (e.KeyCode == Keys.L) {
            Log("Test Key L: Simulating police call for interaction test.");
            if(player.IsInVehicle()) { player.Task.LeaveVehicle(); Script.Wait(1500); } 
            
            FullResetOfScriptStates(); 

            _incidentLocation = player.Position + player.ForwardVector * 10f;
            PoliceCalled = true; 
            _stateTimer = DateTime.Now; 
            Log("Test key: PoliceCalled=true. Dispatch should occur on next tick.");
        }
        if (e.KeyCode == Keys.J) {
            Log("Test Key J: Forcing Full Reset of Script States.");
            FullResetOfScriptStates();
        }
        if (e.KeyCode == Keys.K) {
            Log("Test Key K: Police Interaction Diagnostic.");
            
            string statusMsg = "=== POLICE INTERACTION DIAGNOSTIC ===\n";
            statusMsg += $"PoliceCalled: {PoliceCalled}\n";
            statusMsg += $"_policeDispatched: {_policeDispatched}\n";
            statusMsg += $"_policeArrived: {_policeArrived}\n";
            statusMsg += $"_policeInteractionActive: {_policeInteractionActive}\n";
            statusMsg += $"_makingPoliceDecision: {_makingPoliceDecision}\n";
            statusMsg += $"_policeInteractionStage: {_policeInteractionStage}\n";
            statusMsg += $"_showingResponseMenu: {_showingResponseMenu}\n";
            
            if (_policeVehicle != null && _policeVehicle.Exists()) {
                float distToIncident = _policeVehicle.Position.DistanceTo(_incidentLocation);
                statusMsg += $"Police Vehicle: Exists, Speed: {_policeVehicle.Speed:F2}, Distance to incident: {distToIncident:F2}\n";
                
                // Diagnostic avancé pour les véhicules bloqués
                if (_lastPolicePosition != Vector3.Zero) {
                    float distanceMoved = _policeVehicle.Position.DistanceTo(_lastPolicePosition);
                    statusMsg += $"Vehicle movement: {distanceMoved:F2}m since last check\n";
                }
                
                if (_policeStuckStartTime != DateTime.MinValue) {
                    double stuckDuration = (DateTime.Now - _policeStuckStartTime).TotalSeconds;
                    statusMsg += $"Vehicle stuck duration: {stuckDuration:F1} seconds\n";
                }
                
                statusMsg += $"Vehicle position: {_policeVehicle.Position}\n";
                statusMsg += $"Incident location: {_incidentLocation}\n";
            } else {
                statusMsg += "Police Vehicle: Does not exist\n";
            }
            
            statusMsg += $"Officers Count: {_respondingPolicePeds.Count(o => o != null && o.Exists() && o.IsAlive)}\n";
            
            int officersInVehicle = 0;
            foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                if(_policeVehicle != null && _policeVehicle.Exists() && officer.IsInVehicle(_policeVehicle)) {
                    officersInVehicle++;
                }
                float distToPlayer = officer.Position.DistanceTo(player.Position);
                statusMsg += $"Officer {officer.Handle}: InVehicle={(_policeVehicle != null && officer.IsInVehicle(_policeVehicle))}, DistToPlayer={distToPlayer:F2}\n";
            }
            
            statusMsg += $"Officers still in vehicle: {officersInVehicle}\n";
            statusMsg += $"Time since state change: {(DateTime.Now - _stateTimer).TotalSeconds:F1} seconds\n";
            
            Log(statusMsg);
            GTA.UI.Notification.PostTicker("Diagnostic info logged. Check NPCRoadRage.log", false, false);
            
            // Actions de forçage automatique basées sur le diagnostic
            if (_policeVehicle != null && _policeVehicle.Exists()) {
                float distToIncident = _policeVehicle.Position.DistanceTo(_incidentLocation);
                
                // Si la police est proche et bloquée, forcer l'arrivée
                if (distToIncident < 50.0f && _policeVehicle.Speed < 1.0f && !_policeArrived && officersInVehicle > 0) {
                    Log("K Key: Police close but stuck, forcing arrival and officer exit...");
                    
                    _policeVehicle.Speed = 0f;
                    _policeVehicle.IsSirenActive = false;
                    _policeVehicle.IsEngineRunning = false;
                    
                    foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                        if(_policeVehicle != null && _policeVehicle.Exists() && officer.IsInVehicle(_policeVehicle)) {
                            Log($"K Key: Force teleporting close officer {officer.Handle}");
                            ForceOfficerOutOfVehicle(officer, _policeVehicle);
                        }
                    }
                    
                    // Forcer l'arrivée
                    _policeArrived = true;
                    _policeInteractionActive = true;
                    _stateTimer = DateTime.Now;
                    _policeInteractionStage = -1;
                    
                    GTA.UI.Notification.PostTicker("Forced police arrival and officer exit!", false, false);
                }
                // Si déjà en interaction mais des officiers toujours dans le véhicule
                else if (_policeInteractionActive && officersInVehicle > 0) {
                    Log("K Key: Forcing stuck officers out during interaction...");
                    foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                        if(_policeVehicle != null && _policeVehicle.Exists() && officer.IsInVehicle(_policeVehicle)) {
                            Log($"K Key: Force teleporting interaction officer {officer.Handle}");
                            ForceOfficerOutOfVehicle(officer, _policeVehicle);
                        }
                    }
                    GTA.UI.Notification.PostTicker("Forced officers out during interaction!", false, false);
                }
            }
        }
        if (e.KeyCode == Keys.M) {
            Log("Test Key M: Collision Detection Debug Info.");
            
            Vehicle playerVeh = player.CurrentVehicle;
            if (playerVeh != null && playerVeh.Exists()) {
                string debugMsg = "=== COLLISION DETECTION DEBUG ===\n";
                debugMsg += $"Current collision sensitivity settings:\n";
                debugMsg += $"MinSpeedForSignificantImpact: {MinSpeedForSignificantImpact}\n";
                debugMsg += $"MinSpeedForHighSpeedCollision: {MinSpeedForHighSpeedCollision}\n";
                debugMsg += $"MaxDistanceForHighSpeedCollision: {MaxDistanceForHighSpeedCollision}\n";
                debugMsg += $"Player vehicle speed: {playerVeh.Speed:F2}\n";
                
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(playerVeh.Position, 10.0f)
                                                 .Where(v => v != null && v.Exists() && v != playerVeh)
                                                 .Take(3) // Limit to 3 closest vehicles
                                                 .ToArray();
                
                if (nearbyVehicles.Length > 0) {
                    debugMsg += $"Nearby vehicles ({nearbyVehicles.Length}):\n";
                    foreach (var vehicle in nearbyVehicles) {
                        float dist = playerVeh.Position.DistanceTo(vehicle.Position);
                        bool touching = playerVeh.IsTouching(vehicle);
                        bool damaged = playerVeh.HasBeenDamagedBy(vehicle) || vehicle.HasBeenDamagedBy(playerVeh);
                        debugMsg += $"Vehicle {vehicle.Model.Hash}: Dist={dist:F2}, Touch={touching}, Damaged={damaged}, Speed={vehicle.Speed:F2}\n";
                    }
                } else {
                    debugMsg += "No nearby vehicles detected.\n";
                }
                
                Log(debugMsg);
                GTA.UI.Notification.PostTicker("Collision debug info logged. Check NPCRoadRage.log", false, false);
            } else {
                GTA.UI.Notification.PostTicker("You need to be in a vehicle for collision debug!", false, false);
            }
        }
    }

    private void ForceOfficerOutOfVehicle(Ped officer, Vehicle vehicle)
    {
        if (officer == null || !officer.Exists() || !officer.IsAlive || vehicle == null || !vehicle.Exists())
        {
            Log("ForceOfficerOutOfVehicle: Invalid officer or vehicle");
            return;
        }

        if (!officer.IsInVehicle(vehicle))
        {
            Log($"Officer {officer.Handle} is already out of vehicle");
            return;
        }

        Log($"ForceOfficerOutOfVehicle: Forcing officer {officer.Handle} out of vehicle {vehicle.Handle}");

        // Méthode ultra-directe : Forcer immédiatement la sortie sans attendre
        
        // Étape 1: Arrêter complètement toutes les tâches
        officer.Task.ClearAllImmediately();
        
        // Étape 2: Désactiver toute intelligence artificielle temporairement
        Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 32, false); // Disable task system
        Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 281, false); // Disable AI
        
        // Étape 3: Forcer la sortie avec la commande native la plus directe
        Function.Call(Hash.TASK_LEAVE_VEHICLE, officer.Handle, vehicle.Handle, 0); // 0 = immediate exit
        Script.Wait(100); // Attendre un petit moment pour l'exécution

        // Étape 4: Si toujours dans le véhicule, téléportation forcée
        if (officer.IsInVehicle(vehicle))
        {
            Log($"Officer {officer.Handle} still in vehicle, using forced teleportation");
            
            // Calculer une position de sortie sûre
            Vector3 exitPos = vehicle.Position + vehicle.RightVector * 4.0f + vehicle.ForwardVector * 2.0f;
            exitPos.Z = vehicle.Position.Z + 1.0f; // Un peu au-dessus du sol
            
            // Téléportation directe avec SET_ENTITY_COORDS (plus fiable)
            Function.Call(Hash.SET_ENTITY_COORDS, officer.Handle, exitPos.X, exitPos.Y, exitPos.Z, false, false, false, true);
            Script.Wait(50);
        }

        // Étape 5: Si ENCORE dans le véhicule, méthode agressive
        if (officer.IsInVehicle(vehicle))
        {
            Log($"Officer {officer.Handle} STILL in vehicle, using WARP method");
            
            // Position de sortie alternative (du côté opposé)
            Vector3 exitPos = vehicle.Position + vehicle.RightVector * -4.0f + vehicle.ForwardVector * -2.0f;
            exitPos.Z = vehicle.Position.Z + 1.0f;
            
            // Méthode WARP (la plus agressive)
            Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, officer.Handle, exitPos.X, exitPos.Y, exitPos.Z, true, true, true);
            Script.Wait(50);
        }

        // Étape 6: DERNIÈRE TENTATIVE - Utiliser des méthodes natives ultra-agressives
        if (officer.IsInVehicle(vehicle))
        {
            Log($"Officer {officer.Handle} EXTREMELY stubborn, using nuclear option");
            
            // Forcer l'officier à sortir avec TASK_LEAVE_VEHICLE en mode immédiat
            Function.Call(Hash.TASK_LEAVE_VEHICLE, officer.Handle, vehicle.Handle, 256); // 256 = FLAG_WARP_OUT
            Script.Wait(50);
            
            // Si ça ne marche toujours pas, détacher complètement
            if (officer.IsInVehicle(vehicle))
            {
                Function.Call(Hash.DETACH_ENTITY, officer.Handle, true, true);
                Script.Wait(50);
                
                // Position finale forcée avec WARP
                Vector3 finalPos = vehicle.Position + vehicle.RightVector * 5.0f;
                finalPos.Z = vehicle.Position.Z + 2.0f; // Plus haut pour éviter les collisions
                Function.Call(Hash.SET_ENTITY_COORDS_NO_OFFSET, officer.Handle, finalPos.X, finalPos.Y, finalPos.Z, true, true, true);
            }
        }

        // Étape 7: Réactiver l'IA et configurer l'officier
        Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 32, true); // Re-enable task system
        Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 281, true); // Re-enable AI
        
        // Empêcher le retour dans le véhicule
        officer.BlockPermanentEvents = true;
        Function.Call(Hash.SET_PED_KEEP_TASK, officer.Handle, true);
        Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 429, true); // Can't enter vehicles
        
        // Assurer que l'officier reste au sol
        Function.Call(Hash.SET_PED_TO_RAGDOLL, officer.Handle, 500, 500, 0, false, false, false);

        Log($"ForceOfficerOutOfVehicle completed for officer {officer.Handle}. In vehicle: {officer.IsInVehicle(vehicle)}");
    }

    private void ResetPoliceRedirectionStates()
    {
        _navigationRetryCount = 0;
        _lastPoliceRedirectionTime = DateTime.MinValue;
        _lastPolicePosition = Vector3.Zero;
        _policeStuckStartTime = DateTime.MinValue;
        Log("Police redirection states reset.");
    }

    private void DrawResponseMenu()
    {
        // Affichage du menu avec plusieurs méthodes pour assurer la visibilité
        
        // Méthode 1: Notification persistante pour le titre
        if (_lastMenuDisplayTime == DateTime.MinValue || DateTime.Now > _lastMenuDisplayTime + TimeSpan.FromSeconds(1))
        {
            GTA.UI.Notification.PostTicker("~y~POLICE INTERACTION~w~ - Select your response:", true, false);
            _lastMenuDisplayTime = DateTime.Now;
        }

        // Méthode 2: Subtitle pour l'état actuel du menu
        string menuTitle = $"~b~RESPONSE MENU~w~ - Use ↑↓ or 1-5 keys, then ENTER";
        GTA.UI.Screen.ShowSubtitle(menuTitle, 200);
        
        // Méthode 3: Affichage direct avec coordonnées fixes en utilisant des textes screen
        float screenX = 0.1f; // Position X sur l'écran (gauche)
        float screenY = 0.3f; // Position Y sur l'écran (milieu-haut)
        
        // Afficher le titre du menu
        Function.Call(Hash.SET_TEXT_FONT, 4);
        Function.Call(Hash.SET_TEXT_SCALE, 0.6f, 0.6f);
        Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 0, 255); // Jaune
        Function.Call(Hash.SET_TEXT_OUTLINE);
        Function.Call(Hash.SET_TEXT_CENTRE, false);
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~y~POLICE INTERACTION MENU~w~");
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenX, screenY);
        
        // Afficher chaque option du menu
        for (int i = 0; i < _responseOptions.Length; i++)
        {
            float optionY = screenY + 0.05f + (i * 0.04f); // Espacer les options
            
            // Couleur et style selon la sélection
            if (i == _selectedResponseIndex)
            {
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_SCALE, 0.7f, 0.7f);
                Function.Call(Hash.SET_TEXT_COLOUR, 0, 255, 0, 255); // Vert pour la sélection
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(Hash.SET_TEXT_CENTRE, false);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $">>> {i + 1}. {_responseOptions[i]} <<<");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenX, optionY);
            }
            else
            {
                Function.Call(Hash.SET_TEXT_FONT, 4);
                Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255); // Blanc pour les autres options
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(Hash.SET_TEXT_CENTRE, false);
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"    {i + 1}. {_responseOptions[i]}");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenX, optionY);
            }
        }
        
        // Afficher les instructions de contrôle
        float instructionY = screenY + 0.05f + (_responseOptions.Length * 0.04f) + 0.03f;
        Function.Call(Hash.SET_TEXT_FONT, 4);
        Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
        Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 0, 255); // Jaune
        Function.Call(Hash.SET_TEXT_OUTLINE);
        Function.Call(Hash.SET_TEXT_CENTRE, false);
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "~y~Controls: ↑↓ arrows to navigate, ENTER to select, or press 1-5 directly~w~");
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, screenX, instructionY);
        
        // Méthode 4: Help text en bas d'écran comme backup
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
        string helpText = $"Current selection: ~g~{_selectedResponseIndex + 1}. {_responseOptions[_selectedResponseIndex]}~w~";
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, helpText);
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, 0, 1, -1);
        
        // Méthode 5: Log pour debug
        if (_lastMenuDisplayTime == DateTime.MinValue || DateTime.Now > _lastMenuDisplayTime + TimeSpan.FromSeconds(2))
        {
            Log($"Menu displayed - Selected option: {_selectedResponseIndex + 1}/{_responseOptions.Length} - '{_responseOptions[_selectedResponseIndex]}'");
        }
    }
    
    private void SelectResponse()
    {
        if (_selectedResponseIndex >= 0 && _selectedResponseIndex < _responseOptions.Length)
        {
            _playerResponse = _responseOptions[_selectedResponseIndex];
            _showingResponseMenu = false;
            
            Log($"Player selected response: {_playerResponse}");
            GTA.UI.Notification.PostTicker($"You: {_playerResponse}", false, false);
            
            // Progress to next stage
            _policeInteractionStage++;
            _stateTimer = DateTime.Now;
        }
    }

    // Nouvelle méthode pour faire partir la police de manière naturelle
    private void SendPoliceAway()
    {
        Log("Sending police away naturally - returning to vehicle and driving off.");
        
        if (_policeVehicle == null || !_policeVehicle.Exists())
        {
            Log("No police vehicle to return to. Using emergency cleanup.");
            CleanUpPolice(false);
            return;
        }

        // S'assurer que la sirène est éteinte
        _policeVehicle.IsSirenActive = false;
        _policeVehicle.IsEngineRunning = true;

        // Faire retourner tous les officiers dans le véhicule
        foreach (Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive))
        {
            if (!officer.IsInVehicle(_policeVehicle))
            {
                Log($"Sending officer {officer.Handle} back to police vehicle.");
                officer.BlockPermanentEvents = false; // Permettre les événements permanents
                Function.Call(Hash.SET_PED_CONFIG_FLAG, officer.Handle, 429, false); // Permettre d'entrer dans les véhicules
                officer.Task.EnterVehicle(_policeVehicle, VehicleSeat.Any);
            }
        }

        // Programmer la conduite de départ
        Ped driver = _policeVehicle.Driver;
        if (driver != null && driver.Exists() && driver.IsAlive && _respondingPolicePeds.Contains(driver))
        {
            Log($"Police driver {driver.Handle} is driving away from the scene.");
            
            // Trouver une destination lointaine pour partir
            Vector3 awayPosition = _policeVehicle.Position + _policeVehicle.ForwardVector * 500f;
            Vector3 roadDestination = World.GetNextPositionOnStreet(awayPosition, true);
            
            if (roadDestination == Vector3.Zero)
            {
                // Fallback si aucune route trouvée
                roadDestination = _policeVehicle.Position + _policeVehicle.ForwardVector * 300f;
            }
            
            // Faire conduire la police loin de la scène
            driver.Task.DriveTo(_policeVehicle, roadDestination, 10f, 
                              VehicleDrivingFlags.StopForVehicles, 
                              PolicePatrolSpeed);
            
            Log($"Police departing to: {roadDestination}");
        }
        else
        {
            Log("No valid police driver found. Using emergency cleanup.");
            CleanUpPolice(false);
            return;
        }

        // Marquer pour nettoyage automatique après qu'ils soient partis (pas de suppression immédiate)
        // Les entités seront marquées comme "no longer needed" automatiquement par le jeu
        // après qu'elles soient suffisamment loin du joueur
        _policeVehicle.MarkAsNoLongerNeeded();
        foreach (var officer in _respondingPolicePeds.Where(o => o != null && o.Exists()))
        {
            officer.MarkAsNoLongerNeeded();
        }

        // Nettoyer les références locales
        _policeVehicle = null;
        _respondingPolicePeds.Clear();
        _policeDispatched = false;
        _policeArrived = false;
        PoliceCalled = false;
        _incidentLocation = Vector3.Zero;
        
        _officerLastTaskedTime.Clear();
        _lastOfficerTaskTime = DateTime.MinValue;
        ResetPoliceRedirectionStates();
    }

    // Nouvelle méthode pour faire retourner le NPC à son véhicule
    private void SendNpcBackToVehicle()
    {
        if (_collidedNpcPed == null || !_collidedNpcPed.Exists() || _collidedNpcPed.IsDead)
        {
            Log("No valid NPC to send back to vehicle.");
            return;
        }

        if (_collidedNpcVehicle == null || !_collidedNpcVehicle.Exists())
        {
            Log("NPC vehicle no longer exists. NPC will walk away.");
            _collidedNpcPed.BlockPermanentEvents = false;
            _collidedNpcPed.Task.Wander();
            
            // Marquer comme non nécessaire automatiquement
            _collidedNpcPed.MarkAsNoLongerNeeded();
            return;
        }

        Log($"Sending NPC {_collidedNpcPed.Handle} back to vehicle {_collidedNpcVehicle.Handle}.");
        
        // Réactiver les événements permanents pour un comportement naturel
        _collidedNpcPed.BlockPermanentEvents = false;
        Function.Call(Hash.SET_PED_CONFIG_FLAG, _collidedNpcPed.Handle, 429, false); // Permettre d'entrer dans les véhicules
        
        // Nettoyer les tâches actuelles
        _collidedNpcPed.Task.ClearAll();
        
        // Faire retourner le NPC à son véhicule
        _collidedNpcPed.Task.EnterVehicle(_collidedNpcVehicle, VehicleSeat.Driver);
        
        Log($"NPC {_collidedNpcPed.Handle} tasked to return to vehicle.");
        
        // Programmer la conduite de départ après que le NPC soit dans le véhicule
        // On utilise une tâche séquentielle : d'abord entrer dans le véhicule, puis conduire
        if (_collidedNpcPed.IsInVehicle(_collidedNpcVehicle))
        {
            Log($"NPC {_collidedNpcPed.Handle} is already in vehicle. Programming departure immediately.");
            ProgramNpcDeparture();
        }
        else
        {
            // Le NPC entrera dans le véhicule automatiquement grâce à la tâche EnterVehicle
            // La logique de conduite sera gérée par ProgramNpcDeparture() une fois qu'il sera dans le véhicule
            Log($"NPC {_collidedNpcPed.Handle} will enter vehicle first, then departure will be programmed.");
        }
        
        // Marquer pour nettoyage automatique par le jeu
        _collidedNpcVehicle.MarkAsNoLongerNeeded();
        _collidedNpcPed.MarkAsNoLongerNeeded();
    }
    
    // Méthode helper pour programmer le départ du NPC une fois dans le véhicule
    private void ProgramNpcDeparture()
    {
        if (_collidedNpcPed == null || !_collidedNpcPed.Exists() || 
            _collidedNpcVehicle == null || !_collidedNpcVehicle.Exists() ||
            !_collidedNpcPed.IsInVehicle(_collidedNpcVehicle))
        {
            return;
        }

        Log($"Programming departure for NPC {_collidedNpcPed.Handle}.");
        
        // Faire conduire le NPC loin de la scène
        Vector3 awayPosition = _collidedNpcVehicle.Position + _collidedNpcVehicle.ForwardVector * 200f;
        Vector3 roadDestination = World.GetNextPositionOnStreet(awayPosition, true);
        
        if (roadDestination == Vector3.Zero)
        {
            roadDestination = _collidedNpcVehicle.Position + _collidedNpcVehicle.ForwardVector * 150f;
        }
        
        _collidedNpcPed.Task.DriveTo(_collidedNpcVehicle, roadDestination, 5f, 
                                   VehicleDrivingFlags.StopForVehicles, 
                                   20f); // Vitesse normale
        
        Log($"NPC departing to: {roadDestination}");
    }
}

public static class Utility {
    public static float GetHeadingFromVector(Vector3 direction) {
        return (float)(Math.Atan2(direction.X, direction.Y) * (180.0 / Math.PI));
    }
}
