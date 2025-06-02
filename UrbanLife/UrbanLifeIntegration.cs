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
        /// Vérifie si un PNJ est occupé par un autre système
        /// </summary>
        public static bool IsNPCBusy(Ped ped)
        {
            if (ped == null || !ped.Exists()) return true;
            
            // Vérifier si c'est le joueur
            if (ped.IsPlayer) return true;
            
            // Vérifier si c'est un PNJ de police (utilisé par NPCRoadRage)
            if (IsPolicePed(ped)) return true;
            
            // Vérifier si impliqué dans un incident de NPCRoadRage
            if (IsInvolvedInRoadRageIncident(ped)) return true;
            
            // Vérifier si en mission ou combat
            if (ped.IsInCombat || ped.IsFleeing) return true;
            
            // Vérifier si a des tâches importantes en cours
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
        /// Vérifie si c'est un PNJ de police
        /// </summary>
        private static bool IsPolicePed(Ped ped)
        {
            var policeHashes = new[]
            {
                PedHash.Cop01SFY, PedHash.Cop01SMY, PedHash.Sheriff01SFY, PedHash.Sheriff01SMY,
                PedHash.Swat01SMY, PedHash.Security01SMM, PedHash.Armoured01SMM,
                PedHash.Paramedic01SMM
            };
            
            return policeHashes.Contains((PedHash)ped.Model.Hash);
        }
        
        /// <summary>
        /// Vérifie si le PNJ est impliqué dans un incident NPCRoadRage
        /// </summary>
        private static bool IsInvolvedInRoadRageIncident(Ped ped)
        {
            // Vérifier via réflexion si NPCRoadRage est actif et utilise ce PNJ
            try
            {
                // Accès à la classe NPCRoadRage via réflexion pour éviter les dépendances directes
                var npcRoadRageType = Type.GetType("NPCRoadRage");
                if (npcRoadRageType != null)
                {
                    var policeCalledField = npcRoadRageType.GetField("PoliceCalled", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (policeCalledField != null && (bool)policeCalledField.GetValue(null))
                    {
                        // Si la police est appelée, ne pas interférer avec les PNJ proches du joueur
                        var player = Game.Player.Character;
                        if (player != null && ped.Position.DistanceTo(player.Position) < 100.0f)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // En cas d'erreur, jouer la sécurité
            }
            
            return false;
        }
        
        /// <summary>
        /// Vérifie si le PNJ a des tâches importantes
        /// </summary>
        private static bool HasImportantTasks(Ped ped)
        {
            // Vérifier les animations importantes
            if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, "mp_arresting", "idle", 3) ||
                Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, "cellphone@", "cellphone_call_listen_base", 3))
            {
                return true;
            }
            
            // Vérifier si en train de conduire de manière urgente
            if (ped.IsInVehicle() && ped.CurrentVehicle != null)
            {
                var vehicle = ped.CurrentVehicle;
                if (vehicle.IsSirenActive || vehicle.Speed > 20.0f)
                {
                    return true;
                }
            }
            
            return false;
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