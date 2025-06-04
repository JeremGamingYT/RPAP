using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Système d'intégration pour coordonner UrbanLife avec les autres composants du mod REALIS
    /// </summary>
    public static class UrbanLifeIntegration
    {
        private static HashSet<int> reservedNPCs = new HashSet<int>();
        private static Dictionary<int, DateTime> npcReservationTime = new Dictionary<int, DateTime>();
        private const int RESERVATION_TIMEOUT_MINUTES = 5;
        
        /// <summary>
        /// Réserve un PNJ pour le système UrbanLife
        /// </summary>
        public static bool ReserveNPC(Ped ped)
        {
            if (ped == null || !ped.Exists()) return false;
            
            int handle = ped.Handle;
            
            // Vérifier si déjà réservé par un autre système
            if (IsNPCBusy(ped)) return false;
            
            // Nettoyer les réservations expirées
            CleanupExpiredReservations();
            
            // Réserver le PNJ
            reservedNPCs.Add(handle);
            npcReservationTime[handle] = DateTime.Now;
            
            GTA.UI.Screen.ShowSubtitle($"~g~PNJ {handle} activé pour UrbanLife", 2000);
            return true;
        }
        
        /// <summary>
        /// Libère un PNJ du système UrbanLife
        /// </summary>
        public static void ReleaseNPC(Ped ped)
        {
            if (ped == null) return;
            
            int handle = ped.Handle;
            reservedNPCs.Remove(handle);
            npcReservationTime.Remove(handle);
        }
        
        /// <summary>
        /// Vérifie si un PNJ est déjà utilisé par un autre système
        /// </summary>
        public static bool IsNPCBusy(Ped ped)
        {
            if (!ped.Exists() || ped.IsDead) return true;

            // Vérifier si réservé par UrbanLife
            if (reservedNPCs.Contains(ped.Handle)) return true;

            // Vérifier si c'est un PNJ de police (généralement occupé)
            if (IsPoliceOfficer(ped)) return true;

            // Vérifier si le PNJ a des tâches importantes
            if (HasImportantTasks(ped)) return true;

            return false;
        }
        
        /// <summary>
        /// Vérifie si un PNJ est réservé par UrbanLife
        /// </summary>
        public static bool IsReservedByUrbanLife(Ped ped)
        {
            if (ped == null) return false;
            return reservedNPCs.Contains(ped.Handle);
        }
        
        /// <summary>
        /// Nettoie les réservations expirées
        /// </summary>
        private static void CleanupExpiredReservations()
        {
            var now = DateTime.Now;
            var expiredHandles = npcReservationTime
                .Where(kvp => (now - kvp.Value).TotalMinutes > RESERVATION_TIMEOUT_MINUTES)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var handle in expiredHandles)
            {
                reservedNPCs.Remove(handle);
                npcReservationTime.Remove(handle);
            }
        }
        
        /// <summary>
        /// Vérifie si le PNJ est un officier de police
        /// </summary>
        private static bool IsPoliceOfficer(Ped ped)
        {
            var pedHash = (GTA.PedHash)ped.Model.Hash;
            return pedHash == GTA.PedHash.Cop01SFY || 
                   pedHash == GTA.PedHash.Cop01SMY || 
                   pedHash == GTA.PedHash.Sheriff01SFY ||
                   pedHash == GTA.PedHash.Sheriff01SMY;
        }
        
        /// <summary>
        /// Vérifie si le PNJ a des tâches importantes en cours
        /// </summary>
        private static bool HasImportantTasks(Ped ped)
        {
            try
            {
                // Vérifier si le PNJ est en mission critique
                if (ped.IsInCombat || ped.IsBeingStunned || ped.IsRagdoll) return true;

                // Vérifier si dans un véhicule avec une mission spéciale
                if (ped.IsInVehicle() && ped.CurrentVehicle != null)
                {
                    var vehicle = ped.CurrentVehicle;
                    
                    // Véhicules d'urgence ou de police
                    if (vehicle.ClassType == VehicleClass.Emergency ||
                        vehicle.HasSiren)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return true; // En cas d'erreur, considérer comme occupé
            }
        }
        
        /// <summary>
        /// Obtient des statistiques sur l'utilisation des PNJ
        /// </summary>
        public static IntegrationStats GetStats()
        {
            CleanupExpiredReservations();
            
            return new IntegrationStats
            {
                ReservedNPCs = reservedNPCs.Count,
                TotalNearbyNPCs = World.GetNearbyPeds(Game.Player.Character.Position, 100.0f).Length,
                ActiveSince = DateTime.Now
            };
        }
        
        /// <summary>
        /// Force la libération de tous les PNJ réservés
        /// </summary>
        public static void ReleaseAllNPCs()
        {
            reservedNPCs.Clear();
            npcReservationTime.Clear();
            GTA.UI.Notification.PostTicker("~y~Tous les PNJ UrbanLife libérés", false);
        }
    }
    
    /// <summary>
    /// Statistiques d'intégration
    /// </summary>
    public struct IntegrationStats
    {
        public int ReservedNPCs;
        public int TotalNearbyNPCs;
        public DateTime ActiveSince;
        
        public override string ToString()
        {
            return $"UrbanLife: {ReservedNPCs}/{TotalNearbyNPCs} PNJ gérés";
        }
    }
} 