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
    private readonly TimeSpan _policeDispatchTimeoutDuration = TimeSpan.FromSeconds(90); 
    private readonly TimeSpan _policeExitVehicleTimeoutDuration = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _policeApproachTimeoutDuration = TimeSpan.FromSeconds(15);
    private readonly TimeSpan _policeInteractionDialoguePauseDuration = TimeSpan.FromSeconds(3);
    private readonly TimeSpan _policeDecisionPauseDuration = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _policeArrestSequenceDuration = TimeSpan.FromSeconds(8); 
    private readonly TimeSpan _policeDepartureBoardingTimeoutDuration = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _policeDepartureDriveAwayDuration = TimeSpan.FromSeconds(15);
    private const int AggressiveNpcChancePercent = 20; 
    private const int NumberOfPoliceOfficers = 2; 
    private PedHash _policePedModel = PedHash.Cop01SMY;
    private VehicleHash _policeVehicleModel = VehicleHash.Police;
    private const float PoliceArrivalDistanceThreshold = 15.0f; 
    private const float OfficerApproachDistanceThreshold = 5.0f; 
    private const float MaxInteractionDistance = 40.0f; 
    private const float PoliceDriveSpeed = 30.0f; 
    private const float PolicePatrolSpeed = 20.0f; 
    private const float OfficerApproachSpeed = 1.5f; // Walking/jog speed for approach

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
    private enum PoliceDecision { None, ArrestPlayer, LetPlayerGo }
    private PoliceDecision _currentPoliceDecision = PoliceDecision.None;

    private Random _random = new Random();
    private List<Ped> _respondingPolicePeds = new List<Ped>();
    private Vehicle? _policeVehicle = null;
    private string _playerResponse = string.Empty;

    public NPCRoadRage()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown; 
        Log("NPCRoadRage script loaded. Version 1.18 - Native GoTo Fix.");
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
            if (player.Position.DistanceTo(_policeVehicle.Position) > MaxInteractionDistance) {
                Log("Player moved too far during police interaction. Aborting and cleaning up.");
                GTA.UI.Notification.Show("You left the scene of the incident.");
                EndPoliceInteraction(false, true); 
                return;
            }
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

                    if (isTouching && (playerDamagedNpc || npcDamagedPlayer))
                    {
                        Log($"Collision detected: PlayerVehicle ({playerVeh.Model.Hash}) with NPCDriver ({npcDriver.Model.Hash}) in NPCVehicle ({npcVehicle.Model.Hash}).");
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

                        if (_random.Next(0, 100) < AggressiveNpcChancePercent)  _currentReaction = NpcReactionType.Aggressive;
                        else _currentReaction = NpcReactionType.CallPolice;
                        
                        Log($"NPC {_collidedNpcPed.Handle} reacting: {_currentReaction} at {_incidentLocation}");
                        _npcCollisionCooldowns[npcVehicle.Handle] = DateTime.Now + _npcCollisionCooldownDuration;
                        GTA.UI.Notification.Show($"NPC {npcDriver.Handle} is reacting to collision!");
                        ProcessNpcReaction(); 
                        return; 
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

        bool isLeavingVehicleTaskActive = Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, _collidedNpcPed.Handle, 4); 

        if (_collidedNpcVehicle != null && _collidedNpcVehicle.Exists() && _collidedNpcPed.IsInVehicle(_collidedNpcVehicle))
        {
            if (!isLeavingVehicleTaskActive) 
            {
                Log($"Telling NPC {_collidedNpcPed.Handle} to leave vehicle {_collidedNpcVehicle.Handle}.");
                _collidedNpcPed.Task.LeaveVehicle(_collidedNpcVehicle, false); 
            }
            if (_collidedNpcPed.IsInVehicle(_collidedNpcVehicle) || isLeavingVehicleTaskActive) return;
        }
        
        switch (_currentReaction)
        {
            case NpcReactionType.Aggressive:
                Log($"NPC {_collidedNpcPed.Handle} is out of vehicle. Becoming aggressive towards player.");
                _collidedNpcPed.Task.ClearAll(); 
                _collidedNpcPed.Task.Combat(Game.Player.Character, 0, 0); 
                ResetNpcReactionState(); 
                break;
            case NpcReactionType.CallPolice:
                Log($"NPC {_collidedNpcPed.Handle} is out of vehicle. Attempting to call police.");
                _collidedNpcPed.Task.ClearAll();
                uint taskCowerHash = (uint)Function.Call<int>(Hash.GET_HASH_KEY, "TASK_COWER");
                if ((uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, _collidedNpcPed.Handle, -1) != taskCowerHash) { 
                     _collidedNpcPed.Task.Cower(-1); 
                     Log($"NPC {_collidedNpcPed.Handle} is cowering. Will 'call police' next tick conceptually.");
                } else {
                    if (!PoliceCalled) 
                    {
                        PoliceCalled = true; 
                        _stateTimer = DateTime.Now; 
                        Log($"PoliceCalled flag set to true by NPC {_collidedNpcPed.Handle}. Incident at: {_incidentLocation}");
                        GTA.UI.Notification.Show("An NPC is calling the police!");
                    }
                    ResetNpcReactionState(); 
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

        Ped driver = _policeVehicle.Driver;
        if (driver != null && driver.Exists() && !driver.IsDead && 
            driver.Position.DistanceTo(_incidentLocation) < PoliceArrivalDistanceThreshold && 
            _policeVehicle.Speed < 1.0f)
        {
            Log("Police driver has arrived at the scene and vehicle has stopped.");
            _policeArrived = true;
            _policeVehicle.IsSirenActive = false;
            _stateTimer = DateTime.Now; 
            _policeInteractionStage = -1; 
            Log("Police interaction sequence initiated. Officers will now exit vehicle.");
        } else if (driver != null && driver.Exists() && driver.IsDead) {
            Log("Police driver died en route. Aborting police response.");
            CleanUpPolice();
        }
        else if (DateTime.Now > _stateTimer + _policeDispatchTimeoutDuration) 
        {
            Log("Police dispatch timed out. Cleaning up.");
            CleanUpPolice(); 
        }
    }

    private void ProcessPoliceInteraction()
    {
        Ped player = Game.Player.Character;
        Ped leadOfficer = _respondingPolicePeds.FirstOrDefault(p => p != null && p.Exists() && p.IsAlive); 

        if (leadOfficer == null || player == null || !player.Exists()) { 
            Log("Lead officer or player is invalid. Ending police interaction.");
            EndPoliceInteraction(); return; 
        }

        bool isPlayerControlDisabled = !Game.Player.CanControlCharacter; 
        if (_policeInteractionStage >= 0 && !isPlayerControlDisabled) { 
            Log("Disabling player control for active police interaction.");
            Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0); 
        }
        
        player.Task.LookAt(leadOfficer);
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
                if (DateTime.Now > _stateTimer + _policeExitVehicleTimeoutDuration) {
                    Log("Timeout waiting for officers to exit vehicle. Attempting to force approach or abort.");
                    bool anyOfficerStillInVehicleAfterTimeout = false;
                    foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                        if(_policeVehicle != null && _policeVehicle.Exists() && officer.IsInVehicle(_policeVehicle)) {
                             officer.Task.LeaveVehicle(_policeVehicle, false); 
                             anyOfficerStillInVehicleAfterTimeout = true;
                        }
                    }
                    if (anyOfficerStillInVehicleAfterTimeout && DateTime.Now > _stateTimer + _policeExitVehicleTimeoutDuration + TimeSpan.FromSeconds(5)) {
                        Log("Officers still stuck in vehicle after extended timeout. Aborting."); EndPoliceInteraction(); return;
                    }
                    if (!anyOfficerStillInVehicleAfterTimeout) { _stateTimer = DateTime.Now; } 
                }

                bool allOfficersOutOfVehicle = _respondingPolicePeds.All(p => p == null || !p.Exists() || !p.IsAlive || (_policeVehicle != null && _policeVehicle.Exists() && !p.IsInVehicle(_policeVehicle)) );
                if (!allOfficersOutOfVehicle) return; 

                bool allOfficersApproached = true;
                foreach(Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                    if (officer.Position.DistanceTo(player.Position) > OfficerApproachDistanceThreshold + 1.0f) { 
                        uint currentOfficerTaskHash = (uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, officer.Handle, -1);
                        if (currentOfficerTaskHash != taskGotoEntityHash && currentOfficerTaskHash != taskGotoCoordHash || officer.Velocity.LengthSquared() < 0.1f ) { 
                            Vector3 approachPosition = player.Position + player.ForwardVector * (OfficerApproachDistanceThreshold -1f) + player.RightVector * ((_respondingPolicePeds.IndexOf(officer) % 2 == 0) ? -1.0f : 1.0f);
                            // Using native TASK_GOTO_COORD_ANY_PED for the problematic GoTo
                            Function.Call(Hash.TASK_GOTO_COORD_ANY_PED, 
                                          officer.Handle, 
                                          approachPosition.X, 
                                          approachPosition.Y, 
                                          approachPosition.Z, 
                                          OfficerApproachSpeed, 
                                          0, 
                                          false, 
                                          0, // walkStyle: 0 for default
                                          0);
                            Log($"Officer {officer.Handle} tasked with native TASK_GOTO_COORD_ANY_PED to {approachPosition}.");
                        }
                        allOfficersApproached = false;
                    } else { 
                         if(officer.IsWalking || officer.IsRunning) officer.Task.StandStill(-1); 
                    }
                }
                if (!allOfficersApproached && DateTime.Now < _stateTimer + _policeApproachTimeoutDuration) return; 
                if (!allOfficersApproached && DateTime.Now >= _stateTimer + _policeApproachTimeoutDuration) {
                    Log("Timeout for officers to approach player. Aborting."); EndPoliceInteraction(); return;
                }
                
                Log("All officers approached. Starting dialogue.");
                Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
                _policeInteractionStage = 0;
                _stateTimer = DateTime.Now; 
                break;
            case 0: 
                if (DateTime.Now < _stateTimer + TimeSpan.FromSeconds(1)) return; 
                GTA.UI.Notification.Show("Officer: We received a report of a vehicle collision here.");
                Log("Interaction Stage 0: Officer statement 1.");
                _policeInteractionStage++; _stateTimer = DateTime.Now; 
                break;
            case 1: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                GTA.UI.Notification.Show("Officer: What's your side of the story?");
                Log("Interaction Stage 1: Officer asks player.");
                _playerResponse = Game.GetUserInput(GTA.WindowTitle.FMMC_KEY_TIP8, "", 60); 
                if (string.IsNullOrEmpty(_playerResponse)) _playerResponse = "I have nothing to say."; 
                Log($"Player responded: {_playerResponse}");
                _policeInteractionStage++; _stateTimer = DateTime.Now;
                break;
            case 2: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                if (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive && _incidentLocation.DistanceTo(_collidedNpcPed.Position) < 25f) 
                {
                    if (leadOfficer != null && leadOfficer.Exists()) _collidedNpcPed.Task.LookAt(leadOfficer);
                    else if(player != null && player.Exists()) _collidedNpcPed.Task.LookAt(player);
                    string npcStatement = $"NPC ({_collidedNpcPed.Handle}): They crashed right into me! It was their fault!";
                    GTA.UI.Notification.Show(npcStatement); 
                    Log($"Interaction Stage 2: NPC statement: {npcStatement}");
                } else {
                    Log("Interaction Stage 2: Original NPC not available or too far for statement.");
                    GTA.UI.Notification.Show("Officer: The other party isn't present to give a statement.");
                }
                _policeInteractionStage++; _stateTimer = DateTime.Now;
                break;
            case 3: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                GTA.UI.Notification.Show("Officer: Alright, let me assess the situation...");
                Log("Interaction Stage 3: Officer assessing.");
                _policeInteractionStage++; _stateTimer = DateTime.Now; 
                break;
            case 4: 
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

        if (player != null && player.Exists() && Game.Player.Wanted.WantedLevelAmount > 0) { 
             _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (already wanted).");
        } else if (admitFault) {
            _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (admitted fault).");
        } else if (!playerCooperative && npcAccusatory) {
            _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (uncooperative and NPC accuses).");
        } else if (!playerCooperative) {
             if (_random.Next(0,2) == 0) { _currentPoliceDecision = PoliceDecision.ArrestPlayer; Log("Decision: Arrest Player (uncooperative, 50% chance).");}
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
                    GTA.UI.Notification.Show("Officer: You're coming with us!");
                    if (arrestingOfficer != null && arrestingOfficer.Exists()) 
                    {
                        Log($"Officer {arrestingOfficer.Handle} is arresting player {player.Handle}.");
                        arrestingOfficer.Task.Arrest(player);
                    }
                }
                
                if (DateTime.Now > _stateTimer + _policeArrestSequenceDuration || player.IsCuffed || Function.Call<bool>(Hash.IS_PED_BEING_ARRESTED, player.Handle)) {
                    GTA.UI.Screen.ShowSubtitle("~r~BUSTED!", 5000);
                    Log("Player busted. Ending interaction.");
                    int currentWantedLevel = Game.Player.Wanted.WantedLevelAmount; 
                    Game.Player.Wanted.SetWantedLevel(Math.Max(currentWantedLevel, 1), false); 
                    Game.Player.Wanted.ApplyWantedLevelChangeNow(false); 
                    EndPoliceInteraction(true); 
                }
                break;

            case PoliceDecision.LetPlayerGo:
                if (DateTime.Now < _stateTimer + TimeSpan.FromSeconds(0.5)) { 
                     GTA.UI.Notification.Show("Officer: Alright, be more careful next time. You're free to go.");
                     Log("Police letting player go.");
                }

                if (DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration)
                {
                    if (_policeVehicle != null && _policeVehicle.Exists())
                    {
                        bool allAboardOrDriving = true;
                        Ped driver = _policeVehicle.Driver;
                        uint taskEnterVehicleHash = (uint)Function.Call<int>(Hash.GET_HASH_KEY, "TASK_ENTER_VEHICLE");
                        uint taskDriveWanderHash = (uint)Function.Call<int>(Hash.GET_HASH_KEY, "TASK_VEHICLE_DRIVE_WANDER");


                        foreach (Ped officer in _respondingPolicePeds.Where(o => o != null && o.Exists() && o.IsAlive)) {
                            if (!officer.IsInVehicle(_policeVehicle)) {
                                if((uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, officer.Handle, -1) != taskEnterVehicleHash) { 
                                     officer.Task.EnterVehicle(_policeVehicle, VehicleSeat.Any);
                                }
                                allAboardOrDriving = false;
                            }
                        }
                        
                        if (driver != null && driver.Exists() && driver.IsInVehicle(_policeVehicle)) {
                            if (_policeVehicle.Speed < 1f && (uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, driver.Handle, -1) != taskDriveWanderHash) { 
                                allAboardOrDriving = false; 
                            } else if (_policeVehicle.Speed >=1f) {
                                // allAboardOrDriving stays true
                            }
                        } else { 
                            allAboardOrDriving = false;
                        }

                        if (allAboardOrDriving || DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration + _policeDepartureBoardingTimeoutDuration) 
                        {
                             driver = _policeVehicle.Driver; 
                             if(driver != null && driver.Exists() && _respondingPolicePeds.Contains(driver) && driver.IsInVehicle(_policeVehicle)) {
                                if (_policeVehicle.Speed < 1f && (uint)Function.Call<int>(Hash.GET_SCRIPT_TASK_STATUS, driver.Handle, -1) != taskDriveWanderHash) { 
                                    Vector3 awayPos = _policeVehicle.Position + _policeVehicle.ForwardVector * 300f; 
                                    driver.Task.DriveTo(_policeVehicle, World.GetNextPositionOnStreet(awayPos), 5f, VehicleDrivingFlags.StopForVehicles, PolicePatrolSpeed); 
                                    Log("Police driver tasked to leave.");
                                }
                             } else if (driver == null && _respondingPolicePeds.Any(p => p != null && p.Exists())) { 
                                Log("No police driver to leave, but other officers present. Forcing cleanup."); 
                                EndPoliceInteraction(false); return;
                             } else if (!_respondingPolicePeds.Any(p => p != null && p.Exists())) { 
                                Log("No officers present. Forcing cleanup."); EndPoliceInteraction(false); return;
                             }
                            
                            if (DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration + _policeDepartureBoardingTimeoutDuration + _policeDepartureDriveAwayDuration || 
                                (_policeVehicle != null && _policeVehicle.Exists() && _policeVehicle.Position.DistanceTo(player.Position) > 150f) ) 
                            {
                                Log("Police presumed to have left. Ending interaction.");
                                EndPoliceInteraction(false);
                            }
                        }
                    }
                    else {
                        Log("No police vehicle for police to return to. Ending interaction.");
                        EndPoliceInteraction(false); 
                    }
                }
                break;
        }
    }
    
    private void ResetNpcReactionState() {
        Log($"Resetting NPC reaction state for Handle: {(_collidedNpcPed != null && _collidedNpcPed.Exists() ? _collidedNpcPed.Handle.ToString() : "N/A")}.");
        if (_collidedNpcPed != null && _collidedNpcPed.Exists()) { 
            _collidedNpcPed.MarkAsNoLongerNeeded(); 
        }
        if (_collidedNpcVehicle != null && _collidedNpcVehicle.Exists()) {
            _collidedNpcVehicle.MarkAsNoLongerNeeded();
        }
        _collidedNpcPed = null;
        _collidedNpcVehicle = null;
        _npcReacting = false;
        _currentReaction = NpcReactionType.None;
    }

    public void CleanUpPolice(bool immediate = false) { 
        Log("Cleaning up police units.");
        if (_policeVehicle != null && _policeVehicle.Exists()) {
            _policeVehicle.MarkAsNoLongerNeeded();
            _policeVehicle.Delete();
            _policeVehicle = null;
        }
        foreach (var officer in _respondingPolicePeds) {
            if (officer != null && officer.Exists()) {
                officer.MarkAsNoLongerNeeded();
                officer.Delete();
            }
        }
        _respondingPolicePeds.Clear();
        _policeDispatched = false; 
        _policeArrived = false;
        PoliceCalled = false; 
        _incidentLocation = Vector3.Zero;
        Log("Police cleanup complete.");
    }

    private void EndPoliceInteraction(bool playerArrested = false, bool playerFled = false)
    {
        Log($"Ending police interaction. Player arrested: {playerArrested}, Player fled: {playerFled}");
        Ped player = Game.Player.Character;
        bool isPlayerControlActuallyDisabled = !Game.Player.CanControlCharacter; 
        if(player != null && player.Exists() && isPlayerControlActuallyDisabled) { 
            Function.Call(Hash.ENABLE_ALL_CONTROL_ACTIONS, 0); 
            Log("Player control re-enabled.");
        }
        
        _policeInteractionActive = false;
        _makingPoliceDecision = false;
        _currentPoliceDecision = PoliceDecision.None;
        _policeInteractionStage = 0;
        _playerResponse = string.Empty;
        
        CleanUpPolice(playerArrested || playerFled); 
    }

    private void FullResetOfScriptStates() {
        Log("Performing full reset of script states.");
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
        Log("Full reset complete.");
    }
   
    private void DispatchPolice(Vector3 incidentLocation) {
        if (_policeDispatched) return; 
        Log($"Dispatching police to: {incidentLocation}");
        _policeDispatched = true;
        _policeArrived = false; 
        _stateTimer = DateTime.Now; 

        Vector3 spawnPoint = World.GetNextPositionOnStreet(incidentLocation + (Game.Player.Character.ForwardVector * -100f), true); 
        if (spawnPoint == Vector3.Zero || spawnPoint.DistanceTo(incidentLocation) < 50f) { 
            spawnPoint = incidentLocation + new Vector3(80, 80, 0); 
            Log("Could not find suitable street position for police spawn, using offset.");
        }

        _policeVehicle = World.CreateVehicle(_policeVehicleModel, spawnPoint, Utility.GetHeadingFromVector(incidentLocation - spawnPoint)); 
        if (_policeVehicle == null || !_policeVehicle.Exists())
        {
            Log("Failed to spawn police vehicle.");
            _policeDispatched = false; PoliceCalled = false; return;
        }
        _policeVehicle.PlaceOnGround();
        Log($"Police vehicle {_policeVehicle.Handle} spawned at {spawnPoint}");

        _respondingPolicePeds.Clear(); 
        for (int i = 0; i < NumberOfPoliceOfficers; i++)
        {
            Vector3 officerSpawnPos = _policeVehicle.Position + _policeVehicle.RightVector * (i * 1.5f) - _policeVehicle.ForwardVector * (i * 0.5f);
            Ped officer = World.CreatePed(_policePedModel, officerSpawnPos);
            if (officer != null && officer.Exists())
            {
                _respondingPolicePeds.Add(officer);
                officer.Armor = 100;
                officer.Weapons.Give(WeaponHash.CombatPistol, 250, true, true);
                Log($"Spawned police officer {officer.Handle}");
                if (i == 0) officer.SetIntoVehicle(_policeVehicle, VehicleSeat.Driver);
                else officer.SetIntoVehicle(_policeVehicle, VehicleSeat.Passenger);
            } else Log($"Failed to spawn officer {i+1}.");
        }

        Ped driver = _policeVehicle.Driver;
        if (driver != null && driver.Exists() && _respondingPolicePeds.Contains(driver) && _policeVehicle != null && _policeVehicle.Exists()) 
        {
            Log($"Police driver {driver.Handle} is in vehicle. Tasking to drive to incident: {incidentLocation}");
            VehicleDrivingFlags emergencyFlags = VehicleDrivingFlags.IgnoreTrafficLights | VehicleDrivingFlags.AllowGoingWrongWay | 
                                                 VehicleDrivingFlags.DriveBySight | VehicleDrivingFlags.IgnorePeds | 
                                                 VehicleDrivingFlags.IgnoreVehicles | VehicleDrivingFlags.AllowMedianCrossing;
            driver.Task.DriveTo(_policeVehicle, incidentLocation, PoliceArrivalDistanceThreshold / 2f, emergencyFlags, PoliceDriveSpeed); 
            _policeVehicle.IsSirenActive = true; 
        } else {
            Log("Police driver not found or not set in vehicle correctly. Cannot dispatch.");
            CleanUpPolice(); 
            return;
        }
        GTA.UI.Notification.Show("Police have been dispatched to your location!");
    }

    private void Log(string message) {
        try { File.AppendAllText("NPCRoadRage.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " : " + message + Environment.NewLine); }
        catch (Exception ex) { GTA.UI.Notification.Show("Log Error: " + ex.Message); }
    }

    private void OnKeyDown(object sender, KeyEventArgs e) {
        Ped player = Game.Player.Character;
        if (player == null || !player.Exists()) return; 

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
    }
}

public static class Utility {
    public static float GetHeadingFromVector(Vector3 direction) {
        return (float)(Math.Atan2(direction.X, direction.Y) * (180.0 / Math.PI));
    }
}
