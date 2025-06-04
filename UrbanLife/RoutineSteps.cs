using GTA;
using GTA.Native;
using GTA.Math;
using System;
using REALIS.Common;

namespace REALIS.UrbanLife
{
    public abstract class RoutineStep
    {
        protected DateTime startTime;
        protected bool isStarted;
        public string Description { get; protected set; }
        
        protected RoutineStep(string description)
        {
            Description = description;
            isStarted = false;
        }
        
        public virtual void Execute(Ped ped)
        {
            if (!isStarted)
            {
                startTime = DateTime.Now;
                isStarted = true;
                StartStep(ped);
            }
        }
        
        protected abstract void StartStep(Ped ped);
        public abstract bool IsCompleted(Ped ped);
        public virtual void ContinueExecution(Ped ped) { }
    }
    
    public class GoToLocationStep : RoutineStep
    {
        private Vector3 targetPosition;
        private float arrivalThreshold;
        
        public GoToLocationStep(Vector3 position, string description, float threshold = 5.0f) 
            : base(description)
        {
            targetPosition = position;
            arrivalThreshold = threshold;
        }
        
        protected override void StartStep(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                if (VehicleQueryService.TryAcquireControl(ped.CurrentVehicle))
                {
                    ped.Task.DriveTo(ped.CurrentVehicle, targetPosition, 5.0f, VehicleDrivingFlags.StopForVehicles, 25.0f);
                    VehicleQueryService.ReleaseControl(ped.CurrentVehicle);
                }
            }
            else
            {
                ped.Task.FollowNavMeshTo(targetPosition);
            }
        }
        
        public override bool IsCompleted(Ped ped)
        {
            return ped.Position.DistanceTo(targetPosition) <= arrivalThreshold;
        }
        
        public override void ContinueExecution(Ped ped)
        {
            // Vérifier si le PNJ est bloqué
            if (isStarted && (DateTime.Now - startTime).TotalSeconds > 30)
            {
                // Réessayer le déplacement
                if (ped.IsInVehicle())
                {
                    if (VehicleQueryService.TryAcquireControl(ped.CurrentVehicle))
                    {
                        ped.Task.DriveTo(ped.CurrentVehicle, targetPosition, 5.0f, VehicleDrivingFlags.StopForVehicles, 25.0f);
                        VehicleQueryService.ReleaseControl(ped.CurrentVehicle);
                    }
                }
                else
                {
                    ped.Task.FollowNavMeshTo(targetPosition);
                }
                startTime = DateTime.Now; // Reset timer
            }
        }
    }
    
    public class WaitStep : RoutineStep
    {
        private TimeSpan duration;
        
        public WaitStep(TimeSpan waitDuration, string description) 
            : base(description)
        {
            duration = waitDuration;
        }
        
        protected override void StartStep(Ped ped)
        {
            // Animation d'attente aléatoire
            var animations = new[]
            {
                ("amb@world_human_stand_mobile", "base"),
                ("amb@world_human_smoking", "base"),
                ("amb@world_human_leaning", "base"),
                ("amb@lo_res_idles@", "world_human_lean_male_foot_up_lo_res_base")
            };
            
            var random = new Random();
            var anim = animations[random.Next(animations.Length)];
            
            try
            {
                Function.Call(Hash.TASK_PLAY_ANIM, ped, anim.Item1, anim.Item2, 1.0f, (int)duration.TotalMilliseconds, true, 0.0f);
            }
            catch
            {
                // Si l'animation échoue, juste rester immobile
                ped.Task.StandStill((int)duration.TotalMilliseconds);
            }
        }
        
        public override bool IsCompleted(Ped ped)
        {
            return isStarted && (DateTime.Now - startTime) >= duration;
        }
    }
    
    public class WorkStep : RoutineStep
    {
        private TimeSpan duration;
        private DateTime lastAnimationChange;
        
        public WorkStep(TimeSpan duration, string description) : base(description)
        {
            this.duration = duration;
        }
        
        protected override void StartStep(Ped ped)
        {
            StartWorkAnimation(ped);
            lastAnimationChange = DateTime.Now;
        }
        
        private void StartWorkAnimation(Ped ped)
        {
            var random = new Random();
            var workAnimations = new[]
            {
                ("amb@world_human_welding", "base"),
                ("amb@world_human_hammering", "base"),
                ("amb@world_human_const_drill", "base"),
                ("amb@world_human_gardener_plant", "base"),
                ("amb@world_human_janitor", "base")
            };
            
            var selectedAnimation = workAnimations[random.Next(workAnimations.Length)];
            try
            {
                Function.Call(Hash.TASK_PLAY_ANIM, ped, selectedAnimation.Item1, selectedAnimation.Item2, 1.0f, 10000, true, 0.0f);
            }
            catch
            {
                ped.Task.StandStill(10000);
            }
        }
        
        public override bool IsCompleted(Ped ped)
        {
            return isStarted && (DateTime.Now - startTime) >= duration;
        }
        
        public override void ContinueExecution(Ped ped)
        {
            // Changer d'animation de travail périodiquement pour la variété
            if ((DateTime.Now - lastAnimationChange).TotalMinutes > 2)
            {
                StartWorkAnimation(ped);
                lastAnimationChange = DateTime.Now;
            }
        }
    }
    
    public class TakePhotoStep : RoutineStep
    {
        private bool photoTaken;
        private int photoCount;
        private DateTime lastPhotoTime;
        
        public TakePhotoStep(string description) : base(description)
        {
        }
        
        protected override void StartStep(Ped ped)
        {
            TakePhoto(ped);
            photoTaken = true;
            lastPhotoTime = DateTime.Now;
        }
        
        private void TakePhoto(Ped ped)
        {
            // Animation de prise de photo avec téléphone
            try
            {
                Function.Call(Hash.TASK_PLAY_ANIM, ped, "cellphone@", "cellphone_photo_idle", 1.0f, 3000, false, 0.0f);
            }
            catch
            {
                ped.Task.StandStill(3000);
            }
            photoCount++;
        }
        
        public override bool IsCompleted(Ped ped)
        {
            if (!isStarted) return false;
            
            // Prendre plusieurs photos avec des pauses
            if (photoTaken && (DateTime.Now - lastPhotoTime).TotalSeconds > 3)
            {
                if (photoCount < 3) // Prendre jusqu'à 3 photos
                {
                    photoTaken = false; // Prendre une autre photo
                    return false;
                }
            }
            
            return photoCount >= 3;
        }
    }
    
    public class InteractStep : RoutineStep
    {
        private Vector3 interactionPosition;
        private TimeSpan interactionDuration;
        
        public InteractStep(Vector3 position, TimeSpan duration, string description) 
            : base(description)
        {
            interactionPosition = position;
            interactionDuration = duration;
        }
        
        protected override void StartStep(Ped ped)
        {
            // Se diriger vers le point d'interaction
            ped.Task.FollowNavMeshTo(interactionPosition);
        }
        
        public override bool IsCompleted(Ped ped)
        {
            return isStarted && (DateTime.Now - startTime) >= interactionDuration;
        }
        
        public override void ContinueExecution(Ped ped)
        {
            // Si arrivé au point d'interaction, commencer l'animation
            if (ped.Position.DistanceTo(interactionPosition) <= 2.0f && 
                (DateTime.Now - startTime).TotalSeconds > 2)
            {
                // Animation d'interaction (regarder autour, pointer, etc.)
                try
                {
                    Function.Call(Hash.TASK_PLAY_ANIM, ped, "gestures@m@standing@casual", "gesture_point", 1.0f, 3000, false, 0.0f);
                }
                catch
                {
                    ped.Task.StandStill(3000);
                }
            }
        }
    }
    
    public class EnterVehicleStep : RoutineStep
    {
        private Vehicle targetVehicle;
        private VehicleSeat seat;
        
        public EnterVehicleStep(Vehicle vehicle, VehicleSeat seat, string description) 
            : base(description)
        {
            this.targetVehicle = vehicle;
            this.seat = seat;
        }
        
        protected override void StartStep(Ped ped)
        {
            if (targetVehicle?.Exists() == true)
            {
                if (VehicleQueryService.TryAcquireControl(targetVehicle))
                {
                    ped.Task.EnterVehicle(targetVehicle, seat);
                    VehicleQueryService.ReleaseControl(targetVehicle);
                }
            }
        }
        
        public override bool IsCompleted(Ped ped)
        {
            if (!isStarted) return false;
            
            // Vérifier si le PNJ est dans le véhicule
            bool inVehicle = ped.IsInVehicle(targetVehicle);
            bool vehicleGone = targetVehicle?.Exists() != true;
            bool tooLong = (DateTime.Now - startTime).TotalMinutes > 2;
            
            return inVehicle || vehicleGone || tooLong;
        }
    }
    
    public class DriveToLocationStep : RoutineStep
    {
        private Vector3 destination;
        private float speed;
        
        public DriveToLocationStep(Vector3 destination, float speed, string description) 
            : base(description)
        {
            this.destination = destination;
            this.speed = speed;
        }
        
        protected override void StartStep(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                if (VehicleQueryService.TryAcquireControl(ped.CurrentVehicle))
                {
                    ped.Task.DriveTo(ped.CurrentVehicle, destination, 5.0f, VehicleDrivingFlags.StopForVehicles, speed);
                    VehicleQueryService.ReleaseControl(ped.CurrentVehicle);
                }
            }
        }
        
        public override bool IsCompleted(Ped ped)
        {
            if (!isStarted) return false;
            
            // Vérifier si arrivé à destination
            float distance = ped.Position.DistanceTo(destination);
            bool nearDestination = distance <= 10.0f;
            bool notInVehicle = !ped.IsInVehicle();
            bool tooLong = (DateTime.Now - startTime).TotalMinutes > 10;
            
            return nearDestination || notInVehicle || tooLong;
        }
    }
    
    public enum InteractionType
    {
        Conversation,
        Handshake,
        Argument,
        Trade,
        Help
    }
    
    // Factory pour créer des étapes communes
    public static class RoutineStepFactory
    {
        private static Random random = new Random();
        
        public static WaitStep CreateRandomWait(int minMinutes = 5, int maxMinutes = 30, string description = "Attendre")
        {
            var duration = TimeSpan.FromMinutes(random.Next(minMinutes, maxMinutes));
            return new WaitStep(duration, description);
        }
        
        public static GoToLocationStep CreateGoToNearbyLocation(Vector3 currentPosition, float radius = 50.0f, string description = "Se déplacer")
        {
            var angle = random.NextDouble() * 2 * Math.PI;
            var distance = random.NextDouble() * radius;
            
            var target = new Vector3(
                currentPosition.X + (float)(Math.Cos(angle) * distance),
                currentPosition.Y + (float)(Math.Sin(angle) * distance),
                currentPosition.Z
            );
            
            return new GoToLocationStep(target, description);
        }
        
        public static InteractStep CreateRandomInteraction(Vector3 position, string description = "Interaction")
        {
            var duration = TimeSpan.FromMinutes(random.Next(2, 10));
            return new InteractStep(position, duration, description);
        }
    }
}

// Énumération pour les événements spéciaux (manquante)
namespace REALIS.UrbanLife
{
    public enum SpecialEventType
    {
        Robbery,    // Vol/agression
        Accident,   // Accident
        Fight,      // Bagarre
        Medical,    // Urgence médicale
        Fire        // Incendie
    }
} 