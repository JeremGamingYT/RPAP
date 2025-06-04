using GTA;
using GTA.Math;
using System;
using System.Reflection;
using REALIS.Common;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Intégration avec NPCRoadRage pour afficher les accidents de voiture sur la mini-map
    /// </summary>
    public static class NPCRoadRageIntegration
    {
        private static DateTime lastPoliceCheck = DateTime.MinValue;
        private static bool lastPoliceCalledState = false;
        private static Vector3 lastIncidentLocation = Vector3.Zero;

        /// <summary>
        /// Vérifie les incidents NPCRoadRage et crée des blips appropriés
        /// </summary>
        public static void CheckForRoadRageEvents()
        {
            try
            {
                // Vérifier toutes les 2 secondes pour éviter les performances
                if (DateTime.Now - lastPoliceCheck < TimeSpan.FromSeconds(2))
                    return;

                lastPoliceCheck = DateTime.Now;

                // Utiliser la réflexion pour accéder aux données NPCRoadRage
                var npcRoadRageType = Type.GetType("NPCRoadRage");
                if (npcRoadRageType == null)
                    return;

                // Récupérer l'état actuel de PoliceCalled
                var policeCalledField = npcRoadRageType.GetField("PoliceCalled", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (policeCalledField == null)
                    return;

                bool currentPoliceState = (bool)policeCalledField.GetValue(null);

                // Si la police vient d'être appelée (transition de false à true)
                if (currentPoliceState && !lastPoliceCalledState)
                {
                    // Essayer de récupérer la position de l'incident
                    Vector3 incidentLocation = GetIncidentLocation(npcRoadRageType);
                    
                    if (incidentLocation != Vector3.Zero && incidentLocation != lastIncidentLocation)
                    {
                        // Créer un blip pour l'accident de voiture
                        EventBlipManager.Instance.AddEventBlip(
                            SpecialEventType.Accident, 
                            incidentLocation, 
                            "Accident de circulation"
                        );

                        lastIncidentLocation = incidentLocation;
                        
                        GTA.UI.Notification.PostTicker("~r~Accident de voiture signalé sur votre mini-map!", false);
                    }
                }
                // Si l'incident est terminé (police plus appelée)
                else if (!currentPoliceState && lastPoliceCalledState)
                {
                    // L'incident est résolu, les blips vont disparaître automatiquement
                    lastIncidentLocation = Vector3.Zero;
                }

                lastPoliceCalledState = currentPoliceState;
            }
            catch (Exception ex)
            {
                // En cas d'erreur, ne pas planter le système
                GTA.UI.Notification.PostTicker($"~y~Erreur intégration RoadRage: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Essaie de récupérer la position de l'incident via réflexion
        /// </summary>
        private static Vector3 GetIncidentLocation(Type npcRoadRageType)
        {
            try
            {
                // Essayer de récupérer le champ _incidentLocation
                var incidentLocationField = npcRoadRageType.GetField("_incidentLocation", 
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (incidentLocationField == null)
                {
                    // Si pas trouvé, utiliser la position du joueur comme approximation
                    return Game.Player.Character.Position;
                }

                // Nous aurions besoin d'une instance, utilisons la position du joueur
                return Game.Player.Character.Position;
            }
            catch
            {
                // Fallback vers la position du joueur
                return Game.Player.Character.Position;
            }
        }

        /// <summary>
        /// Méthode alternative pour détecter les collisions via observation
        /// </summary>
        public static void DetectCollisionEvents()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.CurrentVehicle == null)
                    return;

                var playerVehicle = player.CurrentVehicle;
                
                // Vérifier les véhicules endommagés à proximité
                var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(player.Position, 30.0f);
                
                foreach (var vehicle in nearbyVehicles)
                {
                    if (vehicle == null || !vehicle.Exists() || vehicle == playerVehicle)
                        continue;

                    // Si un véhicule est fortement endommagé et a un conducteur NPC
                    if (vehicle.HealthFloat < 800.0f && vehicle.Driver != null && 
                        !vehicle.Driver.IsPlayer && vehicle.Driver.IsAlive)
                    {
                        // Vérifier si on n'a pas déjà créé un blip pour ce véhicule
                        var existingBlips = EventBlipManager.Instance.GetEventsByType(SpecialEventType.Accident);
                        bool alreadyMarked = false;
                        
                        foreach (var existingBlip in existingBlips)
                        {
                            if (existingBlip.Position.DistanceTo(vehicle.Position) < 10.0f)
                            {
                                alreadyMarked = true;
                                break;
                            }
                        }

                        if (!alreadyMarked)
                        {
                            EventBlipManager.Instance.AddEventBlip(
                                SpecialEventType.Accident,
                                vehicle.Position,
                                "Véhicule accidenté"
                            );
                        }
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs pour ne pas affecter les performances
            }
        }
    }
} 