using System;

[Serializable]
public class CrimeEntry
{
    public CriminalRecordSystem.CrimeType Crime { get; set; }
    public CriminalRecordSystem.CrimeSeverity Severity { get; set; }
    public string Location { get; set; } = "";
    public DateTime Date { get; set; }
    public int NotorietyPoints { get; set; }
    public bool HasDecayed { get; set; } = false;
    public string Description { get; set; } = "";
    public bool WitnessesPresent { get; set; } = false;
    public bool PoliceInvolved { get; set; } = false;

    public CrimeEntry()
    {
    }

    public CrimeEntry(CriminalRecordSystem.CrimeType crime, CriminalRecordSystem.CrimeSeverity severity, string location = "")
    {
        Crime = crime;
        Severity = severity;
        Location = location;
        Date = DateTime.Now;
    }

    public string GetDisplayName()
    {
        switch (Crime)
        {
            case CriminalRecordSystem.CrimeType.TrafficViolation:
                return "Traffic Violation";
            case CriminalRecordSystem.CrimeType.Speeding:
                return "Speeding";
            case CriminalRecordSystem.CrimeType.Trespassing:
                return "Trespassing";
            case CriminalRecordSystem.CrimeType.Assault:
                return "Assault";
            case CriminalRecordSystem.CrimeType.Theft:
                return "Theft";
            case CriminalRecordSystem.CrimeType.VehicleTheft:
                return "Vehicle Theft";
            case CriminalRecordSystem.CrimeType.ArmedRobbery:
                return "Armed Robbery";
            case CriminalRecordSystem.CrimeType.Manslaughter:
                return "Manslaughter";
            case CriminalRecordSystem.CrimeType.Murder:
                return "Murder";
            case CriminalRecordSystem.CrimeType.BankRobbery:
                return "Bank Robbery";
            case CriminalRecordSystem.CrimeType.DrugDealing:
                return "Drug Dealing";
            case CriminalRecordSystem.CrimeType.WeaponsCharges:
                return "Weapons Charges";
            case CriminalRecordSystem.CrimeType.ResistingArrest:
                return "Resisting Arrest";
            case CriminalRecordSystem.CrimeType.EvadingPolice:
                return "Evading Police";
            default:
                return Crime.ToString();
        }
    }

    public string GetSeverityDescription()
    {
        switch (Severity)
        {
            case CriminalRecordSystem.CrimeSeverity.Minor:
                return "Minor";
            case CriminalRecordSystem.CrimeSeverity.Moderate:
                return "Moderate";
            case CriminalRecordSystem.CrimeSeverity.Serious:
                return "Serious";
            case CriminalRecordSystem.CrimeSeverity.Severe:
                return "Severe";
            default:
                return Severity.ToString();
        }
    }

    public bool IsRecent(int days = 7)
    {
        return DateTime.Now - Date <= TimeSpan.FromDays(days);
    }

    public bool IsOld(int days = 30)
    {
        return DateTime.Now - Date >= TimeSpan.FromDays(days);
    }

    public double GetAgeInDays()
    {
        return (DateTime.Now - Date).TotalDays;
    }

    public string GetFormattedDescription()
    {
        string desc = $"{GetDisplayName()} ({GetSeverityDescription()})";
        
        if (!string.IsNullOrEmpty(Location))
        {
            desc += $" at {Location}";
        }
        
        desc += $" - {Date:yyyy-MM-dd HH:mm}";
        
        if (HasDecayed)
        {
            desc += " [DECAYED]";
        }
        
        return desc;
    }

    public override string ToString()
    {
        return GetFormattedDescription();
    }
} 