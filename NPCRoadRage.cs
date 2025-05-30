using GTA;
using GTA.Native;
using GTA.Math;
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
    private readonly TimeSpan _policeDispatchTimeoutDuration = TimeSpan.FromSeconds(90); // Timeout for police to arrive
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
    private const float MaxInteractionDistance = 40.0f; // Increased slightly

    // --- State Fields ---
    private Dictionary<int, DateTime> _npcCollisionCooldowns = new Dictionary<int, DateTime>();
    private Ped _collidedNpcPed;
    private Vehicle _collidedNpcVehicle; 
    private Vector3 _incidentLocation; 
    private bool _npcReacting = false;
    private enum NpcReactionType { None, Aggressive, CallPolice }
    private NpcReactionType _currentReaction = NpcReactionType.None;
    private DateTime _stateTimer; // Generic timer for various states

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
    private Vehicle _policeVehicle;
    private string _playerResponse = string.Empty;

    public NPCRoadRage()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown; 
        Log("NPCRoadRage script loaded. Version 1.6 - Refinements.");
        _incidentLocation = Vector3.Zero; 
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;

        // 1. Global Pre-checks
        if (Game.IsLoading || Game.IsCutsceneActive || player == null || !player.Exists() || player.IsDead)
        {
            if(_policeDispatched || _npcReacting || _policeInteractionActive || _makingPoliceDecision) {
                Log("Game state changed (loading/cutscene/player dead). Forcing cleanup of active incident.");
                FullResetOfScriptStates(); 
            }
            return;
        }

        // Abort interaction if player runs too far away
        if ((_policeInteractionActive || _makingPoliceDecision) && _policeVehicle != null && _policeVehicle.Exists()) {
            if (player.Position.DistanceTo(_policeVehicle.Position) > MaxInteractionDistance) {
                Log("Player moved too far during police interaction. Aborting and cleaning up.");
                UI.Notify("You left the scene of the incident.");
                EndPoliceInteraction(false, true); // playerFled = true
                return;
            }
        }

        // --- Main State Machine ---
        if (PoliceCalled && !_policeDispatched && !_policeInteractionActive && !_makingPoliceDecision && !_policeArrived) // Ensure not arrived either
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
            // Idle state: Safe to check for new collisions
            // Safeguard, should be covered by FullReset elsewhere.
            if (_policeDispatched || _policeArrived || PoliceCalled) {
                 // If PoliceCalled is true but nothing else, DispatchPolice should trigger.
                 // If _policeDispatched or _policeArrived is true, something is wrong, reset.
                 if((_policeDispatched || _policeArrived) && !PoliceCalled) { // Anomaly if dispatched/arrived but no call active
                    Log("Anomaly detected: Police dispatched/arrived but PoliceCalled is false. Resetting.");
                    FullResetOfScriptStates();
                 }
                 return;
            }


            Vehicle playerVehicle = player.CurrentVehicle;
            if (playerVehicle == null) return;

            Vehicle[] nearbyVehicles = World.GetNearbyVehicles(playerVehicle.Position, 7.0f, playerVehicle);
            foreach (Vehicle npcVehicle in nearbyVehicles)
            {
                if (npcVehicle == null || !npcVehicle.Exists()) continue;
                if (_npcCollisionCooldowns.ContainsKey(npcVehicle.Handle) && DateTime.Now < _npcCollisionCooldowns[npcVehicle.Handle]) continue;
                
                Ped npcDriver = npcVehicle.Driver;
                if (npcDriver != null && npcDriver.Exists() && npcDriver.IsHuman && !npcDriver.IsPlayer)
                {
                    bool isTouching = playerVehicle.IsTouching(npcVehicle);
                    bool playerDamagedNpc = playerVehicle.HasBeenDamagedBy(npcVehicle); 
                    bool npcDamagedPlayer = npcVehicle.HasBeenDamagedBy(playerVehicle);

                    if (isTouching && (playerDamagedNpc || npcDamagedPlayer))
                    {
                        Log($"Collision detected: PlayerVehicle ({playerVehicle.Model.Hash}) with NPCDriver ({npcDriver.Model.Hash}) in NPCVehicle ({npcVehicle.Model.Hash}).");
                        if (!npcDriver.IsInVehicle(npcVehicle) || npcDriver.IsDead || npcDriver.IsInjured) {
                             Log("NPC no longer valid for reaction (not in vehicle or dead/injured). Applying cooldown.");
                            _npcCollisionCooldowns[npcVehicle.Handle] = DateTime.Now + _npcCollisionCooldownDuration;
                            continue; 
                        }
                        
                        FullResetOfScriptStates(); 

                        _collidedNpcPed = npcDriver;
                        // _collidedNpcPed.IsPersistent = true; // Decided against for now, use MarkAsNoLongerNeeded on cleanup
                        _collidedNpcVehicle = npcVehicle; 
                        // _collidedNpcVehicle.IsPersistent = true;

                        _incidentLocation = npcVehicle.Position; 
                        _npcReacting = true;
                        _stateTimer = DateTime.Now; 

                        if (_random.Next(0, 100) < AggressiveNpcChancePercent)  _currentReaction = NpcReactionType.Aggressive;
                        else _currentReaction = NpcReactionType.CallPolice;
                        
                        Log($"NPC {_collidedNpcPed.Handle} reacting: {_currentReaction} at {_incidentLocation}");
                        _npcCollisionCooldowns[npcVehicle.Handle] = DateTime.Now + _npcCollisionCooldownDuration;
                        UI.Notify($"NPC {npcDriver.Handle} is reacting to collision!");
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

        if (_collidedNpcPed.IsInVehicle(_collidedNpcVehicle) && _collidedNpcVehicle.Exists())
        {
            if (!_collidedNpcPed.IsGettingOutOfVehicle)
            {
                bool isLeavingVehicle = false;
                if (_collidedNpcPed.Tasks.CurrentTask != null) {
                    int taskHash = _collidedNpcPed.Tasks.CurrentTask.Hash;
                    if (taskHash == Game.GenerateHash("TASK_LEAVE_VEHICLE") || taskHash == Game.GenerateHash("CTaskLeaveVehicle")) isLeavingVehicle = true;
                }
                if (!isLeavingVehicle)
                {
                    Log($"Telling NPC {_collidedNpcPed.Handle} to leave vehicle {_collidedNpcVehicle.Handle}.");
                    _collidedNpcPed.Tasks.LeaveVehicle(_collidedNpcVehicle, false); 
                    // Allow some time for task to start, next tick will check IsGettingOutOfVehicle or if still in vehicle
                }
            }
             // If still in vehicle (or getting out), wait for next tick.
            if (_collidedNpcPed.IsInVehicle(_collidedNpcVehicle)) return;
        }
        
        switch (_currentReaction)
        {
            case NpcReactionType.Aggressive:
                Log($"NPC {_collidedNpcPed.Handle} is out of vehicle. Becoming aggressive towards player.");
                _collidedNpcPed.Tasks.ClearAll(); 
                _collidedNpcPed.Task.FightAgainst(Game.Player.Character);
                ResetNpcReactionState(); 
                break;
            case NpcReactionType.CallPolice:
                Log($"NPC {_collidedNpcPed.Handle} is out of vehicle. Attempting to call police.");
                _collidedNpcPed.Tasks.ClearAll();
                // Simple cower and then set flag. More complex animations could be timed with _stateTimer.
                if (_collidedNpcPed.Tasks.CurrentTask?.Hash != Game.GenerateHash("TASK_COWER")) {
                     _collidedNpcPed.Tasks.Cower(-1); 
                     Log($"NPC {_collidedNpcPed.Handle} is cowering. Will 'call police' next tick conceptually.");
                } else {
                    if (!PoliceCalled) 
                    {
                        PoliceCalled = true; // Set the global flag
                        _stateTimer = DateTime.Now; // Reset timer, now for police dispatch timeout
                        Log($"PoliceCalled flag set to true by NPC {_collidedNpcPed.Handle}. Incident at: {_incidentLocation}");
                        UI.Notify("An NPC is calling the police!");
                    }
                    ResetNpcReactionState(); // NPC's role in initiating call is done
                }
                break;
        }
    }

    private void CheckPoliceArrival()
    {
        if (_policeVehicle == null || !_policeVehicle.Exists()) {
             Log("Police vehicle does not exist. Aborting police response. This might be due to a spawn failure or it being removed by the game.");
             CleanUpPolice(); // This resets PoliceCalled, _policeDispatched etc.
             return;
        }
         if (_respondingPolicePeds.All(p => p == null || !p.Exists() || p.IsDead)) {
            Log("All responding police officers are gone or dead. Aborting police response.");
            CleanUpPolice();
            return;
        }

        Ped driver = _policeVehicle.Driver;
        if (driver != null && driver.Exists() && !driver.IsDead && driver.Position.DistanceTo(_incidentLocation) < PoliceArrivalDistanceThreshold && _policeVehicle.Speed < 1.0f)
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
            Log("Police dispatch timed out (_policeDispatchTimeoutDuration). They never arrived or got stuck. Cleaning up.");
            CleanUpPolice(); 
        }
    }

    private void ProcessPoliceInteraction()
    {
        Ped player = Game.Player.Character;
        Ped leadOfficer = _respondingPolicePeds.FirstOrDefault(p => p.Exists() && p.IsAlive); 

        if (leadOfficer == null || !player.Exists()) { 
            Log("Lead officer or player is invalid. Ending police interaction.");
            EndPoliceInteraction(); return; 
        }

        if (_policeInteractionStage >= 0 && Game.Player.CanControlCharacter) {
            Log("Disabling player control for active police interaction.");
            Game.Player.CanControlCharacter = false;
        }
        
        if (player.Exists()) player.Task.LookAt(leadOfficer);
        if (leadOfficer.Exists() && !leadOfficer.IsRagdoll && leadOfficer.Tasks.CurrentTask?.Hash != Game.GenerateHash("TASK_GOTO_ENTITY")) { 
            leadOfficer.Task.LookAt(player);
        }

        switch (_policeInteractionStage)
        {
            case -1: // Officers exiting vehicle and approaching
                if (DateTime.Now > _stateTimer + _policeExitVehicleTimeoutDuration) {
                    Log("Timeout waiting for officers to exit vehicle. Attempting to force approach or abort.");
                    bool anyOfficerStillInVehicle = false;
                    foreach(Ped officer in _respondingPolicePeds.Where(o => o.Exists() && o.IsAlive)) {
                        if(officer.IsInVehicle(_policeVehicle)) {
                             officer.Tasks.LeaveVehicle(_policeVehicle, false); // Re-task, just in case
                             anyOfficerStillInVehicle = true;
                        }
                    }
                    // If after re-tasking they are still stuck, or if they were already out but didn't approach:
                    if (anyOfficerStillInVehicle && DateTime.Now > _stateTimer + _policeExitVehicleTimeoutDuration + TimeSpan.FromSeconds(5)) {
                        Log("Officers still stuck in vehicle after extended timeout. Aborting."); EndPoliceInteraction(); return;
                    }
                    // If out, but didn't trigger approach logic yet
                    if (!anyOfficerStillInVehicle) { _stateTimer = DateTime.Now; } // Reset timer to give them time to approach now
                }

                bool allOfficersOutOfVehicle = _respondingPolicePeds.All(p => !p.Exists() || !p.IsAlive || !p.IsInVehicle(_policeVehicle));
                if (!allOfficersOutOfVehicle) return; 

                bool allOfficersApproached = true;
                foreach(Ped officer in _respondingPolicePeds.Where(o => o.Exists() && o.IsAlive)) {
                    if (officer.Position.DistanceTo(player.Position) > OfficerApproachDistanceThreshold + 1.0f) { // Slightly larger threshold for check
                        // Only re-task if not already approaching or if task seems stuck (e.g. no movement for a bit)
                        if (officer.Tasks.CurrentTask?.Hash != Game.GenerateHash("TASK_GOTO_ENTITY") || officer.Velocity.LengthSquared() < 0.1f ) {
                            Vector3 approachPoint = player.Position + player.ForwardVector * (OfficerApproachDistanceThreshold -1f) + player.RightVector * ((_respondingPolicePeds.IndexOf(officer) % 2 == 0) ? -1.0f : 1.0f);
                            officer.Task.GoTo(approachPoint, false); 
                        }
                        allOfficersApproached = false;
                    } else { // Arrived
                         if(officer.IsWalking || officer.IsRunning) officer.Task.StandStill(-1); // Ensure they stop
                    }
                }
                if (!allOfficersApproached && DateTime.Now < _stateTimer + _policeApproachTimeoutDuration) return; // Still waiting for approach
                if (!allOfficersApproached && DateTime.Now >= _stateTimer + _policeApproachTimeoutDuration) {
                    Log("Timeout for officers to approach player. Aborting."); EndPoliceInteraction(); return;
                }
                
                Log("All officers approached. Starting dialogue.");
                if (!Game.Player.CanControlCharacter) Game.Player.CanControlCharacter = false; // Ensure it's disabled
                _policeInteractionStage = 0;
                _stateTimer = DateTime.Now; 
                break;
            case 0: 
                if (DateTime.Now < _stateTimer + TimeSpan.FromSeconds(1)) return; // Small settle pause
                UI.Notify("Officer: We received a report of a vehicle collision here.", true);
                Log("Interaction Stage 0: Officer statement 1.");
                _policeInteractionStage++; _stateTimer = DateTime.Now; 
                break;
            case 1: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                UI.Notify("Officer: What's your side of the story?", true);
                Log("Interaction Stage 1: Officer asks player.");
                _playerResponse = Game.GetUserInput(WindowTitle.EnterMessage60, "", 60); 
                if (string.IsNullOrEmpty(_playerResponse)) _playerResponse = "I have nothing to say."; 
                Log($"Player responded: {_playerResponse}");
                _policeInteractionStage++; _stateTimer = DateTime.Now;
                break;
            case 2: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                if (_collidedNpcPed != null && _collidedNpcPed.Exists() && _collidedNpcPed.IsAlive && _incidentLocation.DistanceTo(_collidedNpcPed.Position) < 25f) 
                {
                    if (leadOfficer != null && leadOfficer.Exists()) _collidedNpcPed.Task.LookAt(leadOfficer);
                    else if(player.Exists()) _collidedNpcPed.Task.LookAt(player);
                    string npcStatement = $"NPC ({_collidedNpcPed.Handle}): They crashed right into me! It was their fault!";
                    UI.Notify(npcStatement, true); Log($"Interaction Stage 2: NPC statement: {npcStatement}");
                } else {
                    Log("Interaction Stage 2: Original NPC not available or too far for statement.");
                    UI.Notify("Officer: The other party isn't present to give a statement.", true);
                }
                _policeInteractionStage++; _stateTimer = DateTime.Now;
                break;
            case 3: 
                if (DateTime.Now < _stateTimer + _policeInteractionDialoguePauseDuration) return;
                UI.Notify("Officer: Alright, let me assess the situation...", true);
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
        Log("Deciding police action based on player response and NPC statement.");
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

        if (Game.Player.WantedLevel > 0) {
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
        _stateTimer = DateTime.Now; // Reset timer for outcome processing
    }

    private void ProcessPoliceDecisionOutcome()
    {
        Ped player = Game.Player.Character;
        Ped arrestingOfficer = _respondingPolicePeds.FirstOrDefault(p => p.Exists() && p.IsAlive);
        
        if (arrestingOfficer == null && _currentPoliceDecision == PoliceDecision.ArrestPlayer) {
            Log("No officer available to perform arrest. Letting player go by default.");
            _currentPoliceDecision = PoliceDecision.LetPlayerGo; 
            _stateTimer = DateTime.Now; 
        }

        switch (_currentPoliceDecision)
        {
            case PoliceDecision.ArrestPlayer:
                if (!player.IsBeingArrested && !Function.Call<bool>(Hash.IS_PED_CUFFED, player.Handle)) {
                    UI.Notify("Officer: You're coming with us!", true);
                    if (arrestingOfficer != null && player.Exists()) {
                        Log($"Officer {arrestingOfficer.Handle} is arresting player {player.Handle}.");
                        arrestingOfficer.Tasks.Arrest(player);
                    }
                }
                
                if (DateTime.Now > _stateTimer + _policeArrestSequenceDuration || Function.Call<bool>(Hash.IS_PED_CUFFED, player.Handle)) {
                    UI.ShowSubtitle("~r~BUSTED!", 5000);
                    Log("Player busted. Ending interaction.");
                    Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 1); 
                    EndPoliceInteraction(true); 
                }
                break;

            case PoliceDecision.LetPlayerGo:
                 // Show message once using a flag or by checking current stage if we had one for "saying goodbye"
                if (DateTime.Now < _stateTimer + TimeSpan.FromSeconds(0.5)) { // Display for a short time
                     UI.Notify("Officer: Alright, be more careful next time. You're free to go.", true);
                     Log("Police letting player go.");
                }

                if (DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration) // Wait for message to be read
                {
                    if (_policeVehicle != null && _policeVehicle.Exists())
                    {
                        bool allBoardedOrBoarding = true;
                        Ped driver = _policeVehicle.Driver;

                        foreach (Ped officer in _respondingPolicePeds.Where(o => o.Exists() && o.IsAlive)) {
                            if (!officer.IsInVehicle(_policeVehicle)) {
                                if(officer.Tasks.CurrentTask?.Hash != Game.GenerateHash("TASK_ENTER_VEHICLE")) {
                                     officer.Task.EnterVehicle(_policeVehicle, VehicleSeat.Any);
                                }
                                allBoardedOrBoarding = false;
                            }
                        }
                        // If driver is in but not driving yet, or if others are still boarding
                        if (driver != null && driver.Exists() && driver.IsInVehicle(_policeVehicle) && _policeVehicle.Speed < 1f && 
                            driver.Tasks.CurrentTask?.Hash != Game.GenerateHash("TASK_VEHICLE_DRIVE_WANDER")) {
                            allBoardedOrBoarding = false; // Still waiting for driver to start moving
                        }


                        if (allBoardedOrBoarding || DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration + _policeDepartureBoardingTimeoutDuration) 
                        {
                             driver = _policeVehicle.Driver; // Re-check driver
                             if(driver != null && driver.Exists() && _respondingPolicePeds.Contains(driver) && driver.IsInVehicle(_policeVehicle)) {
                                if (_policeVehicle.Speed < 1f && driver.Tasks.CurrentTask?.Hash != Game.GenerateHash("TASK_VEHICLE_DRIVE_WANDER")) { 
                                    Vector3 awayPos = _policeVehicle.Position + _policeVehicle.ForwardVector * 300f; 
                                    driver.Task.DriveTo(_policeVehicle, World.GetNextPositionOnStreet(awayPos), 5f, 20f, DrivingStyle.Normal);
                                    Log("Police driver tasked to leave.");
                                }
                             } else if (driver == null) { 
                                Log("No police driver to leave. Forcing cleanup."); EndPoliceInteraction(false); return;
                             }
                            
                            if (DateTime.Now > _stateTimer + _policeInteractionDialoguePauseDuration + _policeDepartureBoardingTimeoutDuration + _policeDepartureDriveAwayDuration || 
                                (_policeVehicle != null && _policeVehicle.Position.DistanceTo(player.Position) > 150f) ) 
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
        Log($"Resetting NPC reaction state for Handle: {_collidedNpcPed?.Handle.ToString() ?? "N/A"}.");
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

    public void CleanUpPolice(bool immediate = false) { // Added immediate flag for quicker cleanup if needed
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
        PoliceCalled = false; // Critical: Reset this so new incidents can start
        _incidentLocation = Vector3.Zero;
        Log("Police cleanup complete.");
    }

    private void EndPoliceInteraction(bool playerArrested = false, bool playerFled = false)
    {
        Log($"Ending police interaction. Player arrested: {playerArrested}, Player fled: {playerFled}");
        Ped player = Game.Player.Character;
        if(player.Exists() && !Game.Player.CanControlCharacter) Game.Player.CanControlCharacter = true;
        
        _policeInteractionActive = false;
        _makingPoliceDecision = false;
        _currentPoliceDecision = PoliceDecision.None;
        _policeInteractionStage = 0;
        _playerResponse = string.Empty;
        
        // If player arrested, game's wanted system takes over.
        // If player fled, police might search or give up. For script, we clean up.
        // If let go, police leave and then we clean up.
        // The key is that CleanUpPolice resets PoliceCalled, allowing a new incident.
        
        // For simplicity, always clean up our entities.
        // If player arrested, they are usually teleported or screen fades, our entities might look odd if left too long.
        CleanUpPolice(playerArrested || playerFled); // Pass true for immediate if arrested or fled
    }

    private void FullResetOfScriptStates() {
        Log("Performing full reset of script states.");
        ResetNpcReactionState();
        EndPoliceInteraction(false, false); // This calls CleanUpPolice
        _npcCollisionCooldowns.Clear(); 
        // Reset any other global script flags not covered by above to ensure a clean start
        _incidentLocation = Vector3.Zero;
        _playerResponse = string.Empty;
        Log("Full reset complete.");
    }
   
    private void DispatchPolice(Vector3 incidentLocation) {
        if (_policeDispatched) return; 
        Log($"Dispatching police to: {incidentLocation}");
        _policeDispatched = true;
        _policeArrived = false; 
        _stateTimer = DateTime.Now; // Timer for dispatch timeout

        Vector3 spawnPoint = World.GetNextPositionOnStreet(incidentLocation + (Game.Player.Character.ForwardVector * -100f), true); // Spawn further back
        if (spawnPoint == Vector3.Zero || spawnPoint.DistanceTo(incidentLocation) < 50f) { // Fallback if no good street point or too close
            spawnPoint = incidentLocation + new Vector3(80, 80, 0); // Rough offset if street search fails
            Log("Could not find suitable street position for police spawn, using offset.");
        }

        _policeVehicle = World.CreateVehicle(_policeVehicleModel, spawnPoint, Utility.GetHeadingFromVector(incidentLocation - spawnPoint)); // Face towards incident
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
            // Spawn slightly offset from each other to prevent collision
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
        if (driver != null && driver.Exists() && _respondingPolicePeds.Contains(driver))
        {
            Log($"Police driver {driver.Handle} is in vehicle. Tasking to drive to incident: {incidentLocation}");
            driver.Task.DriveTo(_policeVehicle, incidentLocation, PoliceArrivalDistanceThreshold / 2f, 30.0f, DrivingStyle.Rushed); // Speed in m/s, make it high
            _policeVehicle.IsSirenActive = true; 
        } else {
            Log("Police driver not found or not set in vehicle correctly. Cannot dispatch.");
            CleanUpPolice(); // Clean up partially spawned units
            return;
        }
        UI.Notify("Police have been dispatched to your location!");
    }

    private void Log(string message) {
        try { File.AppendAllText("NPCRoadRage.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " : " + message + Environment.NewLine); }
        catch (Exception ex) { UI.Notify("Log Error: " + ex.Message, true); }
    }

    private void OnKeyDown(object sender, KeyEventArgs e) {
        if (e.KeyCode == Keys.L) {
            Log("Test Key L: Simulating police call for interaction test.");
            if(Game.Player.Character.IsInVehicle()) { Game.Player.Character.Task.LeaveVehicle(); Script.Wait(1500); } // Ensure player is out
            
            FullResetOfScriptStates(); // Clears everything including old police incident

            _incidentLocation = Game.Player.Character.Position + Game.Player.Character.ForwardVector * 10f;
            PoliceCalled = true; // This will trigger dispatch in OnTick
            _stateTimer = DateTime.Now; // Set timer for the initial phase (dispatch timeout)
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
