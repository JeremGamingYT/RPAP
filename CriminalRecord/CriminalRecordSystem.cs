using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

public class CriminalRecordSystem : Script
{
    // --- Configuration ---
    private const float MAX_RECOGNITION_DISTANCE = 25.0f;
    private const float MIN_RECOGNITION_DISTANCE = 5.0f;
    private const int RECOGNITION_CHECK_INTERVAL_MS = 500; // Check every 500ms for performance
    private const float MARKER_HEIGHT_OFFSET = 2.0f;
    private const float MARKER_SIZE = 0.8f;
    
    // --- Criminal Record Data ---
    private PlayerCriminalRecord _playerRecord;
    private List<NPCRecognition> _activeRecognitions = new List<NPCRecognition>();
    private DateTime _lastRecognitionCheck = DateTime.MinValue;
    
    // --- Crime Detection ---
    private int _lastWantedLevel = 0;
    private Vehicle? _lastVehicle = null;
    private DateTime _lastCrimeCheck = DateTime.MinValue;
    private DateTime _lastKillCheck = DateTime.MinValue;
    private bool _wasInCombat = false;
    
    // --- File Paths ---
    private readonly string _recordFilePath = Path.Combine("scripts", "criminal_record.json");
    
    // --- Crime Severity Levels ---
    public enum CrimeSeverity
    {
        Minor = 1,      // Traffic violations, trespassing
        Moderate = 2,   // Theft, assault
        Serious = 3,    // Armed robbery, vehicular manslaughter
        Severe = 4      // Murder, bank robbery, terrorism
    }
    
    // --- Crime Types ---
    public enum CrimeType
    {
        TrafficViolation,
        Speeding,
        Trespassing,
        Assault,
        Theft,
        VehicleTheft,
        ArmedRobbery,
        Manslaughter,
        Murder,
        BankRobbery,
        DrugDealing,
        WeaponsCharges,
        ResistingArrest,
        EvadingPolice
    }

    public CriminalRecordSystem()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
        
        _playerRecord = LoadCriminalRecord();
        
        Log("Criminal Record System loaded successfully.");
        GTA.UI.Notification.PostTicker("Criminal Record System: Active", false, false);
    }

    private void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;
        
        if (Game.IsCutsceneActive || player == null || !player.Exists() || player.IsDead)
            return;

        // Auto-detect crimes
        if (DateTime.Now - _lastCrimeCheck > TimeSpan.FromMilliseconds(1000))
        {
            DetectCrimes();
            _lastCrimeCheck = DateTime.Now;
        }

        // Check for recognition every interval
        if (DateTime.Now - _lastRecognitionCheck > TimeSpan.FromMilliseconds(RECOGNITION_CHECK_INTERVAL_MS))
        {
            UpdateNPCRecognition();
            _lastRecognitionCheck = DateTime.Now;
        }

        // Draw recognition markers
        DrawRecognitionMarkers();
        
        // Clean up invalid recognitions
        CleanupInvalidRecognitions();
    }

    private void DetectCrimes()
    {
        Ped player = Game.Player.Character;
        
        // Check wanted level changes (indicates crimes detected by game)
        int currentWanted = Game.Player.Wanted.WantedLevel;
        if (currentWanted > _lastWantedLevel)
        {
            // Player got wanted level - add appropriate crime
            if (currentWanted >= 3)
            {
                AddCrime(CrimeType.Murder, CrimeSeverity.Severe, GetCurrentLocation());
            }
            else if (currentWanted >= 2)
            {
                AddCrime(CrimeType.Assault, CrimeSeverity.Serious, GetCurrentLocation());
            }
            else
            {
                AddCrime(CrimeType.TrafficViolation, CrimeSeverity.Minor, GetCurrentLocation());
            }
        }
        _lastWantedLevel = currentWanted;
        
        // Check vehicle theft
        if (player.IsInVehicle())
        {
            Vehicle currentVehicle = player.CurrentVehicle;
            if (currentVehicle != _lastVehicle && _lastVehicle != null)
            {
                // Player changed vehicle - possible theft
                if (!currentVehicle.PreviouslyOwnedByPlayer)
                {
                    AddCrime(CrimeType.VehicleTheft, CrimeSeverity.Moderate, GetCurrentLocation());
                }
            }
            _lastVehicle = currentVehicle;
        }
        
        // Check for kills
        if (DateTime.Now - _lastKillCheck > TimeSpan.FromMilliseconds(500))
        {
            CheckForKills();
            _lastKillCheck = DateTime.Now;
        }
        
        // Check if player is in combat
        if (player.IsInCombat && !_wasInCombat)
        {
            // Started combat
            AddCrime(CrimeType.Assault, CrimeSeverity.Moderate, GetCurrentLocation());
        }
        _wasInCombat = player.IsInCombat;
    }

    private void CheckForKills()
    {
        Ped player = Game.Player.Character;
        
        // Check for dead peds around player that might have been killed recently
        Ped[] nearbyPeds = World.GetNearbyPeds(player, 50f);
        
        foreach (Ped ped in nearbyPeds)
        {
            if (ped.IsDead && ped.HasBeenDamagedBy(player))
            {
                // Player killed this ped
                if (ped.RelationshipGroup == Function.Call<int>(Hash.GET_HASH_KEY, "COP"))
                {
                    AddCrime(CrimeType.Murder, CrimeSeverity.Severe, GetCurrentLocation());
                }
                else
                {
                    AddCrime(CrimeType.Murder, CrimeSeverity.Serious, GetCurrentLocation());
                }
                
                // Mark ped as processed to avoid double counting
                ped.MarkAsNoLongerNeeded();
            }
        }
    }

    private string GetCurrentLocation()
    {
        Vector3 pos = Game.Player.Character.Position;
        return Function.Call<string>(Hash.GET_NAME_OF_ZONE, pos.X, pos.Y, pos.Z);
    }

    private void UpdateNPCRecognition()
    {
        if (_playerRecord.GetNotorietyLevel() == 0)
            return; // No crimes, no recognition
            
        Ped player = Game.Player.Character;
        Ped[] nearbyPeds = World.GetNearbyPeds(player, MAX_RECOGNITION_DISTANCE);
        
        foreach (Ped ped in nearbyPeds)
        {
            if (!IsValidForRecognition(ped))
                continue;
                
            ProcessPedRecognition(ped, player);
        }
    }
    
    private bool IsValidForRecognition(Ped ped)
    {
        if (ped == null || !ped.Exists() || ped.IsDead)
            return false;
            
        if (ped == Game.Player.Character)
            return false;
            
        // Only cops and certain civilian types can recognize
        int copHash = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
        int securityHash = Function.Call<int>(Hash.GET_HASH_KEY, "SECURITY_GUARD");
        
        if (ped.RelationshipGroup == copHash || 
            ped.RelationshipGroup == securityHash ||
            IsWitnessPed(ped))
        {
            return true;
        }
        
        return false;
    }
    
    private bool IsWitnessPed(Ped ped)
    {
        // Some civilian types are more observant (shop owners, security, etc.)
        PedHash pedHash = (PedHash)ped.Model.Hash;
        
        return pedHash == PedHash.ShopKeep01 ||
               pedHash == PedHash.Security01SMM ||
               pedHash == PedHash.Bouncer01SMM ||
               pedHash == PedHash.Armoured01SMM ||
               pedHash == PedHash.Armoured02SMM;
    }
    
    private void ProcessPedRecognition(Ped ped, Ped player)
    {
        float distance = ped.Position.DistanceTo(player.Position);
        
        // Find existing recognition or create new one
        NPCRecognition recognition = _activeRecognitions.FirstOrDefault(r => r.NPC.Handle == ped.Handle);
        if (recognition == null)
        {
            recognition = new NPCRecognition(ped);
            _activeRecognitions.Add(recognition);
        }
        
        // Update recognition level based on distance and notoriety
        UpdateRecognitionLevel(recognition, distance);
        
        // Check if NPC recognizes player
        if (recognition.RecognitionLevel >= 1.0f && !recognition.HasRecognized)
        {
            OnNPCRecognizedPlayer(recognition);
        }
    }
    
    private void UpdateRecognitionLevel(NPCRecognition recognition, float distance)
    {
        float notorietyBonus = _playerRecord.GetNotorietyLevel() * 0.1f;
        float distanceFactor = Math.Max(0, (MAX_RECOGNITION_DISTANCE - distance) / MAX_RECOGNITION_DISTANCE);
        
        // Police are better at recognition
        int copHash = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
        float pedTypeMultiplier = recognition.NPC.RelationshipGroup == copHash ? 1.5f : 1.0f;
        
        // Base recognition rate (very slow buildup for realism)
        float recognitionRate = 0.02f * pedTypeMultiplier * (1 + notorietyBonus) * distanceFactor;
        
        if (distance <= MIN_RECOGNITION_DISTANCE)
        {
            recognitionRate *= 2.0f; // Double rate when very close
        }
        
        recognition.RecognitionLevel = Math.Min(1.0f, recognition.RecognitionLevel + recognitionRate);
        recognition.LastUpdate = DateTime.Now;
    }
    
    private void OnNPCRecognizedPlayer(NPCRecognition recognition)
    {
        recognition.HasRecognized = true;
        
        int copHash = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
        if (recognition.NPC.RelationshipGroup == copHash)
        {
            OnPoliceRecognizedPlayer(recognition.NPC);
        }
        else
        {
            OnCivilianRecognizedPlayer(recognition.NPC);
        }
        
        Log($"NPC {recognition.NPC.Handle} recognized the player!");
    }
    
    private void OnPoliceRecognizedPlayer(Ped policePed)
    {
        // Police response based on notoriety level
        int notoriety = _playerRecord.GetNotorietyLevel();
        
        Log($"Police recognition triggered - Notoriety: {notoriety}");
        
        if (notoriety >= 4)
        {
            // Very high notoriety - immediate combat and wanted level
            policePed.Task.ClearAllImmediately();
            policePed.Task.Combat(Game.Player.Character);
            GTA.UI.Notification.PostTicker("Police officer recognized you as extremely dangerous!", true, false);
            Game.Player.Wanted.SetWantedLevel(Math.Min(5, notoriety), false);
        }
        else if (notoriety >= 3)
        {
            // High notoriety - aggressive approach
            policePed.Task.ClearAllImmediately();
            policePed.KeepTaskWhenMarkedAsNoLongerNeeded = true;
            policePed.BlockPermanentEvents = true;
            
            // First aim weapon then approach
            if (policePed.Weapons.Current.Hash == WeaponHash.Unarmed)
            {
                policePed.Weapons.Give(WeaponHash.Pistol, 100, true, true);
            }
            policePed.Task.AimGunAtEntity(Game.Player.Character, 5000, true);
            
            GTA.UI.Notification.PostTicker("Police officer recognized you as a dangerous criminal!", true, false);
            Game.Player.Wanted.SetWantedLevel(Math.Min(3, notoriety), false);
        }
        else if (notoriety >= 2)
        {
            // Medium notoriety - cautious approach
            policePed.Task.ClearAllImmediately();
            policePed.KeepTaskWhenMarkedAsNoLongerNeeded = true;
            policePed.BlockPermanentEvents = true;
            policePed.Task.FollowNavMeshTo(Game.Player.Character.Position);
            GTA.UI.Notification.PostTicker("A police officer is approaching you suspiciously...", false, false);
        }
        else
        {
            // Low notoriety - just watch
            policePed.Task.TurnTo(Game.Player.Character, 5000);
            GTA.UI.Notification.PostTicker("A police officer is watching you...", false, false);
        }
    }
    
    private void OnCivilianRecognizedPlayer(Ped civilianPed)
    {
        int notoriety = _playerRecord.GetNotorietyLevel();
        
        if (notoriety >= 3)
        {
            // High notoriety - civilians are scared and may call police
            civilianPed.Task.ClearAllImmediately();
            civilianPed.Task.ReactAndFlee(Game.Player.Character);
            
            Random random = new Random();
            if (random.Next(100) < 30) // 30% chance to call police
            {
                CallPoliceOnPlayer(civilianPed);
            }
            
            GTA.UI.Notification.PostTicker("A civilian recognized you and seems terrified!", false, false);
        }
        else if (notoriety >= 2)
        {
            // Medium notoriety - civilians are wary
            civilianPed.Task.TurnTo(Game.Player.Character, 3000);
            GTA.UI.Notification.PostTicker("A civilian is staring at you suspiciously...", false, false);
        }
    }
    
    private void CallPoliceOnPlayer(Ped caller)
    {
        // Give player some wanted level
        Game.Player.Wanted.SetWantedLevel(Math.Min(2, _playerRecord.GetNotorietyLevel()), false);
        
        GTA.UI.Notification.PostTicker("Someone is calling the police on you!", true, false);
        Log($"Civilian {caller.Handle} called police on player due to recognition.");
    }
    
    private void DrawRecognitionMarkers()
    {
        foreach (NPCRecognition recognition in _activeRecognitions)
        {
            if (!recognition.NPC.Exists() || recognition.NPC.IsDead)
                continue;
                
            float distance = recognition.NPC.Position.DistanceTo(Game.Player.Character.Position);
            if (distance > MAX_RECOGNITION_DISTANCE)
                continue;
                
            // Calculate marker color based on recognition level
            Color markerColor = GetRecognitionColor(recognition.RecognitionLevel);
            
            // Draw arrow marker above NPC pointing down
            Vector3 markerPos = recognition.NPC.Position + Vector3.WorldUp * MARKER_HEIGHT_OFFSET;
            Vector3 markerDir = Vector3.WorldDown; // Point down towards the NPC
            Vector3 markerRot = Vector3.Zero;
            
            // Use cone marker pointing down instead of cylinder
            World.DrawMarker(MarkerType.Cone, markerPos, markerDir, markerRot, 
                           new Vector3(MARKER_SIZE, MARKER_SIZE, 1.0f), markerColor);
        }
    }
    
    private Color GetRecognitionColor(float recognitionLevel)
    {
        // Interpolate from green (0% recognition) to red (100% recognition)
        int red = (int)(255 * recognitionLevel);
        int green = (int)(255 * (1.0f - recognitionLevel));
        int blue = 0;
        int alpha = (int)(200 + (55 * recognitionLevel)); // More visible as recognition increases
        
        return Color.FromArgb(alpha, red, green, blue);
    }
    
    private void CleanupInvalidRecognitions()
    {
        _activeRecognitions.RemoveAll(r => 
            !r.NPC.Exists() || 
            r.NPC.IsDead || 
            r.NPC.Position.DistanceTo(Game.Player.Character.Position) > MAX_RECOGNITION_DISTANCE * 1.5f ||
            DateTime.Now - r.LastUpdate > TimeSpan.FromSeconds(30));
    }
    
    // --- Public Methods for Other Scripts ---
    public void AddCrime(CrimeType crime, CrimeSeverity severity = CrimeSeverity.Minor, string location = "")
    {
        _playerRecord.AddCrime(crime, severity, location);
        SaveCriminalRecord();
        
        string message = $"Crime recorded: {crime} ({severity})";
        GTA.UI.Notification.PostTicker(message, false, false);
        Log(message);
    }
    
    public int GetNotorietyLevel()
    {
        return _playerRecord.GetNotorietyLevel();
    }
    
    public void ClearRecord()
    {
        _playerRecord.ClearRecord();
        SaveCriminalRecord();
        GTA.UI.Notification.PostTicker("Criminal record cleared.", false, false);
    }
    
    // --- Save/Load System ---
    private PlayerCriminalRecord LoadCriminalRecord()
    {
        try
        {
            if (File.Exists(_recordFilePath))
            {
                string json = File.ReadAllText(_recordFilePath);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerCriminalRecord>(json) 
                       ?? new PlayerCriminalRecord();
            }
        }
        catch (Exception ex)
        {
            Log($"Error loading criminal record: {ex.Message}");
        }
        
        return new PlayerCriminalRecord();
    }
    
    private void SaveCriminalRecord()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_recordFilePath));
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(_playerRecord, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_recordFilePath, json);
        }
        catch (Exception ex)
        {
            Log($"Error saving criminal record: {ex.Message}");
        }
    }
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Debug commands
        if (e.KeyCode == Keys.F5)
        {
            // Show criminal record
            ShowCriminalRecordUI();
        }
        else if (e.KeyCode == Keys.F10)
        {
            // Add test crime
            AddCrime(CrimeType.Speeding, CrimeSeverity.Minor, "Test Location");
        }
        else if (e.KeyCode == Keys.F11)
        {
            // Clear record
            ClearRecord();
        }
    }
    
    private void ShowCriminalRecordUI()
    {
        var crimes = _playerRecord.GetCrimeHistory();
        int notoriety = _playerRecord.GetNotorietyLevel();
        
        string message = $"CRIMINAL RECORD\nNotoriety Level: {notoriety}\nTotal Crimes: {crimes.Count}\n\nRecent Crimes:\n";
        
        var recentCrimes = crimes.Take(5);
        foreach (var crime in recentCrimes)
        {
            message += $"• {crime.Crime} ({crime.Severity}) - {crime.Date:yyyy-MM-dd}\n";
        }
        
        GTA.UI.Screen.ShowSubtitle(message, 8000);
    }
    
    private void Log(string message)
    {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [CriminalRecord] {message}";
        Console.WriteLine(logMessage);
        
        try
        {
            File.AppendAllText("CriminalRecordSystem.log", logMessage + Environment.NewLine);
        }
        catch { }
    }
} 