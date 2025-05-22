using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Linq;

public class RealisticFuelSystem : Script
{
    // Configuration du système de carburant
    private readonly float fuelConsumptionBaseRate = 0.03f;  // Consommation de base (par seconde à 60 km/h)
    private readonly float idleConsumptionRate = 0.005f;     // Consommation au ralenti (par seconde)
    private readonly float maxFuelCapacity = 100.0f;         // Capacité maximale par défaut
    private readonly float refillRate = 2.0f;                // Taux de remplissage à la station-service (par seconde)
    
    // Types de carburant
    private enum FuelType
    {
        Gasoline,
        Diesel,
        Electric
    }
    
    // Prix de base du carburant par type
    private readonly Dictionary<FuelType, float> baseFuelPrices = new Dictionary<FuelType, float>
    {
        { FuelType.Gasoline, 1.80f },   // Prix par litre en $
        { FuelType.Diesel, 1.65f },     // Prix par litre en $
        { FuelType.Electric, 0.50f }    // Prix par kWh en $
    };
    
    // Modificateurs de prix par quartier/secteur
    private readonly Dictionary<string, float> areaFuelPriceModifiers = new Dictionary<string, float>
    {
        { "DOWNTOWN", 1.2f },           // Centre-ville plus cher
        { "ROCKFORD", 1.3f },           // Quartier riche plus cher
        { "SANDY", 0.9f },              // Zones rurales moins chères
        { "PALETO", 0.85f }             // Zones éloignées encore moins chères
    };
    
    // Type de carburant par défaut par classe de véhicule
    private readonly Dictionary<VehicleClass, FuelType> vehicleFuelTypes = new Dictionary<VehicleClass, FuelType>
    {
        { VehicleClass.Super, FuelType.Gasoline },
        { VehicleClass.Sports, FuelType.Gasoline },
        { VehicleClass.SportsClassics, FuelType.Gasoline },
        { VehicleClass.Sedans, FuelType.Gasoline },
        { VehicleClass.Compacts, FuelType.Gasoline },
        { VehicleClass.SUVs, FuelType.Gasoline },
        { VehicleClass.Coupes, FuelType.Gasoline },
        { VehicleClass.Muscle, FuelType.Gasoline },
        { VehicleClass.OffRoad, FuelType.Gasoline },
        { VehicleClass.Industrial, FuelType.Diesel },
        { VehicleClass.Utility, FuelType.Diesel },
        { VehicleClass.Commercial, FuelType.Diesel },
        { VehicleClass.Military, FuelType.Diesel }
    };
    
    // Véhicules spécifiques avec type de carburant personnalisé
    private readonly Dictionary<string, FuelType> specialVehicleFuelTypes = new Dictionary<string, FuelType>
    {
        { "VOLTIC", FuelType.Electric },
        { "TEZERACT", FuelType.Electric },
        { "CYCLONE", FuelType.Electric },
        { "RAIDEN", FuelType.Electric },
        { "NEON", FuelType.Electric },
        { "DILETTANTE", FuelType.Electric },
        { "SURGE", FuelType.Electric },
        { "KHAMELION", FuelType.Electric },
        { "IWAGEN", FuelType.Electric }
    };
    
    // Capacité de carburant personnalisée par classe de véhicule (en litres)
    private readonly Dictionary<VehicleClass, float> vehicleFuelCapacities = new Dictionary<VehicleClass, float>
    {
        { VehicleClass.Super, 70.0f },
        { VehicleClass.Sports, 60.0f },
        { VehicleClass.SportsClassics, 65.0f },
        { VehicleClass.Sedans, 55.0f },
        { VehicleClass.Compacts, 45.0f },
        { VehicleClass.SUVs, 75.0f },
        { VehicleClass.Coupes, 50.0f },
        { VehicleClass.Muscle, 80.0f },
        { VehicleClass.OffRoad, 85.0f },
        { VehicleClass.Industrial, 120.0f },
        { VehicleClass.Utility, 100.0f },
        { VehicleClass.Commercial, 150.0f },
        { VehicleClass.Military, 200.0f }
    };
    
    // Stockage du niveau de carburant par véhicule
    private readonly Dictionary<int, float> vehicleFuelLevels = new Dictionary<int, float>();
    private readonly Dictionary<int, FuelType> vehicleFuelTypeAssigned = new Dictionary<int, FuelType>();
    
    // Stations-service
    private readonly List<Vector3> gasStations = new List<Vector3>
    {
        new Vector3(-70.2f, -1761.8f, 29.3f),       // Davis
        new Vector3(265.0f, -1261.3f, 29.3f),       // East Los Santos
        new Vector3(819.6f, -1028.8f, 26.4f),       // La Mesa
        new Vector3(1208.3f, -1402.8f, 35.2f),      // El Burro Heights
        new Vector3(1181.4f, -330.8f, 69.3f),       // Mirror Park
        new Vector3(620.8f, 268.9f, 103.1f),        // Vinewood
        new Vector3(-724.6f, -935.1f, 19.2f),       // Little Seoul
        new Vector3(-526.0f, -1211.0f, 18.2f),      // La Puerta
        new Vector3(-2096.2f, -320.3f, 13.2f),      // Pacific Bluffs
        new Vector3(2581.3f, 362.0f, 108.5f),       // Tataviam Mountains
        new Vector3(176.6f, 6604.2f, 32.0f),        // Paleto Bay
        new Vector3(1702.3f, 6416.4f, 32.7f),       // Mount Chiliad
        new Vector3(2680.1f, 3264.2f, 55.2f),       // Grand Senora Desert
        new Vector3(1039.9f, 2671.3f, 39.5f),       // Grand Senora Desert
        new Vector3(1207.3f, 2660.1f, 37.9f),       // Harmony
        new Vector3(2539.7f, 2594.2f, 37.9f),       // East Joshua Road
        new Vector3(263.8f, 2606.5f, 44.9f),        // Route 68
        new Vector3(49.4f, 2778.7f, 58.0f),         // Route 68
        new Vector3(-93.3f, 6410.8f, 31.6f),        // Paleto Bay
        new Vector3(-2554.9f, 2334.3f, 33.0f)       // Route 68 (Ouest)
    };
    
    // Structure pour les pompes à essence
    private struct FuelPump
    {
        public Vector3 Position;
        public string StationName;
        
        public FuelPump(Vector3 position, string stationName)
        {
            Position = position;
            StationName = stationName;
        }
    }
    
    // Liste des pompes à essence (plusieurs par station)
    private readonly List<FuelPump> fuelPumps = new List<FuelPump>
    {
        // Station Davis (76)
        new FuelPump(new Vector3(-70.2f, -1761.8f, 28.4f), "Davis"),
        new FuelPump(new Vector3(-66.1f, -1767.7f, 28.4f), "Davis"),
        new FuelPump(new Vector3(-63.5f, -1767.9f, 28.4f), "Davis"),
        new FuelPump(new Vector3(-61.9f, -1759.7f, 28.4f), "Davis"),
        
        // East Los Santos (LTD)
        new FuelPump(new Vector3(265.0f, -1261.3f, 28.3f), "East Los Santos"),
        new FuelPump(new Vector3(273.0f, -1268.3f, 28.3f), "East Los Santos"),
        new FuelPump(new Vector3(258.0f, -1268.3f, 28.3f), "East Los Santos"),
        
        // La Mesa (RON)
        new FuelPump(new Vector3(819.6f, -1028.8f, 25.4f), "La Mesa"),
        new FuelPump(new Vector3(810.6f, -1030.8f, 25.4f), "La Mesa"),
        new FuelPump(new Vector3(817.6f, -1038.8f, 25.4f), "La Mesa"),
        
        // El Burro Heights
        new FuelPump(new Vector3(1208.3f, -1402.8f, 34.2f), "El Burro Heights"),
        new FuelPump(new Vector3(1208.3f, -1395.8f, 34.2f), "El Burro Heights"),
        new FuelPump(new Vector3(1212.3f, -1402.8f, 34.2f), "El Burro Heights"),
        
        // Mirror Park (Globe Oil)
        new FuelPump(new Vector3(1181.4f, -330.8f, 68.4f), "Mirror Park"),
        new FuelPump(new Vector3(1184.9f, -329.5f, 68.4f), "Mirror Park"),
        new FuelPump(new Vector3(1186.8f, -338.5f, 68.4f), "Mirror Park"),
        new FuelPump(new Vector3(1182.8f, -339.8f, 68.4f), "Mirror Park"),
        
        // Vinewood (RON)
        new FuelPump(new Vector3(620.8f, 268.9f, 102.1f), "Vinewood"),
        new FuelPump(new Vector3(628.8f, 263.9f, 102.1f), "Vinewood"),
        new FuelPump(new Vector3(622.8f, 258.9f, 102.1f), "Vinewood"),
        new FuelPump(new Vector3(615.8f, 263.9f, 102.1f), "Vinewood"),
        
        // Little Seoul (LTD)
        new FuelPump(new Vector3(-724.6f, -935.1f, 18.2f), "Little Seoul"),
        new FuelPump(new Vector3(-716.6f, -938.1f, 18.2f), "Little Seoul"),
        new FuelPump(new Vector3(-729.6f, -939.1f, 18.2f), "Little Seoul"),
        
        // La Puerta
        new FuelPump(new Vector3(-526.0f, -1211.0f, 17.2f), "La Puerta"),
        new FuelPump(new Vector3(-526.0f, -1204.0f, 17.2f), "La Puerta"),
        new FuelPump(new Vector3(-532.0f, -1211.0f, 17.2f), "La Puerta"),
        
        // Pacific Bluffs
        new FuelPump(new Vector3(-2096.2f, -320.3f, 12.2f), "Pacific Bluffs"),
        new FuelPump(new Vector3(-2089.2f, -320.3f, 12.2f), "Pacific Bluffs"),
        new FuelPump(new Vector3(-2096.2f, -327.3f, 12.2f), "Pacific Bluffs"),
        
        // Tataviam Mountains
        new FuelPump(new Vector3(2581.3f, 362.0f, 107.5f), "Tataviam Mountains"),
        new FuelPump(new Vector3(2581.3f, 355.0f, 107.5f), "Tataviam Mountains"),
        new FuelPump(new Vector3(2588.3f, 362.0f, 107.5f), "Tataviam Mountains"),
        
        // Paleto Bay (LTD)
        new FuelPump(new Vector3(176.6f, 6604.2f, 30.9f), "Paleto Bay"),
        new FuelPump(new Vector3(172.6f, 6603.2f, 30.9f), "Paleto Bay"),
        new FuelPump(new Vector3(177.6f, 6609.2f, 30.9f), "Paleto Bay"),
        new FuelPump(new Vector3(182.6f, 6602.2f, 30.9f), "Paleto Bay"),
        
        // Paleto Bay (2ème station)
        new FuelPump(new Vector3(-93.3f, 6410.8f, 30.6f), "Paleto Bay 2"),
        new FuelPump(new Vector3(-97.3f, 6407.8f, 30.6f), "Paleto Bay 2"),
        new FuelPump(new Vector3(-92.3f, 6416.8f, 30.6f), "Paleto Bay 2"),
        
        // Mount Chiliad
        new FuelPump(new Vector3(1702.3f, 6416.4f, 31.7f), "Mount Chiliad"),
        new FuelPump(new Vector3(1698.3f, 6416.4f, 31.7f), "Mount Chiliad"),
        new FuelPump(new Vector3(1702.3f, 6410.4f, 31.7f), "Mount Chiliad"),
        
        // Sandy Shores (RON)
        new FuelPump(new Vector3(2680.1f, 3264.2f, 54.2f), "Sandy Shores"),
        new FuelPump(new Vector3(2676.1f, 3262.2f, 54.2f), "Sandy Shores"),
        new FuelPump(new Vector3(2678.1f, 3274.2f, 54.2f), "Sandy Shores"),
        new FuelPump(new Vector3(2673.1f, 3274.2f, 54.2f), "Sandy Shores"),
        
        // Grand Senora Desert (1)
        new FuelPump(new Vector3(1039.9f, 2671.3f, 38.5f), "Grand Senora Desert 1"),
        new FuelPump(new Vector3(1039.9f, 2664.3f, 38.5f), "Grand Senora Desert 1"),
        new FuelPump(new Vector3(1046.9f, 2671.3f, 38.5f), "Grand Senora Desert 1"),
        
        // Grand Senora Desert (2)
        new FuelPump(new Vector3(1207.3f, 2660.1f, 36.9f), "Grand Senora Desert 2"),
        new FuelPump(new Vector3(1207.3f, 2653.1f, 36.9f), "Grand Senora Desert 2"),
        new FuelPump(new Vector3(1214.3f, 2660.1f, 36.9f), "Grand Senora Desert 2"),
        
        // East Joshua Road
        new FuelPump(new Vector3(2539.7f, 2594.2f, 36.9f), "East Joshua Road"),
        new FuelPump(new Vector3(2539.7f, 2587.2f, 36.9f), "East Joshua Road"),
        new FuelPump(new Vector3(2546.7f, 2594.2f, 36.9f), "East Joshua Road"),
        
        // Route 68 (1)
        new FuelPump(new Vector3(263.8f, 2606.5f, 43.9f), "Route 68"),
        new FuelPump(new Vector3(263.8f, 2599.5f, 43.9f), "Route 68"),
        new FuelPump(new Vector3(270.8f, 2606.5f, 43.9f), "Route 68"),
        
        // Route 68 (2)
        new FuelPump(new Vector3(49.4f, 2778.7f, 57.0f), "Route 68 North"),
        new FuelPump(new Vector3(49.4f, 2771.7f, 57.0f), "Route 68 North"),
        new FuelPump(new Vector3(56.4f, 2778.7f, 57.0f), "Route 68 North"),
        
        // Route 68 (Ouest)
        new FuelPump(new Vector3(-2554.9f, 2334.3f, 32.0f), "Route 68 West"),
        new FuelPump(new Vector3(-2551.9f, 2327.3f, 32.0f), "Route 68 West"),
        new FuelPump(new Vector3(-2560.9f, 2334.3f, 32.0f), "Route 68 West")
    };
    
    // Variables d'état
    private bool isNearGasStation = false;
    private bool isRefueling = false;
    private bool isAwaitingPayment = false;
    private bool showFuelHUD = true;
    private Vehicle? currentRefuelingVehicle = null;
    private float refuelingAmount = 0.0f;
    
    // Blips des stations-service
    private readonly List<Blip> gasStationBlips = new List<Blip>();
    
    // Variables pour le prix dynamique
    private float fuelPriceMultiplier = 1.0f;
    private bool fuelCrisisActive = false;
    private int crisisDay = 0;
    
    // Variables pour les effets moteur
    private bool engineStalling = false;
    private bool wrongFuelType = false;
    private float wrongFuelDamage = 0.0f;

    // Variables for timed consumption update
    private int lastConsumptionUpdateTime = 0;
    private const int consumptionUpdateInterval = 1000; // ms
    
    // Chemin de sauvegarde
    private readonly string saveFilePath;
    
    // Générateur de nombres aléatoires
    private readonly Random random = new Random();
    
    // Touches de contrôle
    private readonly Keys refuelKey = Keys.E;
    private readonly Keys confirmPaymentKey = Keys.Enter;
    private readonly Keys toggleHUDKey = Keys.K;
    
    // Variables pour le jerrycan
    private bool hasJerryCan = false;
    private readonly float jerryCanCapacity = 20.0f; // 20 litres d'essence
    private float jerryCanFuel = 20.0f;
    private readonly Keys useJerryCanKey = Keys.E;
    private bool isRefuelingWithJerryCan = false;
    private Vehicle? currentJerryCanRefuelVehicle = null;
    private readonly float jerryCanRefillRate = 2.0f; // Litres per second
    
    // Add variables for jerrycan UI and dropped jerrycan
    private bool showJerryCanUI = false;
    private bool jerryCanDropped = false;
    private Vector3 droppedJerryCanPosition = Vector3.Zero;
    private int jerryCanDisappearTimer = 0;
    private readonly int jerryCanDisappearTime = 8000; // 8 seconds to disappear
    
    public RealisticFuelSystem()
    {
        // Intervalle de mise à jour (en ms)
        Interval = 0;
        
        // Chemin de sauvegarde dans le dossier SHVDN
        string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ScriptHookVDotNet");
        Directory.CreateDirectory(baseDir);
        saveFilePath = Path.Combine(baseDir, "fuel_system_data.dat");
        
        // Charger les données sauvegardées
        LoadFuelData();
        
        // S'abonner aux événements
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
        
        // Initialiser les multiplicateurs de prix dynamiques
        UpdateDynamicPrices();
        
        // Créer les blips pour les stations-service
        CreateGasStationBlips();
        
        // Afficher une notification au démarrage
        Notification.PostTicker("~g~Système de carburant réaliste activé", true);
        Notification.PostTicker("~y~Appuyez sur E près d'une station-service pour faire le plein", true);
        Notification.PostTicker("~b~Appuyez sur E pour utiliser un jerrycan sur un véhicule", true);
    }
    
    private void OnTick(object sender, EventArgs e)
    {
        Ped playerPed = Game.Player.Character;
        Vehicle? playerVehicle = playerPed.CurrentVehicle;
        int currentGameTime = Game.GameTime;
        
        // Vérifier si le joueur a un jerrycan
        CheckJerryCan();
        
        // Gérer le jerrycan jeté au sol
        HandleDroppedJerryCan();
        
        // Gérer la consommation de carburant si le joueur est dans un véhicule
        if (playerVehicle != null && playerVehicle.Exists())
        {
            int vehicleID = playerVehicle.Handle; // Ensure vehicleID is defined for use in HUD and other logic

            if (currentGameTime - lastConsumptionUpdateTime >= consumptionUpdateInterval)
            {
                // Initialiser le niveau de carburant si c'est la première fois que ce véhicule est utilisé
                if (!vehicleFuelLevels.ContainsKey(vehicleID))
                {
                    InitializeVehicleFuel(playerVehicle);
                }

                // Obtenir le niveau de carburant actuel
                float currentFuel = vehicleFuelLevels[vehicleID];

                // Vérifier si le moteur est en marche
                if (playerVehicle.IsEngineRunning)
                {
                    // Si le véhicule a le mauvais type de carburant
                    if (wrongFuelType && vehicleID == currentRefuelingVehicle?.Handle)
                    {
                        // Augmenter les dégâts moteur
                        wrongFuelDamage += 0.1f;

                        // Effets de moteur qui tousse
                        if (Game.GameTime % 3000 < 1500) // This Game.GameTime is fine for effects
                        {
                            // Utiliser Health au lieu de HealthPercentage
                            playerVehicle.Health -= 2;
                            Function.Call(Hash.SET_VEHICLE_ENGINE_HEALTH, playerVehicle.Handle,
                                         Function.Call<float>(Hash.GET_VEHICLE_ENGINE_HEALTH, playerVehicle.Handle) - 1.0f);

                            // Effet visuel de fumée du moteur (type 22 = fumée)
                            Function.Call(Hash.ADD_EXPLOSION, playerVehicle.Position.X, playerVehicle.Position.Y, playerVehicle.Position.Z,
                                         22, 0.0f, true, false, 0.0f);
                        }

                        // Si les dégâts sont trop importants, caler le moteur
                        if (wrongFuelDamage > 20.0f)
                        {
                            playerVehicle.IsEngineRunning = false;
                            wrongFuelType = false;
                            Notification.PostTicker("~r~Moteur endommagé par le mauvais type de carburant!", true);
                        }
                    }

                    // Si le véhicule a du carburant
                    if (currentFuel > 0)
                    {
                        // Calculer la consommation de carburant
                        float consumption = CalculateFuelConsumption(playerVehicle);

                        // Soustraire la consommation du niveau de carburant
                        currentFuel -= consumption;

                        // Mettre à jour la valeur dans le dictionnaire
                        vehicleFuelLevels[vehicleID] = Math.Max(0, currentFuel);

                        // Si le carburant est faible, commencer à faire tousser le moteur
                        if (currentFuel < 5.0f && !engineStalling)
                        {
                            engineStalling = true;
                        }

                        // Effets de moteur qui tousse quand le carburant est faible
                        if (engineStalling && currentFuel < 5.0f)
                        {
                            // Plus le carburant est bas, plus les effets sont fréquents
                            if (random.Next(0, 20) < (5 - Math.Floor(currentFuel)))
                            {
                                // Faire tousser le moteur
                                playerVehicle.IsEngineRunning = false;
                                Wait(200); // This Wait might be an issue if Interval is 0. Consider effect timing.
                                playerVehicle.IsEngineRunning = true;
                            }
                        }
                        else
                        {
                            engineStalling = false;
                        }
                    }
                    else
                    {
                        // Plus de carburant, arrêter le moteur
                        playerVehicle.IsEngineRunning = false;
                        Notification.PostTicker("~r~Panne sèche! Plus d'essence.", true);
                    }
                }
                lastConsumptionUpdateTime = currentGameTime;
            }

            // Afficher le HUD du carburant si activé - moved outside timed block
            if (showFuelHUD)
            {
                float fuelLevelForDisplay = 0;
                if (vehicleFuelLevels.TryGetValue(vehicleID, out float currentVehicleFuel))
                {
                    fuelLevelForDisplay = currentVehicleFuel;
                }
                // else, it remains 0, or we could initialize, but InitializeVehicleFuel should have run
                // in the timed block if this is the first time. If not first time, value exists.
                // If it's a new vehicle after the timed block but before next tick, it might show 0 briefly.
                // For safety, can add InitializeVehicleFuel here too or ensure it's always present.
                else if (!vehicleFuelLevels.ContainsKey(vehicleID)) // If truly not there
                {
                     InitializeVehicleFuel(playerVehicle);
                     fuelLevelForDisplay = vehicleFuelLevels.ContainsKey(vehicleID) ? vehicleFuelLevels[vehicleID] : 0;
                }
                DisplayFuelHUD(playerVehicle, fuelLevelForDisplay);
            }
            
            // Si le joueur est en attente de paiement et monte dans un véhicule, c'est un vol
            if (isAwaitingPayment && currentRefuelingVehicle != null)
            {
                if (playerVehicle == currentRefuelingVehicle || 
                    playerPed.Position.DistanceTo(currentRefuelingVehicle.Position) > 8.0f)
                {
                    StealFuel();
                }
            }
        }
        
        // Vérifier si le joueur est près d'une station-service
        isNearGasStation = IsNearGasStation(out Vector3 closestPump, 5.0f);
        
        // Afficher les marqueurs pour les pompes à proximité
        Vector3 playerPos = playerPed.Position;
        
        // Dessiner les marqueurs (visibles à plus grande distance et depuis un véhicule aussi)
        foreach (FuelPump pump in fuelPumps)
        {
            float distance = playerPos.DistanceTo(pump.Position);
            
            // Augmenter la distance de visibilité à 20 mètres
            if (distance < 20.0f)
            {
                DrawGasPumpMarker(pump.Position);
            }
        }
        
        // Afficher les infos de la station-service si à proximité
        if (isNearGasStation && !isRefueling && !isAwaitingPayment)
        {
            // Obtenir le prix actuel dans cette station
            float currentPrice = GetCurrentFuelPrice(closestPump);
            
            // Afficher les instructions
            DisplayHelpTextThisFrame($"Appuyez sur ~INPUT_CONTEXT~ pour faire le plein. Prix: ~g~${currentPrice:F2}/L");
        }
        
        // Gérer le ravitaillement en cours
        if (isRefueling && currentRefuelingVehicle != null)
        {
            // Vérifier que le joueur est toujours à proximité
            if (playerPed.Position.DistanceTo(currentRefuelingVehicle.Position) > 5.0f)
            {
                AbortRefueling("~r~Vous vous êtes trop éloigné du véhicule.");
            }
            else
            {
                // Vérifier si la touche E est maintenue enfoncée
                bool isKeyPressed = Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 0, 51); // 51 = INPUT_CONTEXT (touche E)
                
                if (!isKeyPressed)
                {
                    // Si du carburant a été ajouté, passer à la confirmation
                    if (refuelingAmount > 0)
                    {
                        isRefueling = false;
                        isAwaitingPayment = true;
                        
                        // Notifier le joueur
                        float finalCost = GetRefuelingCost();
                        Notification.PostTicker($"~g~Ravitaillement terminé. ~w~Coût total: ~g~${finalCost:F2}", true);
                        Notification.PostTicker($"~y~Appuyez sur ~b~Entrée ~y~pour confirmer le paiement.", true);
                    }
                    else
                    {
                        // Sinon, simplement annuler le ravitaillement
                        StopRefueling();
                    }
                    return;
                }
                
                // Continuer le ravitaillement
                int vehicleID = currentRefuelingVehicle.Handle;
                float maxFuel = GetVehicleFuelCapacity(currentRefuelingVehicle);
                float currentFuel = vehicleFuelLevels[vehicleID];
                
                // Ajouter du carburant
                if (currentFuel < maxFuel)
                {
                    float fuelToAdd = Math.Min(refillRate * (Interval / 1000.0f), maxFuel - currentFuel);
                    currentFuel += fuelToAdd;
                    refuelingAmount += fuelToAdd;
                    
                    // Mettre à jour le niveau de carburant
                    vehicleFuelLevels[vehicleID] = currentFuel;
                    
                    // Afficher la progression
                    float percentage = (currentFuel / maxFuel) * 100;
                    DisplayHelpTextThisFrame($"Ravitaillement en cours: ~b~{percentage:F1}%~w~ Coût actuel: ~g~${GetRefuelingCost():F2}~w~ (Maintenez ~INPUT_CONTEXT~, relâchez pour terminer)");
                }
                else
                {
                    // Plein terminé, passer à la phase de confirmation
                    isRefueling = false;
                    isAwaitingPayment = true;
                    
                    // Notifier le joueur
                    float finalCost = GetRefuelingCost();
                    Notification.PostTicker($"~g~Réservoir plein! ~w~Coût total: ~g~${finalCost:F2}", true);
                    Notification.PostTicker($"~y~Appuyez sur ~b~Entrée ~y~pour confirmer le paiement.", true);
                }
            }
        }
        
        // Gérer l'attente de paiement
        if (isAwaitingPayment && currentRefuelingVehicle != null)
        {
            // Vérifier que le joueur est toujours à proximité
            if (playerPed.Position.DistanceTo(currentRefuelingVehicle.Position) > 8.0f)
            {
                StealFuel();
            }
            else
            {
                // Afficher le message de confirmation
                float finalCost = GetRefuelingCost();
                DisplayHelpTextThisFrame($"Appuyez sur ~INPUT_FRONTEND_ACCEPT~ pour payer ~g~${finalCost:F2}~w~ ou éloignez-vous pour partir sans payer");
            }
        }
        
        // Afficher l'UI du jerrycan si nécessaire
        if (hasJerryCan && showJerryCanUI)
        {
            DisplayJerryCanUI();
        }

        // Gérer le ravitaillement avec jerrycan en cours
        if (isRefuelingWithJerryCan && currentJerryCanRefuelVehicle != null && currentJerryCanRefuelVehicle.Exists())
        {
            // Ped playerPed = Game.Player.Character; // CS0136 Error: Already defined in this scope (at the start of OnTick)

            // Distance Check
            if (playerPed.Position.DistanceTo(currentJerryCanRefuelVehicle.Position) > 5.0f)
            {
                isRefuelingWithJerryCan = false;
                currentJerryCanRefuelVehicle = null;
                playerPed.Task.ClearAll(); // Stop animation
                Notification.PostTicker("~y~Ravitaillement avec jerrycan arrêté (trop éloigné).", true);
            }
            else
            {
                // Key Press Check
                bool isKeyPressed = Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 0, 51); // INPUT_CONTEXT (E key)

                if (isKeyPressed)
                {
                    if (currentJerryCanRefuelVehicle == null || !currentJerryCanRefuelVehicle.Exists())
                    {
                        isRefuelingWithJerryCan = false;
                        // Optionally, add a Notification.PostTicker here for debugging if this unexpected state is hit.
                        // E.g., Notification.PostTicker("~r~Error: currentJerryCanRefuelVehicle became null unexpectedly.", true);
                        if (Game.Player.Character != null && Game.Player.Character.Exists())
                        {
                            Game.Player.Character.Task.ClearAll(); // Stop animation if player exists
                        }
                        return; // Exit this part of OnTick logic
                    }
                    int vehicleID = currentJerryCanRefuelVehicle.Handle;
                    if (!vehicleFuelLevels.ContainsKey(vehicleID))
                    {
                        InitializeVehicleFuel(currentJerryCanRefuelVehicle);
                    }
                    float currentFuel = vehicleFuelLevels[vehicleID];
                    float capacity = GetVehicleFuelCapacity(currentJerryCanRefuelVehicle);

                    // Check if JerryCan has fuel and vehicle needs fuel
                    if (jerryCanFuel > 0 && currentFuel < capacity)
                    {
                        // float deltaTime = (Interval == 0 ? consumptionUpdateInterval / 1000.0f : Interval / 1000.0f); // Old calculation
                        // float fuelToAdd = Math.Min(jerryCanRefillRate * deltaTime, capacity - currentFuel); // Old calculation
                        // fuelToAdd = Math.Min(fuelToAdd, jerryCanFuel); // Don't add more than jerryCan has // Old calculation

                        float fuelToAdd = jerryCanRefillRate * Game.LastFrameTime;
                        float neededByVehicle = capacity - currentFuel;
                        fuelToAdd = Math.Min(fuelToAdd, neededByVehicle);
                        fuelToAdd = Math.Min(fuelToAdd, jerryCanFuel);

                        vehicleFuelLevels[vehicleID] += fuelToAdd;
                        jerryCanFuel -= fuelToAdd;

                        DisplayHelpTextThisFrame($"Ravitaillement Jerrycan: {vehicleFuelLevels[vehicleID]:F1}/{capacity:F1}L | Jerrycan: {jerryCanFuel:F1}L");

                        // Handle empty JerryCan or full tank during refueling
                        if (jerryCanFuel <= 0 || vehicleFuelLevels[vehicleID] >= capacity)
                        {
                            isRefuelingWithJerryCan = false;
                            playerPed.Task.ClearAll(); // Stop animation
                            Notification.PostTicker(jerryCanFuel <= 0 ? "~y~Jerrycan vide." : "~g~Réservoir plein.", true);
                            if (jerryCanFuel <= 0) { HandleEmptyJerryCanProcedure(playerPed); }
                            currentJerryCanRefuelVehicle = null;
                        }
                    }
                    else // JerryCan empty or tank full at start of tick while key held
                    {
                        isRefuelingWithJerryCan = false;
                        playerPed.Task.ClearAll(); // Stop animation
                        Notification.PostTicker(jerryCanFuel <= 0 ? "~y~Jerrycan vide." : "~g~Réservoir plein.", true);
                        if (jerryCanFuel <= 0 && !jerryCanDropped) { HandleEmptyJerryCanProcedure(playerPed); }
                        currentJerryCanRefuelVehicle = null;
                    }
                }
                else // (!isKeyPressed - key released)
                {
                    isRefuelingWithJerryCan = false;
                    playerPed.Task.ClearAll(); // Stop animation
                    Notification.PostTicker("~y~Ravitaillement avec jerrycan arrêté.", true);
                    currentJerryCanRefuelVehicle = null;
                }
            }
        }
        
        // Sauvegarder périodiquement les données
        // The original condition `Game.GameTime % 60000 < Interval` would always be true if Interval is 0.
        // Changing to check against consumptionUpdateInterval if Interval is 0, or a fixed sensible time.
        // Or, simply run it based on a fixed time like every 60 seconds regardless of Interval.
        // For now, let's make it run if Interval is 0, effectively every tick, which is not ideal for saving.
        // A better approach would be a separate timer for saving.
        // Given the existing logic: if Interval is 0, this was `Game.GameTime % 60000 < 0` (false).
        // If Interval was 1000 (original script), it was `Game.GameTime % 60000 < 1000`.
        // Let's stick to the original logic structure for now, noting Interval is 0.
        if (Game.GameTime % 60000 < (Interval == 0 ? 1000 : Interval) ) // Effectively, if Interval is 0, check against 1000ms.
        {
            SaveFuelData();
        }
    }
    
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Touche pour faire le plein ou utiliser le jerrycan (E correspond à INPUT_CONTEXT dans GTA)
        if (e.KeyCode == refuelKey || e.KeyCode == useJerryCanKey)
        {
            // Si on est en attente de paiement, ne rien faire
            if (isAwaitingPayment)
                return;
            
            Ped playerPed = Game.Player.Character;
            
            // Vérifier si le joueur a un jerrycan et est à pied
            if (hasJerryCan && playerPed.CurrentVehicle == null)
            {
                // Check if jerrycan has fuel
                if (jerryCanFuel <= 0)
                {
                    Notification.PostTicker("~r~Votre jerrycan est vide.", true);
                    return;
                }

                // Find the nearest vehicle
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(playerPed.Position, 5.0f);
                Vehicle? nearestVehicle = null;
                float minDistance = float.MaxValue;

                foreach (Vehicle vehicle in nearbyVehicles)
                {
                    if (vehicle != null && vehicle.Exists()) // Ensure vehicle exists
                    {
                        float distance = vehicle.Position.DistanceTo(playerPed.Position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestVehicle = vehicle;
                        }
                    }
                }

                if (nearestVehicle != null)
                {
                    int vehicleID = nearestVehicle.Handle;
                    if (!vehicleFuelLevels.ContainsKey(vehicleID))
                    {
                        InitializeVehicleFuel(nearestVehicle);
                    }
                    float currentFuel = vehicleFuelLevels[vehicleID];
                    float capacity = GetVehicleFuelCapacity(nearestVehicle);

                    if (currentFuel >= capacity)
                    {
                        Notification.PostTicker("~y~Le réservoir de ce véhicule est déjà plein.", true);
                        return;
                    }

                    // Start refueling with JerryCan
                    isRefuelingWithJerryCan = true;
                    currentJerryCanRefuelVehicle = nearestVehicle;
                    Function.Call(Hash.TASK_PLAY_ANIM, playerPed.Handle, "mp_arresting", "a_uncuff", 8.0f, -8.0f, -1, 48, 0, false, false, false);
                    Notification.PostTicker("~b~Début du ravitaillement avec jerrycan. Maintenez ~INPUT_CONTEXT~ pour continuer.", true);
                }
                else
                {
                    Notification.PostTicker("~y~Aucun véhicule à proximité pour utiliser le jerrycan.", true);
                }
                return; // Important: return after handling jerrycan logic
            }
            
            // Si on est près d'une station-service et pas en train de faire le plein
            if (isNearGasStation && !isRefueling)
            {
                // Le joueur doit être à pied
                if (playerPed.CurrentVehicle == null)
                {
                    // Trouver le véhicule le plus proche
                    Vehicle[] nearbyVehicles = World.GetNearbyVehicles(playerPed.Position, 5.0f);
                    Vehicle? nearestVehicle = null;
                    float minDistance = float.MaxValue;
                    
                    // Parcourir manuellement le tableau pour trouver le plus proche
                    foreach (Vehicle vehicle in nearbyVehicles)
                    {
                        if (vehicle != null)
                        {
                            float distance = vehicle.Position.DistanceTo(playerPed.Position);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestVehicle = vehicle;
                            }
                        }
                    }
                    
                    if (nearestVehicle != null)
                    {
                        StartRefueling(nearestVehicle);
                    }
                    else
                    {
                        Notification.PostTicker("~y~Aucun véhicule à proximité.", true);
                    }
                }
                else
                {
                    Notification.PostTicker("~y~Vous devez descendre du véhicule pour faire le plein.", true);
                }
            }
        }
        
        // Touche pour confirmer le paiement
        if (e.KeyCode == confirmPaymentKey && isAwaitingPayment)
        {
            ConfirmPayment();
        }
        
        // Touche pour activer/désactiver le HUD
        if (e.KeyCode == toggleHUDKey)
        {
            showFuelHUD = !showFuelHUD;
            Notification.PostTicker(showFuelHUD ? "~g~HUD de carburant activé" : "~y~HUD de carburant désactivé", true);
        }
    }
    
    private void ConfirmPayment()
    {
        if (currentRefuelingVehicle == null || !isAwaitingPayment)
            return;
        
        // Calculer le coût
        float finalCost = GetRefuelingCost();
        
        // Déduire l'argent du joueur
        if (DeductMoney(finalCost))
        {
            // Paiement réussi
            Notification.PostTicker($"~g~Paiement effectué: ${finalCost:F2}", true);
            
            // Effet sonore de paiement
            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "PURCHASE", "HUD_LIQUOR_STORE_SOUNDSET", 1);
            
            // Nettoyer les états
            isAwaitingPayment = false;
            currentRefuelingVehicle = null;
            refuelingAmount = 0.0f;
        }
    }
    
    private void StealFuel()
    {
        if (!isAwaitingPayment || currentRefuelingVehicle == null)
            return;
        
        // Le joueur s'est enfui sans payer
        Notification.PostTicker("~r~Vous êtes parti sans payer! La police a été alertée.", true);
        
        // Effet sonore d'alarme
        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "TIMER_STOP", "HUD_MINI_GAME_SOUNDSET", 1);
        
        // Effet visuel de caméra de sécurité
        Function.Call(Hash.ANIMPOSTFX_PLAY, "CamPushInNeutral", 0, false);
        
        // Créer des voitures de police qui poursuivent le joueur
        Vector3 playerPos = Game.Player.Character.Position;
        SpawnPoliceResponse(playerPos);
        
        // Ajouter un niveau de recherche (3 étoiles pour vol de carburant)
        Game.Player.Wanted.SetWantedLevel(3, false);
        Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
        
        // Nettoyer les états
        isAwaitingPayment = false;
        currentRefuelingVehicle = null;
        refuelingAmount = 0.0f;
    }
    
    private void SpawnPoliceResponse(Vector3 location)
    {
        try
        {
            // Créer deux voitures de police à proximité
            Vector3 spawnPos1 = World.GetNextPositionOnStreet(location.Around(80.0f));
            Vector3 spawnPos2 = World.GetNextPositionOnStreet(location.Around(100.0f));
            
            // Création des véhicules de police
            Vehicle police1 = World.CreateVehicle(VehicleHash.Police, spawnPos1);
            Vehicle police2 = World.CreateVehicle(VehicleHash.Police2, spawnPos2);
            
            if (police1 != null)
            {
                // Créer le conducteur et le passager
                Ped cop1 = World.CreatePed(PedHash.Cop01SMY, police1.Position);
                Ped cop2 = World.CreatePed(PedHash.Cop01SMY, police1.Position);
                
                // Placer dans le véhicule
                cop1.Task.WarpIntoVehicle(police1, VehicleSeat.Driver);
                cop2.Task.WarpIntoVehicle(police1, VehicleSeat.Passenger);
                
                // Activer la sirène
                Function.Call(Hash.SET_VEHICLE_SIREN, police1.Handle, true);
                Function.Call(Hash.SET_VEHICLE_HAS_MUTED_SIRENS, police1.Handle, true);
                
                // Faire poursuivre le joueur
                cop1.Task.VehicleChase(Game.Player.Character);
                Function.Call(Hash.SET_TASK_VEHICLE_CHASE_BEHAVIOR_FLAG, cop1.Handle, 1, true); // Conduite agressive
            }
            
            if (police2 != null)
            {
                // Même chose pour le deuxième véhicule
                Ped cop3 = World.CreatePed(PedHash.Cop01SMY, police2.Position);
                cop3.Task.WarpIntoVehicle(police2, VehicleSeat.Driver);
                
                Function.Call(Hash.SET_VEHICLE_SIREN, police2.Handle, true);
                Function.Call(Hash.SET_VEHICLE_HAS_MUTED_SIRENS, police2.Handle, true);
                
                cop3.Task.VehicleChase(Game.Player.Character);
            }
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Erreur lors de l'apparition de la police: {ex.Message}", true);
        }
    }
    
    private void AbortRefueling(string reason)
    {
        StopRefueling();
        Notification.PostTicker(reason, true);
    }
    
    private void OnAborted(object sender, EventArgs e)
    {
        // Sauvegarder les données avant de quitter
        SaveFuelData();
    }
    
    private float CalculateFuelConsumption(Vehicle vehicle)
    {
        // Consommation de base en fonction du temps écoulé depuis la dernière frame
        float consumption = fuelConsumptionBaseRate * (consumptionUpdateInterval / 1000.0f);
        
        // Si le véhicule est au ralenti
        if (vehicle.Speed < 0.1f)
        {
            return idleConsumptionRate * (consumptionUpdateInterval / 1000.0f);
        }
        
        // Facteur basé sur la vitesse (consommation augmente de façon exponentielle avec la vitesse)
        float speedFactor = (float)Math.Pow(vehicle.Speed / 10.0f, 1.5);
        
        // Facteur basé sur l'accélération
        float accelerationFactor = 1.0f;
        if (vehicle.Acceleration > 0.1f)
        {
            accelerationFactor = 1.0f + (vehicle.Acceleration * 5.0f);
        }
        
        // Facteur basé sur le terrain/pente
        float slopeFactor = 1.0f;
        float groundHeight;
        if (World.GetGroundHeight(vehicle.Position, out groundHeight))
        {
            float heightDifference = vehicle.Position.Z - groundHeight;
            
            if (heightDifference > 1.0f) // Montée
            {
                slopeFactor = 1.2f;
            }
            else if (heightDifference < -1.0f) // Descente
            {
                slopeFactor = 0.8f;
            }
        }
        
        // Facteur basé sur le type de véhicule
        float vehicleTypeFactor = GetVehicleTypeFuelFactor(vehicle);
        
        // Calculer la consommation finale
        return consumption * speedFactor * accelerationFactor * slopeFactor * vehicleTypeFactor;
    }
    
    private float GetVehicleTypeFuelFactor(Vehicle vehicle)
    {
        // Facteur de consommation par type de véhicule
        switch (vehicle.ClassType)
        {
            case VehicleClass.Super:
                return 1.5f;
            case VehicleClass.Sports:
                return 1.3f;
            case VehicleClass.Muscle:
                return 1.6f;
            case VehicleClass.SUVs:
                return 1.4f;
            case VehicleClass.Compacts:
                return 0.8f;
            case VehicleClass.Sedans:
                return 1.0f;
            case VehicleClass.Industrial:
                return 1.8f;
            case VehicleClass.Utility:
                return 1.6f;
            case VehicleClass.Commercial:
                return 2.0f;
            case VehicleClass.Military:
                return 2.5f;
            default:
                return 1.0f;
        }
    }
    
    private float GetVehicleFuelCapacity(Vehicle vehicle)
    {
        // Obtenir la capacité en fonction du type de véhicule
        if (vehicleFuelCapacities.TryGetValue(vehicle.ClassType, out float capacity))
        {
            return capacity;
        }
        
        // Capacité par défaut
        return maxFuelCapacity;
    }
    
    private FuelType GetVehicleFuelType(Vehicle vehicle)
    {
        // Vérifier d'abord si c'est un véhicule spécial
        string modelName = vehicle.Model.ToString();
        if (specialVehicleFuelTypes.TryGetValue(modelName, out FuelType specialType))
        {
            return specialType;
        }
        
        // Sinon, utiliser le type par classe
        if (vehicleFuelTypes.TryGetValue(vehicle.ClassType, out FuelType classType))
        {
            return classType;
        }
        
        // Type par défaut
        return FuelType.Gasoline;
    }
    
    private void InitializeVehicleFuel(Vehicle vehicle)
    {
        int vehicleID = vehicle.Handle;
        
        // Déterminer la capacité maximale
        float capacity = GetVehicleFuelCapacity(vehicle);
        
        // Initialiser avec un niveau aléatoire entre 30% et 80%
        float initialLevel = capacity * (0.3f + (float)random.NextDouble() * 0.5f);
        
        // Stocker le niveau de carburant
        vehicleFuelLevels[vehicleID] = initialLevel;
        
        // Attribuer le type de carburant
        vehicleFuelTypeAssigned[vehicleID] = GetVehicleFuelType(vehicle);
    }
    
    private bool IsNearGasStation(out Vector3 closestPump, float radius = 10.0f)
    {
        Vector3 playerPos = Game.Player.Character.Position;
        float minDistance = float.MaxValue;
        closestPump = Vector3.Zero;
        
        foreach (FuelPump pump in fuelPumps)
        {
            float distance = playerPos.DistanceTo(pump.Position);
            if (distance < radius && distance < minDistance)
            {
                minDistance = distance;
                closestPump = pump.Position;
            }
        }
        
        return minDistance < radius;
    }
    
    private void StartRefueling(Vehicle vehicle)
    {
        if (vehicle != null && vehicle.Exists())
        {
            currentRefuelingVehicle = vehicle;
            isRefueling = true;
            isAwaitingPayment = false;
            refuelingAmount = 0.0f;
            
            // Animation de ravitaillement (à implémenter)
            Ped playerPed = Game.Player.Character;
            // Tâche "tenir un objet" (pistolet de pompe)
            Function.Call(Hash.TASK_PLAY_ANIM, playerPed.Handle, "mp_arresting", "a_uncuff", 8.0f, -8.0f, -1, 48, 0, false, false, false);
            
            Notification.PostTicker("~g~Début du ravitaillement. ~w~Maintenez ~y~E ~w~pour continuer.", true);
        }
    }
    
    private void StopRefueling()
    {
        isRefueling = false;
        isAwaitingPayment = false;
        currentRefuelingVehicle = null;
        refuelingAmount = 0.0f;
        
        // Arrêter l'animation
        Ped playerPed = Game.Player.Character;
        playerPed.Task.ClearAll();
    }
    
    private float GetCurrentFuelPrice(Vector3 stationPosition)
    {
        // Déterminer la zone
        string area = GetAreaName(stationPosition);
        float areaMultiplier = 1.0f;
        
        if (areaFuelPriceModifiers.TryGetValue(area, out float modifier))
        {
            areaMultiplier = modifier;
        }
        
        // Prix de base pour l'essence (type le plus courant)
        float basePrice = baseFuelPrices[FuelType.Gasoline];
        
        // Appliquer les modificateurs
        return basePrice * areaMultiplier * fuelPriceMultiplier;
    }
    
    private string GetAreaName(Vector3 position)
    {
        // Déterminer le quartier/zone
        int zoneHash = Function.Call<int>(Hash.GET_ZONE_AT_COORDS, position.X, position.Y, position.Z);
        string zoneName = Function.Call<string>(Hash.GET_NAME_OF_ZONE, position.X, position.Y, position.Z);
        
        // Convertir en clé de zone simplifié
        if (zoneName.Contains("DOWNTOWN"))
            return "DOWNTOWN";
        else if (zoneName.Contains("ROCKFORD"))
            return "ROCKFORD";
        else if (zoneName.Contains("SANDY"))
            return "SANDY";
        else if (zoneName.Contains("PALETO"))
            return "PALETO";
        
        return "DEFAULT";
    }
    
    private float GetRefuelingCost()
    {
        if (currentRefuelingVehicle == null)
            return 0.0f;
        
        // Déterminer le type de carburant utilisé
        FuelType fuelType = FuelType.Gasoline;
        if (vehicleFuelTypeAssigned.TryGetValue(currentRefuelingVehicle.Handle, out FuelType assignedType))
        {
            fuelType = assignedType;
        }
        
        // Obtenir le prix de base pour ce type
        float basePrice = baseFuelPrices[fuelType];
        
        // Appliquer les multiplicateurs
        Vector3 stationPosition = Game.Player.Character.Position;
        string area = GetAreaName(stationPosition);
        float areaMultiplier = 1.0f;
        
        if (areaFuelPriceModifiers.TryGetValue(area, out float modifier))
        {
            areaMultiplier = modifier;
        }
        
        // Calculer le coût total
        return refuelingAmount * basePrice * areaMultiplier * fuelPriceMultiplier;
    }
    
    private bool DeductMoney(float amount)
    {
        try
        {
            // Arrondir à l'entier supérieur
            int intAmount = (int)Math.Ceiling(amount);
            
            // Ne rien faire si le montant est nul ou négatif
            if (intAmount <= 0)
                return false;
            
            // Déduire l'argent du joueur
            int playerMoney = Game.Player.Money;
            
            if (playerMoney >= intAmount)
            {
                // Utiliser la propriété Money pour déduire directement
                Game.Player.Money -= intAmount;
                
                // Confirmer la transaction avec un effet sonore
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "PURCHASE", "HUD_LIQUOR_STORE_SOUNDSET", 1);
                return true;
            }
            else
            {
                // Le joueur n'a pas assez d'argent
                Notification.PostTicker("~r~Vous n'avez pas assez d'argent! La police a été alertée.", true);
                
                // Ajouter 2 étoiles de recherche en utilisant les méthodes recommandées
                Game.Player.Wanted.SetWantedLevel(Math.Min(Game.Player.Wanted.WantedLevel + 2, 5), false);
                Game.Player.Wanted.ApplyWantedLevelChangeNow(false);
                
                // Effet sonore d'alarme
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Beep_Red", "DLC_HEIST_HACKING_SNAKE_SOUNDS", 1);
                return false;
            }
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Erreur lors du paiement: {ex.Message}", true);
            return false;
        }
    }
    
    private void UpdateDynamicPrices()
    {
        // Jour de la semaine
        int dayOfWeek = Function.Call<int>(Hash.GET_CLOCK_DAY_OF_WEEK);
        
        // Variabilité de base par jour
        float dayVariation = 1.0f;
        switch (dayOfWeek)
        {
            case 1: // Lundi
                dayVariation = 0.95f;
                break;
            case 5: // Vendredi
                dayVariation = 1.05f;
                break;
            case 6: // Samedi
                dayVariation = 1.1f;
                break;
            case 0: // Dimanche
                dayVariation = 1.05f;
                break;
        }
        
        // Vérifier si une crise est active
        if (!fuelCrisisActive)
        {
            // 1% de chance par jour qu'une crise commence
            if (random.NextDouble() < 0.01)
            {
                fuelCrisisActive = true;
                crisisDay = 0;
                Notification.PostTicker("~r~Une crise du pétrole commence! Les prix vont augmenter.", true);
            }
        }
        else
        {
            // Gérer la crise existante
            crisisDay++;
            
            // La crise dure entre 3 et 7 jours
            if (crisisDay > random.Next(3, 8))
            {
                fuelCrisisActive = false;
                Notification.PostTicker("~g~La crise du pétrole est terminée. Les prix reviennent à la normale.", true);
            }
        }
        
        // Appliquer le multiplicateur de crise
        float crisisMultiplier = fuelCrisisActive ? 1.5f + (crisisDay * 0.1f) : 1.0f;
        
        // Appliquer une légère variation aléatoire quotidienne
        float randomVariation = 0.9f + (float)(random.NextDouble() * 0.2f);
        
        // Calculer le multiplicateur final
        fuelPriceMultiplier = dayVariation * crisisMultiplier * randomVariation;
    }
    
    private void DisplayFuelHUD(Vehicle vehicle, float fuelLevel)
    {
        // Obtenir la capacité du véhicule
        float capacity = GetVehicleFuelCapacity(vehicle);
        
        // Calculer le pourcentage
        float percentage = (fuelLevel / capacity) * 100;
        
        // Déterminer la couleur en fonction du niveau
        Color color = Color.White;
        if (percentage < 10)
            color = Color.Red;
        else if (percentage < 25)
            color = Color.Orange;
        else if (percentage < 50)
            color = Color.Yellow;
        
        // Déterminer le type de carburant
        string fuelTypeText = "ESSENCE";
        if (vehicleFuelTypeAssigned.TryGetValue(vehicle.Handle, out FuelType fuelType))
        {
            switch (fuelType)
            {
                case FuelType.Gasoline:
                    fuelTypeText = "ESSENCE";
                    break;
                case FuelType.Diesel:
                    fuelTypeText = "DIESEL";
                    break;
                case FuelType.Electric:
                    fuelTypeText = "ÉLECTRIQUE";
                    break;
            }
        }
        
        // Afficher l'indicateur de carburant en utilisant une approche simplifiée
        DisplaySimpleFuelIndicator(fuelTypeText, percentage, color);
    }
    
    // Méthode simplifiée pour afficher un indicateur de carburant (comme le speedometer)
    private void DisplaySimpleFuelIndicator(string fuelType, float percentage, Color color)
    {
        // Utiliser une approche similaire au compteur de vitesse (simple texte)
        Function.Call(Hash.SET_TEXT_FONT, 4);
        Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
        Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, 255);
        Function.Call(Hash.SET_TEXT_DROPSHADOW, 2, 2, 0, 0, 0);
        Function.Call(Hash.SET_TEXT_OUTLINE);
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"{fuelType}: {percentage:F1}%");
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.91f, 0.965f);
    }
    
    private void DisplayHelpTextThisFrame(string text)
    {
        Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
        Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
        Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
    }
    
    private void SaveFuelData()
    {
        try
        {
            // Créer une liste de données à sauvegarder
            List<string> saveData = new List<string>();
            
            // Sauvegarder chaque entrée de niveau de carburant
            foreach (var entry in vehicleFuelLevels)
            {
                // Obtenir le type de carburant
                FuelType fuelType = FuelType.Gasoline;
                if (vehicleFuelTypeAssigned.TryGetValue(entry.Key, out FuelType type))
                {
                    fuelType = type;
                }
                
                // Format: VehicleID|FuelLevel|FuelType
                saveData.Add($"{entry.Key}|{entry.Value}|{(int)fuelType}");
            }
            
            // Écrire dans le fichier
            File.WriteAllLines(saveFilePath, saveData);
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Erreur lors de la sauvegarde: {ex.Message}", true);
        }
    }
    
    private void LoadFuelData()
    {
        try
        {
            // Vérifier si le fichier existe
            if (File.Exists(saveFilePath))
            {
                // Lire toutes les lignes
                string[] saveData = File.ReadAllLines(saveFilePath);
                
                // Traiter chaque ligne
                foreach (string line in saveData)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        // Extraire les données
                        int vehicleID = int.Parse(parts[0]);
                        float fuelLevel = float.Parse(parts[1]);
                        FuelType fuelType = (FuelType)int.Parse(parts[2]);
                        
                        // Stocker les données
                        vehicleFuelLevels[vehicleID] = fuelLevel;
                        vehicleFuelTypeAssigned[vehicleID] = fuelType;
                    }
                }
                
                Notification.PostTicker($"~g~Données de carburant chargées: {vehicleFuelLevels.Count} véhicules", true);
            }
        }
        catch (Exception ex)
        {
            Notification.PostTicker($"~r~Erreur lors du chargement: {ex.Message}", true);
        }
    }
    
    // Affiche les stations-service sur la carte
    private void CreateGasStationBlips()
    {
        // Supprimer les blips existants
        foreach (Blip blip in gasStationBlips)
        {
            if (blip != null && blip.Exists())
            {
                blip.Delete();
            }
        }
        gasStationBlips.Clear();
        
        // Créer un dictionnaire pour regrouper les pompes par station
        Dictionary<string, Vector3> stationPositions = new Dictionary<string, Vector3>();
        
        // Regrouper les pompes par nom de station
        foreach (FuelPump pump in fuelPumps)
        {
            if (!stationPositions.ContainsKey(pump.StationName))
            {
                stationPositions.Add(pump.StationName, pump.Position);
            }
        }
        
        // Créer des blips pour chaque station-service (un seul blip par station)
        foreach (var station in stationPositions)
        {
            Blip stationBlip = World.CreateBlip(station.Value);
            
            // Configurer le blip
            stationBlip.Sprite = BlipSprite.JerryCan;
            stationBlip.Color = BlipColor.Green;
            stationBlip.IsShortRange = true;
            stationBlip.Name = $"Station-service ({station.Key})";
            stationBlip.Scale = 0.8f;
            
            // Ajouter à la liste pour le suivi
            gasStationBlips.Add(stationBlip);
        }
        
        Notification.PostTicker($"~g~{stationPositions.Count} stations-service affichées sur la carte", true);
    }
    
    // Méthode dédiée pour dessiner un marqueur stable à la pompe à essence
    private void DrawGasPumpMarker(Vector3 position)
    {
        // Créer un cylindre vert solide au sol
        Function.Call(Hash.DRAW_MARKER, 
            1,                  // Type 1: flèche (plus visible)
            position.X,
            position.Y, 
            position.Z - 0.5f,  // Ajuster la hauteur pour être visible au-dessus du sol
            0.0f, 0.0f, 0.0f,   // Direction
            0.0f, 0.0f, 0.0f,   // Rotation
            2.0f, 2.0f, 1.0f,   // Échelle plus grande
            0, 255, 0, 255,     // Couleur verte complètement opaque
            false,              // Ne bouge pas
            false,              // Pas de bordure
            2,                  // Face à la caméra
            false,              // Pas de rotation
            0, 0, false);       // Pas de texture
    }
    
    // Méthode pour vérifier si le joueur a un jerrycan et mettre à jour le status
    private void CheckJerryCan()
    {
        Ped playerPed = Game.Player.Character;
        // Vérifier si le joueur a un jerrycan (de manière plus sécurisée)
        bool hadJerryCan = hasJerryCan;
        
        try
        {
            hasJerryCan = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON, playerPed.Handle, (int)WeaponHash.PetrolCan, false);
            
            // Vérifier si le jerrycan est l'arme actuellement équipée (de manière plus sécurisée)
            bool isJerryCanEquipped = false;
            if (hasJerryCan)
            {
                uint selectedWeapon = 0;
                try
                {
                    selectedWeapon = Function.Call<uint>(Hash.GET_SELECTED_PED_WEAPON, playerPed.Handle);
                    isJerryCanEquipped = (selectedWeapon == (uint)WeaponHash.PetrolCan);
                }
                catch
                {
                    // Ignorer les erreurs potentielles
                    isJerryCanEquipped = false;
                }
            }
            
            // Afficher l'UI seulement si le jerrycan est équipé
            showJerryCanUI = isJerryCanEquipped;
            
            // Si le joueur vient d'obtenir un jerrycan, initialiser sa capacité
            if (hasJerryCan && (!hadJerryCan || jerryCanFuel <= 0) && !jerryCanDropped)
            {
                jerryCanFuel = jerryCanCapacity;
                Notification.PostTicker("~b~Jerrycan trouvé! ~w~Capacité: ~g~" + jerryCanCapacity + " litres", true);
            }
        }
        catch
        {
            // En cas d'erreur, désactiver toutes les fonctionnalités liées au jerrycan pour éviter les crashs
            hasJerryCan = false;
            showJerryCanUI = false;
        }
    }
    
    // Méthode pour gérer quand le jerrycan devient vide
    private void HandleEmptyJerryCanProcedure(Ped playerPed)
    {
        if (playerPed == null || !playerPed.Exists())
            return;

        Notification.PostTicker("~y~Votre jerrycan est maintenant vide.", true);

        // Retirer l'arme jerrycan
        Function.Call(Hash.REMOVE_WEAPON_FROM_PED, playerPed.Handle, (int)WeaponHash.PetrolCan);
        Function.Call(Hash.REMOVE_WEAPON_FROM_PED, playerPed.Handle, 883325847); // Legacy hash, ensure removal
        Function.Call(Hash.REMOVE_WEAPON_FROM_PED, playerPed.Handle, 1168162263); // Legacy hash, ensure removal

        Vector3 dropPosition = playerPed.Position;

        int modelHash = Function.Call<int>(Hash.GET_HASH_KEY, "prop_jerrycan_01a");
        if (modelHash == 0)
        {
            Notification.PostTicker("~r~Erreur: Modèle du jerrycan introuvable.", true); // Log error
            // Set states anyway to prevent repeated attempts if possible
            hasJerryCan = false; 
            showJerryCanUI = false;
            isRefuelingWithJerryCan = false; // Ensure to stop any refueling state
            return;
        }

        // float groundZ; // Old declaration for Function.Call
        // // GET_GROUND_Z_FOR_3D_COORD uses 'bool getGroundZ(float x, float y, float z, out float groundZ, bool unk)'
        // // The last parameter 'unk' is usually false for scripts.
        // // Correcting the call to ensure proper syntax for GET_GROUND_Z_FOR_3D_COORD
        // bool groundFound = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, dropPosition.X, dropPosition.Y, dropPosition.Z, out groundZ, false);
        
        Vector3 posForGroundCheck = new Vector3(dropPosition.X, dropPosition.Y, dropPosition.Z);
        float groundZ = World.GetGroundHeight(posForGroundCheck);
        bool groundFound = (groundZ != 0.0f); // Basic check; World.GetGroundHeight returns 0 if not found.
                                             // A more robust check might be Math.Abs(groundZ - dropPosition.Z) < someThreshold if dropPosition.Z is expected to be near ground.
                                             // Or simply if groundZ is not some obviously invalid value if the area can have 0.0f as actual ground.
                                             // For now, groundZ != 0.0f is a common way to check if a ground height was returned.

        float spawnZ = groundFound ? groundZ + 0.2f : dropPosition.Z + 0.1f; // Spawn slightly above ground or ped Z + small offset

        // Créer l'objet jerrycan au sol
        int jerryCanProp = Function.Call<int>(Hash.CREATE_OBJECT,
            modelHash, // Use the checked modelHash
            dropPosition.X, dropPosition.Y, spawnZ,
            true, true, false); // isNetwork = true, thisScriptCheck = true (missionEntity)

        if (jerryCanProp != 0)
        {
            Function.Call(Hash.SET_ENTITY_COLLISION, jerryCanProp, true, true);
            Function.Call(Hash.SET_ENTITY_DYNAMIC, jerryCanProp, true);
            // APPLY_FORCE_TO_ENTITY was here, it has been removed to allow natural settling.
        }

        jerryCanDropped = true;
        // Update droppedJerryCanPosition to the actual spawn coordinates for accurate despawn
        droppedJerryCanPosition = new Vector3(dropPosition.X, dropPosition.Y, spawnZ); 
        jerryCanDisappearTimer = 0;

        hasJerryCan = false; // Player no longer "has" a usable jerrycan (it's empty and dropped)
        showJerryCanUI = false; // No UI if no usable jerrycan
    }
    
    // Méthode pour utiliser le jerrycan
    private void UseJerryCan()
    {
        // This method is now largely deprecated. 
        // The logic to initiate jerrycan refueling is in OnKeyDown.
        // The timed refueling logic is in OnTick.
        // The logic to handle an empty jerrycan is in HandleEmptyJerryCanProcedure.
        // This method could be removed entirely if no other part of the script calls it.
        // For now, leaving it empty as per the plan.
    }
    
    // Méthode pour gérer le jerrycan tombé au sol
    private void HandleDroppedJerryCan()
    {
        if (jerryCanDropped)
        {
            // Incrémenter le timer
            // Ensure this uses a non-zero interval if the main script Interval is 0
            jerryCanDisappearTimer += (Interval == 0 ? consumptionUpdateInterval : Interval);
            
            // Si le timer a dépassé le temps défini, faire disparaître le jerrycan
            if (jerryCanDisappearTimer >= jerryCanDisappearTime)
            {
                // Trouver et supprimer l'objet jerrycan
                int jerrycanObj = Function.Call<int>(Hash.GET_CLOSEST_OBJECT_OF_TYPE, 
                    droppedJerryCanPosition.X, droppedJerryCanPosition.Y, droppedJerryCanPosition.Z, 
                    3.0f, Function.Call<int>(Hash.GET_HASH_KEY, "prop_jerrycan_01a"), false, false, false);
                    
                if (jerrycanObj != 0)
                {
                    Function.Call(Hash.DELETE_OBJECT, jerrycanObj);
                }
                
                // Réinitialiser les variables
                jerryCanDropped = false;
                droppedJerryCanPosition = Vector3.Zero;
            }
            else if (jerryCanDisappearTimer >= jerryCanDisappearTime - 2000)
            {
                // Faire clignoter le jerrycan avant qu'il ne disparaisse
                int jerrycanObj = Function.Call<int>(Hash.GET_CLOSEST_OBJECT_OF_TYPE, 
                    droppedJerryCanPosition.X, droppedJerryCanPosition.Y, droppedJerryCanPosition.Z, 
                    3.0f, Function.Call<int>(Hash.GET_HASH_KEY, "prop_jerrycan_01a"), false, false, false);
                    
                if (jerrycanObj != 0)
                {
                    // Alterner la visibilité pour créer un effet de clignotement
                    bool visible = (jerryCanDisappearTimer / 250) % 2 == 0;
                    Function.Call(Hash.SET_ENTITY_VISIBLE, jerrycanObj, visible, false);
                }
            }
        }
    }
    
    // Méthode pour afficher l'UI du jerrycan (avec gestion des exceptions)
    private void DisplayJerryCanUI()
    {
        try
        {
            // Afficher une jauge de carburant pour le jerrycan
            float percentage = (jerryCanFuel / jerryCanCapacity) * 100;
            
            // Déterminer la couleur en fonction du niveau de carburant
            Color color;
            if (percentage > 60)
                color = Color.FromArgb(255, 50, 200, 50); // Vert
            else if (percentage > 25)
                color = Color.FromArgb(255, 240, 200, 50); // Jaune
            else
                color = Color.FromArgb(255, 220, 50, 50); // Rouge
            
            // Position et dimensions de l'interface - utiliser des valeurs fixes pour éviter les erreurs
            float posX = 0.85f; // Valeur fixe en pourcentage de l'écran (côté droit)
            float posY = 0.95f; // Valeur fixe en pourcentage de l'écran (bas)
            
            // Dessiner un simple rectangle noir semi-transparent en arrière-plan
            Function.Call(Hash.DRAW_RECT, posX, posY, 0.15f, 0.025f, 0, 0, 0, 150);
            
            // Dessiner la jauge de carburant
            float filledWidth = 0.145f * (percentage / 100);
            Function.Call(Hash.DRAW_RECT, posX - ((0.145f - filledWidth) / 2), posY, filledWidth, 0.02f, color.R, color.G, color.B, 200);
            
            // Afficher le texte
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.4f, 0.4f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 255);
            Function.Call(Hash.SET_TEXT_EDGE, 1, 0, 0, 0, 255);
            
            // Utiliser la séquence correcte pour afficher du texte
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"Jerrycan: {jerryCanFuel:F1}/{jerryCanCapacity}L");
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, posX, posY - 0.015f);
        }
        catch
        {
            // En cas d'erreur dans l'affichage, ne rien faire
            // Cela permet d'éviter que le jeu ne crash
        }
    }
}