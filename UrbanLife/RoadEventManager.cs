using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

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
        
        // Configuration
        private const float EVENT_CREATION_DISTANCE_MIN = 80.0f;
        private const float EVENT_CREATION_DISTANCE_MAX = 200.0f;
        private const float EVENT_CLEANUP_DISTANCE = 300.0f;
        private const double BASE_EVENT_PROBABILITY = 0.05f; // 5% par vérification (augmenté de 0.2%)
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
                if ((DateTime.Now - lastEventCreation).TotalMinutes < 1) return; // Réduit de 5 à 1 minute
                
                // Test de probabilité amélioré
                if (random.NextDouble() < BASE_EVENT_PROBABILITY)
                {
                    CreateRandomRoadEvent();
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur vérification événements routiers: {ex.Message}", false);
            }
        }
        
        private void CreateRandomRoadEvent()
        {
            var player = Game.Player.Character;
            var eventTypes = Enum.GetValues(typeof(RoadEventType)).Cast<RoadEventType>().ToList();
            var selectedType = eventTypes[random.Next(eventTypes.Count)];
            
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
                var nearbyVehicles = World.GetNearbyVehicles(position, 50.0f);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création contrôle police: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création accident: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création travaux: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création radar: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création panne: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création urgence médicale: {ex.Message}", false);
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
            
            if (roadEvent.Type == RoadEventType.TrafficAccident || roadEvent.Type == RoadEventType.Paramedics)
            {
                blip.ShowRoute = true;
            }
            
            roadEvent.Blip = blip;
        }
        
        private void UpdateActiveRoadEvents()
        {
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
                case 90: // Phase dépanneuse arrivée
                    var depanElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (depanElapsed.TotalSeconds > 10) // 10 secondes de préparation
                    {
                        // Simulation du remorquage
                        GTA.UI.Notification.PostTicker("~b~Le véhicule en panne est chargé sur la dépanneuse...", false);
                        
                        // Le conducteur en panne monte avec la dépanneuse
                        if (roadEvent.TowTruck?.Exists() == true)
                        {
                            driver.Task.EnterVehicle(roadEvent.TowTruck, VehicleSeat.Passenger);
                        }
                        
                        roadEvent.Phase = 91;
                        roadEvent.RepairStartTime = DateTime.Now; // Reset timer
                    }
                    break;
                case 91: // Chargement du véhicule
                    var chargeElapsed = DateTime.Now - roadEvent.RepairStartTime;
                    if (chargeElapsed.TotalSeconds > 5)
                    {
                        // Faire disparaître le véhicule en panne
                        if (roadEvent.Vehicles.Count > 0 && roadEvent.Vehicles[0].Exists())
                        {
                            roadEvent.Vehicles[0].Delete();
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
                    if (departElapsed.TotalSeconds > 3)
                    {
                        if (roadEvent.Participants.Count > 1 && roadEvent.TowTruck?.Exists() == true)
                        {
                            var towDriver = roadEvent.Participants[1];
                            if (towDriver.IsInVehicle())
                            {
                                Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, towDriver, roadEvent.TowTruck, 20.0f, 786603);
                            }
                        }
                        
                        roadEvent.Blip?.Delete();
                        roadEvent.Phase = 95; // Marqué pour suppression
                        GTA.UI.Notification.PostTicker("~b~Véhicule remorqué avec succès!", false);
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
        /// Fait apparaître la dépanneuse de manière sécurisée
        /// </summary>
        private void SpawnTowTruckSafe(RoadEvent breakdownEvent)
        {
            try
            {
                if (breakdownEvent.Vehicles.Count == 0) return;
                
                var brokenVehicle = breakdownEvent.Vehicles[0];
                if (!brokenVehicle.Exists()) return;
                
                // Position pour la dépanneuse (derrière le véhicule en panne)
                var towTruckPos = brokenVehicle.Position + (brokenVehicle.ForwardVector * -15.0f);
                
                // Créer la dépanneuse
                var towTruck = World.CreateVehicle(VehicleHash.TowTruck, towTruckPos);
                if (towTruck?.Exists() != true) return;
                
                // Créer le conducteur de dépanneuse
                var towDriver = towTruck.CreatePedOnSeat(VehicleSeat.Driver, PedHash.Autoshop01SMM);
                if (towDriver?.Exists() != true)
                {
                    towTruck?.Delete();
                    return;
                }
                
                towTruck.IsPersistent = true;
                towDriver.IsPersistent = true;
                
                breakdownEvent.TowTruck = towTruck;
                breakdownEvent.Participants.Add(towDriver); // Ajouter le dépanneur aux participants
                
                GTA.UI.Notification.PostTicker("~b~La dépanneuse est arrivée!", false);
                
                // Faire sortir le conducteur et commencer le remorquage
                towDriver.Task.LeaveVehicle();
                
                // Programmer la séquence de remorquage via les phases
                breakdownEvent.Phase = 90; // Phase spéciale pour dépanneuse
                breakdownEvent.RepairStartTime = DateTime.Now; // Réutiliser pour timer les phases
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur dépanneuse: {ex.Message}", false);
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
                catch (Exception ex)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur recherche position: {ex.Message}", false);
                    return false;
                }
                
                if (eventPosition == Vector3.Zero) 
                {
                    GTA.UI.Notification.PostTicker("~y~Aucune route sûre trouvée devant vous. Ralentissez et réessayez.", false);
                    return false;
                }
                
                // SEULEMENT le type le plus sûr à haute vitesse
                var eventType = RoadEventType.BrokenDownVehicle; // Le plus simple et sûr
                if (speed < 50.0f) // Seulement en dessous de 50 km/h, autoriser les autres types
                {
                    var safeEventTypes = new[] 
                    { 
                        RoadEventType.BrokenDownVehicle, 
                        RoadEventType.PoliceStop
                    };
                    eventType = safeEventTypes[random.Next(safeEventTypes.Length)];
                }
                
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
                catch (Exception ex)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur création événement: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur F7 critique: {ex.Message}", false);
                // Log pour debug
                System.IO.File.AppendAllText("REALIS_crash_log.txt", 
                    $"{DateTime.Now}: F7 Error - {ex.Message}\n{ex.StackTrace}\n\n");
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
            catch (Exception ex)
            {
                throw new Exception($"FindSafeRoadPositionAheadUltraSafe failed: {ex.Message}");
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
                
                return true;
            }
            catch
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur panne sécurisée: {ex.Message}", false);
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
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur police sécurisée: {ex.Message}", false);
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
                !re.PassengerPickedUp).ToList();
            
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
            
            // Vérifier les touches
            if (Game.IsKeyPressed(System.Windows.Forms.Keys.E))
            {
                RepairBrokenVehicle(breakdownEvent);
            }
            else if (Game.IsKeyPressed(System.Windows.Forms.Keys.F))
            {
                CallTowingService(breakdownEvent);
            }
            else if (Game.IsKeyPressed(System.Windows.Forms.Keys.G))
            {
                OfferRideToDriver(breakdownEvent);
            }
        }
        
        /// <summary>
        /// Répare le véhicule en panne avec animation
        /// </summary>
        private void RepairBrokenVehicle(RoadEvent breakdownEvent)
        {
            var player = Game.Player.Character;
            if (breakdownEvent.Vehicles.Count == 0) return;
            
            var brokenVehicle = breakdownEvent.Vehicles[0];
            if (!brokenVehicle.Exists()) return;
            
            breakdownEvent.CanInteract = false;
            
            GTA.UI.Notification.PostTicker("~g~Vous réparez le véhicule...", false);
            
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
            if (breakdownEvent.TowingCalled) return;
            
            breakdownEvent.TowingCalled = true;
            breakdownEvent.TowingCallTime = DateTime.Now;
            breakdownEvent.CanInteract = false;
            
            var arrivalTime = random.Next(1, 4); // 1-3 minutes
            GTA.UI.Notification.PostTicker($"~b~Dépanneuse appelée! Arrivée dans ~{arrivalTime} minutes.", false);
            
            // Sauvegarder le temps d'arrivée prévu de manière sécurisée
            breakdownEvent.TowingArrivalTime = DateTime.Now.AddMinutes(arrivalTime);
        }
        
        /// <summary>
        /// Propose d'emmener le conducteur
        /// </summary>
        private void OfferRideToDriver(RoadEvent breakdownEvent)
        {
            var player = Game.Player.Character;
            if (player?.CurrentVehicle == null)
            {
                GTA.UI.Notification.PostTicker("~r~Vous devez être dans un véhicule pour proposer de l'emmener!", false);
                return;
            }
            
            if (breakdownEvent.Participants.Count == 0) return;
            
            var driver = breakdownEvent.Participants[0];
            if (!driver.Exists()) return;
            
            // Vérifier s'il y a de la place dans le véhicule
            var playerVehicle = player.CurrentVehicle;
            var availableSeats = GetAvailableSeats(playerVehicle);
            
            if (availableSeats.Count == 0)
            {
                GTA.UI.Notification.PostTicker("~r~Pas de place disponible dans votre véhicule!", false);
                return;
            }
            
            breakdownEvent.CanInteract = false;
            breakdownEvent.PassengerPickedUp = true;
            
            GTA.UI.Notification.PostTicker("~g~Le conducteur accepte votre aide et monte dans votre véhicule!", false);
            
            // Faire monter le conducteur
            var seatToUse = availableSeats.First();
            driver.Task.EnterVehicle(playerVehicle, seatToUse);
            
            // Marquer l'événement comme résolu mais programmer le débarquement
            breakdownEvent.Blip?.Delete();
            breakdownEvent.Phase = 80; // Phase spéciale pour transport de passager
            breakdownEvent.RepairStartTime = DateTime.Now; // Réutiliser pour timer le transport
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
                re.TowingCallTime.HasValue && 
                re.TowTruck == null).ToList();
            
            foreach (var towingEvent in towingEvents)
            {
                // Vérification sécurisée pour éviter le warning nullable
                if (towingEvent.TowingCallTime.HasValue)
                {
                    var elapsed = DateTime.Now - towingEvent.TowingCallTime.Value;
                    // Cette vérification est maintenant gérée par la task asynchrone dans CallTowingService
                    // Mais on pourrait ajouter une vérification de fallback ici si nécessaire
                }
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
} 