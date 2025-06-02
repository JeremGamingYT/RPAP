using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Gestionnaire d'événements spontanés entre PNJ
    /// Crée des événements automatiques comme des agressions nocturnes
    /// </summary>
    public class NPCEventManager
    {
        private Random random;
        private List<SpontaneousEvent> activeEvents;
        private DateTime lastEventCheck;
        private DateTime lastEventCreation;
        
        // Configuration temporelle
        private readonly int[] nightHours = { 20, 21, 22, 23, 0, 1, 2, 3, 4, 5 }; // 20h-5h
        private readonly int[] crimeHotspotHours = { 22, 23, 0, 1, 2 }; // 22h-2h (pic de criminalité)
        
        public NPCEventManager()
        {
            random = new Random();
            activeEvents = new List<SpontaneousEvent>();
            lastEventCheck = DateTime.Now;
            lastEventCreation = DateTime.Now;
        }
        
        public void Update()
        {
            // Vérifier les événements existants
            UpdateActiveEvents();
            
            // Créer de nouveaux événements spontanés
            if ((DateTime.Now - lastEventCheck).TotalSeconds > 10) // Vérifier toutes les 10 secondes
            {
                CheckForNewEvents();
                lastEventCheck = DateTime.Now;
            }
        }
        
        private void CheckForNewEvents()
        {
            try
            {
                var playerPos = Game.Player.Character.Position;
                var currentHour = DateTime.Now.Hour;
                
                // Calculer la probabilité d'événement selon l'heure
                float eventProbability = GetEventProbabilityForTime(currentHour);
                
                // Éviter de créer trop d'événements
                if (activeEvents.Count >= 2) return; // Réduit de 3 à 2
                
                // Délai minimum entre événements plus long
                if ((DateTime.Now - lastEventCreation).TotalMinutes < 3) return; // Augmenté de 2 à 3 minutes
                
                // Test de probabilité
                if (random.NextDouble() < eventProbability)
                {
                    CreateSpontaneousEvent(playerPos, currentHour);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur vérification événements: {ex.Message}", false);
            }
        }
        
        private float GetEventProbabilityForTime(int hour)
        {
            float baseProbability = 0.0005f; // 0.05% de base (réduit de 0.1%)
            
            if (nightHours.Contains(hour))
            {
                baseProbability *= 2.0f; // 2x plus probable la nuit (réduit de 3x)
                
                if (crimeHotspotHours.Contains(hour))
                {
                    baseProbability *= 1.5f; // 1.5x plus probable aux heures chaudes (réduit de 2x)
                }
            }
            
            // Augmenter légèrement dans les zones densément peuplées
            var nearbyPeds = World.GetNearbyPeds(Game.Player.Character.Position, 100.0f);
            if (nearbyPeds.Length > 20)
            {
                baseProbability *= 1.2f; // Réduit de 1.5f
            }
            
            return Math.Min(baseProbability, 0.01f); // Maximum 1% (réduit de 5%)
        }
        
        private void CreateSpontaneousEvent(Vector3 playerPos, int currentHour)
        {
            try
            {
                // AMÉLIORATION: Chercher une zone avec suffisamment de PNJ
                Vector3 bestEventPos = Vector3.Zero;
                bool foundGoodLocation = false;
                
                // Essayer plusieurs positions pour trouver celle avec le plus de PNJ
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    var eventDistance = random.Next(25, 60); // Distance réduite pour rester près des PNJ
                    var eventDirection = Vector3.RandomXY();
                    var testPos = playerPos + eventDirection * eventDistance;
                    
                    // Vérifications de sécurité pour la position
                    if (testPos.Z < -100.0f || testPos.Z > 1000.0f) continue;
                    
                    // Vérifier qu'on n'est pas dans l'eau
                    if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, testPos.X, testPos.Y, testPos.Z, testPos.Z)) continue;
                    
                    // Compter les PNJ disponibles dans cette zone
                    var nearbyPeds = World.GetNearbyPeds(testPos, 40.0f);
                    var availablePeds = nearbyPeds.Where(ped => 
                        ped != Game.Player.Character && 
                        ped.IsAlive && 
                        !ped.IsDead && 
                        !ped.IsPlayer &&
                        !ped.IsInVehicle() && 
                        ped.IsOnFoot).Count();
                    
                    // Si on trouve au moins 2 PNJ disponibles, c'est bon
                    if (availablePeds >= 2)
                    {
                        // Ajuster la position pour qu'elle soit sur le sol
                        float groundZ;
                        if (World.GetGroundHeight(testPos, out groundZ))
                        {
                            bestEventPos = new Vector3(testPos.X, testPos.Y, groundZ);
                            foundGoodLocation = true;
                            break;
                        }
                    }
                }
                
                if (!foundGoodLocation)
                {
                    // Message debug pour comprendre pourquoi aucun événement n'est créé
                    if (random.NextDouble() < 0.1f) // Afficher seulement de temps en temps
                    {
                        GTA.UI.Notification.PostTicker("~y~Pas assez de PNJ à proximité pour créer un événement", false);
                    }
                    return;
                }
                
                // Choisir le type d'événement selon l'heure
                SpontaneousEventType eventType = ChooseEventType(currentHour);
                
                switch (eventType)
                {
                    case SpontaneousEventType.StreetFight:
                        CreateStreetFight(bestEventPos);
                        break;
                    case SpontaneousEventType.Mugging:
                        CreateMugging(bestEventPos);
                        break;
                    case SpontaneousEventType.DrugDeal:
                        CreateDrugDeal(bestEventPos);
                        break;
                    case SpontaneousEventType.Argument:
                        CreateArgument(bestEventPos);
                        break;
                }
                
                lastEventCreation = DateTime.Now;
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création événement: {ex.Message}", false);
            }
        }
        
        private SpontaneousEventType ChooseEventType(int currentHour)
        {
            // Plus d'agressions la nuit
            if (nightHours.Contains(currentHour))
            {
                return random.Next(100) switch
                {
                    < 35 => SpontaneousEventType.Mugging,     // 35% - Agression/vol
                    < 60 => SpontaneousEventType.StreetFight, // 25% - Bagarre de rue
                    < 80 => SpontaneousEventType.DrugDeal,    // 20% - Deal de drogue
                    _ => SpontaneousEventType.Argument        // 20% - Dispute
                };
            }
            else
            {
                // Le jour, événements moins violents
                return random.Next(100) switch
                {
                    < 40 => SpontaneousEventType.Argument,    // 40% - Dispute
                    < 60 => SpontaneousEventType.StreetFight, // 20% - Bagarre
                    < 80 => SpontaneousEventType.Mugging,     // 20% - Agression
                    _ => SpontaneousEventType.DrugDeal        // 20% - Deal
                };
            }
        }
        
        private void CreateStreetFight(Vector3 position)
        {
            try
            {
                // Vérifier que la position est valide
                if (position.Z < -100.0f || position.Z > 1000.0f)
                {
                    return;
                }

                // NOUVEAU: Utiliser des PNJ existants
                var nearbyPeds = World.GetNearbyPeds(position, 40.0f);
                var availablePeds = nearbyPeds.Where(ped => 
                    ped != Game.Player.Character && 
                    ped.IsAlive && 
                    !ped.IsDead && 
                    !ped.IsPlayer &&
                    !ped.IsInVehicle() && 
                    ped.IsOnFoot).ToList();

                if (availablePeds.Count < 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Pas assez de PNJ disponibles pour une bagarre", false);
                    return;
                }

                // Sélectionner deux PNJ pour la bagarre
                var ped1 = availablePeds[random.Next(availablePeds.Count)];
                availablePeds.Remove(ped1);
                var ped2 = availablePeds[random.Next(availablePeds.Count)];
                
                if (ped1 != null && ped2 != null)
                {
                    ConfigureFightingPeds(ped1, ped2);
                    
                    var fightEvent = new SpontaneousEvent
                    {
                        Type = SpontaneousEventType.StreetFight,
                        Position = ped1.Position,
                        Participants = new List<Ped> { ped1, ped2 },
                        StartTime = DateTime.Now,
                        IsPlayerAware = false
                    };
                    
                    activeEvents.Add(fightEvent);
                    
                    // Démarrer la bagarre de manière sécurisée
                    StartFightSequenceSafe(ped1, ped2, fightEvent);
                    
                    NotifyPlayer(fightEvent);
                    
                    GTA.UI.Notification.PostTicker("~g~Bagarre créée avec des PNJ existants", false);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création bagarre: {ex.Message}", false);
            }
        }
        
        private void CreateMugging(Vector3 position)
        {
            try
            {
                // Vérifier que la position est valide et sûre
                if (position.Z < -100.0f || position.Z > 1000.0f)
                {
                    GTA.UI.Notification.PostTicker("~r~Position invalide pour agression", false);
                    return;
                }

                // Vérifier qu'on n'est pas trop près du joueur (risque de crash)
                var playerPos = Game.Player.Character.Position;
                if (position.DistanceTo(playerPos) < 15.0f)
                {
                    GTA.UI.Notification.PostTicker("~r~Trop proche du joueur - agression annulée", false);
                    return;
                }

                // NOUVEAU: Utiliser des PNJ existants au lieu d'en créer
                var nearbyPeds = World.GetNearbyPeds(position, 40.0f); // Chercher dans un rayon de 40m
                var availablePeds = nearbyPeds.Where(ped => 
                    ped != Game.Player.Character && 
                    ped.IsAlive && 
                    !ped.IsDead && 
                    !ped.IsPlayer &&
                    !ped.IsInVehicle() && // Pas dans un véhicule
                    ped.IsOnFoot).ToList();

                if (availablePeds.Count < 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Pas assez de PNJ disponibles pour une agression", false);
                    return;
                }

                // Sélectionner deux PNJ aléatoires
                var aggressor = availablePeds[random.Next(availablePeds.Count)];
                availablePeds.Remove(aggressor);
                var victim = availablePeds[random.Next(availablePeds.Count)];

                if (aggressor != null && victim != null && aggressor.Exists() && victim.Exists())
                {
                    ConfigureMuggingPedsSafe(aggressor, victim);
                    
                    var muggingEvent = new SpontaneousEvent
                    {
                        Type = SpontaneousEventType.Mugging,
                        Position = aggressor.Position, // Utiliser la position de l'agresseur
                        Participants = new List<Ped> { aggressor, victim },
                        Aggressor = aggressor,
                        Victim = victim,
                        StartTime = DateTime.Now,
                        IsPlayerAware = false
                    };
                    
                    activeEvents.Add(muggingEvent);
                    
                    // Démarrer l'agression de manière améliorée
                    StartMuggingSequenceImproved(aggressor, victim, muggingEvent);
                    
                    NotifyPlayer(muggingEvent);
                    
                    GTA.UI.Notification.PostTicker("~g~Agression créée avec des PNJ existants", false);
                }
                else
                {
                    GTA.UI.Notification.PostTicker("~r~Échec sélection des PNJ pour agression", false);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création agression: {ex.Message}", false);
            }
        }
        
        private void CreateDrugDeal(Vector3 position)
        {
            try
            {
                // Vérifier que la position est valide
                if (position.Z < -100.0f || position.Z > 1000.0f)
                {
                    return;
                }

                // NOUVEAU: Utiliser des PNJ existants
                var nearbyPeds = World.GetNearbyPeds(position, 40.0f);
                var availablePeds = nearbyPeds.Where(ped => 
                    ped != Game.Player.Character && 
                    ped.IsAlive && 
                    !ped.IsDead && 
                    !ped.IsPlayer &&
                    !ped.IsInVehicle() && 
                    ped.IsOnFoot).ToList();

                if (availablePeds.Count < 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Pas assez de PNJ disponibles pour un deal", false);
                    return;
                }

                // Sélectionner un dealer et un client
                var dealer = availablePeds[random.Next(availablePeds.Count)];
                availablePeds.Remove(dealer);
                var client = availablePeds[random.Next(availablePeds.Count)];
                
                if (dealer != null && client != null)
                {
                    ConfigureDealPeds(dealer, client);
                    
                    var dealEvent = new SpontaneousEvent
                    {
                        Type = SpontaneousEventType.DrugDeal,
                        Position = dealer.Position,
                        Participants = new List<Ped> { dealer, client },
                        StartTime = DateTime.Now,
                        IsPlayerAware = false
                    };
                    
                    activeEvents.Add(dealEvent);
                    
                    // Démarrer le deal de manière sécurisée
                    StartDrugDealSequenceSafe(dealer, client, dealEvent);
                    
                    NotifyPlayer(dealEvent);
                    
                    GTA.UI.Notification.PostTicker("~g~Deal de drogue créé avec des PNJ existants", false);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création deal: {ex.Message}", false);
            }
        }
        
        private void CreateArgument(Vector3 position)
        {
            try
            {
                // Vérifier que la position est valide
                if (position.Z < -100.0f || position.Z > 1000.0f)
                {
                    return;
                }

                // NOUVEAU: Utiliser des PNJ existants
                var nearbyPeds = World.GetNearbyPeds(position, 40.0f);
                var availablePeds = nearbyPeds.Where(ped => 
                    ped != Game.Player.Character && 
                    ped.IsAlive && 
                    !ped.IsDead && 
                    !ped.IsPlayer &&
                    !ped.IsInVehicle() && 
                    ped.IsOnFoot).ToList();

                if (availablePeds.Count < 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Pas assez de PNJ disponibles pour une dispute", false);
                    return;
                }

                // Sélectionner deux PNJ pour la dispute
                var ped1 = availablePeds[random.Next(availablePeds.Count)];
                availablePeds.Remove(ped1);
                var ped2 = availablePeds[random.Next(availablePeds.Count)];
                
                if (ped1 != null && ped2 != null)
                {
                    ConfigureArgumentPeds(ped1, ped2);
                    
                    var argumentEvent = new SpontaneousEvent
                    {
                        Type = SpontaneousEventType.Argument,
                        Position = ped1.Position,
                        Participants = new List<Ped> { ped1, ped2 },
                        StartTime = DateTime.Now,
                        IsPlayerAware = false
                    };
                    
                    activeEvents.Add(argumentEvent);
                    
                    // Démarrer la dispute de manière sécurisée
                    StartArgumentSequenceSafe(ped1, ped2, argumentEvent);
                    
                    NotifyPlayer(argumentEvent);
                    
                    GTA.UI.Notification.PostTicker("~g~Dispute créée avec des PNJ existants", false);
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création dispute: {ex.Message}", false);
            }
        }
        
        private Model GetRandomAggressivePed()
        {
            var aggressivePeds = new[]
            {
                PedHash.StrPunk01GMY, PedHash.StrPunk02GMY, PedHash.Azteca01GMY,
                PedHash.BallaEast01GMY, PedHash.BallaOrig01GMY, PedHash.BallaSout01GMY,
                PedHash.Families01GFY, PedHash.Lost01GMY, PedHash.Lost02GMY, PedHash.Lost03GMY
            };
            
            return new Model(aggressivePeds[random.Next(aggressivePeds.Length)]);
        }
        
        private Model GetSafeCriminalPed()
        {
            // Utiliser des modèles PNJ plus stables
            var safeCriminals = new[]
            {
                PedHash.StrPunk01GMY, 
                PedHash.StrPunk02GMY, 
                PedHash.Azteca01GMY
            };
            
            return new Model(safeCriminals[random.Next(safeCriminals.Length)]);
        }
        
        private Model GetSafeCivilianPed()
        {
            // Utiliser des modèles PNJ civils très stables
            var safeCivilians = new[]
            {
                PedHash.Business01AMY,
                PedHash.Business02AFY,
                PedHash.Tourist01AFY
            };
            
            return new Model(safeCivilians[random.Next(safeCivilians.Length)]);
        }
        
        private Model GetRandomCriminalPed()
        {
            var criminalPeds = new[]
            {
                PedHash.Dealer01SMY, PedHash.Robber01SMY, PedHash.StrPunk01GMY,
                PedHash.StrPunk02GMY, PedHash.Prisoner01SMY, PedHash.Prisoner01
            };
            
            return new Model(criminalPeds[random.Next(criminalPeds.Length)]);
        }
        
        private Model GetRandomCivilianPed()
        {
            // Utiliser seulement des PedHash confirmés qui existent dans la documentation
            var civilianPeds = new[]
            {
                PedHash.Business01AMM, PedHash.Business01AMY, PedHash.Downtown01AMY,
                PedHash.Hipster01AMY, PedHash.Hipster02AMY, PedHash.Salton01AMM, 
                PedHash.Salton01AMY, PedHash.Soucent01AMM, PedHash.Soucent01AMY,
                PedHash.Soucent02AFM, PedHash.Soucent02AMM, PedHash.Soucent03AMM // Correction: utiliser des PedHash existants
            };
            
            return new Model(civilianPeds[random.Next(civilianPeds.Length)]);
        }
        
        private void ConfigureFightingPeds(Ped ped1, Ped ped2)
        {
            ped1.IsPersistent = true;
            ped2.IsPersistent = true;
            ped1.BlockPermanentEvents = true;
            ped2.BlockPermanentEvents = true;
            
            // Donner parfois des armes blanches
            if (random.NextDouble() < 0.3f) // 30% de chance
            {
                ped1.Weapons.Give(WeaponHash.Knife, 1, false, true);
            }
            if (random.NextDouble() < 0.3f)
            {
                ped2.Weapons.Give(WeaponHash.Knife, 1, false, true);
            }
        }
        
        private void ConfigureMuggingPeds(Ped aggressor, Ped victim)
        {
            aggressor.IsPersistent = true;
            victim.IsPersistent = true;
            aggressor.BlockPermanentEvents = true;
            victim.BlockPermanentEvents = true;
            
            // L'agresseur a toujours une arme
            var weapons = new[] { WeaponHash.Pistol, WeaponHash.Knife, WeaponHash.SwitchBlade };
            aggressor.Weapons.Give(weapons[random.Next(weapons.Length)], 50, false, true);
        }
        
        private void ConfigureMuggingPedsSafe(Ped aggressor, Ped victim)
        {
            try
            {
                if (aggressor?.Exists() != true || victim?.Exists() != true) return;
                
                aggressor.IsPersistent = true;
                victim.IsPersistent = true;
                aggressor.BlockPermanentEvents = true;
                victim.BlockPermanentEvents = true;
                
                // NE PAS donner d'arme automatiquement pour éviter les crashes
                // L'arme sera donnée seulement si nécessaire plus tard
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur config agression: {ex.Message}", false);
            }
        }
        
        private void ConfigureDealPeds(Ped dealer, Ped client)
        {
            dealer.IsPersistent = true;
            client.IsPersistent = true;
            dealer.BlockPermanentEvents = true;
            client.BlockPermanentEvents = true;
            
            // Le dealer peut avoir une arme de protection
            if (random.NextDouble() < 0.6f)
            {
                dealer.Weapons.Give(WeaponHash.Pistol, 30, false, true);
            }
        }
        
        private void ConfigureArgumentPeds(Ped ped1, Ped ped2)
        {
            ped1.IsPersistent = true;
            ped2.IsPersistent = true;
            ped1.BlockPermanentEvents = true;
            ped2.BlockPermanentEvents = true;
        }
        
        private void StartFightSequenceSafe(Ped ped1, Ped ped2, SpontaneousEvent eventObj)
        {
            if (ped1?.Exists() != true || ped2?.Exists() != true) return;

            try
            {
                // Phase 1: Se faire face et se provoquer
                ped1.Task.TurnTo(ped2);
                ped2.Task.TurnTo(ped1);
                eventObj.Phase = EventPhase.Active;
                
                // Ajouter un timestamp pour gérer les phases sans threading
                eventObj.PhaseStartTime = DateTime.Now;
                eventObj.CurrentPhase = 1;
                
                GTA.UI.Notification.PostTicker("~o~Bagarre de rue en cours!", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur séquence bagarre: {ex.Message}", false);
            }
        }
        
        private void StartMuggingSequenceImproved(Ped aggressor, Ped victim, SpontaneousEvent eventObj)
        {
            if (aggressor?.Exists() != true || victim?.Exists() != true) return;

            try
            {
                // Phase 1: L'agresseur s'approche rapidement de la victime
                aggressor.Task.RunTo(victim.Position);
                eventObj.Phase = EventPhase.Active;
                
                // Ajouter un timestamp pour gérer les phases sans threading
                eventObj.PhaseStartTime = DateTime.Now;
                eventObj.CurrentPhase = 1;
                
                GTA.UI.Notification.PostTicker("~r~Agression en cours - approchez-vous pour intervenir!", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur séquence agression: {ex.Message}", false);
            }
        }
        
        private void StartDrugDealSequenceSafe(Ped dealer, Ped client, SpontaneousEvent eventObj)
        {
            if (dealer?.Exists() != true || client?.Exists() != true) return;

            try
            {
                // Phase 1: Le client approche
                client.Task.FollowNavMeshTo(dealer.Position);
                eventObj.Phase = EventPhase.Active;
                
                // Ajouter un timestamp pour gérer les phases sans threading
                eventObj.PhaseStartTime = DateTime.Now;
                eventObj.CurrentPhase = 1;
                
                GTA.UI.Notification.PostTicker("~p~Transaction suspecte en cours...", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur séquence deal: {ex.Message}", false);
            }
        }
        
        private void StartArgumentSequenceSafe(Ped ped1, Ped ped2, SpontaneousEvent eventObj)
        {
            if (ped1?.Exists() != true || ped2?.Exists() != true) return;

            try
            {
                // Phase 1: Se faire face
                ped1.Task.TurnTo(ped2);
                ped2.Task.TurnTo(ped1);
                eventObj.Phase = EventPhase.Active;
                
                // Ajouter un timestamp pour gérer les phases sans threading
                eventObj.PhaseStartTime = DateTime.Now;
                eventObj.CurrentPhase = 1;
                
                GTA.UI.Notification.PostTicker("~y~Dispute en cours...", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur séquence dispute: {ex.Message}", false);
            }
        }
        
        private void NotifyPlayer(SpontaneousEvent eventObj)
        {
            var distance = eventObj.Position.DistanceTo(Game.Player.Character.Position);
            
            string message = eventObj.Type switch
            {
                SpontaneousEventType.StreetFight => $"~o~Bagarre de rue signalée à {distance:F0}m",
                SpontaneousEventType.Mugging => $"~r~Agression en cours à {distance:F0}m",
                SpontaneousEventType.DrugDeal => $"~p~Transaction suspecte à {distance:F0}m",
                SpontaneousEventType.Argument => $"~y~Dispute animée à {distance:F0}m",
                _ => $"~w~Incident à {distance:F0}m"
            };
            
            GTA.UI.Notification.PostTicker(message, false);
            
            // Ajouter un blip temporaire
            var blip = World.CreateBlip(eventObj.Position);
            blip.Sprite = eventObj.Type switch
            {
                SpontaneousEventType.StreetFight => BlipSprite.PersonalVehicleCar,
                SpontaneousEventType.Mugging => BlipSprite.Waypoint,
                SpontaneousEventType.DrugDeal => BlipSprite.Information,
                SpontaneousEventType.Argument => BlipSprite.Information,
                _ => BlipSprite.Information
            };
            blip.Color = eventObj.Type switch
            {
                SpontaneousEventType.StreetFight => BlipColor.Orange,
                SpontaneousEventType.Mugging => BlipColor.Yellow,
                SpontaneousEventType.DrugDeal => BlipColor.Purple,
                SpontaneousEventType.Argument => BlipColor.Yellow,
                _ => BlipColor.White
            };
            blip.Scale = 0.7f;
            
            eventObj.Blip = blip;
        }
        
        private void UpdateActiveEvents()
        {
            try
            {
                for (int i = activeEvents.Count - 1; i >= 0; i--)
                {
                    var eventObj = activeEvents[i];
                    var elapsedTime = DateTime.Now - eventObj.StartTime;
                    
                    // Vérifier si l'événement doit être nettoyé
                    if (elapsedTime.TotalMinutes > 5 || !EventStillActive(eventObj)) // Réduit de 10 à 5 minutes
                    {
                        CleanupEvent(eventObj);
                        activeEvents.RemoveAt(i);
                        continue;
                    }
                    
                    // Mettre à jour les séquences d'événements
                    UpdateEventSequence(eventObj);
                    
                    // Mettre à jour la position du blip pour suivre l'agresseur
                    UpdateEventBlipPosition(eventObj);
                    
                    // Vérifier si le joueur s'approche
                    var distanceToPlayer = eventObj.Position.DistanceTo(Game.Player.Character.Position);
                    if (distanceToPlayer < 30.0f && !eventObj.IsPlayerAware)
                    {
                        eventObj.IsPlayerAware = true;
                        OnPlayerNearEvent(eventObj);
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur mise à jour événements: {ex.Message}", false);
            }
        }
        
        private void UpdateEventSequence(SpontaneousEvent eventObj)
        {
            if (eventObj.Aggressor?.Exists() != true && eventObj.Participants.Count < 2)
                return;
                
            var timeSincePhaseStart = DateTime.Now - eventObj.PhaseStartTime;
            
            switch (eventObj.Type)
            {
                case SpontaneousEventType.Mugging:
                    UpdateMuggingSequence(eventObj, timeSincePhaseStart);
                    break;
                case SpontaneousEventType.StreetFight:
                    UpdateStreetFightSequence(eventObj, timeSincePhaseStart);
                    break;
                case SpontaneousEventType.DrugDeal:
                    UpdateDrugDealSequence(eventObj, timeSincePhaseStart);
                    break;
                case SpontaneousEventType.Argument:
                    UpdateArgumentSequence(eventObj, timeSincePhaseStart);
                    break;
            }
        }
        
        private void UpdateMuggingSequence(SpontaneousEvent eventObj, TimeSpan timeSincePhaseStart)
        {
            if (eventObj.Aggressor?.Exists() != true || eventObj.Victim?.Exists() != true)
                return;
                
            var aggressor = eventObj.Aggressor;
            var victim = eventObj.Victim;
            
            switch (eventObj.CurrentPhase)
            {
                case 1: // Phase d'approche
                    if (timeSincePhaseStart.TotalSeconds > 3 || aggressor.Position.DistanceTo(victim.Position) < 2.0f)
                    {
                        // Phase 2: L'agresseur menace la victime
                        aggressor.Task.TurnTo(victim);
                        victim.Task.TurnTo(aggressor);
                        eventObj.CurrentPhase = 2;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
                    
                case 2: // Phase de menace
                    if (timeSincePhaseStart.TotalSeconds > 1)
                    {
                        // Phase 3: La victime réagit avec peur
                        victim.Task.HandsUp(3000);
                        Function.Call(GTA.Native.Hash.PLAY_PAIN, victim, 6, 0, 0);
                        eventObj.CurrentPhase = 3;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
                    
                case 3: // Phase de peur de la victime
                    if (timeSincePhaseStart.TotalSeconds > 2)
                    {
                        // Phase 4: L'agresseur pousse ou bouscule la victime
                        aggressor.Task.Combat(victim);
                        eventObj.CurrentPhase = 4;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
                    
                case 4: // Phase de combat
                    if (timeSincePhaseStart.TotalSeconds > 3)
                    {
                        // Phase 5: L'agresseur s'enfuit
                        var escapeDirection = Vector3.RandomXY().Normalized;
                        var escapePos = aggressor.Position + (escapeDirection * 50.0f);
                        aggressor.Task.FleeFrom(victim, 10000);
                        eventObj.Phase = EventPhase.Ending;
                        eventObj.CurrentPhase = 5;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
            }
        }
        
        private void UpdateStreetFightSequence(SpontaneousEvent eventObj, TimeSpan timeSincePhaseStart)
        {
            var validParticipants = eventObj.Participants.Where(p => p?.Exists() == true && !p.IsDead).ToList();
            if (validParticipants.Count < 2) return;
            
            var ped1 = validParticipants[0];
            var ped2 = validParticipants[1];
            
            switch (eventObj.CurrentPhase)
            {
                case 1: // Phase de provocation
                    if (timeSincePhaseStart.TotalSeconds > 2)
                    {
                        // Phase 2: Début du combat
                        ped1.Task.Combat(ped2);
                        ped2.Task.Combat(ped1);
                        eventObj.CurrentPhase = 2;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
                    
                case 2: // Phase de combat
                    if (timeSincePhaseStart.TotalSeconds > 10)
                    {
                        // Phase 3: Fin du combat (l'un des deux fuit)
                        var winner = random.NextDouble() < 0.5 ? ped1 : ped2;
                        var loser = winner == ped1 ? ped2 : ped1;
                        
                        winner.Task.ClearAll();
                        loser.Task.FleeFrom(winner, 5000);
                        eventObj.Phase = EventPhase.Ending;
                        eventObj.CurrentPhase = 3;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
            }
        }
        
        private void UpdateDrugDealSequence(SpontaneousEvent eventObj, TimeSpan timeSincePhaseStart)
        {
            var validParticipants = eventObj.Participants.Where(p => p?.Exists() == true && !p.IsDead).ToList();
            if (validParticipants.Count < 2) return;
            
            var dealer = validParticipants[0];
            var client = validParticipants[1];
            
            switch (eventObj.CurrentPhase)
            {
                case 1: // Phase d'approche
                    if (timeSincePhaseStart.TotalSeconds > 3 || dealer.Position.DistanceTo(client.Position) < 2.0f)
                    {
                        // Phase 2: Face à face pour le deal
                        dealer.Task.TurnTo(client);
                        client.Task.TurnTo(dealer);
                        eventObj.CurrentPhase = 2;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
                    
                case 2: // Phase de transaction
                    if (timeSincePhaseStart.TotalSeconds > 5)
                    {
                        // Phase 3: Chacun part de son côté
                        dealer.Task.Wander();
                        client.Task.Wander();
                        eventObj.Phase = EventPhase.Ending;
                        eventObj.CurrentPhase = 3;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
            }
        }
        
        private void UpdateArgumentSequence(SpontaneousEvent eventObj, TimeSpan timeSincePhaseStart)
        {
            var validParticipants = eventObj.Participants.Where(p => p?.Exists() == true && !p.IsDead).ToList();
            if (validParticipants.Count < 2) return;
            
            var ped1 = validParticipants[0];
            var ped2 = validParticipants[1];
            
            switch (eventObj.CurrentPhase)
            {
                case 1: // Phase de dispute verbale
                    if (timeSincePhaseStart.TotalSeconds > 5)
                    {
                        // 30% de chance que ça dégénère en bagarre
                        if (random.NextDouble() < 0.3f)
                        {
                            ped1.Task.Combat(ped2);
                            ped2.Task.Combat(ped1);
                            eventObj.Type = SpontaneousEventType.StreetFight; // Escalade
                            eventObj.CurrentPhase = 2;
                        }
                        else
                        {
                            // Sinon, ils se calment et partent
                            ped1.Task.Wander();
                            ped2.Task.Wander();
                            eventObj.Phase = EventPhase.Ending;
                            eventObj.CurrentPhase = 3;
                        }
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
                    
                case 2: // Phase de bagarre (si escalade)
                    if (timeSincePhaseStart.TotalSeconds > 8)
                    {
                        // Fin de la bagarre
                        var winner = random.NextDouble() < 0.5 ? ped1 : ped2;
                        var loser = winner == ped1 ? ped2 : ped1;
                        
                        winner.Task.ClearAll();
                        loser.Task.FleeFrom(winner, 5000);
                        eventObj.Phase = EventPhase.Ending;
                        eventObj.CurrentPhase = 3;
                        eventObj.PhaseStartTime = DateTime.Now;
                    }
                    break;
            }
        }
        
        private void UpdateEventBlipPosition(SpontaneousEvent eventObj)
        {
            if (eventObj.Blip?.Exists() != true) return;
            
            Vector3 newPosition = eventObj.Position;
            
            // Pour les agressions, faire suivre le blip à l'agresseur
            if (eventObj.Type == SpontaneousEventType.Mugging && eventObj.Aggressor?.Exists() == true)
            {
                newPosition = eventObj.Aggressor.Position;
                eventObj.Position = newPosition; // Mettre à jour la position de l'événement
            }
            // Pour les bagarres, suivre le centre entre les combattants
            else if (eventObj.Type == SpontaneousEventType.StreetFight && eventObj.Participants.Count >= 2)
            {
                var validParticipants = eventObj.Participants.Where(p => p?.Exists() == true).ToList();
                if (validParticipants.Count >= 2)
                {
                    var centerPos = Vector3.Zero;
                    foreach (var participant in validParticipants)
                    {
                        centerPos += participant.Position;
                    }
                    newPosition = centerPos / validParticipants.Count;
                    eventObj.Position = newPosition;
                }
            }
            
            // Mettre à jour la position du blip
            eventObj.Blip.Position = newPosition;
        }
        
        private bool EventStillActive(SpontaneousEvent eventObj)
        {
            try
            {
                // Vérifier que les participants existent encore et sont vivants
                return eventObj.Participants.Any(p => p?.Exists() == true && p.IsAlive && !p.IsDead);
            }
            catch
            {
                return false; // En cas d'erreur, considérer comme inactif
            }
        }
        
        private void OnPlayerNearEvent(SpontaneousEvent eventObj)
        {
            string message = eventObj.Type switch
            {
                SpontaneousEventType.StreetFight => "~o~Vous assistez à une bagarre! Vous pouvez intervenir.",
                SpontaneousEventType.Mugging => "~r~Vous assistez à une agression! Intervenez pour aider la victime!",
                SpontaneousEventType.DrugDeal => "~p~Vous assistez à un deal de drogue...",
                SpontaneousEventType.Argument => "~y~Vous assistez à une dispute...",
                _ => "~w~Vous assistez à un incident..."
            };
            
            GTA.UI.Notification.PostTicker(message, false);
        }
        
        private void CleanupEvent(SpontaneousEvent eventObj)
        {
            // Nettoyer le blip
            eventObj.Blip?.Delete();
            
            // Marquer les PNJ comme plus nécessaires après un délai
            foreach (var ped in eventObj.Participants)
            {
                if (ped?.Exists() == true)
                {
                    ped.MarkAsNoLongerNeeded();
                }
            }
        }
        
        public List<SpontaneousEvent> GetActiveEvents()
        {
            return activeEvents.ToList();
        }
        
        public SpontaneousEvent? GetNearestEvent(Vector3 position, float maxDistance = 50.0f)
        {
            return activeEvents
                .Where(e => e.Position.DistanceTo(position) <= maxDistance)
                .OrderBy(e => e.Position.DistanceTo(position))
                .FirstOrDefault();
        }
        
        // Méthode pour permettre au joueur de forcer un événement (debug)
        public void ForceCreateEvent(SpontaneousEventType type, Vector3? position = null)
        {
            var eventPos = position ?? Game.Player.Character.Position + Vector3.RandomXY() * 30.0f;
            
            switch (type)
            {
                case SpontaneousEventType.StreetFight:
                    CreateStreetFight(eventPos);
                    break;
                case SpontaneousEventType.Mugging:
                    CreateMugging(eventPos);
                    break;
                case SpontaneousEventType.DrugDeal:
                    CreateDrugDeal(eventPos);
                    break;
                case SpontaneousEventType.Argument:
                    CreateArgument(eventPos);
                    break;
            }
        }
    }
    
    public class SpontaneousEvent
    {
        public SpontaneousEventType Type { get; set; }
        public Vector3 Position { get; set; }
        public List<Ped> Participants { get; set; } = new List<Ped>();
        public Ped? Aggressor { get; set; }
        public Ped? Victim { get; set; }
        public DateTime StartTime { get; set; }
        public EventPhase Phase { get; set; } = EventPhase.Starting;
        public bool IsPlayerAware { get; set; }
        public Blip? Blip { get; set; }
        public DateTime PhaseStartTime { get; set; }
        public int CurrentPhase { get; set; }
    }
    
    public enum SpontaneousEventType
    {
        StreetFight,    // Bagarre de rue
        Mugging,        // Agression/Vol
        DrugDeal,       // Deal de drogue
        Argument        // Dispute (peut escalader)
    }
    
    public enum EventPhase
    {
        Starting,       // Événement qui démarre
        Active,         // Événement en cours
        Ending,         // Événement qui se termine
        Completed       // Événement terminé
    }
} 