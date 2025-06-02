using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.UrbanLife
{
    public class NPCRoutineManager
    {
        private List<NPCRoutine> activeRoutines;
        private Random random;
        private LocationDatabase locationDatabase;
        
        public NPCRoutineManager()
        {
            activeRoutines = new List<NPCRoutine>();
            random = new Random();
            locationDatabase = new LocationDatabase();
        }
        
        public NPCRoutine CreateRoutine(RoutineType routineType, Ped ped)
        {
            NPCRoutine routine;
            
            switch (routineType)
            {
                case RoutineType.Business:
                    routine = new BusinessRoutine(ped, locationDatabase, random);
                    break;
                case RoutineType.Tourist:
                    routine = new TouristRoutine(ped, locationDatabase, random);
                    break;
                case RoutineType.Worker:
                    routine = new WorkerRoutine(ped, locationDatabase, random);
                    break;
                case RoutineType.Student:
                    routine = new StudentRoutine(ped, locationDatabase, random);
                    break;
                case RoutineType.Civilian:
                default:
                    routine = new CivilianRoutine(ped, locationDatabase, random);
                    break;
            }
            
            activeRoutines.Add(routine);
            return routine;
        }
        
        public void Update()
        {
            // Nettoyer les routines terminées ou invalides
            activeRoutines.RemoveAll(r => !r.IsValid() || r.IsCompleted);
            
            // Mettre à jour toutes les routines actives
            foreach (var routine in activeRoutines.ToList())
            {
                routine.Update();
            }
        }
        
        public int GetActiveRoutinesCount()
        {
            return activeRoutines.Count;
        }
        
        public void RemoveRoutine(NPCRoutine routine)
        {
            activeRoutines.Remove(routine);
        }
    }
    
    public abstract class NPCRoutine
    {
        protected Ped ped;
        protected LocationDatabase locationDatabase;
        protected Random random;
        protected List<RoutineStep> steps;
        protected int currentStepIndex;
        protected DateTime startTime;
        protected DateTime lastUpdate;
        
        public bool IsCompleted { get; protected set; }
        public RoutineType Type { get; protected set; }
        
        protected NPCRoutine(Ped ped, LocationDatabase locationDatabase, Random random)
        {
            this.ped = ped ?? throw new ArgumentNullException(nameof(ped));
            this.locationDatabase = locationDatabase;
            this.random = random;
            this.steps = new List<RoutineStep>();
            this.currentStepIndex = 0;
            this.startTime = DateTime.Now;
            this.lastUpdate = DateTime.Now;
        }
        
        public virtual void Update()
        {
            if (!IsValid() || IsCompleted) return;
            
            // Ne pas mettre à jour trop fréquemment
            if ((DateTime.Now - lastUpdate).TotalSeconds < 2) return;
            lastUpdate = DateTime.Now;
            
            // Vérifier si l'étape actuelle est terminée
            if (currentStepIndex < steps.Count)
            {
                var currentStep = steps[currentStepIndex];
                
                if (currentStep.IsCompleted(ped))
                {
                    currentStepIndex++;
                    
                    // Si ce n'était pas la dernière étape, commencer la suivante
                    if (currentStepIndex < steps.Count)
                    {
                        steps[currentStepIndex].Execute(ped);
                    }
                    else
                    {
                        IsCompleted = true;
                    }
                }
                else
                {
                    // Continuer l'étape actuelle si nécessaire
                    currentStep.ContinueExecution(ped);
                }
            }
        }
        
        protected abstract void GenerateSteps();
        
        public bool IsValid()
        {
            return ped?.Exists() == true && ped.IsAlive && !ped.IsDead;
        }
        
        protected void StartRoutine()
        {
            GenerateSteps();
            if (steps.Count > 0)
            {
                steps[0].Execute(ped);
            }
        }
    }
    
    public class BusinessRoutine : NPCRoutine
    {
        public BusinessRoutine(Ped ped, LocationDatabase locationDatabase, Random random) 
            : base(ped, locationDatabase, random)
        {
            Type = RoutineType.Business;
            StartRoutine();
        }
        
        protected override void GenerateSteps()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            
            // Routine matinale (6h-12h)
            if (currentTime.Hours >= 6 && currentTime.Hours < 12)
            {
                // Aller au bureau
                var office = locationDatabase.GetRandomLocation(LocationType.Office);
                steps.Add(new GoToLocationStep(office.Position, "Se rendre au bureau"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(10, 30)), "Travailler"));
                
                // Parfois aller chercher un café
                if (random.NextDouble() < 0.4)
                {
                    var cafe = locationDatabase.GetRandomLocation(LocationType.Cafe);
                    steps.Add(new GoToLocationStep(cafe.Position, "Acheter un café"));
                    steps.Add(new WaitStep(TimeSpan.FromMinutes(5), "Boire le café"));
                    steps.Add(new GoToLocationStep(office.Position, "Retourner au bureau"));
                }
            }
            // Pause déjeuner (12h-14h)
            else if (currentTime.Hours >= 12 && currentTime.Hours < 14)
            {
                var restaurant = locationDatabase.GetRandomLocation(LocationType.Restaurant);
                steps.Add(new GoToLocationStep(restaurant.Position, "Aller déjeuner"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(20, 45)), "Déjeuner"));
            }
            // Après-midi (14h-18h)
            else if (currentTime.Hours >= 14 && currentTime.Hours < 18)
            {
                var office = locationDatabase.GetRandomLocation(LocationType.Office);
                steps.Add(new GoToLocationStep(office.Position, "Retourner au bureau"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(30, 60)), "Travailler"));
            }
            // Soirée (18h+)
            else
            {
                // Rentrer à la maison ou aller dans un bar
                if (random.NextDouble() < 0.7)
                {
                    var home = locationDatabase.GetRandomLocation(LocationType.Residential);
                    steps.Add(new GoToLocationStep(home.Position, "Rentrer à la maison"));
                }
                else
                {
                    var bar = locationDatabase.GetRandomLocation(LocationType.Bar);
                    steps.Add(new GoToLocationStep(bar.Position, "Aller prendre un verre"));
                    steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(30, 90)), "Boire un verre"));
                }
            }
        }
    }
    
    public class TouristRoutine : NPCRoutine
    {
        public TouristRoutine(Ped ped, LocationDatabase locationDatabase, Random random) 
            : base(ped, locationDatabase, random)
        {
            Type = RoutineType.Tourist;
            StartRoutine();
        }
        
        protected override void GenerateSteps()
        {
            // Les touristes visitent des lieux d'intérêt
            var landmark = locationDatabase.GetRandomLocation(LocationType.Landmark);
            steps.Add(new GoToLocationStep(landmark.Position, "Visiter un lieu touristique"));
            steps.Add(new TakePhotoStep("Prendre des photos"));
            steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(10, 20)), "Observer le paysage"));
            
            // Parfois aller au restaurant
            if (random.NextDouble() < 0.6)
            {
                var restaurant = locationDatabase.GetRandomLocation(LocationType.Restaurant);
                steps.Add(new GoToLocationStep(restaurant.Position, "Aller au restaurant"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(20, 40)), "Manger"));
            }
            
            // Shopping
            if (random.NextDouble() < 0.5)
            {
                var shop = locationDatabase.GetRandomLocation(LocationType.Shop);
                steps.Add(new GoToLocationStep(shop.Position, "Faire du shopping"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(15, 30)), "Acheter des souvenirs"));
            }
        }
    }
    
    public class WorkerRoutine : NPCRoutine
    {
        public WorkerRoutine(Ped ped, LocationDatabase locationDatabase, Random random) 
            : base(ped, locationDatabase, random)
        {
            Type = RoutineType.Worker;
            StartRoutine();
        }
        
        protected override void GenerateSteps()
        {
            // Les travailleurs vont sur des chantiers ou lieux de travail spécifiques
            var worksite = locationDatabase.GetRandomLocation(LocationType.Construction);
            steps.Add(new GoToLocationStep(worksite.Position, "Aller au travail"));
            steps.Add(new WorkStep(TimeSpan.FromMinutes(random.Next(45, 90)), "Travailler"));
            
            // Pause
            if (random.NextDouble() < 0.8)
            {
                var nearbyStore = locationDatabase.GetRandomLocation(LocationType.ConvenienceStore);
                steps.Add(new GoToLocationStep(nearbyStore.Position, "Faire une pause"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(15), "Pause"));
                steps.Add(new GoToLocationStep(worksite.Position, "Retourner au travail"));
            }
        }
    }
    
    public class StudentRoutine : NPCRoutine
    {
        public StudentRoutine(Ped ped, LocationDatabase locationDatabase, Random random) 
            : base(ped, locationDatabase, random)
        {
            Type = RoutineType.Student;
            StartRoutine();
        }
        
        protected override void GenerateSteps()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            
            // Heures de cours (8h-17h)
            if (currentTime.Hours >= 8 && currentTime.Hours < 17)
            {
                var school = locationDatabase.GetRandomLocation(LocationType.School);
                steps.Add(new GoToLocationStep(school.Position, "Aller en cours"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(30, 90)), "Suivre les cours"));
                
                // Pause déjeuner
                if (currentTime.Hours >= 12 && currentTime.Hours < 14)
                {
                    var fastfood = locationDatabase.GetRandomLocation(LocationType.FastFood);
                    steps.Add(new GoToLocationStep(fastfood.Position, "Aller manger"));
                    steps.Add(new WaitStep(TimeSpan.FromMinutes(20), "Déjeuner"));
                }
            }
            // Soirée
            else
            {
                // Aller au parc, à la bibliothèque ou traîner
                var activities = new[]
                {
                    LocationType.Park,
                    LocationType.Mall,
                    LocationType.Cafe
                };
                
                var location = locationDatabase.GetRandomLocation(activities[random.Next(activities.Length)]);
                steps.Add(new GoToLocationStep(location.Position, "Activité de loisir"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(30, 60)), "Se détendre"));
            }
        }
    }
    
    public class CivilianRoutine : NPCRoutine
    {
        public CivilianRoutine(Ped ped, LocationDatabase locationDatabase, Random random) 
            : base(ped, locationDatabase, random)
        {
            Type = RoutineType.Civilian;
            StartRoutine();
        }
        
        protected override void GenerateSteps()
        {
            // Routine plus aléatoire pour les civils
            var activities = new[]
            {
                LocationType.Park,
                LocationType.Shop,
                LocationType.Cafe,
                LocationType.Mall,
                LocationType.Beach
            };
            
            var location1 = locationDatabase.GetRandomLocation(activities[random.Next(activities.Length)]);
            steps.Add(new GoToLocationStep(location1.Position, "Première activité"));
            steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(15, 45)), "Première activité"));
            
            // Deuxième activité parfois
            if (random.NextDouble() < 0.6)
            {
                var location2 = locationDatabase.GetRandomLocation(activities[random.Next(activities.Length)]);
                steps.Add(new GoToLocationStep(location2.Position, "Deuxième activité"));
                steps.Add(new WaitStep(TimeSpan.FromMinutes(random.Next(10, 30)), "Deuxième activité"));
            }
        }
    }
    
    public enum RoutineType
    {
        Business,   // Homme/femme d'affaires
        Tourist,    // Touriste
        Worker,     // Ouvrier/travailleur
        Student,    // Étudiant
        Civilian    // Civil normal
    }
} 