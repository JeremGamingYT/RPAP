using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class PlayerCriminalRecord
{
    public List<CrimeEntry> CrimeHistory { get; set; } = new List<CrimeEntry>();
    public DateTime FirstCrimeDate { get; set; } = DateTime.MinValue;
    public DateTime LastCrimeDate { get; set; } = DateTime.MinValue;
    public int TotalNotorietyPoints { get; set; } = 0;

    public void AddCrime(CriminalRecordSystem.CrimeType crime, CriminalRecordSystem.CrimeSeverity severity, string location = "")
    {
        var crimeEntry = new CrimeEntry
        {
            Crime = crime,
            Severity = severity,
            Location = location,
            Date = DateTime.Now,
            NotorietyPoints = GetNotorietyPointsForCrime(crime, severity)
        };

        CrimeHistory.Add(crimeEntry);
        TotalNotorietyPoints += crimeEntry.NotorietyPoints;
        
        if (FirstCrimeDate == DateTime.MinValue)
            FirstCrimeDate = DateTime.Now;
            
        LastCrimeDate = DateTime.Now;

        // Decay old crimes over time to make the system more realistic
        DecayOldCrimes();
    }

    private int GetNotorietyPointsForCrime(CriminalRecordSystem.CrimeType crime, CriminalRecordSystem.CrimeSeverity severity)
    {
        int basePoints = (int)severity;
        
        // Some crimes are inherently more notorious
        switch (crime)
        {
            case CriminalRecordSystem.CrimeType.Murder:
                return basePoints * 4;
            case CriminalRecordSystem.CrimeType.BankRobbery:
                return basePoints * 3;
            case CriminalRecordSystem.CrimeType.ArmedRobbery:
                return basePoints * 2;
            case CriminalRecordSystem.CrimeType.EvadingPolice:
                return basePoints * 2;
            case CriminalRecordSystem.CrimeType.ResistingArrest:
                return basePoints * 2;
            case CriminalRecordSystem.CrimeType.VehicleTheft:
                return basePoints + 1;
            case CriminalRecordSystem.CrimeType.Manslaughter:
                return basePoints * 3;
            case CriminalRecordSystem.CrimeType.Assault:
                return basePoints + 1;
            case CriminalRecordSystem.CrimeType.DrugDealing:
                return basePoints * 2;
            case CriminalRecordSystem.CrimeType.WeaponsCharges:
                return basePoints * 2;
            default:
                return basePoints;
        }
    }

    private void DecayOldCrimes()
    {
        // Crimes decay their notoriety impact over time
        DateTime cutoffDate = DateTime.Now.AddDays(-30); // 30 days for significant decay
        
        foreach (var crime in CrimeHistory.Where(c => c.Date < cutoffDate))
        {
            if (!crime.HasDecayed)
            {
                int decayAmount = crime.NotorietyPoints / 2; // Reduce by half
                TotalNotorietyPoints -= decayAmount;
                crime.NotorietyPoints -= decayAmount;
                crime.HasDecayed = true;
            }
        }
        
        // Remove very old minor crimes completely (90 days)
        DateTime removeDate = DateTime.Now.AddDays(-90);
        var crimesToRemove = CrimeHistory.Where(c => 
            c.Date < removeDate && 
            c.Severity == CriminalRecordSystem.CrimeSeverity.Minor).ToList();
            
        foreach (var crime in crimesToRemove)
        {
            TotalNotorietyPoints -= crime.NotorietyPoints;
            CrimeHistory.Remove(crime);
        }
        
        // Ensure notoriety never goes below 0
        TotalNotorietyPoints = Math.Max(0, TotalNotorietyPoints);
    }

    public int GetNotorietyLevel()
    {
        // Convert total points to a 0-5 scale
        if (TotalNotorietyPoints == 0) return 0;
        if (TotalNotorietyPoints <= 5) return 1;
        if (TotalNotorietyPoints <= 15) return 2;
        if (TotalNotorietyPoints <= 30) return 3;
        if (TotalNotorietyPoints <= 50) return 4;
        return 5; // Maximum notoriety
    }

    public List<CrimeEntry> GetCrimeHistory()
    {
        return CrimeHistory.OrderByDescending(c => c.Date).ToList();
    }

    public List<CrimeEntry> GetRecentCrimes(int days = 7)
    {
        DateTime cutoff = DateTime.Now.AddDays(-days);
        return CrimeHistory.Where(c => c.Date >= cutoff).OrderByDescending(c => c.Date).ToList();
    }

    public int GetCrimeCount(CriminalRecordSystem.CrimeType crimeType)
    {
        return CrimeHistory.Count(c => c.Crime == crimeType);
    }

    public bool HasCrime(CriminalRecordSystem.CrimeType crimeType)
    {
        return CrimeHistory.Any(c => c.Crime == crimeType);
    }

    public void ClearRecord()
    {
        CrimeHistory.Clear();
        TotalNotorietyPoints = 0;
        FirstCrimeDate = DateTime.MinValue;
        LastCrimeDate = DateTime.MinValue;
    }

    public string GetNotorietyDescription()
    {
        int level = GetNotorietyLevel();
        switch (level)
        {
            case 0: return "Clean Record";
            case 1: return "Minor Offender";
            case 2: return "Known Criminal";
            case 3: return "Dangerous Individual";
            case 4: return "High-Priority Target";
            case 5: return "Most Wanted";
            default: return "Unknown";
        }
    }

    public bool IsRecognizableBy(NPCType npcType)
    {
        int notoriety = GetNotorietyLevel();
        
        switch (npcType)
        {
            case NPCType.Police:
                return notoriety >= 1; // Police can recognize anyone with a record
            case NPCType.Security:
                return notoriety >= 2; // Security guards recognize known criminals
            case NPCType.Civilian:
                return notoriety >= 3; // Civilians only recognize dangerous individuals
            default:
                return false;
        }
    }
}

public enum NPCType
{
    Police,
    Security,
    Civilian
} 