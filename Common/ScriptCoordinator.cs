using GTA;
using System;
using REALIS.TrafficAI;
using REALIS.UrbanLife;
using RPAP;

namespace REALIS.Common
{
    /// <summary>
    /// Simple coordinator to instantiate and centralize major modules.
    /// More complex sequencing can be added later.
    /// </summary>
    public class ScriptCoordinator : Script
    {
        private readonly TrafficIntelligenceManager _trafficAI;
        private readonly UrbanLifeMain _urbanLife;
        private readonly RealisticFuelSystem _fuelSystem;
        private readonly RealisticVehicleHandling _vehicleHandling;
        private readonly RealisticVehicleIntegrity _vehicleIntegrity;
        private readonly RealisticTyreWearAndTemperature _tyreWear;
        private readonly CriminalRecordSystem _criminalRecord;
        private readonly CriminalRecordIntegration _criminalIntegration;

        public ScriptCoordinator()
        {
            _trafficAI = new TrafficIntelligenceManager();
            _urbanLife = new UrbanLifeMain();

            _fuelSystem = new RealisticFuelSystem();
            _vehicleHandling = new RealisticVehicleHandling();
            _vehicleIntegrity = new RealisticVehicleIntegrity();
            _tyreWear = new RealisticTyreWearAndTemperature();
            _criminalRecord = new CriminalRecordSystem();
            _criminalIntegration = new CriminalRecordIntegration();
            _criminalIntegration.SetCriminalSystem(_criminalRecord);

            Tick += OnTick;
            Interval = 1000;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Modules have their own event handling but can be coordinated here.
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"Coordinator error: {ex.Message}", false);
            }
        }
    }
}
