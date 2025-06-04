using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using REALIS.Common;

/// <summary>
/// Intégration du système de casier judiciaire avec les autres mods existants
/// Cette classe surveille automatiquement les actions du joueur et enregistre les crimes
/// </summary>
public class CriminalRecordIntegration : Script
{
    private CriminalRecordSystem? _criminalSystem;
    private DateTime _lastWantedCheck = DateTime.MinValue;
    private DateTime _lastVehicleTheftCheck = DateTime.MinValue;
    private DateTime _lastWeaponCheck = DateTime.MinValue;
    private DateTime _lastSpeedCheck = DateTime.MinValue;
    
    // State tracking
    private int _lastWantedLevel = 0;
    private Vehicle? _lastVehicle = null;
    private WeaponHash _lastWeapon = WeaponHash.Unarmed;
    private float _lastSpeed = 0f;
    private Vector3 _lastPosition = Vector3.Zero;
    
    // Configuration
    private const float SPEED_LIMIT_HIGHWAY = 80f; // km/h
    private const float SPEED_LIMIT_CITY = 50f;    // km/h
    private const float SPEED_CHECK_INTERVAL = 2f; // seconds
    private const float VEHICLE_CHECK_INTERVAL = 1f; // seconds
    
    public CriminalRecordIntegration()
    {
        // Attendre que CriminalRecordSystem soit initialisé
        Tick += WaitForCriminalSystem;
    }
    
    private void WaitForCriminalSystem(object sender, EventArgs e)
    {
        // Simple approach - just try to find if any criminal record system exists
        // In practice, you would implement a better discovery mechanism
        _criminalSystem = null; // Will be set when found
        
        Tick -= WaitForCriminalSystem;
        Tick += OnTick;
        Log("Criminal Record Integration initialized (simplified mode).");
    }
    
    private void OnTick(object sender, EventArgs e)
    {
        if (_criminalSystem == null)
            return;
            
        Ped player = Game.Player.Character;
        
        if (Game.IsCutsceneActive || player == null || !player.Exists() || player.IsDead)
            return;
        
        // Vérifier les différents types de crimes
        CheckWantedLevelChanges();
        CheckVehicleTheft();
        CheckWeaponCrimes();
        CheckSpeedingViolations();
        CheckSpecialCrimes();
    }
    
    private void CheckWantedLevelChanges()
    {
        if (DateTime.Now - _lastWantedCheck < TimeSpan.FromSeconds(1))
            return;
            
        _lastWantedCheck = DateTime.Now;
        
        int currentWanted = Game.Player.Wanted.WantedLevel;
        
        if (currentWanted > _lastWantedLevel)
        {
            // Le joueur a gagné du wanted level - quelque chose s'est passé
            switch (currentWanted)
            {
                case 1:
                    _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.TrafficViolation, 
                                           CriminalRecordSystem.CrimeSeverity.Minor, GetCurrentZoneName());
                    break;
                case 2:
                    _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.Assault, 
                                           CriminalRecordSystem.CrimeSeverity.Moderate, GetCurrentZoneName());
                    break;
                case 3:
                    _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.ArmedRobbery, 
                                           CriminalRecordSystem.CrimeSeverity.Serious, GetCurrentZoneName());
                    break;
                case 4:
                case 5:
                    _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.Murder, 
                                           CriminalRecordSystem.CrimeSeverity.Severe, GetCurrentZoneName());
                    break;
            }
        }
        
        _lastWantedLevel = currentWanted;
    }
    
    private void CheckVehicleTheft()
    {
        if (DateTime.Now - _lastVehicleTheftCheck < TimeSpan.FromSeconds(VEHICLE_CHECK_INTERVAL))
            return;
            
        _lastVehicleTheftCheck = DateTime.Now;
        
        Ped player = Game.Player.Character;
        
        if (player.IsInVehicle())
        {
            Vehicle currentVehicle = player.CurrentVehicle;
            
            if (currentVehicle != _lastVehicle)
            {
                // Le joueur a changé de véhicule
                if (_lastVehicle != null && _lastVehicle.Exists())
                {
                    // Vérifier si c'est un vol de véhicule
                    if (!IsPlayerOwnedVehicle(currentVehicle) && 
                        currentVehicle.PreviouslyOwnedByPlayer == false)
                    {
                        var severity = GetVehicleTheftSeverity(currentVehicle);
                        _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.VehicleTheft, 
                                               severity, GetCurrentZoneName());
                    }
                }
                
                _lastVehicle = currentVehicle;
            }
        }
        else
        {
            _lastVehicle = null;
        }
    }
    
    private void CheckWeaponCrimes()
    {
        if (DateTime.Now - _lastWeaponCheck < TimeSpan.FromSeconds(1))
            return;
            
        _lastWeaponCheck = DateTime.Now;
        
        Ped player = Game.Player.Character;
        WeaponHash currentWeapon = player.Weapons.Current.Hash;
        
        if (currentWeapon != _lastWeapon)
        {
            // Le joueur a changé d'arme
            if (IsIllegalWeapon(currentWeapon) && player.IsInPublic())
            {
                _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.WeaponsCharges, 
                                       CriminalRecordSystem.CrimeSeverity.Moderate, GetCurrentZoneName());
            }
            
            _lastWeapon = currentWeapon;
        }
    }
    
    private void CheckSpeedingViolations()
    {
        if (DateTime.Now - _lastSpeedCheck < TimeSpan.FromSeconds(SPEED_CHECK_INTERVAL))
            return;
            
        _lastSpeedCheck = DateTime.Now;
        
        Ped player = Game.Player.Character;
        
        if (!player.IsInVehicle())
            return;
            
        Vehicle vehicle = player.CurrentVehicle;
        float speed = vehicle.Speed * 3.6f; // Convert to km/h
        
        float speedLimit = IsOnHighway() ? SPEED_LIMIT_HIGHWAY : SPEED_LIMIT_CITY;
        float speedOver = speed - speedLimit;
        
        if (speedOver > 20f) // Dépassement significatif
        {
            var severity = speedOver > 50f ? CriminalRecordSystem.CrimeSeverity.Moderate : 
                          CriminalRecordSystem.CrimeSeverity.Minor;
                          
            _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.Speeding, 
                                   severity, GetCurrentZoneName());
                                   
            // Espacer les infractions de vitesse pour éviter le spam
            _lastSpeedCheck = DateTime.Now.AddSeconds(10);
        }
        
        _lastSpeed = speed;
    }
    
    private void CheckSpecialCrimes()
    {
        Ped player = Game.Player.Character;
        
        // Vérifier les crimes spéciaux basés sur l'état du jeu
        if (player.IsBeingStunned && Game.Player.Wanted.WantedLevel > 0)
        {
            // Joueur arrêté - résistance à l'arrestation possible
            if (Game.IsControlPressed(Control.Attack) || Game.IsControlPressed(Control.Aim))
            {
                _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.ResistingArrest, 
                                       CriminalRecordSystem.CrimeSeverity.Serious, GetCurrentZoneName());
            }
        }
        
        // Vérifier si le joueur fuit la police
        if (Game.Player.Wanted.WantedLevel > 0 && player.IsInVehicle() && player.CurrentVehicle.Speed > 15f)
        {
            Vehicle[] policeVehicles = VehicleQueryService.GetNearbyVehicles(player.Position, 100f)
                .Where(v => IsPoliceVehicle(v)).ToArray();
                
            if (policeVehicles.Length > 0)
            {
                // Le joueur fuit activement
                bool hasRecentEvading = HasRecentCrime(CriminalRecordSystem.CrimeType.EvadingPolice, TimeSpan.FromMinutes(2));
                
                if (!hasRecentEvading)
                {
                    _criminalSystem!.AddCrime(CriminalRecordSystem.CrimeType.EvadingPolice, 
                                           CriminalRecordSystem.CrimeSeverity.Serious, GetCurrentZoneName());
                }
            }
        }
    }
    
    // Méthodes utilitaires
    private bool IsPlayerOwnedVehicle(Vehicle vehicle)
    {
        // Logique pour déterminer si le véhicule appartient au joueur
        // Ceci est simplifié - dans un vrai mod, vous pourriez vouloir un système plus sophistiqué
        return vehicle.PreviouslyOwnedByPlayer;
    }
    
    private CriminalRecordSystem.CrimeSeverity GetVehicleTheftSeverity(Vehicle vehicle)
    {
        if (IsPoliceVehicle(vehicle) || IsEmergencyVehicle(vehicle))
            return CriminalRecordSystem.CrimeSeverity.Serious;
        else if (vehicle.Model.IsExpensiveVehicle())
            return CriminalRecordSystem.CrimeSeverity.Moderate;
        else
            return CriminalRecordSystem.CrimeSeverity.Minor;
    }
    
    private bool IsPoliceVehicle(Vehicle vehicle)
    {
        // Simple check for police vehicles by model hash
        VehicleHash[] policeVehicles = {
            VehicleHash.Police, VehicleHash.Police2, VehicleHash.Police3, VehicleHash.Police4,
            VehicleHash.PoliceT, VehicleHash.Sheriff, VehicleHash.Sheriff2
        };
        
        return policeVehicles.Contains((VehicleHash)vehicle.Model.Hash);
    }
    
    private bool IsEmergencyVehicle(Vehicle vehicle)
    {
        VehicleHash[] emergencyVehicles = {
            VehicleHash.Ambulance, VehicleHash.FireTruck
        };
        
        return emergencyVehicles.Contains((VehicleHash)vehicle.Model.Hash);
    }
    
    private bool IsIllegalWeapon(WeaponHash weapon)
    {
        return weapon != WeaponHash.Unarmed && 
               weapon != WeaponHash.Flashlight &&
               weapon != WeaponHash.Nightstick &&
               weapon != WeaponHash.Hammer &&
               weapon != WeaponHash.Crowbar;
    }
    
    private bool IsOnHighway()
    {
        // Logique simplifiée pour détecter les autoroutes
        string zoneName = GetCurrentZoneName();
        return zoneName.Contains("HIGHWAY") || zoneName.Contains("FREEWAY");
    }
    
    private string GetCurrentZoneName()
    {
        Ped player = Game.Player.Character;
        return World.GetZoneDisplayName(player.Position);
    }
    
    private bool HasRecentCrime(CriminalRecordSystem.CrimeType crimeType, TimeSpan timeframe)
    {
        // Cette méthode nécessiterait l'accès aux données du casier judiciaire
        // Pour l'instant, on utilise une logique simplifiée
        return false; // TODO: Implémenter l'accès aux données du casier
    }
    
    private void Log(string message)
    {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [CriminalIntegration] {message}";
        Console.WriteLine(logMessage);
    }
}

// Extensions utilitaires
public static class VehicleExtensions
{
    public static bool IsExpensiveVehicle(this Model model)
    {
        // Liste des véhicules chers/de luxe
        VehicleHash[] expensiveVehicles = {
            VehicleHash.Adder, VehicleHash.Zentorno, VehicleHash.EntityXF,
            VehicleHash.T20, VehicleHash.Osiris, VehicleHash.Reaper,
            VehicleHash.XA21, VehicleHash.Vagner, VehicleHash.Krieger
        };
        
        return expensiveVehicles.Contains((VehicleHash)model.Hash);
    }
}

public static class PedExtensions
{
    public static bool IsInPublic(this Ped ped)
    {
        // Vérifier si le joueur est dans un lieu public
        Ped[] nearbyPeds = World.GetNearbyPeds(ped, 30f);
        return nearbyPeds.Length > 3; // Plus de 3 personnes = lieu public
    }
} 