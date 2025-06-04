using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using REALIS.Common;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Gestionnaire des mini-événements routiers
    /// Crée des scènes de la vie quotidienne sur les routes
    /// </summary>
    public class RoadEventManager
    {
        private Random random;
        private List<RoadEvent> activeRoadEvents;
        private DateTime lastEventCheck;
        private DateTime lastEventCreation;
        private DateTime lastInteractionCheck;
        
        // NOUVEAU: Variables pour éviter le spam des touches
        private DateTime lastEKeyPress = DateTime.MinValue;
        private DateTime lastFKeyPress = DateTime.MinValue;
        private DateTime lastGKeyPress = DateTime.MinValue;
        private const double KEY_COOLDOWN_SECONDS = 1.0; // 1 seconde entre chaque appui
        
        // Configuration
        private const float EVENT_CREATION_DISTANCE_MIN = 80.0f;
        private const float EVENT_CREATION_DISTANCE_MAX = 200.0f;
        private const float EVENT_CLEANUP_DISTANCE = 300.0f;
        private const double BASE_EVENT_PROBABILITY = 0.08f; // 8% par vérification (augmenté de 5% à 8%)
        private const float INTERACTION_DISTANCE = 5.0f; // Distance pour interagir avec les pannes
        
        public RoadEventManager()
        {
            random = new Random();
            activeRoadEvents = new List<RoadEvent>();
            lastEventCheck = DateTime.Now;
            lastEventCreation = DateTime.Now;
            lastInteractionCheck = DateTime.Now;
        }
        
        public void Update()
        {
            // Nettoyer les événements anciens/éloignés
            CleanupDistantEvents();
            
            // Créer de nouveaux événements
            if ((DateTime.Now - lastEventCheck).TotalSeconds > 5) // Vérifier toutes les 5 secondes (réduit de 15)
            {
                CheckForNewRoadEvents();
                lastEventCheck = DateTime.Now;
            }
            
            // Vérifier les interactions avec les pannes
            if ((DateTime.Now - lastInteractionCheck).TotalSeconds > 1) // Vérifier toutes les secondes
            {
                CheckBreakdownInteractions();
                lastInteractionCheck = DateTime.Now;
            }
            
            // La surveillance automatique des passagers provoque parfois des
            // plantages lorsqu'on conduit. Elle est désactivée pour l'instant
            // pour éviter ces crashs.
            //MonitorPassengerStability();
            
            // Mettre à jour les événements actifs
            UpdateActiveRoadEvents();
        }
        
        private void CheckForNewRoadEvents()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.CurrentVehicle == null) return; // Seulement quand le joueur conduit
                
                // Limiter le nombre d'événements actifs
                if (activeRoadEvents.Count >= 3) return; // Augmenté de 2 à 3
                
                // Délai minimum entre événements réduit
                if ((DateTime.Now - lastEventCreation).TotalSeconds < 30) return; // Réduit de 1 minute à 30 secondes
                
                // Test de probabilité amélioré
                if (random.NextDouble() < BASE_EVENT_PROBABILITY)
                {
                    CreateRandomRoadEvent();
                }
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur vérification événements routiers", false);
            }
        }
        
        private void CreateRandomRoadEvent()
        {
            var player = Game.Player.Character;
            
            // NOUVEAU: Favoriser les événements de panne (plus intéressants pour l'interaction)
            RoadEventType selectedType;
            var randomValue = random.NextDouble();
            
            if (randomValue < 0.5) // 50% de chance d'avoir une panne
            {
                selectedType = RoadEventType.BrokenDownVehicle;
            }
            else if (randomValue < 0.7) // 20% de chance de contrôle police
            {
                selectedType = RoadEventType.PoliceStop;
            }
            else if (randomValue < 0.85) // 15% de chance d'accident
            {
                selectedType = RoadEventType.TrafficAccident;
            }
            else if (randomValue < 0.95) // 10% de chance de paramédics
            {
                selectedType = RoadEventType.Paramedics;
            }
            else // 5% de chance pour les autres
            {
                var otherTypes = new[] { RoadEventType.RoadConstruction, RoadEventType.SpeedControl };
                selectedType = otherTypes[random.Next(otherTypes.Length)];
            }
            
            // Trouver une position sur une route devant le joueur
            var eventPosition = FindSuitableRoadPositionAhead();
            if (eventPosition == Vector3.Zero) return;
            
            switch (selectedType)
            {
                case RoadEventType.PoliceStop:
                    CreatePoliceStopEvent(eventPosition);
                    break;
                case RoadEventType.TrafficAccident:
                    CreateTrafficAccidentEvent(eventPosition);
                    break;
                case RoadEventType.RoadConstruction:
                    CreateRoadConstructionEvent(eventPosition);
                    break;
                case RoadEventType.SpeedControl:
                    CreateSpeedControlEvent(eventPosition);
                    break;
                case RoadEventType.BrokenDownVehicle:
                    CreateBrokenDownVehicleEvent(eventPosition);
                    break;
                case RoadEventType.Paramedics:
                    CreateParamedicsEvent(eventPosition);
                    break;
            }
        }
        
        private Vector3 FindSuitableRoadPositionAhead()
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            
            // Priorité à la direction devant le joueur (en voiture)
            Vector3 playerForward = Vector3.Zero;
            if (player.CurrentVehicle != null)
            {
                playerForward = player.CurrentVehicle.ForwardVector;
            }
            else
            {
                playerForward = player.ForwardVector;
            }
            
            // Essayer plusieurs positions devant le joueur d'abord
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Vector3 direction;
                float distance;
                
                if (attempt < 8) // 8 premiers essais : devant le joueur
                {
                    // Angle léger par rapport à la direction du joueur (-30° à +30°)
                    float angle = ((float)random.NextDouble() - 0.5f) * 60.0f; // -30 à +30 degrés
                    
                    // Rotation manuelle autour de l'axe Z
                    float angleRad = (float)(angle * Math.PI / 180.0);
                    float cos = (float)Math.Cos(angleRad);
                    float sin = (float)Math.Sin(angleRad);
                    
                    var rotatedForward = new Vector3(
                        playerForward.X * cos - playerForward.Y * sin,
                        playerForward.X * sin + playerForward.Y * cos,
                        playerForward.Z
                    );
                    
                    direction = rotatedForward.Normalized;
                    distance = random.Next(100, 250); // Plus proche pour être visible
                }
                else // Autres essais : positions aléatoires
                {
                    direction = Vector3.RandomXY().Normalized;
                    distance = random.Next((int)EVENT_CREATION_DISTANCE_MIN, (int)EVENT_CREATION_DISTANCE_MAX);
                }
                
                var testPos = playerPos + (direction * distance);
                
                // Vérifier que c'est sur une route avec une logique améliorée
                float groundZ;
                if (World.GetGroundHeight(testPos, out groundZ))
                {
                    testPos.Z = groundZ + 1.0f; // Légèrement au-dessus du sol
                    
                    // Méthode améliorée : d'abord vérifier si c'est déjà sur une route
                    if (IsValidRoadPosition(testPos))
                    {
                        return testPos;
                    }
                    
                    // Si pas sur route, forcer le positionnement sur la route la plus proche
                    var roadPos = ForceRoadPosition(testPos);
                    if (roadPos != testPos && IsValidRoadPosition(roadPos))
                    {
                        return roadPos;
                    }
                }
            }
            
            return Vector3.Zero; // Échec
        }
        
        private bool IsValidRoadPosition(Vector3 position)
        {
            try
            {
                // Méthode 1: Utiliser GET_CLOSEST_VEHICLE_NODE pour trouver une route proche
                Vector3 nodePosition = Vector3.Zero;
                if (GTA.Native.Function.Call<bool>(GTA.Native.Hash.GET_CLOSEST_VEHICLE_NODE, 
                    position.X, position.Y, position.Z, 1, 3.0f, 0))
                {
                    // Si on trouve un nœud proche, c'est probablement une route
                    return true;
                }
                
                // Méthode 2: Utiliser IS_POINT_ON_ROAD avec une tolérance plus large
                bool isOnRoad = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_POINT_ON_ROAD, position.X, position.Y, position.Z, 0);
                if (isOnRoad)
                {
                    return true;
                }
                
                // Méthode 3: Vérifier le trafic dans la zone (réduit le rayon)
                var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(position, 50.0f);
                if (nearbyVehicles.Length >= 1)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                // En cas d'erreur, rejeter la position
                return false;
            }
        }
        
        /// <summary>
        /// Force le positionnement sur une route proche en utilisant GET_CLOSEST_VEHICLE_NODE
        /// </summary>
        private Vector3 ForceRoadPosition(Vector3 originalPosition)
        {
            try
            {
                Vector3 roadPosition = Vector3.Zero;
                
                // Utiliser GET_CLOSEST_VEHICLE_NODE plus simple
                var result = GTA.Native.Function.Call<Vector3>(GTA.Native.Hash.GET_CLOSEST_VEHICLE_NODE,
                    originalPosition.X, originalPosition.Y, originalPosition.Z, 1, 10.0f, 0);
                
                if (result != Vector3.Zero)
                {
                    return result;
                }
                
                return originalPosition; // Si échec, retourner la position originale
            }
            catch
            {
                return originalPosition;
            }
        }
        
        private void CreatePoliceStopEvent(Vector3 position)
        {
            try
            {
                // Créer un véhicule civil
                var civilCarModels = new[] { VehicleHash.Blista, VehicleHash.Premier, VehicleHash.Fugitive, VehicleHash.Stratum };
                var civilCar = World.CreateVehicle(civilCarModels[random.Next(civilCarModels.Length)], position);
                
                if (civilCar == null) return;
                
                // Créer un conducteur civil
                var civilDriver = civilCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business01AMY);
                
                // Créer un véhicule de police
                var policePos = position + new Vector3(0f, -8.0f, 0f);
                var policeCar = World.CreateVehicle(VehicleHash.Police, policePos);
                
                if (policeCar == null) 
                {
                    civilCar?.Delete();
                    return;
                }
                
                // Créer un policier
                var officer = policeCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Cop01SMY);
                
                // Configuration
                civilDriver.IsPersistent = true;
                officer.IsPersistent = true;
                civilCar.IsPersistent = true;
                policeCar.IsPersistent = true;
                
                // Sortir les personnages des véhicules
                civilDriver.Task.LeaveVehicle();
                officer.Task.LeaveVehicle();
                
                // Créer l'événement
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.PoliceStop,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { civilCar, policeCar },
                    Participants = new List<Ped> { civilDriver, officer },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                
                // Ajouter un blip
                AddRoadEventBlip(roadEvent, "Contrôle de police", BlipSprite.PoliceOfficer, BlipColor.Blue);
                
                lastEventCreation = DateTime.Now;
                GTA.UI.Notification.PostTicker("~b~Contrôle de police repéré sur la route", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création contrôle police", false);
            }
        }
        
        private void CreateTrafficAccidentEvent(Vector3 position)
        {
            try
            {
                // Créer deux véhicules endommagés
                var car1 = World.CreateVehicle(VehicleHash.Sultan, position);
                var car2 = World.CreateVehicle(VehicleHash.Primo, position + new Vector3(0f, 5.0f, 0f));
                
                if (car1 == null || car2 == null)
                {
                    car1?.Delete();
                    car2?.Delete();
                    return;
                }
                
                // Endommager les véhicules
                car1.HealthFloat = 400.0f;
                car2.HealthFloat = 350.0f;
                car1.EngineHealth = 100.0f;
                car2.EngineHealth = 50.0f;
                
                // Créer les conducteurs
                var driver1 = car1.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business01AMY);
                var driver2 = car2.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business02AFY);
                
                // Configuration
                driver1.IsPersistent = true;
                driver2.IsPersistent = true;
                car1.IsPersistent = true;
                car2.IsPersistent = true;
                
                // Les faire sortir et discuter
                driver1.Task.LeaveVehicle();
                driver2.Task.LeaveVehicle();
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.TrafficAccident,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { car1, car2 },
                    Participants = new List<Ped> { driver1, driver2 },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                
                AddRoadEventBlip(roadEvent, "Accident de circulation", BlipSprite.Devin, BlipColor.Red);
                
                lastEventCreation = DateTime.Now;
                GTA.UI.Notification.PostTicker("~r~Accident de la route repéré", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création accident", false);
            }
        }
        
        private void CreateRoadConstructionEvent(Vector3 position)
        {
            try
            {
                // Créer des véhicules de chantier
                var constructionTruck = World.CreateVehicle(VehicleHash.Rubble, position);
                var utilityTruck = World.CreateVehicle(VehicleHash.UtilityTruck, position + new Vector3(8.0f, 0f, 0f));
                
                if (constructionTruck == null) return;
                
                // Créer des ouvriers
                var worker1 = World.CreatePed(PedHash.Autoshop01SMM, position + new Vector3(0f, 3.0f, 0f));
                var worker2 = World.CreatePed(PedHash.Autoshop02SMM, position + new Vector3(0f, 6.0f, 0f));
                
                // Configuration
                worker1.IsPersistent = true;
                worker2.IsPersistent = true;
                constructionTruck.IsPersistent = true;
                if (utilityTruck != null) utilityTruck.IsPersistent = true;
                
                var vehicles = new List<Vehicle> { constructionTruck };
                if (utilityTruck != null) vehicles.Add(utilityTruck);
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.RoadConstruction,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = vehicles,
                    Participants = new List<Ped> { worker1, worker2 },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                
                AddRoadEventBlip(roadEvent, "Travaux routiers", BlipSprite.Cargobob, BlipColor.Orange);
                
                lastEventCreation = DateTime.Now;
                GTA.UI.Notification.PostTicker("~o~Travaux routiers en cours", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création travaux", false);
            }
        }
        
        private void CreateSpeedControlEvent(Vector3 position)
        {
            try
            {
                // Voiture de police cachée
                var policeCar = World.CreateVehicle(VehicleHash.Police2, position);
                if (policeCar == null) return;
                
                var officer = policeCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Cop01SMY);
                
                officer.IsPersistent = true;
                policeCar.IsPersistent = true;
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.SpeedControl,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { policeCar },
                    Participants = new List<Ped> { officer },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                
                AddRoadEventBlip(roadEvent, "Radar mobile", BlipSprite.PoliceStation, BlipColor.Blue);
                
                lastEventCreation = DateTime.Now;
                GTA.UI.Notification.PostTicker("~b~Radar mobile détecté", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création radar", false);
            }
        }
        
        private void CreateBrokenDownVehicleEvent(Vector3 position)
        {
            try
            {
                var brokenCar = World.CreateVehicle(VehicleHash.Asea, position);
                if (brokenCar == null) return;
                
                // Endommager le véhicule
                brokenCar.EngineHealth = 0.0f;
                brokenCar.HealthFloat = 600.0f;
                
                var driver = brokenCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business01AMY);
                driver.IsPersistent = true;
                brokenCar.IsPersistent = true;
                
                // Le faire sortir et regarder le moteur
                driver.Task.LeaveVehicle();
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.BrokenDownVehicle,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { brokenCar },
                    Participants = new List<Ped> { driver },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                
                AddRoadEventBlip(roadEvent, "Véhicule en panne", BlipSprite.Garage, BlipColor.Yellow);
                
                lastEventCreation = DateTime.Now;
                GTA.UI.Notification.PostTicker("~y~Véhicule en panne sur la route", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création panne", false);
            }
        }
        
        private void CreateParamedicsEvent(Vector3 position)
        {
            try
            {
                var ambulance = World.CreateVehicle(VehicleHash.Ambulance, position);
                if (ambulance == null) return;
                
                var paramedic1 = ambulance.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Paramedic01SMM);
                var paramedic2 = World.CreatePed(PedHash.Paramedic01SMM, position + new Vector3(0f, 4.0f, 0f));
                var patient = World.CreatePed(PedHash.Business01AMY, position + new Vector3(0f, 2.0f, 0f));
                
                paramedic1.IsPersistent = true;
                paramedic2.IsPersistent = true;
                patient.IsPersistent = true;
                ambulance.IsPersistent = true;
                
                // Le patient est au sol
                Function.Call(Hash.TASK_PLAY_ANIM, patient, "amb@medic@standing@kneel@base", "base", 8.0f, -1, 1, 0.0f, 0);
                
                paramedic1.Task.LeaveVehicle();
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.Paramedics,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { ambulance },
                    Participants = new List<Ped> { paramedic1, paramedic2, patient },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                
                AddRoadEventBlip(roadEvent, "Intervention médicale", BlipSprite.Hospital, BlipColor.White);
                
                lastEventCreation = DateTime.Now;
                GTA.UI.Notification.PostTicker("~w~Intervention médicale en cours", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création intervention médicale", false);
            }
        }
        
        private void AddRoadEventBlip(RoadEvent roadEvent, string name, BlipSprite sprite, BlipColor color)
        {
            var blip = World.CreateBlip(roadEvent.Position);
            blip.Sprite = sprite;
            blip.Color = color;
            blip.Scale = 0.8f;
            blip.Name = name;
            blip.IsShortRange = false;
            blip.IsFlashing = true;
            
            if (roadEvent.Type == RoadEventType.TrafficAccident
                || roadEvent.Type == RoadEventType.Paramedics
                || roadEvent.Type == RoadEventType.BrokenDownVehicle)
            {
                // Afficher l'itinéraire violet comme pour un point GPS normal
                blip.ShowRoute = true;
            }
            
            roadEvent.Blip = blip;
        }
        
        private void UpdateActiveRoadEvents()
        {
            // NOUVEAU: Nettoyage de sécurité en début de cycle
            CleanupCorruptedEvents();
            
            for (int i = activeRoadEvents.Count - 1; i >= 0; i--)
            {
                var roadEvent = activeRoadEvents[i];
                var elapsedTime = DateTime.Now - roadEvent.StartTime;
                
                // Supprimer les événements anciens (10 minutes)
                if (elapsedTime.TotalMinutes > 10)
                {
                    CleanupRoadEvent(roadEvent);
                    activeRoadEvents.RemoveAt(i);
                    continue;
                }
                
                // Supprimer les événements marqués pour suppression (phase 95, 4, 5)
                if (roadEvent.Phase >= 95 || roadEvent.Phase == 4 || roadEvent.Phase == 5)
                {
                    CleanupRoadEvent(roadEvent);
                    activeRoadEvents.RemoveAt(i);
                    continue;
                }
                
                // Mettre à jour les comportements selon le type d'événement
                UpdateRoadEventBehavior(roadEvent, elapsedTime);
            }
        }
        
        private void UpdateRoadEventBehavior(RoadEvent roadEvent, TimeSpan elapsedTime)
        {
            switch (roadEvent.Type)
            {
                case RoadEventType.PoliceStop:
                    UpdatePoliceStopBehavior(roadEvent, elapsedTime);
                    break;
                case RoadEventType.TrafficAccident:
                    UpdateTrafficAccidentBehavior(roadEvent, elapsedTime);
                    break;
                case RoadEventType.BrokenDownVehicle:
                    UpdateBrokenDownVehicleBehavior(roadEvent, elapsedTime);
                    break;
                case RoadEventType.Paramedics:
                    UpdateParamedicsBehavior(roadEvent, elapsedTime);
                    break;
            }
        }
        
        private void UpdatePoliceStopBehavior(RoadEvent roadEvent, TimeSpan elapsedTime)
        {
            if (roadEvent.Participants.Count < 2) return;
            
            var civilian = roadEvent.Participants[0];
            var officer = roadEvent.Participants[1];
            
            if (!civilian.Exists() || !officer.Exists()) return;
            
            switch (roadEvent.Phase)
            {
                case 1: // Phase initiale - sortie des véhicules
                    if (elapsedTime.TotalSeconds > 3)
                    {
                        // L'officier s'approche du civil
                        officer.Task.FollowNavMeshTo(civilian.Position + new Vector3(0f, 2.0f, 0f));
                        roadEvent.Phase = 2;
                    }
                    break;
                case 2: // Discussion
                    if (elapsedTime.TotalSeconds > 15)
                    {
                        // Retour aux véhicules
                        civilian.Task.EnterVehicle(roadEvent.Vehicles[0], VehicleSeat.Driver);
                        officer.Task.EnterVehicle(roadEvent.Vehicles[1], VehicleSeat.Driver);
                        roadEvent.Phase = 3;
                    }
                    break;
                case 3: // Fin - départ
                    if (elapsedTime.TotalSeconds > 25)
                    {
                        if (civilian.IsInVehicle() && officer.IsInVehicle())
                        {
                            // Partir dans des directions différentes
                            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, civilian, roadEvent.Vehicles[0], 15.0f, 786603);
                            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, officer, roadEvent.Vehicles[1], 15.0f, 786603);
                        }
                        roadEvent.Phase = 4; // Marqué pour suppression
                    }
                    break;
            }
        }
        
        private void UpdateTrafficAccidentBehavior(RoadEvent roadEvent, TimeSpan elapsedTime)
        {
            if (roadEvent.Participants.Count < 2) return;
            
            var driver1 = roadEvent.Participants[0];
            var driver2 = roadEvent.Participants[1];
            
            if (!driver1.Exists() || !driver2.Exists()) return;
            
            switch (roadEvent.Phase)
            {
                case 1: // Sortie et observation des dégâts
                    if (elapsedTime.TotalSeconds > 5)
                    {
                        driver1.Task.TurnTo(driver2);
                        driver2.Task.TurnTo(driver1);
                        roadEvent.Phase = 2;
                    }
                    break;
                case 2: // Discussion/dispute
                    if (elapsedTime.TotalSeconds > 20)
                    {
                        // Parfois ils appellent la police
                        if (random.NextDouble() < 0.3f)
                        {
                            Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, driver1, "WORLD_HUMAN_STAND_MOBILE", 0, 1);
                        }
                        roadEvent.Phase = 3;
                    }
                    break;
            }
        }
        
        private void UpdateBrokenDownVehicleBehavior(RoadEvent roadEvent, TimeSpan elapsedTime)
        {
            if (roadEvent.Participants.Count < 1) return;
            
            var driver = roadEvent.Participants[0];
            if (!driver.Exists()) return;
            
            // Gérer la réparation en cours (sécurisé)
            if (roadEvent.IsBeingRepaired)
            {
                var repairElapsed = DateTime.Now - roadEvent.RepairStartTime;
                if (repairElapsed.TotalSeconds >= 5.0) // 5 secondes de réparation
                {
                    CompleteVehicleRepair(roadEvent);
                    return; // L'événement sera nettoyé
                }
            }
            
            // Gérer l'arrivée de la dépanneuse (sécurisé)
            if (roadEvent.TowingCalled && roadEvent.TowingArrivalTime.HasValue)
            {
                if (DateTime.Now >= roadEvent.TowingArrivalTime.Value)
                {
                    SpawnTowTruckSafe(roadEvent);
                    roadEvent.TowingArrivalTime = null; // Éviter de respawner
                }
            }
            
            switch (roadEvent.Phase)
            {
                case 1: // Inspection du véhicule
                    if (elapsedTime.TotalSeconds > 3)
                    {
                        // Aller vers le capot
                        if (roadEvent.Vehicles.Count > 0 && roadEvent.Vehicles[0].Exists())
                        {
                            var hood = roadEvent.Vehicles[0].Position + roadEvent.Vehicles[0].ForwardVector * 3.0f;
                            driver.Task.FollowNavMeshTo(hood);
                        }
                        roadEvent.Phase = 2;
                    }
                    break;
                case 2: // Regarder le moteur
                    if (elapsedTime.TotalSeconds > 10)
                    {
                        // Téléphoner (dépanneuse)
                        Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, driver, "WORLD_HUMAN_STAND_MOBILE", 0, 1);
                        roadEvent.Phase = 3;
                    }
                    break;
                case 89: // NOUVELLE PHASE: Dépanneuse en route vers le véhicule en panne
                    var towTruckApproachElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    
                    if (roadEvent.TowTruck?.Exists() == true && roadEvent.Participants.Count > 1)
                    {
                        var towDriver = roadEvent.Participants[1]; // Le conducteur de dépanneuse
                        var brokenVehicle = roadEvent.Vehicles[0];
                        
                        // Vérifier si la dépanneuse est arrivée près du véhicule en panne
                        if (brokenVehicle?.Exists() == true)
                        {
                            var distance = roadEvent.TowTruck.Position.DistanceTo(brokenVehicle.Position);
                            
                            if (distance <= 20.0f) // Arrivée proche
                            {
                                // Arrêter la dépanneuse
                                towDriver?.Task.LeaveVehicle();
                                
                                // Changer le blip pour indiquer l'arrivée
                                if (roadEvent.Blip?.Exists() == true)
                                {
                                    roadEvent.Blip.IsFlashing = false;
                                    roadEvent.Blip.Name = "Dépanneuse arrivée";
                                }
                                
                                GTA.UI.Notification.PostTicker("~g~La dépanneuse est arrivée sur les lieux!", false);
                                GTA.UI.Screen.ShowSubtitle("~g~Le dépanneur se prépare à charger le véhicule...", 4000);
                                
                                roadEvent.Phase = 90; // Passer à la phase d'embarquement
                                roadEvent.RepairStartTime = DateTime.Now;
                            }
                            else if (towTruckApproachElapsed.TotalSeconds > 2) // Mettre à jour l'itinéraire toutes les 2 secondes
                            {
                                // Continuer à conduire vers le véhicule en panne
                                var targetPos = brokenVehicle.Position + (brokenVehicle.ForwardVector * -15.0f);
                                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, towDriver, roadEvent.TowTruck, 
                                    targetPos.X, targetPos.Y, targetPos.Z, 25.0f, 786603, 5.0f);
                                roadEvent.RepairStartTime = DateTime.Now; // Reset timer
                            }
                        }
                        
                        // Timeout de sécurité (2 minutes max)
                        if (towTruckApproachElapsed.TotalSeconds > 120)
                        {
                            GTA.UI.Notification.PostTicker("~y~La dépanneuse met trop de temps... Force l'arrivée.", false);
                            roadEvent.Phase = 90;
                            roadEvent.RepairStartTime = DateTime.Now;
                        }
                    }
                    break;
                case 90: // Phase dépanneuse arrivée
                    var depanElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (depanElapsed.TotalSeconds > 8) // 8 secondes de préparation
                    {
                        // CORRECTION : Faire monter le conducteur accidenté dans la dépanneuse
                        if (roadEvent.TowTruck?.Exists() == true && driver?.Exists() == true)
                        {
                            // Le conducteur accidenté monte en tant que passager
                            driver.Task.EnterVehicle(roadEvent.TowTruck, VehicleSeat.Passenger);
                            
                            GTA.UI.Notification.PostTicker("~g~Le conducteur accidenté monte dans la dépanneuse...", false);
                            GTA.UI.Screen.ShowSubtitle("~b~Le véhicule va être chargé sur la dépanneuse...", 4000);
                        }
                        
                        roadEvent.Phase = 91;
                        roadEvent.RepairStartTime = DateTime.Now; // Reset timer
                    }
                    break;
                case 91: // Chargement du véhicule
                    var chargeElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (chargeElapsed.TotalSeconds > 5)
                    {
                        // CORRECTION: Au lieu de supprimer le véhicule, le faire "disparaître" visuellement
                        // mais garder une référence pour la simulation de remorquage
                        if (roadEvent.Vehicles.Count > 0 && roadEvent.Vehicles[0].Exists())
                        {
                            var brokenVehicle = roadEvent.Vehicles[0];
                            
                            // NOUVEAU: Attacher le véhicule à la dépanneuse de manière invisible
                            if (roadEvent.TowTruck?.Exists() == true)
                            {
                                // Rendre le véhicule invisible au lieu de le supprimer
                                brokenVehicle.IsVisible = false;
                                brokenVehicle.IsCollisionEnabled = false;
                                
                                // Programmer la suppression après le départ (pour éviter les bugs)
                                // On le supprimera dans la phase 92
                            }
                        }
                        
                        // Le conducteur de dépanneuse repart
                        if (roadEvent.Participants.Count > 1 && roadEvent.TowTruck?.Exists() == true)
                        {
                            var towDriver = roadEvent.Participants[1]; // Le dépanneur est le 2ème participant
                            towDriver.Task.EnterVehicle(roadEvent.TowTruck, VehicleSeat.Driver);
                        }
                        
                        roadEvent.Phase = 92;
                        roadEvent.RepairStartTime = DateTime.Now; // Reset timer
                    }
                    break;
                case 92: // Départ de la dépanneuse
                    var departElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (departElapsed.TotalSeconds > 5) // Plus de temps pour monter
                    {
                        if (roadEvent.Participants.Count > 1 && roadEvent.TowTruck?.Exists() == true)
                        {
                            var towDriver = roadEvent.Participants[1]; // Le dépanneur est le 2ème participant
                            
                            // CORRECTION : Vérifier que les deux sont dans la dépanneuse
                            bool towDriverReady = towDriver?.IsInVehicle() == true && towDriver.CurrentVehicle == roadEvent.TowTruck;
                            bool accidentDriverReady = driver?.IsInVehicle() == true && driver.CurrentVehicle == roadEvent.TowTruck;
                            
                            if (towDriverReady && accidentDriverReady)
                            {
                                // NOUVEAU: Protections renforcées pour empêcher les sorties
                                if (driver?.Exists() == true)
                                {
                                    driver.BlockPermanentEvents = true;
                                    driver.CanBeDraggedOutOfVehicle = false;
                                    driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                    driver.CanBeTargetted = false; // Éviter les interactions externes
                                }
                                
                                if (towDriver?.Exists() == true)
                                {
                                    towDriver.BlockPermanentEvents = true;
                                    towDriver.CanBeDraggedOutOfVehicle = false;
                                    towDriver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                }
                                
                                // Les deux sont dans la dépanneuse, on peut partir DÉFINITIVEMENT
                                Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, towDriver, roadEvent.TowTruck, 25.0f, 786603);
                                GTA.UI.Notification.PostTicker("~b~La dépanneuse repart avec le conducteur et le véhicule!", false);
                                
                                // NOUVEAU: Nettoyer le véhicule en panne maintenant qu'on part
                                if (roadEvent.Vehicles.Count > 0 && roadEvent.Vehicles[0].Exists())
                                {
                                    roadEvent.Vehicles[0].Delete();
                                }
                                
                                // TERMINER L'ÉVÉNEMENT IMMÉDIATEMENT (pas de phase d'attente)
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 95; // Marqué pour suppression
                                GTA.UI.Notification.PostTicker("~g~Mission de dépannage terminée avec succès!", false);
                            }
                            else if (towDriverReady && !accidentDriverReady)
                            {
                                // Le dépanneur est prêt mais pas le conducteur accidenté
                                GTA.UI.Notification.PostTicker("~y~La dépanneuse attend que le conducteur monte...", false);
                                
                                // Forcer le conducteur à monter s'il n'est pas dedans
                                if (driver?.Exists() == true && !driver.IsInVehicle())
                                {
                                    driver.Task.ClearAllImmediately();
                                    driver.Task.EnterVehicle(roadEvent.TowTruck, VehicleSeat.Passenger);
                                }
                            }
                            else if (!towDriverReady)
                            {
                                // Le dépanneur n'est pas encore dans son véhicule
                                towDriver?.Task.EnterVehicle(roadEvent.TowTruck, VehicleSeat.Driver);
                            }
                        }
                        
                        // Timeout de sécurité (20 secondes max)
                        if (departElapsed.TotalSeconds > 20)
                        {
                            GTA.UI.Notification.PostTicker("~g~Dépannage terminé (timeout sécurité)", false);
                            // Nettoyer le véhicule en panne en cas de timeout
                            if (roadEvent.Vehicles.Count > 0 && roadEvent.Vehicles[0].Exists())
                            {
                                roadEvent.Vehicles[0].Delete();
                            }
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95; // Marqué pour suppression
                        }
                    }
                    break;
                case 99: // Départ après réparation
                    var reparElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (reparElapsed.TotalSeconds > 3 && roadEvent.Vehicles.Count > 0)
                    {
                        if (driver.IsInVehicle())
                        {
                            Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, roadEvent.Vehicles[0], 20.0f, 786603);
                        }
                        roadEvent.Phase = 95; // Marqué pour suppression
                    }
                    break;
                case 80: // Transport de passager
                    var transportElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (transportElapsed.TotalSeconds > 30) // 30 secondes de trajet
                    {
                        if (driver?.Exists() == true && driver.IsInVehicle())
                        {
                            driver.Task.LeaveVehicle();
                            GTA.UI.Notification.PostTicker("~g~Le passager vous remercie et descend!", false);
                        }
                        roadEvent.Phase = 95; // Marqué pour suppression
                    }
                    break;
                case 81: // NOUVELLE PHASE: Approche du véhicule du joueur
                    var approachElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (approachElapsed.TotalSeconds > 5) // 5 secondes d'approche maximum
                    {
                        var player = Game.Player.Character;
                        if (player?.CurrentVehicle != null && player.CurrentVehicle.Exists())
                        {
                            var availableSeats = GetAvailableSeats(player.CurrentVehicle);
                            if (availableSeats.Count > 0)
                            {
                                var seatToUse = availableSeats.First();
                                driver.Task.EnterVehicle(player.CurrentVehicle, seatToUse);
                                
                                GTA.UI.Notification.PostTicker("~g~Le conducteur monte dans votre véhicule!", false);
                                
                                // Passer à la phase de transport
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 80;
                                roadEvent.RepairStartTime = DateTime.Now;
                            }
                            else
                            {
                                GTA.UI.Notification.PostTicker("~r~Plus de place dans le véhicule!", false);
                                roadEvent.Phase = 95; // Terminer l'événement
                            }
                        }
                        else
                        {
                            GTA.UI.Notification.PostTicker("~r~Le joueur n'est plus dans un véhicule!", false);
                            roadEvent.Phase = 95; // Terminer l'événement
                        }
                    }
                    break;
                case 82: // NOUVELLE PHASE: Le PNJ suit le joueur à pied
                    var followElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    var player82 = Game.Player.Character;
                    
                    // CORRECTION ANTI-CRASH: Protections de sécurité
                    try
                    {
                        if (player82?.Exists() != true || driver?.Exists() != true || driver.IsDead)
                        {
                            // Entités invalides - terminer l'événement
                            GTA.UI.Notification.PostTicker("~r~Suivi interrompu (entités invalides)", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95;
                            break;
                        }
                        
                        // Vérifier si le joueur est maintenant dans un véhicule
                        Vehicle? playerVehicle = null;
                        try
                        {
                            if (player82.IsInVehicle())
                            {
                                playerVehicle = player82.CurrentVehicle;
                                if (playerVehicle?.Exists() != true)
                                {
                                    playerVehicle = null;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            playerVehicle = null;
                        }
                        
                        if (playerVehicle != null)
                        {
                            var availableSeats = GetAvailableSeats(playerVehicle);
                            if (availableSeats.Count > 0)
                            {
                                var distanceToVehicle = driver.Position.DistanceTo(playerVehicle.Position);
                                if (distanceToVehicle <= 8.0f) // Assez proche pour monter
                                {
                                    try
                                    {
                                        // CORRECTION: Vider les tâches précédentes avant de faire monter
                                        driver.Task.ClearAllImmediately();
                                        Script.Wait(100);
                                        
                                        var seatToUse = availableSeats.First();
                                        driver.Task.EnterVehicle(playerVehicle, seatToUse);
                                        
                                        // NOUVEAU: Empêcher le PNJ de sortir du véhicule de manière inattendue
                                        // Application IMMÉDIATE des protections
                                        driver.BlockPermanentEvents = true;
                                        driver.CanBeDraggedOutOfVehicle = false;
                                        driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                        driver.CanBeTargetted = false; // Éviter interactions externes
                                        
                                        // NOUVEAU: Appliquer plusieurs fois pour s'assurer que ça tient
                                        Script.Wait(50);
                                        driver.BlockPermanentEvents = true;
                                        driver.CanBeDraggedOutOfVehicle = false;
                                        driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                        
                                        GTA.UI.Notification.PostTicker("~g~Le conducteur monte dans votre véhicule!", false);
                                        GTA.UI.Screen.ShowSubtitle("~y~Conduisez vers la destination marquée sur la mini-map", 4000);
                                        
                                        // Passer à la phase de transport vers destination
                                        roadEvent.Phase = 83;
                                        roadEvent.RepairStartTime = DateTime.Now;
                                    }
                                    catch (Exception)
                                    {
                                        // Erreur lors de l'entrée en véhicule
                                        roadEvent.Blip?.Delete();
                                        roadEvent.Phase = 95;
                                        break;
                                    }
                                }
                                else if (distanceToVehicle <= 20.0f) // Distance augmentée pour plus de tolérance
                                {
                                    try
                                    {
                                        // CORRECTION: Utiliser FollowNavMeshTo au lieu de GoTo pour plus de fiabilité
                                        driver.Task.ClearAll();
                                        Script.Wait(50);
                                        driver.Task.FollowNavMeshTo(playerVehicle.Position);
                                        
                                        // Afficher un message d'aide
                                        if (followElapsed.TotalSeconds > 5 && (int)(followElapsed.TotalSeconds) % 10 == 0)
                                        {
                                            GTA.UI.Notification.PostTicker("~b~Le passager se dirige vers votre véhicule...", false);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Problème de navigation - teleporter près du véhicule
                                        try
                                        {
                                            var teleportPos = playerVehicle.Position + playerVehicle.RightVector * 3.0f;
                                            driver.Position = teleportPos;
                                            GTA.UI.Notification.PostTicker("~b~Le passager vous rattrape...", false);
                                        }
                                        catch
                                        {
                                            // Téléportation échouée - terminer l'événement
                                            roadEvent.Blip?.Delete();
                                            roadEvent.Phase = 95;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        // Trop loin du véhicule, continuer à suivre le joueur
                                        driver.Task.ClearAll();
                                        Script.Wait(50);
                                        driver.Task.FollowToOffsetFromEntity(player82, new GTA.Math.Vector3(2.0f, -2.0f, 0.0f), 1.5f);
                                    }
                                    catch (Exception)
                                    {
                                        // Problème de suivi - terminer l'événement
                                        roadEvent.Blip?.Delete();
                                        roadEvent.Phase = 95;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                GTA.UI.Notification.PostTicker("~r~Plus de place dans votre véhicule!", false);
                                roadEvent.Phase = 95; // Terminer l'événement
                                break;
                            }
                        }
                        else
                        {
                            try
                            {
                                // Le joueur est toujours à pied, continuer à le suivre
                                if (followElapsed.TotalSeconds > 3) // Mise à jour toutes les 3 secondes pour éviter le spam
                                {
                                    var distanceToPlayer = driver.Position.DistanceTo(player82.Position);
                                    if (distanceToPlayer > 3.0f && distanceToPlayer < 100.0f) // Limite de distance pour éviter les téléportations extrêmes
                                    {
                                        driver.Task.ClearAll();
                                        Script.Wait(50);
                                        driver.Task.FollowToOffsetFromEntity(player82, new GTA.Math.Vector3(2.0f, -2.0f, 0.0f), 1.5f);
                                    }
                                    else if (distanceToPlayer >= 100.0f)
                                    {
                                        // Trop loin - téléporter le conducteur près du joueur
                                        var teleportPos = player82.Position + player82.RightVector * 3.0f;
                                        driver.Position = teleportPos;
                                        GTA.UI.Notification.PostTicker("~b~Le passager vous rattrape...", false);
                                    }
                                    roadEvent.RepairStartTime = DateTime.Now; // Reset timer
                                }
                                
                                // Message d'aide périodique
                                if (followElapsed.TotalSeconds > 15 && (int)(followElapsed.TotalSeconds) % 20 == 0)
                                {
                                    GTA.UI.Notification.PostTicker("~b~Le passager vous suit. Montez dans un véhicule pour l'emmener.", false);
                                }
                            }
                            catch (Exception)
                            {
                                // Erreur de suivi - terminer l'événement
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 95;
                                break;
                            }
                        }
                        
                        // Timeout si trop long (2 minutes au lieu de 90 secondes)
                        if (followElapsed.TotalSeconds > 120)
                        {
                            GTA.UI.Notification.PostTicker("~y~Le conducteur se lasse d'attendre et repart...", false);
                            roadEvent.Phase = 95; // Terminer l'événement
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // PROTECTION FINALE: En cas de crash, toujours terminer proprement l'événement
                        try
                        {
                            GTA.UI.Notification.PostTicker("~r~Erreur suivi sécurisée - événement terminé", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95;
                            
                            // Log l'erreur pour debug
                            System.IO.File.AppendAllText("UrbanLife_follow_crash.log", 
                                $"{DateTime.Now}: Phase 82 Error - {ex.Message}\n{ex.StackTrace}\n\n");
                        }
                        catch
                        {
                            // Dernière protection - forcer la phase 95
                            roadEvent.Phase = 95;
                        }
                    }
                    break;
                case 83: // NOUVELLE PHASE: Transport vers la destination
                    var destinationElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    var player83 = Game.Player.Character;
                    
                    // NOUVEAU: Période de grâce ÉTENDUE pour permettre au PNJ de s'installer dans le véhicule
                    if (destinationElapsed.TotalSeconds < 5.0) // Augmenté de 3 à 5 secondes
                    {
                        // NOUVEAU: Pendant la période de grâce, renforcer les protections du PNJ
                        if (driver?.Exists() == true && destinationElapsed.TotalSeconds > 1.0)
                        {
                            // Réappliquer les protections toutes les secondes pendant la période de grâce
                            if ((int)destinationElapsed.TotalSeconds != (int)(destinationElapsed.TotalSeconds - 0.1))
                            {
                                driver.BlockPermanentEvents = true;
                                driver.CanBeDraggedOutOfVehicle = false;
                                driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                driver.CanBeTargetted = false;
                                
                                // NOUVEAU: Si le PNJ sort pendant la période de grâce, le remettre immédiatement
                                if (!driver.IsInVehicle() && player83?.CurrentVehicle?.Exists() == true)
                                {
                                    var availableSeats = GetAvailableSeats(player83.CurrentVehicle);
                                    if (availableSeats.Count > 0)
                                    {
                                        var seatToUse = availableSeats.First();
                                        driver.Task.ClearAllImmediately();
                                        Script.Wait(50);
                                        driver.Task.EnterVehicle(player83.CurrentVehicle, seatToUse);
                                        GTA.UI.Notification.PostTicker("~b~Le passager remonte dans le véhicule...", false);
                                    }
                                }
                            }
                        }
                        // Attendre avant de commencer les vérifications
                        break;
                    }
                    // CORRECTION ANTI-CRASH: Vérifications de sécurité complètes
                    try
                    {
                        if (player83?.Exists() != true || driver?.Exists() != true || roadEvent.PassengerDestination == null)
                        {
                            // Entités invalides - terminer l'événement proprement
                            GTA.UI.Notification.PostTicker("~r~Transport interrompu (entités invalides)", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95;
                            break;
                        }
                        
                        // Vérifier si le driver est mort
                        if (driver.IsDead)
                        {
                            GTA.UI.Notification.PostTicker("~r~Le passager ne peut plus continuer...", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95;
                            break;
                        }
                        
                        // CORRECTION: Obtenir les véhicules de manière sécurisée
                        Vehicle? driverVehicle = null;
                        Vehicle? playerVehicle = null;
                        
                        try
                        {
                            if (driver.IsInVehicle())
                            {
                                driverVehicle = driver.CurrentVehicle;
                                // Vérifier que le véhicule existe vraiment
                                if (driverVehicle?.Exists() != true)
                                {
                                    driverVehicle = null;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Le CurrentVehicle peut lever une exception si l'entité est corrompue
                            driverVehicle = null;
                        }
                        
                        try
                        {
                            if (player83.IsInVehicle())
                            {
                                playerVehicle = player83.CurrentVehicle;
                                // Vérifier que le véhicule existe vraiment
                                if (playerVehicle?.Exists() != true)
                                {
                                    playerVehicle = null;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Le CurrentVehicle peut lever une exception si l'entité est corrompue
                            playerVehicle = null;
                        }
                        
                        // CORRECTION: Calculer la distance de manière sécurisée
                        float distanceToDestination = float.MaxValue;
                        try
                        {
                            if (roadEvent.PassengerDestination?.Position != null)
                            {
                                distanceToDestination = player83.Position.DistanceTo(roadEvent.PassengerDestination.Position);
                            }
                        }
                        catch (Exception)
                        {
                            // Position invalide
                            GTA.UI.Notification.PostTicker("~r~Destination corrompue", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95;
                            break;
                        }
                        
                        // SCÉNARIO 1: Conducteur et joueur dans le même véhicule (transport normal)
                        if (driverVehicle != null && playerVehicle != null && driverVehicle == playerVehicle)
                        {
                            // NOUVEAU: Réappliquer les protections périodiquement pendant le transport
                            if ((int)destinationElapsed.TotalSeconds % 10 == 0) // Toutes les 10 secondes
                            {
                                driver.BlockPermanentEvents = true;
                                driver.CanBeDraggedOutOfVehicle = false;
                                driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                driver.CanBeTargetted = false;
                            }
                            
                            // Vérifier si on est arrivé à destination
                            if (distanceToDestination <= 20.0f) // Distance augmentée pour plus de tolérance
                            {
                                try
                                {
                                    // CORRECTION: Sécuriser la sortie du véhicule
                                    driver.Task.ClearAllImmediately();
                                    Script.Wait(100);
                                    driver.Task.LeaveVehicle();
                                    
                                    GTA.UI.Notification.PostTicker($"~g~\"Merci beaucoup ! Je suis arrivé à {roadEvent.PassengerDestination?.Name}.\"", false);
                                    GTA.UI.Screen.ShowSubtitle("~g~Mission accomplie ! Le passager est arrivé à destination.", 4000);
                                    
                                    roadEvent.Blip?.Delete();
                                    roadEvent.Phase = 95; // Terminer l'événement
                                }
                                catch (Exception)
                                {
                                    // Même en cas d'erreur, terminer l'événement
                                    roadEvent.Blip?.Delete();
                                    roadEvent.Phase = 95;
                                }
                            }
                            else
                            {
                                // Transport en cours - donner des indications périodiquement
                                if (destinationElapsed.TotalSeconds > 10 && (int)(destinationElapsed.TotalSeconds) % 45 == 0)
                                {
                                    try
                                    {
                                        GTA.UI.Notification.PostTicker($"~y~\"Plus que {(int)distanceToDestination}m vers {roadEvent.PassengerDestination?.Name}...\"", false);
                                    }
                                    catch
                                    {
                                        // Ignorer les erreurs de notification
                                    }
                                }
                            }
                            
                            // Timeout si trop long (8 minutes au lieu de 5)
                            if (destinationElapsed.TotalSeconds > 480)
                            {
                                try
                                {
                                    driver.Task.ClearAllImmediately();
                                    Script.Wait(100);
                                    driver.Task.LeaveVehicle();
                                    GTA.UI.Notification.PostTicker("~y~\"Je vais descendre ici, merci quand même !\"", false);
                                }
                                catch { }
                                
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 95; // Terminer l'événement
                            }
                        }
                        // SCÉNARIO 2: Conducteur et joueur dans des véhicules différents
                        else if (driverVehicle != null && playerVehicle != null && driverVehicle != playerVehicle)
                        {
                            try
                            {
                                // Le conducteur est dans le mauvais véhicule
                                GTA.UI.Notification.PostTicker("~y~Le passager vous suit vers votre véhicule...", false);
                                driver.Task.ClearAllImmediately();
                                Script.Wait(50);
                                driver.Task.LeaveVehicle();
                                roadEvent.Phase = 82; // Retour à la phase de suivi
                                roadEvent.RepairStartTime = DateTime.Now;
                            }
                            catch (Exception)
                            {
                                // En cas d'erreur, terminer l'événement
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 95;
                            }
                        }
                        // SCÉNARIO 3: Conducteur pas dans un véhicule, joueur dans un véhicule
                        else if (driverVehicle == null && playerVehicle != null)
                        {
                            try
                            {
                                // Vérifier s'il peut monter dans le véhicule du joueur
                                var availableSeats = GetAvailableSeats(playerVehicle);
                                var distanceToVehicle = driver.Position.DistanceTo(playerVehicle.Position);
                                
                                if (availableSeats.Count > 0 && distanceToVehicle <= 12.0f) // Distance augmentée
                                {
                                    // Faire monter le conducteur
                                    driver.Task.ClearAllImmediately();
                                    Script.Wait(100);
                                    
                                    var seatToUse = availableSeats.First();
                                    driver.Task.EnterVehicle(playerVehicle, seatToUse);
                                    
                                    // NOUVEAU: Protéger le PNJ contre les sorties involontaires - VERSION RENFORCÉE
                                    Script.Wait(100); // Attendre que la montée commence
                                    driver.BlockPermanentEvents = true;
                                    driver.CanBeDraggedOutOfVehicle = false;
                                    driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                    driver.CanBeTargetted = false;
                                    
                                    // NOUVEAU: Double application des protections avec délai
                                    Script.Wait(50);
                                    driver.BlockPermanentEvents = true;
                                    driver.CanBeDraggedOutOfVehicle = false;
                                    driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                    
                                    GTA.UI.Notification.PostTicker("~g~Le passager remonte dans votre véhicule.", false);
                                    GTA.UI.Screen.ShowSubtitle("~y~Conduisez vers la destination marquée sur la mini-map", 4000);
                                    
                                    // Passer à la phase de transport vers destination
                                    roadEvent.Phase = 83;
                                    roadEvent.RepairStartTime = DateTime.Now;
                                }
                                else if (distanceToVehicle > 12.0f && distanceToVehicle < 50.0f)
                                {
                                    // Faire suivre le conducteur vers le véhicule (avec limite de distance)
                                    driver.Task.ClearAllImmediately();
                                    Script.Wait(50);
                                    driver.Task.FollowToOffsetFromEntity(player83, new GTA.Math.Vector3(2.0f, -2.0f, 0.0f), 1.8f);
                                }
                                else if (distanceToVehicle >= 50.0f)
                                {
                                    // Trop loin - téléporter le conducteur plus près
                                    var teleportPos = playerVehicle.Position + playerVehicle.RightVector * 5.0f;
                                    driver.Position = teleportPos;
                                    GTA.UI.Notification.PostTicker("~b~Le passager vous rattrape...", false);
                                }
                                else
                                {
                                    // Plus de place dans le véhicule
                                    GTA.UI.Notification.PostTicker("~r~Plus de place dans votre véhicule!", false);
                                    roadEvent.Phase = 95; // Terminer l'événement
                                }
                            }
                            catch (Exception)
                            {
                                // En cas d'erreur, terminer l'événement
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 95;
                            }
                        }
                        // SCÉNARIO 4: Ni le conducteur ni le joueur ne sont dans un véhicule
                        else if (driverVehicle == null && playerVehicle == null)
                        {
                            try
                            {
                                // Retourner à la phase de suivi à pied
                                driver.Task.ClearAllImmediately();
                                Script.Wait(50);
                                driver.Task.FollowToOffsetFromEntity(player83, new GTA.Math.Vector3(2.0f, -2.0f, 0.0f), 1.5f);
                                roadEvent.Phase = 82; // Retour à la phase de suivi
                                roadEvent.RepairStartTime = DateTime.Now;
                                GTA.UI.Notification.PostTicker("~y~Le passager vous suit à pied. Montez dans un véhicule.", false);
                            }
                            catch (Exception)
                            {
                                // En cas d'erreur, terminer l'événement
                                roadEvent.Blip?.Delete();
                                roadEvent.Phase = 95;
                            }
                        }
                        else
                        {
                            // Situation inconnue ou corrompue - terminer l'événement
                            GTA.UI.Notification.PostTicker("~y~Le transport a été interrompu.", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95; // Terminer l'événement
                        }
                    }
                    catch (Exception ex)
                    {
                        // PROTECTION FINALE: En cas de crash, toujours terminer proprement l'événement
                        try
                        {
                            GTA.UI.Notification.PostTicker("~r~Erreur transport sécurisée - événement terminé", false);
                            roadEvent.Blip?.Delete();
                            roadEvent.Phase = 95;
                            
                            // Log l'erreur pour debug
                            System.IO.File.AppendAllText("UrbanLife_transport_crash.log", 
                                $"{DateTime.Now}: Phase 83 Error - {ex.Message}\n{ex.StackTrace}\n\n");
                        }
                        catch
                        {
                            // Dernière protection - forcer la phase 95 même si on ne peut rien faire d'autre
                            roadEvent.Phase = 95;
                        }
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Complète la réparation du véhicule de manière sécurisée
        /// </summary>
        private void CompleteVehicleRepair(RoadEvent breakdownEvent)
        {
            try
            {
                if (breakdownEvent.Vehicles.Count == 0) return;
                
                var brokenVehicle = breakdownEvent.Vehicles[0];
                if (!brokenVehicle.Exists()) return;
                
                // Réparer le véhicule
                brokenVehicle.Repair();
                brokenVehicle.EngineHealth = 1000.0f;
                brokenVehicle.HealthFloat = 1000.0f;
                
                // Le conducteur remonte et repart
                if (breakdownEvent.Participants.Count > 0)
                {
                    var driver = breakdownEvent.Participants[0];
                    if (driver?.Exists() == true)
                    {
                        driver.Task.EnterVehicle(brokenVehicle, VehicleSeat.Driver);
                        // Programmer le départ dans 3 secondes via la phase
                        breakdownEvent.Phase = 99; // Phase spéciale pour départ après réparation
                        breakdownEvent.RepairStartTime = DateTime.Now; // Réutiliser pour timer le départ
                    }
                }
                
                breakdownEvent.IsRepaired = true;
                breakdownEvent.IsBeingRepaired = false;
                breakdownEvent.Blip?.Delete();
                
                GTA.UI.Notification.PostTicker("~g~Véhicule réparé! Le conducteur vous remercie.", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur réparation: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Fait apparaître la dépanneuse de manière sécurisée et réaliste
        /// </summary>
        private void SpawnTowTruckSafe(RoadEvent breakdownEvent)
        {
            try
            {
                if (breakdownEvent.Vehicles.Count == 0) return;
                
                var brokenVehicle = breakdownEvent.Vehicles[0];
                if (!brokenVehicle.Exists()) return;
                
                // NOUVEAU: Créer la dépanneuse LOIN du joueur pour qu'elle "arrive" vraiment
                var playerPos = Game.Player.Character.Position;
                var directionFromPlayer = Vector3.RandomXY().Normalized;
                
                // Position de spawn : 150-200 mètres du joueur, pas visible
                var spawnDistance = random.Next(150, 201);
                var towTruckSpawnPos = playerPos + (directionFromPlayer * spawnDistance);
                
                // Ajuster au sol
                float groundZ;
                if (World.GetGroundHeight(towTruckSpawnPos, out groundZ))
                {
                    towTruckSpawnPos.Z = groundZ + 2.0f;
                }
                
                // Créer la dépanneuse loin du joueur
                var towTruck = World.CreateVehicle(VehicleHash.TowTruck, towTruckSpawnPos);
                if (towTruck?.Exists() != true) 
                {
                    GTA.UI.Notification.PostTicker("~r~Erreur: Impossible de créer la dépanneuse", false);
                    return;
                }
                
                // Configuration de la dépanneuse
                towTruck.IsPersistent = true;
                towTruck.Health = 1000;
                towTruck.EngineHealth = 1000;
                
                // Créer le conducteur de dépanneuse
                var towDriver = towTruck.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Autoshop01SMM);
                if (towDriver?.Exists() != true)
                {
                    towTruck?.Delete();
                    GTA.UI.Notification.PostTicker("~r~Erreur: Impossible de créer le conducteur de dépanneuse", false);
                    return;
                }
                
                towDriver.IsPersistent = true;
                towDriver.BlockPermanentEvents = true;
                
                // Stocker la dépanneuse et le conducteur
                breakdownEvent.TowTruck = towTruck;
                breakdownEvent.Participants.Add(towDriver);
                
                // NOUVEAU: Créer un blip sur la DÉPANNEUSE qui arrive (pas sur le PNJ)
                breakdownEvent.Blip?.Delete();
                var towTruckBlip = towTruck.AddBlip();
                if (towTruckBlip?.Exists() == true)
                {
                    towTruckBlip.Sprite = BlipSprite.TowTruck;
                    towTruckBlip.Color = BlipColor.Blue;
                    towTruckBlip.Scale = 1.2f;
                    towTruckBlip.Name = "Dépanneuse en approche";
                    towTruckBlip.IsFlashing = true; // Clignotant pour montrer qu'elle arrive
                    breakdownEvent.Blip = towTruckBlip;
                }
                
                // NOUVEAU: Faire CONDUIRE la dépanneuse vers le véhicule en panne
                var targetPos = brokenVehicle.Position + (brokenVehicle.ForwardVector * -15.0f);
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, towDriver, towTruck, 
                    targetPos.X, targetPos.Y, targetPos.Z, 25.0f, 786603, 5.0f);
                
                GTA.UI.Notification.PostTicker("~b~Dépanneuse en approche! Suivez le blip bleu.", false);
                GTA.UI.Screen.ShowSubtitle("~b~La dépanneuse arrive de loin... Regardez la mini-map!", 5000);
                
                // NOUVEAU: Phase 89 = Dépanneuse en route (nouvelle phase)
                breakdownEvent.Phase = 89;
                breakdownEvent.RepairStartTime = DateTime.Now;
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur dépanneuse", false);
            }
        }
        
        private void UpdateParamedicsBehavior(RoadEvent roadEvent, TimeSpan elapsedTime)
        {
            if (roadEvent.Participants.Count < 3) return;
            
            var paramedic1 = roadEvent.Participants[0];
            var paramedic2 = roadEvent.Participants[1];
            var patient = roadEvent.Participants[2];
            
            switch (roadEvent.Phase)
            {
                case 1: // Soins au patient
                    if (elapsedTime.TotalSeconds > 5)
                    {
                        paramedic1.Task.FollowNavMeshTo(patient.Position + new Vector3(1.5f, 0f, 0f));
                        paramedic2.Task.FollowNavMeshTo(patient.Position + new Vector3(-1.5f, 0f, 0f));
                        roadEvent.Phase = 2;
                    }
                    break;
                case 2: // Transport vers l'ambulance
                    if (elapsedTime.TotalSeconds > 15)
                    {
                        patient.Task.FollowNavMeshTo(roadEvent.Vehicles[0].Position);
                        roadEvent.Phase = 3;
                    }
                    break;
                case 3: // Départ
                    if (elapsedTime.TotalSeconds > 25)
                    {
                        paramedic1.Task.EnterVehicle(roadEvent.Vehicles[0], VehicleSeat.Driver);
                        paramedic2.Task.EnterVehicle(roadEvent.Vehicles[0], VehicleSeat.Passenger);
                        patient.Task.EnterVehicle(roadEvent.Vehicles[0], VehicleSeat.LeftRear);
                        roadEvent.Phase = 4;
                    }
                    break;
                case 4: // Partir
                    if (elapsedTime.TotalSeconds > 35 && paramedic1.IsInVehicle())
                    {
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, paramedic1, roadEvent.Vehicles[0], 25.0f, 786603);
                        roadEvent.Phase = 5; // Marqué pour suppression
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Nettoie les événements corrompus qui pourraient causer des crashes
        /// </summary>
        private void CleanupCorruptedEvents()
        {
            try
            {
                for (int i = activeRoadEvents.Count - 1; i >= 0; i--)
                {
                    var roadEvent = activeRoadEvents[i];
                    bool shouldRemove = false;
                    
                    // Vérifier les véhicules corrompus
                    for (int v = roadEvent.Vehicles.Count - 1; v >= 0; v--)
                    {
                        var vehicle = roadEvent.Vehicles[v];
                        if (vehicle == null || !vehicle.Exists())
                        {
                            roadEvent.Vehicles.RemoveAt(v);
                        }
                    }
                    
                    // Vérifier les participants corrompus
                    for (int p = roadEvent.Participants.Count - 1; p >= 0; p--)
                    {
                        var ped = roadEvent.Participants[p];
                        if (ped == null || !ped.Exists())
                        {
                            roadEvent.Participants.RemoveAt(p);
                        }
                    }
                    
                    // Si l'événement n'a plus d'entités valides, le supprimer
                    if (roadEvent.Vehicles.Count == 0 && roadEvent.Participants.Count == 0)
                    {
                        shouldRemove = true;
                    }
                    
                    // Vérifier les blips corrompus
                    if (roadEvent.Blip != null && !roadEvent.Blip.Exists())
                    {
                        roadEvent.Blip = null;
                    }
                    
                    if (shouldRemove)
                    {
                        CleanupRoadEvent(roadEvent);
                        activeRoadEvents.RemoveAt(i);
                    }
                }
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~o~Nettoyage sécurité", false);
            }
        }

        private void CleanupDistantEvents()
        {
            var player = Game.Player.Character;
            if (player == null) return;
            
            for (int i = activeRoadEvents.Count - 1; i >= 0; i--)
            {
                var roadEvent = activeRoadEvents[i];
                var distance = roadEvent.Position.DistanceTo(player.Position);
                
                if (distance > EVENT_CLEANUP_DISTANCE)
                {
                    CleanupRoadEvent(roadEvent);
                    activeRoadEvents.RemoveAt(i);
                }
            }
        }
        
        private void CleanupRoadEvent(RoadEvent roadEvent)
        {
            // Supprimer le blip
            roadEvent.Blip?.Delete();
            
            // Marquer les entités comme plus nécessaires
            foreach (var vehicle in roadEvent.Vehicles)
            {
                if (vehicle?.Exists() == true)
                {
                    vehicle.MarkAsNoLongerNeeded();
                }
            }
            
            foreach (var ped in roadEvent.Participants)
            {
                if (ped?.Exists() == true)
                {
                    ped.MarkAsNoLongerNeeded();
                }
            }
        }
        
        public void ClearAllEvents()
        {
            foreach (var roadEvent in activeRoadEvents)
            {
                CleanupRoadEvent(roadEvent);
            }
            activeRoadEvents.Clear();
        }
        
        public List<RoadEvent> GetActiveEvents()
        {
            return activeRoadEvents.ToList();
        }
        
        /// <summary>
        /// Force la création d'un événement routier (utilisé par F7)
        /// </summary>
        public bool ForceCreateRoadEvent()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.CurrentVehicle == null) 
                {
                    GTA.UI.Notification.PostTicker("~r~Vous devez être dans un véhicule!", false);
                    return false;
                }
                
                // NOUVELLE VÉRIFICATION : Vitesse excessive
                var playerVehicle = player.CurrentVehicle;
                var speed = playerVehicle.Speed * 3.6f; // Convertir en km/h
                if (speed > 120.0f) // Plus de 120 km/h
                {
                    GTA.UI.Notification.PostTicker("~r~Ralentissez avant de créer un événement! (vitesse max: 120 km/h)", false);
                    return false;
                }
                
                // NOUVELLE VÉRIFICATION : État du véhicule
                if (playerVehicle.HealthFloat < 500.0f)
                {
                    GTA.UI.Notification.PostTicker("~r~Votre véhicule est trop endommagé!", false);
                    return false;
                }
                
                // Vérifier qu'on n'est pas dans l'eau ou dans une zone interdite
                var playerPos = player.Position;
                if (playerPos.Z < -50.0f || playerPos.Z > 500.0f)
                {
                    GTA.UI.Notification.PostTicker("~r~Position invalide pour créer un événement!", false);
                    return false;
                }
                
                // Vérifier l'eau avec plus de sécurité
                try
                {
                    float waterHeight = 0.0f;
                    if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, playerPos.X, playerPos.Y, playerPos.Z, waterHeight))
                    {
                        if (Math.Abs(playerPos.Z - waterHeight) < 5.0f) // Proche de l'eau
                        {
                            GTA.UI.Notification.PostTicker("~r~Impossible de créer un événement près de l'eau!", false);
                            return false;
                        }
                    }
                }
                catch
                {
                    // Si la vérification d'eau échoue, on continue mais on sera plus prudent
                    GTA.UI.Notification.PostTicker("~y~Attention: Vérification d'eau impossible", false);
                }
                
                // Limiter le nombre d'événements actifs
                if (activeRoadEvents.Count >= 2) // Réduire à 2 pour plus de sécurité
                {
                    GTA.UI.Notification.PostTicker("~y~Trop d'événements actifs! Attendez qu'ils se terminent.", false);
                    return false;
                }
                
                // NOUVELLE VÉRIFICATION : Délai minimum entre créations F7
                var timeSinceLastEvent = DateTime.Now - lastEventCreation;
                if (timeSinceLastEvent.TotalSeconds < 15) // 15 secondes minimum
                {
                    var remainingTime = 15 - (int)timeSinceLastEvent.TotalSeconds;
                    GTA.UI.Notification.PostTicker($"~y~Attendez {remainingTime}s avant le prochain événement F7", false);
                    return false;
                }
                
                // Essayer de trouver une position sécurisée avec plus de sécurité
                Vector3 eventPosition = Vector3.Zero;
                try
                {
                    eventPosition = FindSafeRoadPositionAheadUltraSafe();
                }
                catch (Exception)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur recherche position", false);
                    return false;
                }
                
                if (eventPosition == Vector3.Zero) 
                {
                    GTA.UI.Notification.PostTicker("~y~Aucune route sûre trouvée devant vous. Ralentissez et réessayez.", false);
                    return false;
                }
                
                // NOUVEAU: Vérification anti-conflit pour F7 - éviter les crashes près d'autres événements
                var nearbyActiveEvents = activeRoadEvents.Where(e => 
                {
                    try
                    {
                        return eventPosition.DistanceTo(e.Position) < 100.0f; // Zone de sécurité élargie pour F7
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
                
                if (nearbyActiveEvents.Count > 0)
                {
                    var eventTypes = string.Join(", ", nearbyActiveEvents.Select(e => e.Type.ToString()));
                    GTA.UI.Notification.PostTicker($"~y~Événements proches détectés ({eventTypes}). Éloignez-vous pour utiliser F7.", false);
                    return false;
                }
                
                // SEULEMENT le type le plus sûr à haute vitesse
                var eventType = RoadEventType.BrokenDownVehicle; // Le plus simple et sûr PAR DÉFAUT
                if (speed < 50.0f) // Seulement en dessous de 50 km/h, autoriser les autres types
                {
                    var randomChoice = random.NextDouble();
                    if (randomChoice < 0.7) // 70% de chance de panne même à basse vitesse
                    {
                        eventType = RoadEventType.BrokenDownVehicle;
                    }
                    else // 30% pour les autres types
                    {
                        var safeEventTypes = new[] 
                        { 
                            RoadEventType.PoliceStop
                        };
                        eventType = safeEventTypes[random.Next(safeEventTypes.Length)];
                    }
                }
                
                // Créer l'événement selon le type avec sécurité renforcée
                
                // Créer l'événement selon le type avec sécurité renforcée
                bool success = false;
                try
                {
                    switch (eventType)
                    {
                        case RoadEventType.PoliceStop:
                            success = CreatePoliceStopEventUltraSafe(eventPosition);
                            break;
                        case RoadEventType.BrokenDownVehicle:
                            success = CreateBrokenDownVehicleEventUltraSafe(eventPosition);
                            break;
                    }
                }
                catch (Exception)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur création événement", false);
                    return false;
                }
                
                if (success)
                {
                    lastEventCreation = DateTime.Now;
                    GTA.UI.Notification.PostTicker("~g~Mini-événement créé avec succès!", false);
                    return true;
                }
                else
                {
                    GTA.UI.Notification.PostTicker("~r~Échec de création de l'événement!", false);
                    return false;
                }
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur F7 critique", false);
                // Log pour debug
                System.IO.File.AppendAllText("REALIS_crash_log.txt", 
                    $"{DateTime.Now}: F7 Error\n\n");
                return false;
            }
        }
        
        /// <summary>
        /// Version ultra-sécurisée de la recherche de position routière
        /// </summary>
        private Vector3 FindSafeRoadPositionAheadUltraSafe()
        {
            try
            {
                var player = Game.Player.Character;
                var playerPos = player.Position;
                
                // Vérifications de sécurité initiales renforcées
                if (playerPos.Z < -200.0f || playerPos.Z > 2000.0f) 
                {
                    throw new Exception("Position joueur hors limites extrêmes");
                }
                
                Vector3 playerForward = Vector3.Zero;
                if (player.CurrentVehicle?.Exists() == true)
                {
                    playerForward = player.CurrentVehicle.ForwardVector;
                }
                else
                {
                    playerForward = player.ForwardVector;
                }
                
                // Vérifier que le vecteur forward est valide
                if (playerForward.LengthSquared() < 0.1f)
                {
                    playerForward = new Vector3(1, 0, 0); // Direction par défaut
                }
                
                // Distances plus courtes et plus sûres
                var distances = new[] { 30.0f, 40.0f, 50.0f, 60.0f, 70.0f };
                var angles = new[] { 0.0f, -10.0f, 10.0f, -20.0f, 20.0f };
                
                foreach (var distance in distances)
                {
                    foreach (var angle in angles)
                    {
                        try
                        {
                            // Calculer la direction avec rotation sécurisée
                            float angleRad = (float)(angle * Math.PI / 180.0);
                            float cos = (float)Math.Cos(angleRad);
                            float sin = (float)Math.Sin(angleRad);
                            
                            var rotatedForward = new Vector3(
                                playerForward.X * cos - playerForward.Y * sin,
                                playerForward.X * sin + playerForward.Y * cos,
                                playerForward.Z
                            );
                            
                            // Normaliser avec vérification
                            if (rotatedForward.LengthSquared() > 0.1f)
                            {
                                rotatedForward = rotatedForward.Normalized;
                            }
                            else
                            {
                                continue; // Passer à l'essai suivant si vecteur invalide
                            }
                            
                            var testPos = playerPos + (rotatedForward * distance);
                            
                            // Vérifications de sécurité ultra-strictes
                            if (IsSafePositionUltraSafe(testPos))
                            {
                                return testPos;
                            }
                        }
                        catch
                        {
                            // Ignorer cet essai et continuer
                            continue;
                        }
                    }
                }
                
                return Vector3.Zero; // Aucune position sûre trouvée
            }
            catch (Exception)
            {
                throw new Exception($"FindSafeRoadPositionAheadUltraSafe failed");
            }
        }
        
        /// <summary>
        /// Vérifications de sécurité ultra-strictes pour une position
        /// </summary>
        private bool IsSafePositionUltraSafe(Vector3 position)
        {
            try
            {
                // Vérifier les coordonnées de base avec marges plus strictes
                if (position.X < -5000.0f || position.X > 5000.0f) return false;
                if (position.Y < -5000.0f || position.Y > 5000.0f) return false;
                if (position.Z < -100.0f || position.Z > 1000.0f) return false;
                
                // Vérifier l'eau avec gestion d'erreur
                try
                {
                    float waterHeight = 0.0f;
                    if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, position.X, position.Y, position.Z, waterHeight))
                    {
                        if (Math.Abs(position.Z - waterHeight) < 10.0f) return false;
                    }
                }
                catch
                {
                    return false; // En cas d'erreur, rejeter la position
                }
                
                // Obtenir la hauteur du sol avec gestion d'erreur
                float groundZ;
                try
                {
                    if (!World.GetGroundHeight(position, out groundZ)) return false;
                    if (Math.Abs(position.Z - groundZ) > 50.0f) return false; // Pas trop loin du sol
                }
                catch
                {
                    return false;
                }
                
                // Ajuster la position au sol
                position.Z = groundZ + 2.0f; // Un peu plus haut pour sécurité
                
                // Vérifier qu'on est sur une route avec gestion d'erreur
                try
                {
                    if (!Function.Call<bool>(Hash.IS_POINT_ON_ROAD, position.X, position.Y, position.Z, 0))
                    {
                        // Essayer de forcer sur une route proche
                        var roadPos = Function.Call<Vector3>(Hash.GET_CLOSEST_VEHICLE_NODE,
                            position.X, position.Y, position.Z, 1, 20.0f, 0);
                        
                        if (roadPos == Vector3.Zero) return false;
                        
                        // Vérifier que la position de route est valide
                        if (roadPos.X < -5000.0f || roadPos.X > 5000.0f) return false;
                        if (roadPos.Y < -5000.0f || roadPos.Y > 5000.0f) return false;
                        if (roadPos.Z < -100.0f || roadPos.Z > 1000.0f) return false;
                        
                        position = roadPos;
                    }
                }
                catch
                {
                    return false;
                }
                
                // Vérifier qu'il n'y a pas d'autres événements trop proches
                foreach (var existingEvent in activeRoadEvents)
                {
                    try
                    {
                        if (position.DistanceTo(existingEvent.Position) < 50.0f) // Distance augmentée
                            return false;
                    }
                    catch
                    {
                        continue; // Ignorer si erreur de calcul de distance
                    }
                }
                
                // NOUVEAU: Vérification spéciale pour F7 - éviter les conflits d'événements
                var nearbyActiveEvents = activeRoadEvents.Where(e => 
                {
                    try
                    {
                        return position.DistanceTo(e.Position) < 100.0f; // Zone de sécurité élargie pour F7
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
                
                if (nearbyActiveEvents.Count > 0)
                {
                    var eventTypes = string.Join(", ", nearbyActiveEvents.Select(e => e.Type.ToString()));
                    GTA.UI.Notification.PostTicker($"~y~Événements proches détectés ({eventTypes}). Essayez plus loin.", false);
                    return false;
                }
                
                return true;
            }
            catch (Exception)
            {
                return false; // En cas d'erreur, toujours rejeter
            }
        }
        
        /// <summary>
        /// Version sécurisée de création d'événement de panne
        /// </summary>
        private bool CreateBrokenDownVehicleEventUltraSafe(Vector3 position)
        {
            try
            {
                // Vérifications préalables
                if (!IsSafePositionUltraSafe(position)) return false;
                
                var brokenCar = World.CreateVehicle(VehicleHash.Asea, position);
                if (brokenCar?.Exists() != true) 
                {
                    GTA.UI.Notification.PostTicker("~r~Échec création véhicule", false);
                    return false;
                }
                
                // Sécuriser le véhicule
                brokenCar.IsPersistent = true;
                brokenCar.EngineHealth = 0.0f;
                brokenCar.HealthFloat = 600.0f;
                
                var driver = brokenCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business01AMY);
                if (driver?.Exists() != true)
                {
                    brokenCar?.Delete();
                    return false;
                }
                
                driver.IsPersistent = true;
                
                // Faire sortir le conducteur SANS Task.Run asynchrone
                driver.Task.LeaveVehicle();
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.BrokenDownVehicle,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { brokenCar },
                    Participants = new List<Ped> { driver },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                AddRoadEventBlip(roadEvent, "Véhicule en panne", BlipSprite.Garage, BlipColor.Yellow);
                
                return true;
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur panne sécurisée", false);
                return false;
            }
        }
        
        /// <summary>
        /// Version sécurisée de création de contrôle de police
        /// </summary>
        private bool CreatePoliceStopEventUltraSafe(Vector3 position)
        {
            try
            {
                if (!IsSafePositionUltraSafe(position)) return false;
                
                var civilCarModels = new[] { VehicleHash.Blista, VehicleHash.Premier, VehicleHash.Fugitive };
                var civilCar = World.CreateVehicle(civilCarModels[random.Next(civilCarModels.Length)], position);
                
                if (civilCar?.Exists() != true) return false;
                
                var civilDriver = civilCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Business01AMY);
                if (civilDriver?.Exists() != true)
                {
                    civilCar?.Delete();
                    return false;
                }
                
                var policePos = position + new Vector3(0f, -8.0f, 0f);
                var policeCar = World.CreateVehicle(VehicleHash.Police, policePos);
                
                if (policeCar?.Exists() != true) 
                {
                    civilCar?.Delete();
                    civilDriver?.Delete();
                    return false;
                }
                
                var officer = policeCar.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Cop01SMY);
                if (officer?.Exists() != true)
                {
                    civilCar?.Delete();
                    civilDriver?.Delete();
                    policeCar?.Delete();
                    return false;
                }
                
                // Sécuriser tous les éléments
                civilDriver.IsPersistent = true;
                officer.IsPersistent = true;
                civilCar.IsPersistent = true;
                policeCar.IsPersistent = true;
                
                // Actions synchrones SANS Task.Run
                civilDriver.Task.LeaveVehicle();
                officer.Task.LeaveVehicle();
                
                var roadEvent = new RoadEvent
                {
                    Type = RoadEventType.PoliceStop,
                    Position = position,
                    StartTime = DateTime.Now,
                    Vehicles = new List<Vehicle> { civilCar, policeCar },
                    Participants = new List<Ped> { civilDriver, officer },
                    Phase = 1
                };
                
                activeRoadEvents.Add(roadEvent);
                AddRoadEventBlip(roadEvent, "Contrôle de police", BlipSprite.PoliceOfficer, BlipColor.Blue);
                
                return true;
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur police sécurisée", false);
                return false;
            }
        }
        
        /// <summary>
        /// Vérifie les interactions possibles avec les pannes
        /// </summary>
        private void CheckBreakdownInteractions()
        {
            var player = Game.Player.Character;
            if (player == null) return;
            
            var breakdownEvents = activeRoadEvents.Where(re => 
                re.Type == RoadEventType.BrokenDownVehicle && 
                re.CanInteract && 
                !re.IsRepaired && 
                !re.PassengerPickedUp &&
                !re.TowingCalled).ToList(); // CORRECTION: Exclure les événements où dépanneuse déjà appelée
            
            foreach (var breakdownEvent in breakdownEvents)
            {
                var distance = player.Position.DistanceTo(breakdownEvent.Position);
                if (distance <= INTERACTION_DISTANCE)
                {
                    ShowBreakdownInteractionOptions(breakdownEvent);
                    break; // Une seule interaction à la fois
                }
            }
            
            // Vérifier l'arrivée des dépanneuses
            CheckTowingArrivals();
        }
        
        /// <summary>
        /// Affiche les options d'interaction pour une panne
        /// </summary>
        private void ShowBreakdownInteractionOptions(RoadEvent breakdownEvent)
        {
            var messages = new List<string>
            {
                "~g~Options d'aide:",
                "~w~E - Réparer le véhicule",
                "~w~F - Appeler une dépanneuse",
                "~w~G - Proposer de l'emmener"
            };
            
            foreach (var message in messages)
            {
                GTA.UI.Notification.PostTicker(message, false);
            }
            
            // CORRECTION: Détection améliorée des touches sans spam
            var now = DateTime.Now;
            
            // Vérifier la touche E avec cooldown
            if (Game.IsKeyPressed(System.Windows.Forms.Keys.E) && 
                (now - lastEKeyPress).TotalSeconds >= KEY_COOLDOWN_SECONDS)
            {
                lastEKeyPress = now;
                RepairBrokenVehicle(breakdownEvent);
                GTA.UI.Notification.PostTicker("~g~Réparation en cours...", false);
            }
            // Vérifier la touche F avec cooldown
            else if (Game.IsKeyPressed(System.Windows.Forms.Keys.F) && 
                     (now - lastFKeyPress).TotalSeconds >= KEY_COOLDOWN_SECONDS)
            {
                lastFKeyPress = now;
                CallTowingService(breakdownEvent);
                GTA.UI.Notification.PostTicker("~b~Dépanneuse appelée...", false);
            }
            // Vérifier la touche G avec cooldown
            else if (Game.IsKeyPressed(System.Windows.Forms.Keys.G) && 
                     (now - lastGKeyPress).TotalSeconds >= KEY_COOLDOWN_SECONDS)
            {
                lastGKeyPress = now;
                OfferRideToDriver(breakdownEvent);
                GTA.UI.Notification.PostTicker("~g~Proposition d'aide...", false);
            }
        }
        
        /// <summary>
        /// Répare le véhicule en panne avec animation
        /// </summary>
        private void RepairBrokenVehicle(RoadEvent breakdownEvent)
        {
            // CORRECTION: Vérifier qu'on peut encore réparer
            if (!breakdownEvent.CanInteract || breakdownEvent.IsBeingRepaired || 
                breakdownEvent.IsRepaired || breakdownEvent.TowingCalled)
            {
                GTA.UI.Notification.PostTicker("~y~Action déjà en cours ou terminée!", false);
                return;
            }
            
            var player = Game.Player.Character;
            if (breakdownEvent.Vehicles.Count == 0) return;
            
            var brokenVehicle = breakdownEvent.Vehicles[0];
            if (!brokenVehicle.Exists()) return;
            
            breakdownEvent.CanInteract = false;
            
            GTA.UI.Notification.PostTicker("~g~Vous réparez le véhicule...", false);
            GTA.UI.Screen.ShowSubtitle("~g~Réparation en cours - Attendez 5 secondes", 3000);
            
            // Animation de réparation
            player.Task.PlayAnimation("mini@repair", "fixing_a_ped", 8.0f, 5000, AnimationFlags.None);
            
            // Programmer la fin de la réparation de manière SÉCURISÉE
            breakdownEvent.RepairStartTime = DateTime.Now;
            breakdownEvent.IsBeingRepaired = true;
        }
        
        /// <summary>
        /// Appelle un service de dépannage
        /// </summary>
        private void CallTowingService(RoadEvent breakdownEvent)
        {
            // CORRECTION: Vérifier qu'on peut encore appeler
            if (breakdownEvent.TowingCalled || breakdownEvent.IsRepaired || 
                breakdownEvent.IsBeingRepaired || !breakdownEvent.CanInteract)
            {
                if (breakdownEvent.TowingCalled)
                {
                    var existingArrival = breakdownEvent.TowingArrivalTime ?? DateTime.Now.AddMinutes(2);
                    GTA.UI.Notification.PostTicker($"~y~Dépanneuse déjà appelée! Arrivée: {existingArrival:HH:mm:ss}", false);
                }
                else
                {
                    GTA.UI.Notification.PostTicker("~y~Action déjà en cours ou terminée!", false);
                }
                return;
            }
            
            breakdownEvent.TowingCalled = true;
            breakdownEvent.TowingCallTime = DateTime.Now;
            breakdownEvent.CanInteract = false;
            
            // TEMPS RÉDUIT POUR TEST: 30-90 secondes au lieu de 1-3 minutes
            var arrivalTimeSeconds = random.Next(30, 91); // 30-90 secondes
            
            // CORRECTION: Afficher le temps d'attente estimé
            var estimatedArrival = DateTime.Now.AddSeconds(arrivalTimeSeconds);
            GTA.UI.Notification.PostTicker($"~b~Dépanneuse appelée! Arrivée dans {arrivalTimeSeconds}s", false);
            GTA.UI.Screen.ShowSubtitle($"~b~Temps d'attente: {arrivalTimeSeconds} secondes", 5000);
            
            // Sauvegarder le temps d'arrivée prévu de manière sécurisée
            breakdownEvent.TowingArrivalTime = estimatedArrival;
            
            // CORRECTION: Créer immédiatement un blip de dépanneuse en route
            CreateTowTruckIncomingBlip(breakdownEvent);
        }

        /// <summary>
        /// Crée un blip pour indiquer qu'une dépanneuse arrive
        /// </summary>
        private void CreateTowTruckIncomingBlip(RoadEvent breakdownEvent)
        {
            try
            {
                // Supprimer l'ancien blip de panne
                breakdownEvent.Blip?.Delete();
                
                // Créer un nouveau blip bleu pour la dépanneuse en route
                var incomingBlip = World.CreateBlip(breakdownEvent.Position);
                if (incomingBlip?.Exists() == true)
                {
                    incomingBlip.Sprite = BlipSprite.TowTruck;
                    incomingBlip.Color = BlipColor.Blue;
                    incomingBlip.Scale = 0.8f;
                    incomingBlip.Name = "Dépanneuse en route";
                    incomingBlip.IsFlashing = true;
                    
                    // Remplacer le blip de l'événement
                    breakdownEvent.Blip = incomingBlip;
                    
                    GTA.UI.Notification.PostTicker("~b~Blip bleu ajouté sur la mini-map pour suivre l'arrivée!", false);
                }
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur blip dépanneuse", false);
            }
        }
        
        /// <summary>
        /// Propose d'emmener le conducteur - VERSION MODIFIÉE
        /// Le conducteur suit d'abord le joueur, puis monte dans sa voiture et demande d'être déposé quelque part
        /// </summary>
        private void OfferRideToDriver(RoadEvent breakdownEvent)
        {
            try
            {
                // CORRECTION ANTI-CRASH: Vérifications de sécurité primaires
                if (breakdownEvent == null)
                {
                    GTA.UI.Notification.PostTicker("~r~Erreur: Événement invalide", false);
                    return;
                }
                
                // CORRECTION: Vérifier qu'on peut encore proposer de l'aide
                if (!breakdownEvent.CanInteract || breakdownEvent.PassengerPickedUp || 
                    breakdownEvent.IsRepaired || breakdownEvent.TowingCalled)
                {
                    if (breakdownEvent.PassengerPickedUp)
                    {
                        GTA.UI.Notification.PostTicker("~y~Le conducteur est déjà pris en charge!", false);
                    }
                    else
                    {
                        GTA.UI.Notification.PostTicker("~y~Action déjà en cours ou terminée!", false);
                    }
                    return;
                }
                
                var player = Game.Player.Character;
                
                // CORRECTION: Vérifications de sécurité renforcées
                if (player?.Exists() != true)
                {
                    GTA.UI.Notification.PostTicker("~r~Erreur: Joueur non valide", false);
                    return;
                }
                
                if (breakdownEvent.Participants == null || breakdownEvent.Participants.Count == 0) 
                {
                    GTA.UI.Notification.PostTicker("~r~Aucun conducteur trouvé pour cet événement", false);
                    return;
                }
                
                var driver = breakdownEvent.Participants[0];
                if (driver == null || !driver.Exists() || driver.IsDead)
                {
                    GTA.UI.Notification.PostTicker("~r~Le conducteur n'est plus disponible", false);
                    return;
                }
                
                // NOUVELLE PROTECTION: Vérifier la distance pour éviter les actions à trop grande distance
                float distanceToDriver;
                try
                {
                    // À ce point, driver ne peut pas être null grâce aux vérifications ci-dessus
                    distanceToDriver = player.Position.DistanceTo(driver!.Position);
                }
                catch (Exception)
                {
                    GTA.UI.Notification.PostTicker("~r~Erreur de calcul de distance", false);
                    return;
                }
                
                if (distanceToDriver > 15.0f)
                {
                    GTA.UI.Notification.PostTicker("~y~Vous êtes trop loin du conducteur", false);
                    return;
                }
                
                // NOUVELLE LOGIQUE: Ne plus exiger d'être dans un véhicule au début
                breakdownEvent.CanInteract = false;
                breakdownEvent.PassengerPickedUp = true;
                
                // Générer une destination aléatoire
                var destinations = GetRandomDestinations();
                if (destinations == null || destinations.Count == 0)
                {
                    GTA.UI.Notification.PostTicker("~r~Erreur: Aucune destination disponible", false);
                    return;
                }
                
                var randomDestination = destinations[random.Next(destinations.Count)];
                
                // Stocker la destination dans l'événement
                breakdownEvent.PassengerDestination = randomDestination;
                breakdownEvent.RepairStartTime = DateTime.Now; // Timer pour gérer les phases
                
                // Créer un blip pour la destination
                try
                {
                    var destinationBlip = World.CreateBlip(randomDestination.Position);
                    if (destinationBlip != null && destinationBlip.Exists())
                    {
                        destinationBlip.Sprite = BlipSprite.Waypoint;
                        destinationBlip.Color = BlipColor.Yellow;
                        destinationBlip.Scale = 1.0f;
                        destinationBlip.Name = randomDestination.Name;
                        // Afficher un tracé GPS pour guider le joueur
                        destinationBlip.ShowRoute = true;
                        
                        // Remplacer le blip de l'événement par la destination
                        breakdownEvent.Blip?.Delete();
                        breakdownEvent.Blip = destinationBlip;
                    }
                }
                catch (Exception)
                {
                    // Continuer même si le blip ne peut pas être créé
                    GTA.UI.Notification.PostTicker("~y~Blip de destination non créé", false);
                }
                
                GTA.UI.Notification.PostTicker("~g~Le conducteur accepte votre aide!", false);
                GTA.UI.Notification.PostTicker($"~y~Il dit: \"Merci ! Pouvez-vous m'emmener à {randomDestination.Name} ?\"", false);
                GTA.UI.Screen.ShowSubtitle($"~g~Destination: {randomDestination.Name} (marquée sur la mini-map)", 5000);
                
                // CORRECTION: Sécuriser la tâche de suivi
                try
                {
                    driver.Task.ClearAllImmediately();
                    Script.Wait(100);
                    driver.Task.FollowToOffsetFromEntity(player, new GTA.Math.Vector3(2.0f, -2.0f, 0.0f), 1.5f);
                }
                catch (Exception)
                {
                    GTA.UI.Notification.PostTicker("~r~Problème de suivi - le conducteur se téléporte près de vous", false);
                    try
                    {
                        // Fallback : téléporter le conducteur près du joueur
                        var teleportPos = player.Position + player.RightVector * 2.0f;
                        driver.Position = teleportPos;
                    }
                    catch
                    {
                        // Si même la téléportation échoue, annuler l'opération
                        GTA.UI.Notification.PostTicker("~r~Erreur critique - operation annulée", false);
                        breakdownEvent.CanInteract = true;
                        breakdownEvent.PassengerPickedUp = false;
                        return;
                    }
                }
                
                // Marquer pour la phase suivante
                breakdownEvent.Phase = 82; // Phase: Suit le joueur à pied
                
                GTA.UI.Notification.PostTicker("~b~Le conducteur vous suit. Approchez-vous de votre véhicule pour qu'il monte.", false);
                GTA.UI.Screen.ShowSubtitle("~w~1) Le passager vous suit à pied\n~w~2) Montez dans votre véhicule\n~w~3) Il montera automatiquement\n~w~4) Conduisez vers la destination jaune", 8000);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur lors de l'aide au conducteur", false);
                
                // CORRECTION: En cas d'erreur, nettoyer l'événement de manière sécurisée
                try
                {
                    if (breakdownEvent != null)
                    {
                        breakdownEvent.CanInteract = false;
                        breakdownEvent.Phase = 95; // Marqué pour suppression
                        breakdownEvent.Blip?.Delete();
                    }
                }
                catch
                {
                    // Ignorer les erreurs de nettoyage
                }
                
                // Log l'erreur pour debug
                try
                {
                    System.IO.File.AppendAllText("UrbanLife_offer_ride_crash.log", 
                        $"{DateTime.Now}: OfferRideToDriver Error\n\n");
                }
                catch
                {
                    // Ignorer les erreurs de logging
                }
            }
        }
        
        /// <summary>
        /// Obtient une liste de destinations aléatoires possibles
        /// </summary>
        private List<Destination> GetRandomDestinations()
        {
            return new List<Destination>
            {
                // Centres commerciaux et lieux publics
                new Destination("Centre commercial Del Perro", new GTA.Math.Vector3(-1305f, -394f, 36f)),
                new Destination("Aéroport de Los Santos", new GTA.Math.Vector3(-1037f, -2737f, 13f)),
                new Destination("Hôpital Central", new GTA.Math.Vector3(294f, -1448f, 29f)),
                new Destination("Commissariat de Mission Row", new GTA.Math.Vector3(428f, -981f, 30f)),
                new Destination("Garage Benny's", new GTA.Math.Vector3(-205f, -1308f, 31f)),
                new Destination("Station-service Globe Oil", new GTA.Math.Vector3(49f, 2778f, 58f)),
                new Destination("Banque Fleeca", new GTA.Math.Vector3(147f, -1036f, 29f)),
                new Destination("SuperMarché 24/7", new GTA.Math.Vector3(372f, 325f, 103f)),
                
                // Quartiers résidentiels
                new Destination("Maison à Vinewood Hills", new GTA.Math.Vector3(-174f, 502f, 137f)),
                new Destination("Appartements à Vespucci", new GTA.Math.Vector3(-1251f, -1427f, 4f)),
                new Destination("Maison à Rockford Hills", new GTA.Math.Vector3(-884f, -25f, 50f)),
                new Destination("Quartier de Grove Street", new GTA.Math.Vector3(77f, -1948f, 21f)),
                
                // Lieux de travail
                new Destination("Bureau Downtown", new GTA.Math.Vector3(-141f, -620f, 168f)),
                new Destination("Entrepôt du Port", new GTA.Math.Vector3(1207f, -3113f, 5f)),
                new Destination("Usine de Murrieta Heights", new GTA.Math.Vector3(716f, -962f, 30f)),
                new Destination("Marina près de Vespucci", new GTA.Math.Vector3(-1603f, -1085f, 13f)),
                
                // Lieux de loisirs
                new Destination("Plage de Vespucci", new GTA.Math.Vector3(-1223f, -1491f, 4f)),
                new Destination("Parc près de Vinewood", new GTA.Math.Vector3(215f, -1160f, 29f)),
                new Destination("Terrain de golf", new GTA.Math.Vector3(-1349f, 155f, 57f)),
                new Destination("Casino Diamond", new GTA.Math.Vector3(1089f, 206f, -49f))
            };
        }

        /// <summary>
        /// Obtient les sièges disponibles dans un véhicule
        /// </summary>
        private List<VehicleSeat> GetAvailableSeats(Vehicle vehicle)
        {
            var availableSeats = new List<VehicleSeat>();
            var allSeats = new[] { VehicleSeat.Passenger, VehicleSeat.LeftRear, VehicleSeat.RightRear };
            
            foreach (var seat in allSeats)
            {
                if (vehicle.IsSeatFree(seat))
                {
                    availableSeats.Add(seat);
                }
            }
            
            return availableSeats;
        }

        /// <summary>
        /// Vérifie l'arrivée des dépanneuses programmées
        /// </summary>
        private void CheckTowingArrivals()
        {
            var towingEvents = activeRoadEvents.Where(re => 
                re.TowingCalled && 
                re.TowingArrivalTime.HasValue && 
                re.TowTruck == null).ToList();
            
            foreach (var towingEvent in towingEvents)
            {
                // Vérifier si c'est le moment de faire apparaître la dépanneuse
                if (towingEvent.TowingArrivalTime.HasValue && DateTime.Now >= towingEvent.TowingArrivalTime.Value)
                {
                    SpawnTowTruckSafe(towingEvent);
                    towingEvent.TowingArrivalTime = null; // Éviter de respawner
                }
            }
        }

        /// <summary>
        /// Méthode de diagnostic pour débugger les problèmes de transport de passagers
        /// </summary>
        private void DiagnosePassengerIssues(RoadEvent roadEvent, Ped driver, Ped player)
        {
            try
            {
                var debugInfo = new System.Text.StringBuilder();
                debugInfo.AppendLine("=== DIAGNOSTIC PASSAGER ===");
                debugInfo.AppendLine($"Phase: {roadEvent.Phase}");
                debugInfo.AppendLine($"Driver exists: {driver?.Exists()}");
                debugInfo.AppendLine($"Driver is alive: {driver?.IsAlive}");
                debugInfo.AppendLine($"Driver in vehicle: {driver?.IsInVehicle()}");
                debugInfo.AppendLine($"Driver vehicle: {driver?.CurrentVehicle?.Model}");
                debugInfo.AppendLine($"Player exists: {player?.Exists()}");
                debugInfo.AppendLine($"Player in vehicle: {player?.IsInVehicle()}");
                debugInfo.AppendLine($"Player vehicle: {player?.CurrentVehicle?.Model}");
                debugInfo.AppendLine($"Same vehicle: {driver?.CurrentVehicle == player?.CurrentVehicle}");
                debugInfo.AppendLine($"Destination: {roadEvent.PassengerDestination?.Name}");
                
                if (player?.CurrentVehicle != null)
                {
                    var availableSeats = GetAvailableSeats(player.CurrentVehicle);
                    debugInfo.AppendLine($"Available seats: {availableSeats.Count}");
                }
                
                // Log dans un fichier pour debug
                System.IO.File.AppendAllText("UrbanLife_passenger_debug.log", 
                    $"{DateTime.Now}: {debugInfo}\n\n");
                    
                GTA.UI.Notification.PostTicker("~o~Debug info écrit dans UrbanLife_passenger_debug.log", false);
            }
            catch (Exception)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur diagnostic", false);
            }
        }
        
        /// <summary>
        /// NOUVEAU: Surveille automatiquement les PNJ en transport pour empêcher les sorties involontaires
        /// </summary>
        private void MonitorPassengerStability()
        {
            try
            {
                var transportEvents = activeRoadEvents.Where(re => 
                    re.Type == RoadEventType.BrokenDownVehicle && 
                    (re.Phase == 83 || re.Phase == 82) && 
                    re.PassengerPickedUp).ToList();
                
                foreach (var roadEvent in transportEvents)
                {
                    if (roadEvent.Participants.Count == 0) continue;
                    
                    var driver = roadEvent.Participants[0];
                    var player = Game.Player.Character;
                    
                    if (driver?.Exists() != true || player?.Exists() != true) continue;
                    
                    // Si nous sommes en phase 83 (transport actif)
                    if (roadEvent.Phase == 83)
                    {
                        var transportTime = DateTime.Now - roadEvent.RepairStartTime;
                        
                        // Après la période de grâce, vérifier si le PNJ est toujours dans le véhicule
                        if (transportTime.TotalSeconds > 6.0) // 1 seconde après la période de grâce
                        {
                            if (player.IsInVehicle() && !driver.IsInVehicle())
                            {
                                // DÉTECTION: Le PNJ est sorti involontairement !
                                var playerVehicle = player.CurrentVehicle;
                                if (playerVehicle?.Exists() == true)
                                {
                                    var availableSeats = GetAvailableSeats(playerVehicle);
                                    var distanceToVehicle = driver.Position.DistanceTo(playerVehicle.Position);
                                    
                                    if (availableSeats.Count > 0 && distanceToVehicle <= 15.0f)
                                    {
                                        // CORRECTION AUTOMATIQUE: Remettre le PNJ dans le véhicule
                                        driver.Task.ClearAllImmediately();
                                        Script.Wait(50);
                                        
                                        var seatToUse = availableSeats.First();
                                        driver.Task.EnterVehicle(playerVehicle, seatToUse);
                                        
                                        // Réappliquer toutes les protections
                                        Script.Wait(100);
                                        driver.BlockPermanentEvents = true;
                                        driver.CanBeDraggedOutOfVehicle = false;
                                        driver.KnockOffVehicleType = KnockOffVehicleType.Never;
                                        driver.CanBeTargetted = false;
                                        
                                        GTA.UI.Notification.PostTicker("~r~CORRECTION: Le passager remonte automatiquement!", false);
                                        
                                        // Log l'incident pour debug
                                        System.IO.File.AppendAllText("UrbanLife_passenger_auto_fix.log", 
                                            $"{DateTime.Now}: Auto-fix applied - PNJ était sorti involontairement\n");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log l'erreur mais ne pas interrompre le jeu
                System.IO.File.AppendAllText("UrbanLife_monitor_error.log", 
                    $"{DateTime.Now}: Monitor Error - {ex.Message}\n");
            }
        }
    }
    
    public class RoadEvent
    {
        public RoadEventType Type { get; set; }
        public Vector3 Position { get; set; }
        public DateTime StartTime { get; set; }
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public List<Ped> Participants { get; set; } = new List<Ped>();
        public Blip? Blip { get; set; }
        public int Phase { get; set; } = 1;
        
        // Propriétés pour les interactions des pannes
        public bool CanInteract { get; set; } = true;
        public bool TowingCalled { get; set; } = false;
        public DateTime? TowingCallTime { get; set; }
        public Vehicle? TowTruck { get; set; }
        public bool IsRepaired { get; set; } = false;
        public bool PassengerPickedUp { get; set; } = false;
        public DateTime RepairStartTime { get; set; }
        public bool IsBeingRepaired { get; set; } = false;
        public DateTime? TowingArrivalTime { get; set; }
        
        // Nouvelle propriété pour stocker la destination du passager
        public Destination? PassengerDestination { get; set; }
    }
    
    public enum RoadEventType
    {
        PoliceStop,        // Contrôle de police
        TrafficAccident,   // Accident de circulation
        RoadConstruction,  // Travaux routiers
        SpeedControl,      // Radar mobile
        BrokenDownVehicle, // Véhicule en panne
        Paramedics         // Intervention médicale
    }

    /// <summary>
    /// Représente une destination pour les passagers
    /// </summary>
    public class Destination
    {
        public string Name { get; set; }
        public GTA.Math.Vector3 Position { get; set; }

        public Destination(string name, GTA.Math.Vector3 position)
        {
            Name = name;
            Position = position;
        }
    }
}
