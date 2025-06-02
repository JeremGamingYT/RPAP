using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Système d'intervention du joueur dans les événements spontanés
    /// Permet au joueur d'intervenir dans les agressions, bagarres, etc.
    /// </summary>
    public class PlayerInterventionSystem
    {
        private NPCEventManager eventManager;
        private Random random;
        private List<InterventionResult> recentInterventions;
        private DateTime lastInterventionCheck;
        
        public PlayerInterventionSystem(NPCEventManager eventManager)
        {
            this.eventManager = eventManager;
            random = new Random();
            recentInterventions = new List<InterventionResult>();
            lastInterventionCheck = DateTime.Now;
        }
        
        public void Update()
        {
            // Vérifier les interventions possibles
            if ((DateTime.Now - lastInterventionCheck).TotalSeconds > 2)
            {
                CheckForInterventionOpportunities();
                lastInterventionCheck = DateTime.Now;
            }
            
            // Nettoyer les anciennes interventions
            CleanupOldInterventions();
        }
        
        private void CheckForInterventionOpportunities()
        {
            var playerPos = Game.Player.Character.Position;
            var nearbyEvent = eventManager.GetNearestEvent(playerPos, 15.0f); // Distance d'intervention
            
            if (nearbyEvent != null && CanInterventInEvent(nearbyEvent))
            {
                CheckPlayerInterventionActions(nearbyEvent);
            }
        }
        
        private bool CanInterventInEvent(SpontaneousEvent eventObj)
        {
            // Vérifier si l'événement est en cours et permet l'intervention
            if (eventObj.Phase != EventPhase.Active) return false;
            
            // Vérifier que l'événement n'a pas déjà eu une intervention récente
            var recentIntervention = recentInterventions.FirstOrDefault(r => 
                r.EventPosition.DistanceTo(eventObj.Position) < 10.0f &&
                (DateTime.Now - r.InterventionTime).TotalMinutes < 5);
                
            return recentIntervention == null;
        }
        
        private void CheckPlayerInterventionActions(SpontaneousEvent eventObj)
        {
            var player = Game.Player.Character;
            
            // Détecter les actions d'intervention du joueur
            bool playerIntervening = false;
            InterventionType interventionType = InterventionType.None;
            
            // Détection si le joueur tire
            if (Game.IsControlPressed(Control.Attack) && player.IsShooting)
            {
                playerIntervening = true;
                interventionType = InterventionType.Violence;
            }
            // Détection si le joueur court vers l'événement
            else if (player.IsRunning && IsPlayerHeadingTowards(player, eventObj.Position))
            {
                playerIntervening = true;
                interventionType = InterventionType.Physical;
            }
            // Détection si le joueur klaxonne (pour attirer l'attention)
            else if (player.IsInVehicle() && Game.IsControlPressed(Control.VehicleHorn))
            {
                var distanceToEvent = player.Position.DistanceTo(eventObj.Position);
                if (distanceToEvent < 20.0f)
                {
                    playerIntervening = true;
                    interventionType = InterventionType.Distraction;
                }
            }
            
            if (playerIntervening)
            {
                ProcessPlayerIntervention(eventObj, interventionType);
            }
        }
        
        private bool IsPlayerHeadingTowards(Ped player, Vector3 targetPos)
        {
            var playerDir = player.ForwardVector;
            var toTarget = (targetPos - player.Position).Normalized;
            var dot = Vector3.Dot(playerDir, toTarget);
            
            return dot > 0.7f; // 70% de direction vers la cible
        }
        
        private void ProcessPlayerIntervention(SpontaneousEvent eventObj, InterventionType interventionType)
        {
            var player = Game.Player.Character;
            var intervention = new InterventionResult
            {
                EventType = eventObj.Type,
                InterventionType = interventionType,
                EventPosition = eventObj.Position,
                InterventionTime = DateTime.Now,
                Success = DetermineInterventionSuccess(eventObj, interventionType)
            };
            
            recentInterventions.Add(intervention);
            
            // Appliquer les effets de l'intervention
            ApplyInterventionEffects(eventObj, intervention);
            
            // Notifier le joueur
            NotifyInterventionResult(intervention);
        }
        
        private bool DetermineInterventionSuccess(SpontaneousEvent eventObj, InterventionType interventionType)
        {
            float successChance = 0.5f; // 50% de base
            
            // Ajuster selon le type d'événement
            switch (eventObj.Type)
            {
                case SpontaneousEventType.Mugging:
                    successChance = 0.8f; // Plus facile d'arrêter une agression
                    break;
                case SpontaneousEventType.StreetFight:
                    successChance = 0.6f; // Modéré pour les bagarres
                    break;
                case SpontaneousEventType.DrugDeal:
                    successChance = 0.9f; // Très facile de faire fuir les dealers
                    break;
                case SpontaneousEventType.Argument:
                    successChance = 0.7f; // Assez facile de calmer une dispute
                    break;
            }
            
            // Ajuster selon le type d'intervention
            switch (interventionType)
            {
                case InterventionType.Violence:
                    successChance += 0.2f; // Efficace mais risqué
                    break;
                case InterventionType.Physical:
                    successChance += 0.1f; // Modérément efficace
                    break;
                case InterventionType.Distraction:
                    successChance -= 0.1f; // Moins efficace mais plus sûr
                    break;
            }
            
            // Correction: utiliser Math.Min et Math.Max au lieu de MathHelper.Clamp
            successChance = Math.Max(0.1f, Math.Min(0.95f, successChance));
            return random.NextDouble() < successChance;
        }
        
        private void ApplyInterventionEffects(SpontaneousEvent eventObj, InterventionResult intervention)
        {
            var player = Game.Player.Character;
            
            if (intervention.Success)
            {
                // Intervention réussie
                ApplySuccessfulInterventionEffects(eventObj, intervention);
            }
            else
            {
                // Intervention échouée
                ApplyFailedInterventionEffects(eventObj, intervention);
            }
            
            // Effets communs selon le type d'intervention
            ApplyInterventionTypeEffects(eventObj, intervention.InterventionType);
        }
        
        private void ApplySuccessfulInterventionEffects(SpontaneousEvent eventObj, InterventionResult intervention)
        {
            switch (eventObj.Type)
            {
                case SpontaneousEventType.Mugging:
                    // Sauver la victime, faire fuir l'agresseur
                    if (eventObj.Aggressor?.Exists() == true)
                    {
                        var escapePos = eventObj.Aggressor.Position + Vector3.RandomXY() * 50.0f;
                        eventObj.Aggressor.Task.RunTo(escapePos);
                        eventObj.Aggressor.MarkAsNoLongerNeeded();
                    }
                    
                    if (eventObj.Victim?.Exists() == true)
                    {
                        // La victime remercie le joueur
                        eventObj.Victim.Task.TurnTo(Game.Player.Character);
                    }
                    break;
                    
                case SpontaneousEventType.StreetFight:
                    // Séparer les combattants
                    foreach (var participant in eventObj.Participants)
                    {
                        if (participant?.Exists() == true)
                        {
                            participant.Task.ClearAll();
                            participant.Task.Wander();
                        }
                    }
                    break;
                    
                case SpontaneousEventType.DrugDeal:
                    // Faire fuir les participants
                    foreach (var participant in eventObj.Participants)
                    {
                        if (participant?.Exists() == true)
                        {
                            var fleePos = participant.Position + Vector3.RandomXY() * 30.0f;
                            participant.Task.RunTo(fleePos);
                        }
                    }
                    break;
                    
                case SpontaneousEventType.Argument:
                    // Calmer les disputeurs
                    foreach (var participant in eventObj.Participants)
                    {
                        if (participant?.Exists() == true)
                        {
                            participant.Task.ClearAll();
                            participant.Task.StandStill(5000);
                        }
                    }
                    break;
            }
            
            // Marquer l'événement comme terminé
            eventObj.Phase = EventPhase.Completed;
        }
        
        private void ApplyFailedInterventionEffects(SpontaneousEvent eventObj, InterventionResult intervention)
        {
            // L'intervention a échoué - possibles conséquences négatives
            var player = Game.Player.Character;
            
            switch (eventObj.Type)
            {
                case SpontaneousEventType.Mugging:
                    // L'agresseur peut se retourner contre le joueur
                    if (eventObj.Aggressor?.Exists() == true && random.NextDouble() < 0.3f)
                    {
                        eventObj.Aggressor.Task.Combat(player);
                    }
                    break;
                    
                case SpontaneousEventType.StreetFight:
                    // Les combattants peuvent s'allier contre le joueur
                    if (random.NextDouble() < 0.2f)
                    {
                        foreach (var participant in eventObj.Participants)
                        {
                            if (participant?.Exists() == true)
                            {
                                participant.Task.Combat(player);
                            }
                        }
                    }
                    break;
                    
                case SpontaneousEventType.DrugDeal:
                    // Les dealers peuvent devenir hostiles
                    foreach (var participant in eventObj.Participants)
                    {
                        if (participant?.Exists() == true && random.NextDouble() < 0.4f)
                        {
                            participant.Task.Combat(player);
                        }
                    }
                    break;
            }
        }
        
        private void ApplyInterventionTypeEffects(SpontaneousEvent eventObj, InterventionType interventionType)
        {
            var player = Game.Player.Character;
            
            switch (interventionType)
            {
                case InterventionType.Violence:
                    // Intervention violente - peut attirer la police
                    if (random.NextDouble() < 0.3f)
                    {
                        // Appeler la police avec un délai
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL, player, 1, false);
                        Function.Call(Hash.SET_PLAYER_WANTED_LEVEL_NOW, player, false);
                    }
                    break;
                    
                case InterventionType.Physical:
                    // Intervention physique - risque de blessure mineure
                    if (random.NextDouble() < 0.1f)
                    {
                        player.Health -= 10;
                    }
                    break;
                    
                case InterventionType.Distraction:
                    // Intervention par distraction - plus sûre mais moins efficace
                    // Pas d'effets négatifs
                    break;
            }
        }
        
        private void NotifyInterventionResult(InterventionResult intervention)
        {
            string message = "";
            string color = intervention.Success ? "~g~" : "~r~";
            
            if (intervention.Success)
            {
                message = intervention.EventType switch
                {
                    SpontaneousEventType.Mugging => "Agression stoppée! Vous avez sauvé la victime.",
                    SpontaneousEventType.StreetFight => "Bagarre interrompue! Les combattants se sont calmés.",
                    SpontaneousEventType.DrugDeal => "Deal interrompu! Les suspects ont fui.",
                    SpontaneousEventType.Argument => "Dispute calmée! Les personnes se sont séparées.",
                    _ => "Intervention réussie!"
                };
            }
            else
            {
                message = intervention.EventType switch
                {
                    SpontaneousEventType.Mugging => "Échec de l'intervention! L'agresseur vous a vu.",
                    SpontaneousEventType.StreetFight => "Échec! Les combattants vous ignorent.",
                    SpontaneousEventType.DrugDeal => "Échec! Les suspects vous ont remarqué.",
                    SpontaneousEventType.Argument => "Échec! Votre intervention a été ignorée.",
                    _ => "Intervention échouée!"
                };
            }
            
            GTA.UI.Notification.PostTicker($"{color}{message}", false);
            
            // Message additionnel selon le type d'intervention
            string interventionTypeMessage = intervention.InterventionType switch
            {
                InterventionType.Violence => "~o~Attention: Intervention violente détectée!",
                InterventionType.Physical => "~y~Intervention physique effectuée.",
                InterventionType.Distraction => "~b~Intervention discrète effectuée.",
                _ => ""
            };
            
            if (!string.IsNullOrEmpty(interventionTypeMessage))
            {
                GTA.UI.Notification.PostTicker(interventionTypeMessage, false);
            }
        }
        
        private void CleanupOldInterventions()
        {
            var cutoffTime = DateTime.Now.AddMinutes(-10);
            recentInterventions.RemoveAll(i => i.InterventionTime < cutoffTime);
        }
        
        public List<InterventionResult> GetRecentInterventions()
        {
            return recentInterventions.ToList();
        }
        
        public int GetInterventionScore()
        {
            var recentSuccesses = recentInterventions
                .Where(i => i.Success && (DateTime.Now - i.InterventionTime).TotalHours < 24)
                .Count();
                
            return recentSuccesses;
        }
    }
    
    public class InterventionResult
    {
        public SpontaneousEventType EventType { get; set; }
        public InterventionType InterventionType { get; set; }
        public Vector3 EventPosition { get; set; }
        public DateTime InterventionTime { get; set; }
        public bool Success { get; set; }
    }
    
    public enum InterventionType
    {
        None,           // Aucune intervention
        Violence,       // Intervention violente (tir, combat)
        Physical,       // Intervention physique (courir vers, se mettre entre)
        Distraction     // Distraction (klaxon, crier)
    }
} 