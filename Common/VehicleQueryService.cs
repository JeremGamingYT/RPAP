using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.Common
{
    /// <summary>
    /// Centralise les requêtes World.GetNearbyVehicles et gère un cache court terme.
    /// Fournit également un système de verrou sur les véhicules pour éviter les conflits
    /// entre scripts qui tentent de les contrôler.
    /// </summary>
    public static class VehicleQueryService
    {
        private static Vehicle[] _cached = Array.Empty<Vehicle>();
        private static Vector3 _lastPosition = Vector3.Zero;
        private static float _lastRadius = 0f;
        private static DateTime _lastUpdate = DateTime.MinValue;
        private const int CacheDurationMs = 500;

        private static readonly HashSet<int> Controlled = new();
        private static readonly object LockObj = new();

        public static Vehicle[] GetNearbyVehicles(Vector3 position, float radius)
        {
            try
            {
                bool refresh = (DateTime.Now - _lastUpdate).TotalMilliseconds > CacheDurationMs
                                 || position.DistanceToSquared(_lastPosition) > 4f
                                 || radius > _lastRadius;

                if (refresh)
                {
                    _cached = World.GetNearbyVehicles(position, radius);
                    _lastPosition = position;
                    _lastRadius = radius;
                    _lastUpdate = DateTime.Now;
                }

                return _cached.Where(v => v != null && v.Exists()
                                         && v.Position.DistanceToSquared(position) <= radius * radius)
                               .ToArray();
            }
            catch
            {
                return Array.Empty<Vehicle>();
            }
        }

        public static bool TryAcquireControl(Vehicle veh)
        {
            if (veh == null || !veh.Exists()) return false;
            lock (LockObj)
            {
                if (Controlled.Contains(veh.Handle)) return false;
                Controlled.Add(veh.Handle);
                return true;
            }
        }

        public static void ReleaseControl(Vehicle veh)
        {
            if (veh == null) return;
            lock (LockObj)
            {
                Controlled.Remove(veh.Handle);
            }
        }

        public static bool IsUnderControl(Vehicle veh)
        {
            if (veh == null) return false;
            lock (LockObj) return Controlled.Contains(veh.Handle);
        }
    }
}
