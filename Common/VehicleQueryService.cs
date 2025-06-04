using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.Common
{
    /// <summary>
    /// Service de requête de véhicules compatible avec l'architecture centralisée.
    /// Maintient la rétrocompatibilité tout en déléguant la gestion des verrous au gestionnaire central.
    /// </summary>
    public static class VehicleQueryService
    {
        private static Vehicle[] _cached = Array.Empty<Vehicle>();
        private static Vector3 _lastPosition = Vector3.Zero;
        private static float _lastRadius = 0f;
        private static DateTime _lastUpdate = DateTime.MinValue;
        private const int CacheDurationMs = 500;

        /// <summary>
        /// Obtient les véhicules proches avec mise en cache pour optimiser les performances.
        /// Utilise une approche thread-safe et compatible avec l'architecture centralisée.
        /// </summary>
        public static Vehicle[] GetNearbyVehicles(Vector3 position, float radius)
        {
            try
            {
                bool refresh = (DateTime.Now - _lastUpdate).TotalMilliseconds > CacheDurationMs
                                 || position.DistanceToSquared(_lastPosition) > 4f
                                 || radius > _lastRadius;

                if (refresh)
                {
                    // Utilise la méthode native avec protection d'erreur
                    _cached = GetSafeNearbyVehicles(position, radius);
                    _lastPosition = position;
                    _lastRadius = radius;
                    _lastUpdate = DateTime.Now;
                }

                // Filtre les véhicules valides dans le rayon demandé
                return _cached.Where(v => IsVehicleValid(v) && 
                                         v.Position.DistanceToSquared(position) <= radius * radius)
                               .ToArray();
            }
            catch (Exception ex)
            {
                SafeLogError($"GetNearbyVehicles error: {ex.Message}");
                return Array.Empty<Vehicle>();
            }
        }

        /// <summary>
        /// Tente d'acquérir le contrôle d'un véhicule.
        /// Utilise le gestionnaire central si disponible, sinon utilise l'ancien système.
        /// </summary>
        public static bool TryAcquireControl(Vehicle veh)
        {
            if (veh == null || !veh.Exists()) return false;

            try
            {
                // Utilise le gestionnaire central si disponible
                var centralManager = CentralEventManager.Instance;
                if (centralManager != null)
                {
                    return centralManager.TryLockVehicle(veh.Handle, "VehicleQueryService", 0);
                }

                // Fallback vers l'ancien système de verrous local (rétrocompatibilité)
                return TryAcquireControlLocal(veh);
            }
            catch (Exception ex)
            {
                SafeLogError($"TryAcquireControl error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Libère le contrôle d'un véhicule.
        /// Compatible avec le gestionnaire central et l'ancien système.
        /// </summary>
        public static void ReleaseControl(Vehicle veh)
        {
            if (veh == null) return;

            try
            {
                // Utilise le gestionnaire central si disponible
                var centralManager = CentralEventManager.Instance;
                if (centralManager != null)
                {
                    centralManager.UnlockVehicle(veh.Handle);
                    return;
                }

                // Fallback vers l'ancien système local
                ReleaseControlLocal(veh);
            }
            catch (Exception ex)
            {
                SafeLogError($"ReleaseControl error: {ex.Message}");
            }
        }

        /// <summary>
        /// Vérifie si un véhicule est sous contrôle.
        /// Compatible avec le gestionnaire central et l'ancien système.
        /// </summary>
        public static bool IsUnderControl(Vehicle veh)
        {
            if (veh == null) return false;

            try
            {
                // Utilise le gestionnaire central si disponible
                var centralManager = CentralEventManager.Instance;
                if (centralManager != null)
                {
                    return centralManager.IsVehicleLocked(veh.Handle);
                }

                // Fallback vers l'ancien système local
                return IsUnderControlLocal(veh);
            }
            catch (Exception ex)
            {
                SafeLogError($"IsUnderControl error: {ex.Message}");
                return false;
            }
        }

        #region Méthodes Internes

        private static Vehicle[] GetSafeNearbyVehicles(Vector3 position, float radius)
        {
            try
            {
                var vehicles = World.GetNearbyVehicles(position, radius);
                return vehicles?.Where(IsVehicleValid).ToArray() ?? Array.Empty<Vehicle>();
            }
            catch (Exception ex)
            {
                SafeLogError($"GetSafeNearbyVehicles error: {ex.Message}");
                return Array.Empty<Vehicle>();
            }
        }

        private static bool IsVehicleValid(Vehicle vehicle)
        {
            try
            {
                return vehicle != null && vehicle.Exists() && !vehicle.IsDead;
            }
            catch
            {
                return false;
            }
        }

        private static void SafeLogError(string message)
        {
            try
            {
                // Évite les logs répétitifs en limitant la fréquence
                GTA.UI.Notification.PostTicker($"~o~[VehicleQuery] {message}", false);
            }
            catch
            {
                // Même le logging peut échouer, on ignore silencieusement
            }
        }

        #endregion

        #region Ancien Système de Verrous (Rétrocompatibilité)

        private static readonly HashSet<int> _localControlled = new();
        private static readonly object _localLockObj = new();

        private static bool TryAcquireControlLocal(Vehicle veh)
        {
            lock (_localLockObj)
            {
                if (_localControlled.Contains(veh.Handle)) return false;
                _localControlled.Add(veh.Handle);
                return true;
            }
        }

        private static void ReleaseControlLocal(Vehicle veh)
        {
            lock (_localLockObj)
            {
                _localControlled.Remove(veh.Handle);
            }
        }

        private static bool IsUnderControlLocal(Vehicle veh)
        {
            lock (_localLockObj)
            {
                return _localControlled.Contains(veh.Handle);
            }
        }

        /// <summary>
        /// Nettoie les verrous locaux orphelins (maintenance préventive)
        /// </summary>
        public static void CleanupLocalLocks()
        {
            try
            {
                lock (_localLockObj)
                {
                    var allVehicles = World.GetAllVehicles();
                    var validHandles = new HashSet<int>(allVehicles.Where(IsVehicleValid).Select(v => v.Handle));
                    
                    var toRemove = _localControlled.Where(handle => !validHandles.Contains(handle)).ToList();
                    foreach (var handle in toRemove)
                    {
                        _localControlled.Remove(handle);
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"CleanupLocalLocks error: {ex.Message}");
            }
        }

        #endregion
    }
}
