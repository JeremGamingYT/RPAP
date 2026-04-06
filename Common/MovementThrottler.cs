using System;
using System.Collections.Generic;

namespace REALIS.Common
{
    /// <summary>
    /// Système de throttling pour limiter l'impact du spam de touches
    /// sur les systèmes de gestion des véhicules.
    /// </summary>
    public static class MovementThrottler
    {
        private static readonly Dictionary<string, DateTime> _lastOperationTime = new();
        private static readonly object _lockObject = new();

        /// <summary>
        /// Vérifie si une opération peut être exécutée en fonction du throttling.
        /// </summary>
        /// <param name="operationKey">Clé unique pour l'opération</param>
        /// <param name="minimumIntervalMs">Intervalle minimum entre les opérations en millisecondes</param>
        /// <returns>True si l'opération peut être exécutée</returns>
        public static bool CanExecute(string operationKey, int minimumIntervalMs)
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                
                if (!_lastOperationTime.TryGetValue(operationKey, out var lastTime))
                {
                    _lastOperationTime[operationKey] = now;
                    return true;
                }

                var elapsed = (now - lastTime).TotalMilliseconds;
                if (elapsed >= minimumIntervalMs)
                {
                    _lastOperationTime[operationKey] = now;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Nettoie les anciennes entrées pour éviter la fuite mémoire.
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var keysToRemove = new List<string>();

                foreach (var kvp in _lastOperationTime)
                {
                    if ((now - kvp.Value).TotalMinutes > 5) // Supprime les entrées plus anciennes que 5 minutes
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _lastOperationTime.Remove(key);
                }
            }
        }

        /// <summary>
        /// Méthode spécialisée pour les requêtes de véhicules proches.
        /// </summary>
        public static bool CanQueryNearbyVehicles()
        {
            return CanExecute("QueryNearbyVehicles", 3000); // AUGMENTÉ: 1 requête toutes les 3 secondes maximum
        }

        /// <summary>
        /// Méthode spécialisée pour le traitement du trafic.
        /// </summary>
        public static bool CanProcessTraffic()
        {
            return CanExecute("ProcessTraffic", 8000); // AUGMENTÉ: 1 traitement toutes les 8 secondes maximum
        }

        /// <summary>
        /// Méthode spécialisée pour les verrous de véhicules.
        /// </summary>
        public static bool CanLockVehicle(int vehicleHandle)
        {
            return CanExecute($"LockVehicle_{vehicleHandle}", 3000); // AUGMENTÉ: 1 verrou par véhicule toutes les 3 secondes maximum
        }
    }
} 