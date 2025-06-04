using GTA;
using System;

namespace REALIS.TrafficAI
{
    /// <summary>
    /// Représente un véhicule PNJ potentiellement bloqué.
    /// </summary>
    internal class BlockedVehicleInfo
    {
        public Ped Driver { get; }
        public Vehicle Vehicle { get; }
        public float BlockedTime { get; set; }
        public bool Honked { get; set; }

        public BlockedVehicleInfo(Ped driver, Vehicle vehicle)
        {
            Driver = driver;
            Vehicle = vehicle;
            BlockedTime = 0f;
            Honked = false;
        }
    }
}
