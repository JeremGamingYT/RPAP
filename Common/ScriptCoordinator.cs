using GTA;
using System;
using REALIS.TrafficAI;
using REALIS.UrbanLife;

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

        public ScriptCoordinator()
        {
            _trafficAI = new TrafficIntelligenceManager();
            _urbanLife = new UrbanLifeMain();

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
                GTA.UI.Notification.Show($"Coordinator error: {ex.Message}");
            }
        }
    }
}
