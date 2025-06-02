using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Threading.Tasks;

namespace REALIS.UrbanLife
{
    public class SmartNPC
    {
        public Ped Ped { get; private set; }
        public NPCRoutine? CurrentRoutine { get; private set; }
        public NPCPersonality Personality { get; private set; }
        public NPCState State { get; set; } = NPCState.Normal;
        
        private NPCRoutineManager routineManager;
        private NoiseReactionManager noiseManager;
        private Random random;
        private DateTime lastRoutineCheck;
        private DateTime lastNoiseReactionCheck;
        private DateTime creationTime;
        
        // Événements spéciaux
        public SpecialEventType? CurrentSpecialEvent { get; private set; }
        public Ped? SpecialEventTarget { get; private set; }
        public DateTime SpecialEventStartTime { get; private set; }
        
        // État de réaction aux bruits
        public bool IsReactingToNoise { get; private set; }
        public NoiseType LastHeardNoise { get; private set; }
        public Vector3 LastNoisePosition { get; private set; }
        public DateTime LastNoiseTime { get; private set; }
        
        public SmartNPC(Ped ped, NPCRoutineManager routineManager, NoiseReactionManager noiseManager)
        {
            this.Ped = ped ?? throw new ArgumentNullException(nameof(ped));
            this.routineManager = routineManager;
            this.noiseManager = noiseManager;
            this.random = new Random();
            this.lastRoutineCheck = DateTime.Now;
            this.lastNoiseReactionCheck = DateTime.Now;
            this.creationTime = DateTime.Now;
            
            // Générer une personnalité aléatoire
            GeneratePersonality();
            
            // Assigner une routine initiale
            AssignRandomRoutine();
            
            // S'enregistrer pour les réactions aux bruits
            noiseManager.RegisterNPC(this);
        }
        
        private void GeneratePersonality()
        {
            var personalities = Enum.GetValues(typeof(NPCPersonality));
            Personality = (NPCPersonality)personalities.GetValue(random.Next(personalities.Length));
        }
        
        private void AssignRandomRoutine()
        {
            // Déterminer le type de routine basé sur l'apparence du PNJ
            var routineType = DetermineRoutineType();
            CurrentRoutine = routineManager.CreateRoutine(routineType, Ped);
        }
        
        private RoutineType DetermineRoutineType()
        {
            PedHash pedHash = (PedHash)Ped.Model.Hash;
            
            // Routines basées sur l'apparence
            if (IsBusinessPerson(pedHash))
                return RoutineType.Business;
            else if (IsTourist(pedHash))
                return RoutineType.Tourist;
            else if (IsWorker(pedHash))
                return RoutineType.Worker;
            else if (IsStudent(pedHash))
                return RoutineType.Student;
            
            return RoutineType.Civilian;
        }
        
        private bool IsBusinessPerson(PedHash pedHash)
        {
            var businessPeds = new[]
            {
                PedHash.Business01AFY, PedHash.Business01AMM, PedHash.Business01AMY,
                PedHash.Business02AFM, PedHash.Business02AFY, PedHash.Business02AMY,
                PedHash.Business03AFY, PedHash.Business03AMY, PedHash.Business04AFY
            };
            return Array.Exists(businessPeds, p => p == pedHash);
        }
        
        private bool IsTourist(PedHash pedHash)
        {
            var touristPeds = new[]
            {
                PedHash.Tourist01AFM, PedHash.Tourist01AFY, PedHash.Tourist01AMM,
                PedHash.Beach01AFY, PedHash.Beach01AMY, PedHash.Hiker01AFY
            };
            return Array.Exists(touristPeds, p => p == pedHash);
        }
        
        private bool IsWorker(PedHash pedHash)
        {
            var workerPeds = new[]
            {
                PedHash.Construct01SMY, PedHash.Construct02SMY, PedHash.Gardener01SMM,
                PedHash.Postal01SMM, PedHash.Postal02SMM, PedHash.Armoured01SMM
            };
            return Array.Exists(workerPeds, p => p == pedHash);
        }
        
        private bool IsStudent(PedHash pedHash)
        {
            var studentPeds = new[]
            {
                PedHash.Hipster01AFY, PedHash.Hipster01AMY, PedHash.Skater01AFY,
                PedHash.Skater01AMM, PedHash.Skater01AMY
            };
            return Array.Exists(studentPeds, p => p == pedHash);
        }
        
        public void Update()
        {
            if (!IsValid()) return;
            
            // Vérifier les réactions aux bruits plus fréquemment
            if ((DateTime.Now - lastNoiseReactionCheck).TotalMilliseconds > 200)
            {
                CheckNoiseReactions();
                lastNoiseReactionCheck = DateTime.Now;
            }
            
            // Vérifier les routines moins fréquemment
            if ((DateTime.Now - lastRoutineCheck).TotalSeconds > 5)
            {
                UpdateRoutine();
                lastRoutineCheck = DateTime.Now;
            }
            
            // Mettre à jour l'état spécial si nécessaire
            if (CurrentSpecialEvent.HasValue)
            {
                UpdateSpecialEvent();
            }
        }
        
        private void CheckNoiseReactions()
        {
            var nearbyNoiseSources = noiseManager.GetNoisesNear(Ped.Position, 50.0f);
            
            foreach (var noise in nearbyNoiseSources)
            {
                if (ShouldReactToNoise(noise))
                {
                    ReactToNoise(noise);
                    break; // Une réaction à la fois
                }
            }
            
            // Réinitialiser automatiquement l'état de réaction après un certain temps
            if (IsReactingToNoise && (DateTime.Now - LastNoiseTime).TotalSeconds > 10)
            {
                IsReactingToNoise = false;
                State = NPCState.Normal;
            }
        }
        
        private bool ShouldReactToNoise(NoiseSource noise)
        {
            // Ne pas réagir si déjà en train de réagir
            if (IsReactingToNoise) return false;
            
            // Vérifier si le joueur s'approche simplement sans faire de bruit violent
            var player = Game.Player.Character;
            float distanceToPlayer = Ped.Position.DistanceTo(player.Position);
            
            // Si le joueur est très proche et que c'est juste un klaxon ou un bruit mineur,
            // réduire drastiquement les chances de réaction
            if (distanceToPlayer < 15.0f && 
                (noise.Type == NoiseType.CarHorn || noise.Type == NoiseType.Siren))
            {
                // Très faible chance de réaction si le joueur est proche
                if (random.NextDouble() > 0.02f) return false;
            }
            
            // Distance et type de bruit
            float distance = Ped.Position.DistanceTo(noise.Position);
            float reactionChance = GetNoiseReactionChance(noise.Type, distance);
            
            return random.NextDouble() < reactionChance;
        }
        
        private float GetNoiseReactionChance(NoiseType noiseType, float distance)
        {
            float baseChance = 0.5f;
            
            // Ajuster selon le type de bruit
            switch (noiseType)
            {
                case NoiseType.Gunshot:
                    baseChance = 0.9f;
                    break;
                case NoiseType.Explosion:
                    baseChance = 0.95f;
                    break;
                case NoiseType.Scream:
                    baseChance = 0.7f;
                    break;
                case NoiseType.CarHorn:
                    // Réduction drastique des réactions aux klaxons
                    baseChance = 0.05f; // Très faible réaction
                    break;
                case NoiseType.CarCrash:
                    baseChance = 0.8f;
                    break;
                case NoiseType.Siren:
                    baseChance = 0.4f; // Réduction aussi
                    break;
            }
            
            // Ajuster selon la personnalité
            switch (Personality)
            {
                case NPCPersonality.Curious:
                    baseChance += 0.1f; // Moins curieux pour les petits bruits
                    break;
                case NPCPersonality.Fearful:
                    if (noiseType == NoiseType.CarHorn || noiseType == NoiseType.Siren)
                        baseChance -= 0.1f; // Moins peureux pour les bruits normaux
                    else
                        baseChance += 0.3f;
                    break;
                case NPCPersonality.Brave:
                    baseChance += 0.1f;
                    break;
                case NPCPersonality.Apathetic:
                    baseChance -= 0.3f;
                    break;
            }
            
            // Réduire avec la distance
            float distanceModifier = Math.Max(0.1f, 1.0f - (distance / 30.0f)); // Portée réduite
            
            return Math.Max(0.0f, Math.Min(1.0f, baseChance * distanceModifier));
        }
        
        private void ReactToNoise(NoiseSource noise)
        {
            IsReactingToNoise = true;
            LastHeardNoise = noise.Type;
            LastNoisePosition = noise.Position;
            LastNoiseTime = DateTime.Now;
            
            // Réaction selon la personnalité et le type de bruit
            switch (noise.Type)
            {
                case NoiseType.Gunshot:
                case NoiseType.Explosion:
                    ReactToViolentNoise(noise);
                    break;
                    
                case NoiseType.Scream:
                    ReactToScream(noise);
                    break;
                    
                case NoiseType.CarHorn:
                    ReactToHorn();
                    break;
                    
                case NoiseType.CarCrash:
                    ReactToCrash(noise);
                    break;
                    
                case NoiseType.Siren:
                    ReactToSiren();
                    break;
            }
        }
        
        private void ReactToViolentNoise(NoiseSource noise)
        {
            switch (Personality)
            {
                case NPCPersonality.Fearful:
                case NPCPersonality.Normal:
                    // Fuir en panique
                    Function.Call(Hash.TASK_SMART_FLEE_PED, Ped, Game.Player.Character, 100.0f, 5000);
                    State = NPCState.Panicking;
                    break;
                    
                case NPCPersonality.Curious:
                    // Se cacher et regarder
                    Ped.Task.StandStill(5000);
                    Ped.Task.LookAt(noise.Position, 10000);
                    State = NPCState.Observing;
                    break;
                    
                case NPCPersonality.Brave:
                    // S'approcher prudemment
                    Ped.Task.FollowNavMeshTo(noise.Position.Around(10.0f));
                    State = NPCState.Investigating;
                    break;
                    
                case NPCPersonality.Apathetic:
                    // Ignorer largement
                    break;
            }
            
            // Chance d'appeler la police
            if (random.NextDouble() < 0.3f && noise.Type == NoiseType.Gunshot)
            {
                CallPolice();
            }
        }
        
        private void ReactToScream(NoiseSource noise)
        {
            if (Personality == NPCPersonality.Brave || Personality == NPCPersonality.Curious)
            {
                // Aller voir ce qui se passe
                Ped.Task.RunTo(noise.Position.Around(5.0f));
                State = NPCState.Investigating;
            }
            else
            {
                // S'éloigner
                Function.Call(Hash.TASK_SMART_FLEE_PED, Ped, Game.Player.Character, 100.0f, 5000);
                State = NPCState.Nervous;
            }
        }
        
        private void ReactToHorn()
        {
            // Ne pas réagir si le PNJ est dans un véhicule
            if (Ped.IsInVehicle())
            {
                // Simple regard très rapide dans la direction du klaxon, sans sortir du véhicule
                Ped.Task.LookAt(LastNoisePosition, 1000);
                return;
            }
            
            // Si à pied, simple regard rapide
            Ped.Task.LookAt(LastNoisePosition, 1500);
            
            // Réinitialiser l'état de réaction rapidement pour les klaxons
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                IsReactingToNoise = false;
            });
        }
        
        private void ReactToCrash(NoiseSource noise)
        {
            // La plupart des gens vont voir un accident
            if (random.NextDouble() < 0.7f)
            {
                Ped.Task.RunTo(noise.Position.Around(8.0f));
                State = NPCState.Observing;
            }
        }
        
        private void ReactToSiren()
        {
            // S'écarter du chemin
            Ped.Task.StandStill(3000);
        }
        
        private void CallPolice()
        {
            // Animation de téléphone
            Function.Call(Hash.TASK_PLAY_ANIM, Ped, "cellphone@", "cellphone_call_listen_base", 8.0f, 5000, false, 0.0f);
            
            // Après un délai, la police arrive (simulation)
            Function.Call(Hash.SET_CREATE_RANDOM_COPS, true);
        }
        
        private void UpdateRoutine()
        {
            // Ne pas changer de routine si en réaction à un bruit ou événement spécial
            if (IsReactingToNoise || CurrentSpecialEvent.HasValue) return;
            
            // Ne pas interrompre les PNJ dans leurs véhicules sauf cas particulier
            if (Ped.IsInVehicle() && Ped.CurrentVehicle?.Driver == Ped)
            {
                // Laisser les conducteurs tranquilles dans la plupart des cas
                if (random.NextDouble() < 0.95f) return;
            }
            
            CurrentRoutine?.Update();
            
            // Possibilité réduite de changer de routine
            if (CurrentRoutine?.IsCompleted == true || random.NextDouble() < 0.03f) // Réduit de 0.1 à 0.03
            {
                AssignRandomRoutine();
            }
        }
        
        private void UpdateSpecialEvent()
        {
            var timeSinceEvent = DateTime.Now - SpecialEventStartTime;
            
            // Les événements spéciaux durent maximum 5 minutes
            if (timeSinceEvent.TotalMinutes > 5)
            {
                ClearSpecialEvent();
                return;
            }
            
            if (CurrentSpecialEvent.HasValue)
            {
                switch (CurrentSpecialEvent.Value)
                {
                    case SpecialEventType.Robbery:
                        UpdateRobberyEvent();
                        break;
                    case SpecialEventType.Accident:
                        UpdateAccidentEvent();
                        break;
                    case SpecialEventType.Fight:
                        UpdateFightEvent();
                        break;
                }
            }
        }
        
        private void UpdateRobberyEvent()
        {
            // La victime reste figée de peur
            if (State != NPCState.Panicking)
            {
                State = NPCState.Panicking;
                Ped.Task.HandsUp(10000);
            }
        }
        
        private void UpdateAccidentEvent()
        {
            // Rester au sol
            if (!Ped.IsRagdoll)
            {
                Function.Call(Hash.TASK_PLAY_ANIM, Ped, "dam_ko", "ko_shot_front", 1.0f, -1, false, 0.0f);
            }
        }
        
        private void UpdateFightEvent()
        {
            // Continuer à se battre si l'adversaire est toujours là
            if (SpecialEventTarget?.IsAlive == true)
            {
                Ped.Task.Combat(SpecialEventTarget);
            }
            else
            {
                ClearSpecialEvent();
            }
        }
        
        public void SetSpecialEvent(SpecialEventType eventType, Ped? target)
        {
            CurrentSpecialEvent = eventType;
            SpecialEventTarget = target;
            SpecialEventStartTime = DateTime.Now;
        }
        
        public void ClearSpecialEvent()
        {
            CurrentSpecialEvent = null;
            SpecialEventTarget = null;
            State = NPCState.Normal;
            IsReactingToNoise = false;
        }
        
        public bool CanTriggerSpecialEvent()
        {
            // Conditions pour déclencher un événement spécial
            if (CurrentSpecialEvent.HasValue) return false;
            if (IsReactingToNoise) return false;
            if ((DateTime.Now - creationTime).TotalMinutes < 2) return false; // Attendre 2 minutes avant le premier événement
            
            // Faible probabilité
            return random.NextDouble() < 0.001f; // 0.1% de chance par update
        }
        
        public bool IsValid()
        {
            return Ped != null && Ped.Exists() && Ped.IsAlive && !Ped.IsDead;
        }
    }
    
    public enum NPCPersonality
    {
        Normal,      // Réactions standards
        Fearful,     // Fuit rapidement
        Curious,     // S'approche des événements
        Brave,       // Intervient dans les situations
        Apathetic    // Ignore la plupart des événements
    }
    
    public enum NPCState
    {
        Normal,         // État normal
        Observing,      // Observe quelque chose
        Panicking,      // En panique
        Nervous,        // Nerveux
        Investigating,  // Enquête sur quelque chose
        Helping         // Aide quelqu'un
    }
} 