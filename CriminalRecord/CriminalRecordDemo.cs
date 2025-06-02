using GTA;
using GTA.UI;
using System;
using System.Windows.Forms;

/// <summary>
/// Script de démonstration pour le système de casier judiciaire
/// Montre comment utiliser les différentes fonctionnalités
/// </summary>
public class CriminalRecordDemo : Script
{
    private CriminalRecordSystem? _criminalSystem;
    private DateTime _lastDemoMessage = DateTime.MinValue;
    private bool _demoMode = false;
    
    public CriminalRecordDemo()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
        
        GTA.UI.Notification.PostTicker("Criminal Record Demo loaded. Press F8 to toggle demo mode.", false, false);
    }
    
    private void OnTick(object sender, EventArgs e)
    {
        if (!_demoMode)
            return;
            
        // Afficher des messages d'aide périodiquement
        if (DateTime.Now - _lastDemoMessage > TimeSpan.FromSeconds(30))
        {
            ShowDemoHelp();
            _lastDemoMessage = DateTime.Now;
        }
    }
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F8:
                ToggleDemoMode();
                break;
                
            case Keys.NumPad1:
                if (_demoMode) AddDemoCrime(CriminalRecordSystem.CrimeType.Speeding, CriminalRecordSystem.CrimeSeverity.Minor);
                break;
                
            case Keys.NumPad2:
                if (_demoMode) AddDemoCrime(CriminalRecordSystem.CrimeType.VehicleTheft, CriminalRecordSystem.CrimeSeverity.Moderate);
                break;
                
            case Keys.NumPad3:
                if (_demoMode) AddDemoCrime(CriminalRecordSystem.CrimeType.ArmedRobbery, CriminalRecordSystem.CrimeSeverity.Serious);
                break;
                
            case Keys.NumPad4:
                if (_demoMode) AddDemoCrime(CriminalRecordSystem.CrimeType.Murder, CriminalRecordSystem.CrimeSeverity.Severe);
                break;
                
            case Keys.NumPad0:
                if (_demoMode) ClearDemoRecord();
                break;
        }
    }
    
    private void ToggleDemoMode()
    {
        _demoMode = !_demoMode;
        
        if (_demoMode)
        {
            GTA.UI.Notification.PostTicker("Demo Mode: ON", false, false);
            ShowDemoHelp();
        }
        else
        {
            GTA.UI.Notification.PostTicker("Demo Mode: OFF", false, false);
        }
    }
    
    private void ShowDemoHelp()
    {
        string helpText = "CRIMINAL RECORD DEMO\n" +
                         "NumPad 1: Add Speeding (Minor)\n" +
                         "NumPad 2: Add Vehicle Theft (Moderate)\n" +
                         "NumPad 3: Add Armed Robbery (Serious)\n" +
                         "NumPad 4: Add Murder (Severe)\n" +
                         "NumPad 0: Clear Record\n" +
                         "F9: Show Criminal Record\n" +
                         "F8: Toggle Demo Mode";
                         
        GTA.UI.Screen.ShowSubtitle(helpText, 10000);
    }
    
    private void AddDemoCrime(CriminalRecordSystem.CrimeType crime, CriminalRecordSystem.CrimeSeverity severity)
    {
        if (_criminalSystem == null)
        {
            // Try to find the criminal system
            _criminalSystem = FindCriminalSystem();
        }
        
        if (_criminalSystem != null)
        {
            string location = GetCurrentLocationName();
            _criminalSystem.AddCrime(crime, severity, location);
            
            string message = $"Demo: Added {crime} ({severity}) at {location}";
            GTA.UI.Notification.PostTicker(message, false, false);
            
            // Show current notoriety level
            int notoriety = _criminalSystem.GetNotorietyLevel();
            GTA.UI.Screen.ShowSubtitle($"Current Notoriety Level: {notoriety}", 3000);
        }
        else
        {
            GTA.UI.Notification.PostTicker("Criminal Record System not found!", true, false);
        }
    }
    
    private void ClearDemoRecord()
    {
        if (_criminalSystem == null)
        {
            _criminalSystem = FindCriminalSystem();
        }
        
        if (_criminalSystem != null)
        {
            _criminalSystem.ClearRecord();
            GTA.UI.Notification.PostTicker("Demo: Criminal record cleared!", false, false);
        }
        else
        {
            GTA.UI.Notification.PostTicker("Criminal Record System not found!", true, false);
        }
    }
    
    private CriminalRecordSystem? FindCriminalSystem()
    {
        // In a real implementation, you would have a proper way to find the system
        // For now, we'll return null and let the user know
        return null;
    }
    
    private string GetCurrentLocationName()
    {
        Ped player = Game.Player.Character;
        if (player != null && player.Exists())
        {
            return World.GetZoneDisplayName(player.Position);
        }
        return "Unknown Location";
    }
} 