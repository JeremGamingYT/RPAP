using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using REALIS.Common;

namespace REALIS.UrbanLife
{
    public class NoiseReactionManager
    {
        private List<NoiseSource> activeSources;
        private List<SmartNPC> registeredNPCs;
        private Random random;
        private DateTime lastCheck;
        
        // Détection automatique des bruits
        private Vector3 lastPlayerPosition;
        private float lastPlayerSpeed;
        private DateTime lastShotTime;
        private DateTime lastExplosionTime;
        
        public NoiseReactionManager()
        {
            activeSources = new List<NoiseSource>();
            registeredNPCs = new List<SmartNPC>();
            random = new Random();
            lastCheck = DateTime.Now;
        }
        
        public void RegisterNPC(SmartNPC npc)
        {
            if (!registeredNPCs.Contains(npc))
            {
                registeredNPCs.Add(npc);
            }
        }
        
        public void UnregisterNPC(SmartNPC npc)
        {
            registeredNPCs.Remove(npc);
        }
        
        public void Update()
        {
            // Nettoyer les sources de bruit expirées
            CleanupExpiredSources();
            
            // Détecter de nouveaux bruits
            DetectGameSounds();
            
            // Nettoyer les PNJ invalides
            registeredNPCs.RemoveAll(npc => !npc.IsValid());
            
            lastCheck = DateTime.Now;
        }
        
        private void CleanupExpiredSources()
        {
            var now = DateTime.Now;
            activeSources.RemoveAll(source => (now - source.CreationTime).TotalSeconds > source.Duration);
        }
        
        private void DetectGameSounds()
        {
            var player = Game.Player.Character;
            var playerPos = player.Position;
            
            // Détecter les tirs d'armes
            DetectGunshots(player, playerPos);
            
            // Détecter les explosions
            DetectExplosions(playerPos);
            
            // Détecter les klaxons de véhicules
            DetectCarHorns(playerPos);
            
            // Détecter les collisions de véhicules
            DetectCarCrashes(playerPos);
            
            // Détecter les sirènes
            DetectSirens(playerPos);
            
            // Détecter les cris (simulation)
            DetectScreams(playerPos);
            
            lastPlayerPosition = playerPos;
            lastPlayerSpeed = player.Velocity.Length();
        }
        
        private void DetectGunshots(Ped player, Vector3 playerPos)
        {
            // Vérifier si le joueur tire
            if (Game.IsControlPressed(GTA.Control.Attack) && player.IsAiming)
            {
                var now = DateTime.Now;
                if ((now - lastShotTime).TotalMilliseconds > 500) // Éviter le spam
                {
                    AddNoiseSource(NoiseType.Gunshot, playerPos, 8.0f, 100.0f);
                    lastShotTime = now;
                }
            }
            
            // Vérifier les autres PNJ qui tirent (méthode native)
            var nearbyPeds = World.GetNearbyPeds(playerPos, 200.0f);
            foreach (var ped in nearbyPeds)
            {
                if (ped != player && ped.IsAlive && ped.IsShooting)
                {
                    AddNoiseSource(NoiseType.Gunshot, ped.Position, 8.0f, 100.0f);
                }
            }
        }
        
        private void DetectExplosions(Vector3 playerPos)
        {
            // Utiliser la méthode native pour détecter les explosions récentes
            // Ceci est une approximation car il n'y a pas d'API directe
            var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(playerPos, 200.0f);
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle.IsOnFire || vehicle.IsDead)
                {
                    var now = DateTime.Now;
                    if ((now - lastExplosionTime).TotalSeconds > 5)
                    {
                        AddNoiseSource(NoiseType.Explosion, vehicle.Position, 15.0f, 300.0f);
                        lastExplosionTime = now;
                    }
                }
            }
        }
        
        private void DetectCarHorns(Vector3 playerPos)
        {
            // Détecter quand le joueur klaxonne
            if (Game.IsControlPressed(GTA.Control.VehicleHorn))
            {
                var player = Game.Player.Character;
                if (player.IsInVehicle())
                {
                    // Réduire la portée et la durée pour les klaxons
                    AddNoiseSource(NoiseType.CarHorn, playerPos, 1.5f, 25.0f);
                }
            }
            
            // Réduire drastiquement la simulation de klaxons des autres véhicules
            var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(playerPos, 50.0f);
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle.Driver != null && vehicle.Driver != Game.Player.Character)
                {
                    // Simulation très réduite : chance aléatoire qu'un véhicule klaxonne
                    if (random.NextDouble() < 0.0001f) // Très très faible probabilité
                    {
                        AddNoiseSource(NoiseType.CarHorn, vehicle.Position, 1.0f, 20.0f);
                    }
                }
            }
        }
        
        private void DetectCarCrashes(Vector3 playerPos)
        {
            var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(playerPos, 100.0f);
            foreach (var vehicle in nearbyVehicles)
            {
                if (vehicle.HasCollided && vehicle.Speed > 10.0f)
                {
                    // Véhicule en collision à haute vitesse
                    AddNoiseSource(NoiseType.CarCrash, vehicle.Position, 10.0f, 150.0f);
                }
            }
        }
        
        private void DetectSirens(Vector3 playerPos)
        {
            var nearbyVehicles = VehicleQueryService.GetNearbyVehicles(playerPos, 200.0f);
            foreach (var vehicle in nearbyVehicles)
            {
                // Vérifier si c'est un véhicule d'urgence avec sirène
                if (IsEmergencyVehicle(vehicle) && vehicle.IsSirenActive)
                {
                    AddNoiseSource(NoiseType.Siren, vehicle.Position, 12.0f, 200.0f);
                }
            }
        }
        
        private bool IsEmergencyVehicle(Vehicle vehicle)
        {
            // Vérifier si c'est un véhicule d'urgence en utilisant le hash du modèle
            var modelHash = vehicle.Model.Hash;
            
            // Liste des véhicules de police et d'urgence connus
            var emergencyVehicleHashes = new[]
            {
                VehicleHash.Police, VehicleHash.Police2, VehicleHash.Police3, VehicleHash.Police4,
                VehicleHash.PoliceT, VehicleHash.Policeb, VehicleHash.Sheriff,
                VehicleHash.Sheriff2, VehicleHash.Ambulance, VehicleHash.FireTruck,
                VehicleHash.FBI, VehicleHash.FBI2, VehicleHash.Riot,
                VehicleHash.Riot2, VehicleHash.Predator
            };
            
            return Array.Exists(emergencyVehicleHashes, hash => (int)hash == modelHash);
        }
        
        private void DetectScreams(Vector3 playerPos)
        {
            // Simulation de cris lors d'événements violents
            var nearbyPeds = World.GetNearbyPeds(playerPos, 50.0f);
            foreach (var ped in nearbyPeds)
            {
                if (ped != Game.Player.Character && ped.IsAlive)
                {
                    // Crier si blessé, en fuite, ou en panique
                    if (ped.IsInjured || ped.IsFleeing || ped.IsInCombat)
                    {
                        if (random.NextDouble() < 0.01f) // Faible chance
                        {
                            AddNoiseSource(NoiseType.Scream, ped.Position, 5.0f, 75.0f);
                        }
                    }
                }
            }
        }
        
        public void AddNoiseSource(NoiseType type, Vector3 position, float duration, float range)
        {
            // Éviter les doublons proches
            var existingSimilar = activeSources.FirstOrDefault(s => 
                s.Type == type && 
                s.Position.DistanceTo(position) < 10.0f &&
                (DateTime.Now - s.CreationTime).TotalSeconds < 2.0);
                
            if (existingSimilar == null)
            {
                var noiseSource = new NoiseSource(type, position, duration, range);
                activeSources.Add(noiseSource);
                
                // Déclencher les réactions des PNJ proches
                TriggerNPCReactions(noiseSource);
            }
        }
        
        private void TriggerNPCReactions(NoiseSource noise)
        {
            foreach (var npc in registeredNPCs.ToList())
            {
                if (!npc.IsValid()) continue;
                
                float distance = npc.Ped.Position.DistanceTo(noise.Position);
                if (distance <= noise.Range)
                {
                    // Le PNJ peut entendre ce bruit
                    // La réaction sera gérée par le SmartNPC lui-même
                }
            }
        }
        
        public List<NoiseSource> GetNoisesNear(Vector3 position, float radius)
        {
            return activeSources.Where(source => 
                source.Position.DistanceTo(position) <= radius).ToList();
        }
        
        public int GetActiveNoiseSourcesCount()
        {
            return activeSources.Count;
        }
        
        // Méthode pour créer manuellement des bruits (utile pour les événements scriptés)
        public void CreateCustomNoise(NoiseType type, Vector3 position, float duration = 5.0f, float range = 100.0f)
        {
            AddNoiseSource(type, position, duration, range);
        }
        
        // Méthode pour simuler des témoins qui appellent la police
        public void SimulatePoliceCall(Vector3 incidentLocation, NoiseType incidentType)
        {
            // Les types de bruits graves déclenchent plus facilement des appels police
            float callProbability = 0.0f;
            
            switch (incidentType)
            {
                case NoiseType.Gunshot:
                    callProbability = 0.8f;
                    break;
                case NoiseType.Explosion:
                    callProbability = 0.95f;
                    break;
                case NoiseType.Scream:
                    callProbability = 0.4f;
                    break;
                case NoiseType.CarCrash:
                    callProbability = 0.6f;
                    break;
                default:
                    callProbability = 0.1f;
                    break;
            }
            
            if (random.NextDouble() < callProbability)
            {
                // Simuler l'arrivée de la police avec un délai
                Function.Call(Hash.SET_CREATE_RANDOM_COPS, true);
                
                // Créer un waypoint pour la police vers le lieu de l'incident
                Function.Call(Hash.SET_NEW_WAYPOINT, incidentLocation.X, incidentLocation.Y);
            }
        }
    }
    
    public class NoiseSource
    {
        public NoiseType Type { get; }
        public Vector3 Position { get; }
        public float Duration { get; }
        public float Range { get; }
        public DateTime CreationTime { get; }
        public float Intensity { get; }
        
        public NoiseSource(NoiseType type, Vector3 position, float duration, float range, float intensity = 1.0f)
        {
            Type = type;
            Position = position;
            Duration = duration;
            Range = range;
            Intensity = intensity;
            CreationTime = DateTime.Now;
        }
        
        public bool IsExpired()
        {
            return (DateTime.Now - CreationTime).TotalSeconds > Duration;
        }
        
        public float GetIntensityAtDistance(float distance)
        {
            if (distance >= Range) return 0.0f;
            
            // L'intensité diminue avec la distance
            float distanceRatio = distance / Range;
            return Intensity * (1.0f - distanceRatio);
        }
    }
    
    public enum NoiseType
    {
        Gunshot,     // Tir d'arme
        Explosion,   // Explosion
        Scream,      // Cri de personne
        CarHorn,     // Klaxon de voiture
        CarCrash,    // Collision de véhicule
        Siren,       // Sirène d'urgence
        Construction, // Bruit de chantier
        Music,       // Musique forte
        Argument     // Dispute bruyante
    }
} 