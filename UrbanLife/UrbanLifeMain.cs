using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace REALIS.UrbanLife
{
    // Import pour RoadEventType
    using static RoadEventManager;
    
    public class UrbanLifeMain : Script
    {
        private NPCRoutineManager routineManager;
        private NoiseReactionManager noiseManager;
        private EventBlipManager blipManager;
        private List<SmartNPC> smartNPCs;
        private NPCEventManager eventManager;
        private PlayerInterventionSystem interventionSystem;
        private Random random;
        private RoadEventManager roadEventManager;
        
        // Configuration
        private int maxSmartNPCs = 15;
        private int updateInterval = 2000; // millisecondes
        private DateTime lastUpdate = DateTime.Now;
        private DateTime lastDebugUpdate = DateTime.Now;
        private DateTime lastErrorNotification = DateTime.Now;
        private DateTime lastGKeyPress = DateTime.Now;
        
        public UrbanLifeMain()
        {
            routineManager = new NPCRoutineManager();
            noiseManager = new NoiseReactionManager();
            blipManager = EventBlipManager.Instance;
            roadEventManager = new RoadEventManager();
            smartNPCs = new List<SmartNPC>();
            eventManager = new NPCEventManager();
            interventionSystem = new PlayerInterventionSystem(eventManager);
            random = new Random();
            
            Tick += OnTick;
            KeyDown += OnKeyDown;
            Interval = 250;
            
            GTA.UI.Screen.ShowSubtitle("~g~Système de Vie Urbaine Améliorée activé", 3000);
            GTA.UI.Notification.PostTicker("~b~UrbanLife chargé! F11=Debug, F10=Force activation", false);
        }
        
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Mise à jour moins fréquente pour les performances
                if ((DateTime.Now - lastUpdate).TotalMilliseconds < updateInterval)
                    return;
                
                lastUpdate = DateTime.Now;
                
                UpdateSmartNPCs();
                routineManager.Update();
                noiseManager.Update();
                CheckForSpecialEvents();
                
                // NOUVEAU: Mise à jour des mini-événements routiers
                roadEventManager.Update();
                
                // Affichage des informations de debug (optionnel)
                if (Game.IsKeyPressed(System.Windows.Forms.Keys.F11))
                {
                    ShowDebugInfo();
                }
            }
            catch (Exception ex)
            {
                if ((DateTime.Now - lastErrorNotification).TotalSeconds > 10)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur UrbanLife: {ex.Message}", false);
                    lastErrorNotification = DateTime.Now;
                }
            }
        }
        
        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            try
            {
                // Vérification de sécurité globale
                var player = Game.Player.Character;
                if (player == null || !player.Exists() || player.IsDead)
                {
                    return;
                }

                if (e.KeyCode == System.Windows.Forms.Keys.F8)
                {
                    ShowDebugInfo();
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F9)
                {
                    // CORRECTION F9: Réinitialisation sécurisée avec cooldown
                    try
                    {
                        // Vérifier si on peut faire une réinitialisation (éviter les spams)
                        if (roadEventManager != null)
                        {
                            roadEventManager.ClearAllEvents();
                        }
                        
                        UrbanLifeIntegration.ReleaseAllNPCs();
                        smartNPCs.Clear();
                        
                        // Forcer un délai avant que F7 ne puisse être utilisé
                        GTA.UI.Notification.PostTicker("~y~Système UrbanLife réinitialisé - Attendez 3 secondes avant F7", false);
                        
                        // Programmer un délai sécurisé
                        System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ => {
                            GTA.UI.Notification.PostTicker("~g~F7 maintenant disponible", false);
                        });
                    }
                    catch (Exception ex)
                    {
                        GTA.UI.Notification.PostTicker($"~r~Erreur F9: {ex.Message}", false);
                    }
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F10)
                {
                    // Force un événement spontané (nouveau système) - SÉCURISÉ
                    ForceCreateSpontaneousEventSafe();
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F11)
                {
                    // Force une agression nocturne - SÉCURISÉ
                    ForceCreateMuggingSafe();
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F12)
                {
                    // Force une bagarre de rue - SÉCURISÉ
                    ForceCreateFightSafe();
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.L)
                {
                    // Effacer tous les blips d'événements
                    blipManager.ClearAllBlips();
                    GTA.UI.Notification.PostTicker("~g~Tous les blips d'événements ont été effacés!", false);
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F7)
                {
                    // CORRECTION F7: Protection contre les crashes à répétition
                    try
                    {
                        // Vérifications de sécurité renforcées
                        if (roadEventManager == null)
                        {
                            GTA.UI.Notification.PostTicker("~r~Système d'événements non initialisé!", false);
                            return;
                        }

                        // Vérifier si le joueur est dans un véhicule SEULEMENT pour la recommandation
                        if (player?.CurrentVehicle == null)
                        {
                            // Permettre la création même hors véhicule mais avertir
                            GTA.UI.Notification.PostTicker("~y~Recommandé: Soyez dans un véhicule pour de meilleurs événements", false);
                        }
                        
                        // Vérifier l'état du RoadEventManager avant l'appel
                        var activeEventsCount = roadEventManager.GetActiveEvents()?.Count ?? 0;
                        if (activeEventsCount >= 3)
                        {
                            GTA.UI.Notification.PostTicker("~y~Trop d'événements actifs! Attendez qu'ils se terminent.", false);
                            return;
                        }

                        // Appel sécurisé avec gestion d'erreur
                        bool eventCreated = roadEventManager.ForceCreateRoadEvent();
                        
                        if (eventCreated)
                        {
                            GTA.UI.Notification.PostTicker("~g~Mini-événement routier créé!", false);
                            GTA.UI.Screen.ShowSubtitle("~g~Regardez votre mini-map pour localiser l'événement!", 4000);
                        }
                        else
                        {
                            GTA.UI.Notification.PostTicker("~y~Impossible de créer un événement maintenant. Conditions non remplies.", false);
                        }
                    }
                    catch (Exception ex)
                    {
                        GTA.UI.Notification.PostTicker($"~r~Erreur F7 sécurisée: {ex.Message}", false);
                        // Log l'erreur pour debug mais ne pas crasher
                        System.IO.File.AppendAllText("UrbanLife_F7_errors.log", 
                            $"{DateTime.Now}: F7 Error - {ex.Message}\n{ex.StackTrace}\n\n");
                    }
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.G)
                {
                    // NOUVELLE GESTION DE LA TOUCHE G - Plus flexible et sécurisée
                    try
                    {
                        HandleGKeyPress();
                    }
                    catch (Exception ex)
                    {
                        GTA.UI.Notification.PostTicker($"~r~Erreur G sécurisée: {ex.Message}", false);
                    }
                }
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur touches globale: {ex.Message}", false);
            }
        }
        
        /// <summary>
        /// Gestion sécurisée de la touche G
        /// </summary>
        private void HandleGKeyPress()
        {
            try
            {
                var player = Game.Player.Character;
                if (player?.Exists() != true)
                {
                    return;
                }

                // NOUVEAU: Cooldown pour éviter le spam de la touche G
                var now = DateTime.Now;
                if ((now - lastGKeyPress).TotalSeconds < 1.5) // 1.5 secondes entre chaque appui
                {
                    GTA.UI.Notification.PostTicker("~y~Attendez un moment avant de refaire G", false);
                    return;
                }
                lastGKeyPress = now;

                // CORRECTION: Vérifications de sécurité du RoadEventManager
                if (roadEventManager == null)
                {
                    GTA.UI.Notification.PostTicker("~r~Système d'événements non initialisé!", false);
                    return;
                }

                // Vérifier s'il y a des événements de panne actifs
                List<RoadEvent>? activeEvents = null;
                try
                {
                    activeEvents = roadEventManager.GetActiveEvents()?.Where(e => 
                        e?.Type == RoadEventType.BrokenDownVehicle && 
                        e.CanInteract &&
                        e.Position.DistanceTo(player.Position) <= 10.0f) // Distance augmentée de 8 à 10
                        .ToList();
                }
                catch (Exception)
                {
                    GTA.UI.Notification.PostTicker("~r~Erreur lors de la vérification des événements", false);
                    return;
                }

                if (activeEvents == null || !activeEvents.Any())
                {
                    GTA.UI.Notification.PostTicker("~y~Aucun événement de panne proche pour utiliser G", false);
                    return;
                }

                var nearestEvent = activeEvents.OrderBy(e => e?.Position.DistanceTo(player.Position) ?? float.MaxValue).FirstOrDefault();
                
                // NOUVELLE PROTECTION: Vérifier que l'événement est valide
                if (nearestEvent?.Position == null)
                {
                    GTA.UI.Notification.PostTicker("~r~Événement invalide", false);
                    return;
                }

                // Vérifier que l'événement a un conducteur valide
                if (nearestEvent.Participants?.Count == 0 || 
                    nearestEvent.Participants?[0]?.Exists() != true ||
                    nearestEvent.Participants?[0]?.IsDead == true)
                {
                    GTA.UI.Notification.PostTicker("~r~Aucun conducteur disponible", false);
                    return;
                }

                // CORRECTION: Appel sécurisé avec gestion d'erreur et vérification des méthodes
                try
                {
                    var offerRideMethod = roadEventManager.GetType()
                        .GetMethod("OfferRideToDriver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (offerRideMethod == null)
                    {
                        GTA.UI.Notification.PostTicker("~r~Méthode OfferRideToDriver non trouvée", false);
                        return;
                    }
                    
                    offerRideMethod.Invoke(roadEventManager, new object[] { nearestEvent });
                    GTA.UI.Notification.PostTicker("~g~Aide proposée au conducteur", false);
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    // Capturer les exceptions spécifiques de réflexion
                    var innerEx = ex.InnerException ?? ex;
                    GTA.UI.Notification.PostTicker($"~r~Erreur d'aide: {innerEx.Message}", false);
                    
                    // Log l'erreur
                    try
                    {
                        System.IO.File.AppendAllText("UrbanLife_G_key_error.log", 
                            $"{DateTime.Now}: G Key Error - {innerEx.Message}\n{innerEx.StackTrace}\n\n");
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur générale: {ex.Message}", false);
                    
                    // Log l'erreur
                    try
                    {
                        System.IO.File.AppendAllText("UrbanLife_G_key_error.log", 
                            $"{DateTime.Now}: G Key General Error - {ex.Message}\n{ex.StackTrace}\n\n");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // Protection de dernier recours
                GTA.UI.Notification.PostTicker("~r~Erreur critique touche G", false);
                
                try
                {
                    System.IO.File.AppendAllText("UrbanLife_G_key_critical.log", 
                        $"{DateTime.Now}: G Key Critical Error - {ex.Message}\n{ex.StackTrace}\n\n");
                }
                catch { }
            }
        }
        
        private void ForceActivateSystem()
        {
            var nearbyPeds = World.GetNearbyPeds(Game.Player.Character.Position, 50.0f);
            int activated = 0;
            
            foreach (var ped in nearbyPeds.Take(10)) // Limiter à 10 pour test
            {
                if (ped != Game.Player.Character && 
                    !smartNPCs.Any(s => s.Ped == ped) &&
                    ped.IsAlive && !ped.IsDead)
                {
                    if (UrbanLifeIntegration.ReserveNPC(ped))
                    {
                        var smartNPC = new SmartNPC(ped, routineManager, noiseManager);
                        smartNPCs.Add(smartNPC);
                        activated++;
                    }
                }
            }
            
            GTA.UI.Notification.PostTicker($"~g~Force: {activated} PNJ activés", false);
            
            // Créer un événement de test
            if (smartNPCs.Count > 0)
            {
                var testNPC = smartNPCs.First();
                TriggerSpecialEvent(testNPC);
                GTA.UI.Notification.PostTicker("~y~Événement spécial déclenché pour test", false);
            }
        }
        
        private void UpdateSmartNPCs()
        {
            // Mettre à jour le gestionnaire de blips
            blipManager.Update();
            
            // Mettre à jour le nouveau gestionnaire d'événements spontanés
            eventManager.Update();
            
            // Mettre à jour le système d'intervention du joueur
            interventionSystem.Update();
            
            // Vérifier les événements spéciaux (garder pour compatibilité mais réduire la fréquence)
            CheckForSpecialEvents();
            
            // Créer de nouveaux PNJ intelligents si nécessaire (réduire pour éviter le conflit)
            if (smartNPCs.Count < maxSmartNPCs)
            {
                CreateSmartNPCs();
            }
            
            // Mettre à jour tous les PNJ intelligents
            foreach (var smartNPC in smartNPCs.ToList())
            {
                try
                {
                    smartNPC.Update();
                }
                catch (Exception ex)
                {
                    GTA.UI.Notification.PostTicker($"~r~Erreur PNJ: {ex.Message}", false);
                    // Libérer le PNJ en cas d'erreur
                    UrbanLifeIntegration.ReleaseNPC(smartNPC.Ped);
                    smartNPCs.Remove(smartNPC);
                }
            }
        }
        
        private void CreateSmartNPCs()
        {
            var nearbyPeds = World.GetNearbyPeds(Game.Player.Character.Position, 100.0f);
            int npcsCounted = 0;
            int npcsCreated = 0;
            
            foreach (var ped in nearbyPeds)
            {
                if (smartNPCs.Count >= maxSmartNPCs) break;
                
                npcsCounted++;
                
                // Vérifier si ce PNJ n'est pas déjà géré
                if (ped != Game.Player.Character && 
                    !smartNPCs.Any(s => s.Ped == ped) &&
                    ShouldBecomeSmartNPC(ped))
                {
                    // Essayer de réserver le PNJ
                    if (UrbanLifeIntegration.ReserveNPC(ped))
                    {
                        var smartNPC = new SmartNPC(ped, routineManager, noiseManager);
                        smartNPCs.Add(smartNPC);
                        npcsCreated++;
                        
                        GTA.UI.Screen.ShowSubtitle($"~g~Nouveau PNJ intelligent créé! ({smartNPCs.Count}/{maxSmartNPCs})", 2000);
                    }
                }
            }
            
            // Message de debug plus visible
            if (npcsCreated > 0)
            {
                GTA.UI.Notification.PostTicker($"~g~UrbanLife: {npcsCreated} nouveaux PNJ activés ({smartNPCs.Count} total)", false);
            }
        }
        
        private bool ShouldBecomeSmartNPC(Ped ped)
        {
            // Conditions pour qu'un PNJ devienne "intelligent"
            if (!ped.IsAlive || ped.IsPlayer || ped.IsDead) return false;
            
            // Utiliser le système d'intégration pour vérifier si le PNJ est disponible
            if (UrbanLifeIntegration.IsNPCBusy(ped)) return false;
            
            // Ne pas affecter les PNJ dans les véhicules la plupart du temps
            if (ped.IsInVehicle() && random.Next(100) < 85) return false;
            
            // Probabilité basée sur le type de PNJ
            PedHash pedHash = (PedHash)ped.Model.Hash;
            
            // PNJ riches ont plus de chances d'avoir des routines complexes mais moins qu'avant
            if (IsWealthyNPC(pedHash)) return random.Next(100) < 40; // Réduit de 80 à 40
            
            // PNJ normaux ont beaucoup moins de chances
            return random.Next(100) < 10; // Réduit de 30 à 10
        }
        
        private bool IsWealthyNPC(PedHash pedHash)
        {
            // Liste des PNJ considérés comme riches
            var wealthyPeds = new[]
            {
                PedHash.Business01AFY, PedHash.Business01AMM, PedHash.Business01AMY,
                PedHash.Business02AFM, PedHash.Business02AFY, PedHash.Business02AMY,
                PedHash.Business03AFY, PedHash.Business03AMY, PedHash.Business04AFY,
                PedHash.Bevhills01AFM, PedHash.Bevhills01AFY, PedHash.Bevhills01AMM,
                PedHash.Bevhills01AMY, PedHash.Bevhills02AFM, PedHash.Bevhills02AFY,
                PedHash.Bevhills02AMM, PedHash.Bevhills02AMY
            };
            
            return wealthyPeds.Contains(pedHash);
        }
        
        private void CheckForSpecialEvents()
        {
            // Réduire drastiquement la fréquence des événements SmartNPC pour éviter le conflit
            // avec les nouveaux événements spontanés
            var playerPos = Game.Player.Character.Position;
            var nearbySmartNPCs = smartNPCs.Where(npc => 
                npc.Ped.Position.DistanceTo(playerPos) <= 100.0f && 
                npc.CanTriggerSpecialEvent()).ToList();

            foreach (var smartNPC in nearbySmartNPCs)
            {
                // Probabilité très faible maintenant que nous avons les événements spontanés
                if (random.NextDouble() < 0.0001f) // 0.01% de chance (réduit de 0.05%)
                {
                    TriggerSpecialEvent(smartNPC);
                }
            }
        }
        
        private void TriggerSpecialEvent(SmartNPC smartNPC)
        {
            // Vérifier que le SmartNPC est bien proche du joueur
            float distanceToPlayer = smartNPC.Ped.Position.DistanceTo(Game.Player.Character.Position);
            if (distanceToPlayer > 100.0f)
            {
                return; // Ne pas créer d'événement si trop loin
            }

            // Favoriser les événements les plus visibles/intéressants
            var eventType = random.Next(100) switch
            {
                < 40 => SpecialEventType.Robbery,   // 40% - Plus visible
                < 60 => SpecialEventType.Fight,     // 20% - Très visible
                < 75 => SpecialEventType.Accident,  // 15% - Visible
                < 90 => SpecialEventType.Medical,   // 15% - Modérément visible
                _ => SpecialEventType.Fire           // 10% - Très visible
            };
            
            switch (eventType)
            {
                case SpecialEventType.Robbery:
                    CreateRobberyEvent(smartNPC);
                    break;
                case SpecialEventType.Accident:
                    CreateAccidentEvent(smartNPC);
                    break;
                case SpecialEventType.Fight:
                    CreateFightEvent(smartNPC);
                    break;
                case SpecialEventType.Medical:
                    CreateMedicalEvent(smartNPC);
                    break;
                case SpecialEventType.Fire:
                    CreateFireEvent(smartNPC);
                    break;
            }
        }
        
        private void CreateRobberyEvent(SmartNPC victim)
        {
            // Créer un événement d'agression réaliste
            var robberModel = new Model(PedHash.StrPunk01GMY);
            robberModel.Request(5000);
            
            if (robberModel.IsLoaded)
            {
                // Créer le voleur pas trop loin de la victime mais visible
                var robberPos = victim.Ped.Position.Around(random.Next(3, 8)); // Entre 3 et 8 mètres
                var robber = World.CreatePed(robberModel, robberPos);
                
                if (robber != null)
                {
                    // Configuration du voleur
                    robber.IsPersistent = true;
                    robber.BlockPermanentEvents = true;
                    robber.Weapons.Give(WeaponHash.Knife, 1, false, true);
                    
                    // Configuration de la victime
                    victim.Ped.BlockPermanentEvents = true;
                    
                    // Séquence d'agression réaliste
                    robber.Task.RunTo(victim.Ped.Position);
                    
                    // Programmer les actions suivantes
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(2000); // Attendre 2 secondes
                        
                        if (robber?.Exists() == true && victim?.Ped?.Exists() == true)
                        {
                            // Le voleur menace la victime
                            robber.Task.AimGunAtEntity(victim.Ped, 3000);
                            
                            await System.Threading.Tasks.Task.Delay(1000);
                            
                            // La victime lève les mains
                            victim.Ped.Task.HandsUp(5000);
                            
                            await System.Threading.Tasks.Task.Delay(3000);
                            
                            // Le voleur s'enfuit
                            if (robber?.Exists() == true)
                            {
                                var escapePos = robber.Position + Vector3.RandomXY() * 30.0f;
                                robber.Task.RunTo(escapePos);
                                
                                // Nettoyer après 10 secondes
                                await System.Threading.Tasks.Task.Delay(10000);
                                if (robber?.Exists() == true)
                                {
                                    robber.MarkAsNoLongerNeeded();
                                }
                            }
                        }
                    });
                    
                    // Ajouter un blip sur la mini-map
                    blipManager.AddEventBlip(SpecialEventType.Robbery, victim.Ped.Position, 
                        $"Agression en cours ! Distance: {victim.Ped.Position.DistanceTo(Game.Player.Character.Position):F0}m");
                    
                    // Marquer l'événement
                    victim.SetSpecialEvent(SpecialEventType.Robbery, robber);
                    
                    GTA.UI.Notification.PostTicker($"~r~Agression signalée à {victim.Ped.Position.DistanceTo(Game.Player.Character.Position):F0}m de votre position!", false);
                }
            }
        }
        
        private void CreateAccidentEvent(SmartNPC npc)
        {
            // Simulation d'un accident (chute, malaise, etc.) plus réaliste
            npc.Ped.BlockPermanentEvents = true;
            
            // Animation d'accident plus visible
            Function.Call(Hash.TASK_PLAY_ANIM, npc.Ped, "dam_ko", "ko_shot_front", 8.0f, -1, false, 0.0f);
            
            // Ajouter un blip sur la mini-map avec distance
            float distance = npc.Ped.Position.DistanceTo(Game.Player.Character.Position);
            blipManager.AddEventBlip(SpecialEventType.Accident, npc.Ped.Position, 
                $"Personne blessée - Distance: {distance:F0}m");
            
            npc.SetSpecialEvent(SpecialEventType.Accident, null);
            
            GTA.UI.Notification.PostTicker($"~y~Accident signalé à {distance:F0}m de votre position!", false);
        }
        
        private void CreateFightEvent(SmartNPC npc1)
        {
            // Trouver un autre PNJ proche pour une bagarre plus réaliste
            var nearbySmartNPCs = smartNPCs.Where(n => 
                n != npc1 && 
                n.Ped.Position.DistanceTo(npc1.Ped.Position) < 15.0f && // Augmenté à 15m
                !n.CurrentSpecialEvent.HasValue).ToList(); // Pas déjà en événement
            
            if (nearbySmartNPCs.Any())
            {
                var npc2 = nearbySmartNPCs.First();
                
                // Configuration des combattants
                npc1.Ped.BlockPermanentEvents = true;
                npc2.Ped.BlockPermanentEvents = true;
                
                // Animation de provocation avant la bagarre
                npc1.Ped.Task.TurnTo(npc2.Ped);
                npc2.Ped.Task.TurnTo(npc1.Ped);
                
                // Programmer la bagarre avec un délai
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(1500); // Tension
                    
                    if (npc1?.Ped?.Exists() == true && npc2?.Ped?.Exists() == true)
                    {
                        // Les deux PNJ se battent
                        npc1.Ped.Task.Combat(npc2.Ped);
                        npc2.Ped.Task.Combat(npc1.Ped);
                    }
                });
                
                // Ajouter un blip sur la mini-map avec distance
                float distance = npc1.Ped.Position.DistanceTo(Game.Player.Character.Position);
                blipManager.AddEventBlip(SpecialEventType.Fight, npc1.Ped.Position, 
                    $"Bagarre de rue - Distance: {distance:F0}m");
                
                npc1.SetSpecialEvent(SpecialEventType.Fight, npc2.Ped);
                npc2.SetSpecialEvent(SpecialEventType.Fight, npc1.Ped);
                
                GTA.UI.Notification.PostTicker($"~o~Bagarre signalée à {distance:F0}m de votre position!", false);
            }
        }
        
        private void CreateFireEvent(SmartNPC npc)
        {
            // Créer un petit feu près du PNJ mais visible
            var firePos = npc.Ped.Position + Vector3.RandomXY() * 5.0f; // Plus proche
            
            // Créer un feu réel dans le jeu
            Function.Call(Hash.START_SCRIPT_FIRE, firePos.X, firePos.Y, firePos.Z, 25, true);
            
            // Le PNJ panique et s'enfuit de manière visible
            npc.Ped.BlockPermanentEvents = true;
            npc.Ped.Task.ReactAndFlee(npc.Ped); // Réaction de panique
            
            // Programmer la fuite
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1000);
                
                if (npc?.Ped?.Exists() == true)
                {
                    // Créer un PNJ temporaire pour la fuite ou utiliser Function.Call
                    Function.Call(Hash.TASK_SMART_FLEE_PED, npc.Ped, Game.Player.Character, 200.0f, 10000);
                }
            });
            
            // Ajouter un blip sur la mini-map avec distance
            float distance = firePos.DistanceTo(Game.Player.Character.Position);
            blipManager.AddEventBlip(SpecialEventType.Fire, firePos, 
                $"Incendie signalé - Distance: {distance:F0}m");
            
            npc.SetSpecialEvent(SpecialEventType.Fire, null);
            
            GTA.UI.Notification.PostTicker($"~o~Incendie signalé à {distance:F0}m de votre position!", false);
        }
        
        private void CreateMedicalEvent(SmartNPC npc)
        {
            // Le PNJ tombe et a besoin d'aide médicale de manière plus visible
            npc.Ped.BlockPermanentEvents = true;
            
            // Animation de malaise plus réaliste
            Function.Call(Hash.TASK_PLAY_ANIM, npc.Ped, "move_injured_generic", "idle", 8.0f, -1, false, 0.0f);
            
            // Programmer des gémissements/cris pour attirer l'attention
            System.Threading.Tasks.Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    await System.Threading.Tasks.Task.Delay(3000);
                    
                    if (npc?.Ped?.Exists() == true)
                    {
                        // Cris de douleur
                        Function.Call(Hash.PLAY_PAIN, npc.Ped, 6, 0, 0);
                    }
                }
            });
            
            // Ajouter un blip sur la mini-map avec distance
            float distance = npc.Ped.Position.DistanceTo(Game.Player.Character.Position);
            blipManager.AddEventBlip(SpecialEventType.Medical, npc.Ped.Position, 
                $"Urgence médicale - Distance: {distance:F0}m");
            
            npc.SetSpecialEvent(SpecialEventType.Medical, null);
            
            GTA.UI.Notification.PostTicker($"~w~Urgence médicale signalée à {distance:F0}m de votre position!", false);
        }
        
        private void ShowDebugInfo()
        {
            var playerPos = Game.Player.Character.Position;
            var currentHour = DateTime.Now.Hour;
            var activeEvents = eventManager.GetActiveEvents();
            var nearestEvent = eventManager.GetNearestEvent(playerPos);
            var interventionScore = interventionSystem.GetInterventionScore();
            
            // Informations sur les événements routiers
            var activeRoadEvents = roadEventManager.GetActiveEvents();
            
            GTA.UI.Notification.PostTicker($"~w~SmartNPCs: {smartNPCs.Count}/{maxSmartNPCs}", false);
            GTA.UI.Notification.PostTicker($"~w~Événements spontanés: {activeEvents.Count}", false);
            GTA.UI.Notification.PostTicker($"~c~Événements routiers: {activeRoadEvents.Count}/3", false);
            GTA.UI.Notification.PostTicker($"~w~Score d'intervention: {interventionScore}", false);
            GTA.UI.Notification.PostTicker($"~w~Heure: {currentHour}h (Nuit: {IsNightTime(currentHour)})", false);
            
            // Informations sur le joueur et sa position pour les événements routiers
            var player = Game.Player.Character;
            if (player?.CurrentVehicle != null)
            {
                GTA.UI.Notification.PostTicker($"~g~Dans véhicule: {player.CurrentVehicle.LocalizedName}", false);
                GTA.UI.Notification.PostTicker($"~g~Vitesse: {player.CurrentVehicle.Speed * 3.6f:F0} km/h", false);
            }
            else
            {
                GTA.UI.Notification.PostTicker("~y~À pied - pas d'événements routiers", false);
            }
            
            // Afficher le plus proche événement routier
            if (activeRoadEvents.Count > 0)
            {
                var nearestRoadEvent = activeRoadEvents
                    .OrderBy(re => re.Position.DistanceTo(playerPos))
                    .First();
                var distance = nearestRoadEvent.Position.DistanceTo(playerPos);
                GTA.UI.Notification.PostTicker($"~c~Événement routier proche: {nearestRoadEvent.Type} à {distance:F0}m", false);
            }
            
            if (nearestEvent != null)
            {
                var distance = nearestEvent.Position.DistanceTo(playerPos);
                GTA.UI.Notification.PostTicker($"~y~Événement proche: {nearestEvent.Type} à {distance:F0}m", false);
                
                // Afficher les conseils d'intervention
                ShowInterventionTips(nearestEvent);
            }
            
            // Instructions pour les nouvelles touches
            GTA.UI.Notification.PostTicker("~b~F7: Événement routier | F10: Événement spontané | F11: Debug", false);
        }
        
        private void ShowInterventionTips(SpontaneousEvent eventObj)
        {
            if (eventObj.Position.DistanceTo(Game.Player.Character.Position) < 30.0f)
            {
                string tip = eventObj.Type switch
                {
                    SpontaneousEventType.Mugging => "~g~Conseil: Courez vers la victime ou tirez pour arrêter l'agression!",
                    SpontaneousEventType.StreetFight => "~g~Conseil: Courez vers les combattants pour les séparer!",
                    SpontaneousEventType.DrugDeal => "~g~Conseil: Klaxonnez ou approchez-vous pour interrompre le deal!",
                    SpontaneousEventType.Argument => "~g~Conseil: Approchez-vous pour calmer la situation!",
                    _ => ""
                };
                
                if (!string.IsNullOrEmpty(tip))
                {
                    GTA.UI.Notification.PostTicker(tip, false);
                }
            }
        }
        
        private bool IsNightTime(int hour)
        {
            return hour >= 20 || hour <= 5;
        }

        private void ForceCreateNearbyEvent()
        {
            // Trouver les SmartNPCs les plus proches du joueur
            var playerPos = Game.Player.Character.Position;
            var nearestSmartNPC = smartNPCs
                .Where(npc => npc.Ped.Position.DistanceTo(playerPos) <= 50.0f && 
                             !npc.CurrentSpecialEvent.HasValue)
                .OrderBy(npc => npc.Ped.Position.DistanceTo(playerPos))
                .FirstOrDefault();

            if (nearestSmartNPC != null)
            {
                TriggerSpecialEvent(nearestSmartNPC);
                GTA.UI.Notification.PostTicker($"~g~Événement forcé créé à {nearestSmartNPC.Ped.Position.DistanceTo(playerPos):F0}m!", false);
            }
            else
            {
                // Si pas de SmartNPC proche, en créer un
                var nearbyPeds = World.GetNearbyPeds(playerPos, 30.0f);
                var availablePed = nearbyPeds
                    .Where(ped => ped != Game.Player.Character && 
                                 ped.IsAlive && 
                                 !ped.IsPlayer &&
                                 !smartNPCs.Any(s => s.Ped == ped))
                    .OrderBy(ped => ped.Position.DistanceTo(playerPos))
                    .FirstOrDefault();

                if (availablePed != null && UrbanLifeIntegration.ReserveNPC(availablePed))
                {
                    var newSmartNPC = new SmartNPC(availablePed, routineManager, noiseManager);
                    smartNPCs.Add(newSmartNPC);
                    
                    TriggerSpecialEvent(newSmartNPC);
                    GTA.UI.Notification.PostTicker($"~g~Nouveau SmartNPC créé et événement forcé à {availablePed.Position.DistanceTo(playerPos):F0}m!", false);
                }
                else
                {
                    GTA.UI.Notification.PostTicker("~r~Aucun PNJ disponible à proximité pour créer un événement!", false);
                }
            }
        }
        
        private void ForceCreateSpontaneousEventSafe()
        {
            try
            {
                // Vérifier qu'on n'a pas trop d'événements
                var activeEvents = eventManager.GetActiveEvents();
                if (activeEvents.Count >= 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Trop d'événements actifs! Attendez qu'ils se terminent.", false);
                    return;
                }

                // Créer un événement aléatoire de manière sécurisée
                var eventTypes = new SpontaneousEventType[] {
                    SpontaneousEventType.Argument, // Commencer par le moins dangereux
                    SpontaneousEventType.DrugDeal,
                    SpontaneousEventType.StreetFight,
                    SpontaneousEventType.Mugging
                };
                var randomType = eventTypes[random.Next(eventTypes.Length)];
                
                // Position sécurisée près du joueur
                var playerPos = Game.Player.Character.Position;
                var safeDistance = 25.0f; // Distance fixe et sûre
                var direction = Vector3.RandomXY();
                var eventPos = playerPos + direction * safeDistance;
                
                eventManager.ForceCreateEvent(randomType, eventPos);
                
                string eventName = randomType switch
                {
                    SpontaneousEventType.StreetFight => "Bagarre de rue",
                    SpontaneousEventType.Mugging => "Agression",
                    SpontaneousEventType.DrugDeal => "Deal de drogue",
                    SpontaneousEventType.Argument => "Dispute",
                    _ => "Événement"
                };
                
                GTA.UI.Notification.PostTicker($"~g~{eventName} créé à {safeDistance}m!", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création événement: {ex.Message}", false);
            }
        }
        
        private void ForceCreateMuggingSafe()
        {
            try
            {
                // Vérification 1: Pas trop d'événements actifs
                var activeEvents = eventManager.GetActiveEvents();
                if (activeEvents.Count >= 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Trop d'événements actifs! Attendez qu'ils se terminent.", false);
                    return;
                }

                // Vérification 2: Le joueur n'est pas en véhicule (peut causer des problèmes)
                if (Game.Player.Character.IsInVehicle())
                {
                    GTA.UI.Notification.PostTicker("~y~Sortez du véhicule avant de créer une agression!", false);
                    return;
                }

                // Vérification 3: Position sécurisée
                var playerPos = Game.Player.Character.Position;
                
                // Éviter les zones d'eau ou positions dangereuses
                if (playerPos.Z < 0.0f) // Sous l'eau
                {
                    GTA.UI.Notification.PostTicker("~r~Position dangereuse - impossible de créer une agression ici!", false);
                    return;
                }
                
                // Choisir une position sûre à une distance fixe et sécurisée
                var safeDirection = Vector3.RandomXY().Normalized;
                var eventPos = playerPos + (safeDirection * 35.0f); // Distance fixe de 35m
                
                // Vérifier que la position finale est sur le sol
                float groundZ;
                if (World.GetGroundHeight(eventPos, out groundZ))
                {
                    eventPos.Z = groundZ + 1.0f;
                }
                else
                {
                    GTA.UI.Notification.PostTicker("~r~Impossible de trouver une position sûre pour l'agression!", false);
                    return;
                }
                
                // Créer l'événement avec la position sécurisée
                eventManager.ForceCreateEvent(SpontaneousEventType.Mugging, eventPos);
                GTA.UI.Notification.PostTicker("~g~Agression créée en sécurité à 35m!", false);
                
                // Délai pour éviter les créations multiples accidentelles
                System.Threading.Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur sécurisée création agression: {ex.Message}", false);
            }
        }
        
        private void ForceCreateFightSafe()
        {
            try
            {
                var activeEvents = eventManager.GetActiveEvents();
                if (activeEvents.Count >= 2)
                {
                    GTA.UI.Notification.PostTicker("~r~Trop d'événements actifs! Attendez qu'ils se terminent.", false);
                    return;
                }

                var playerPos = Game.Player.Character.Position;
                var eventPos = playerPos + Vector3.RandomXY() * 25.0f;
                
                eventManager.ForceCreateEvent(SpontaneousEventType.StreetFight, eventPos);
                GTA.UI.Notification.PostTicker("~o~Bagarre créée à 25m!", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~Erreur création bagarre: {ex.Message}", false);
            }
        }
    }
} 