using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Linq;

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
        
        // Configuration
        private const float EVENT_CREATION_DISTANCE_MIN = 80.0f;
        private const float EVENT_CREATION_DISTANCE_MAX = 200.0f;
        private const float EVENT_CLEANUP_DISTANCE = 300.0f;
        private const double BASE_EVENT_PROBABILITY = 0.002f; // 0.2% par vérification
        
        public RoadEventManager()
        {
            random = new Random();
            activeRoadEvents = new List<RoadEvent>();
            lastEventCheck = DateTime.Now;
            lastEventCreation = DateTime.Now;
        }
        
        public void Update()
        {
            // Nettoyer les événements anciens/éloignés
            CleanupDistantEvents();
            
            // Créer de nouveaux événements
            if ((DateTime.Now - lastEventCheck).TotalSeconds > 15) // Vérifier toutes les 15 secondes
            {
                CheckForNewRoadEvents();
                lastEventCheck = DateTime.Now;
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
                if (activeRoadEvents.Count >= 2) return;
                
                // Délai minimum entre événements
                if ((DateTime.Now - lastEventCreation).TotalMinutes < 5) return;
                
                // Test de probabilité
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
            
            // Trouver une position sur une route
            var eventPosition = FindSuitableRoadPosition();
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
        
        private Vector3 FindSuitableRoadPosition()
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            
            // Essayer plusieurs fois de trouver une position sur une route
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var direction = Vector3.RandomXY().Normalized;
                var distance = random.Next((int)EVENT_CREATION_DISTANCE_MIN, (int)EVENT_CREATION_DISTANCE_MAX);
                var testPos = playerPos + (direction * distance);
                
                // Vérifier que c'est sur une route
                float groundZ;
                if (World.GetGroundHeight(testPos, out groundZ))
                {
                    testPos.Z = groundZ;
                    
                    // Vérifier si c'est près d'une route
                    var nearbyVehicles = World.GetNearbyVehicles(testPos, 50.0f);
                    if (nearbyVehicles.Length > 2) // S'il y a du trafic, c'est probablement une route
                    {
                        return testPos;
                    }
                }
            }
            
            return Vector3.Zero; // Échec
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
                
                AddRoadEventBlip(roadEvent, "Accident de la route", BlipSprite.Devin, BlipColor.Red);
                
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
            blip.Scale = 0.6f;
            blip.Name = name;
            blip.IsShortRange = true;
            
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
            
            switch (roadEvent.Phase)
            {
                case 1: // Inspection du véhicule
                    if (elapsedTime.TotalSeconds > 3)
                    {
                        // Aller vers le capot
                        var hood = roadEvent.Vehicles[0].Position + roadEvent.Vehicles[0].ForwardVector * 3.0f;
                        driver.Task.FollowNavMeshTo(hood);
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