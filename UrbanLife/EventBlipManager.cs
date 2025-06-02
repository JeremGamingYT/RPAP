using GTA;
using GTA.Math;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.UrbanLife
{
    /// <summary>
    /// Gestionnaire pour afficher les événements spéciaux sur la mini-map
    /// </summary>
    public class EventBlipManager
    {
        private static EventBlipManager? instance;
        private readonly Dictionary<string, EventBlip> activeBlips;
        private readonly Random random;

        public static EventBlipManager Instance => instance ??= new EventBlipManager();

        private EventBlipManager()
        {
            activeBlips = new Dictionary<string, EventBlip>();
            random = new Random();
        }

        /// <summary>
        /// Ajoute un blip pour un événement spécial
        /// </summary>
        public void AddEventBlip(SpecialEventType eventType, Vector3 position, string? customDescription = null)
        {
            string eventId = $"{eventType}_{position}_{DateTime.Now.Ticks}";
            
            if (activeBlips.ContainsKey(eventId))
                return;

            var blipInfo = GetBlipInfoForEvent(eventType);
            Blip blip = World.CreateBlip(position);
            
            if (blip != null)
            {
                blip.Sprite = blipInfo.Sprite;
                blip.Color = blipInfo.Color;
                blip.Scale = blipInfo.Scale;
                blip.IsShortRange = blipInfo.IsShortRange;
                blip.Name = customDescription ?? blipInfo.DefaultName;
                
                // Faire clignoter certains types d'événements
                if (blipInfo.ShouldBlink)
                {
                    blip.IsFlashing = true;
                }

                var eventBlip = new EventBlip
                {
                    Blip = blip,
                    EventType = eventType,
                    Position = position,
                    CreationTime = DateTime.Now,
                    Duration = blipInfo.Duration
                };

                activeBlips[eventId] = eventBlip;
                
                // Notification au joueur
                ShowEventNotification(eventType, position);
            }
        }

        /// <summary>
        /// Met à jour tous les blips actifs
        /// </summary>
        public void Update()
        {
            var expiredBlips = new List<string>();
            
            foreach (var kvp in activeBlips)
            {
                var eventBlip = kvp.Value;
                
                // Vérifier si le blip a expiré
                if (DateTime.Now - eventBlip.CreationTime > eventBlip.Duration)
                {
                    expiredBlips.Add(kvp.Key);
                    continue;
                }
                
                // Vérifier si le blip existe toujours
                if (eventBlip.Blip == null || !eventBlip.Blip.Exists())
                {
                    expiredBlips.Add(kvp.Key);
                    continue;
                }
                
                // Mise à jour de l'apparence selon l'âge de l'événement
                UpdateBlipAppearance(eventBlip);
            }
            
            // Supprimer les blips expirés
            foreach (string expiredId in expiredBlips)
            {
                RemoveEventBlip(expiredId);
            }
        }

        /// <summary>
        /// Supprime un blip d'événement
        /// </summary>
        public void RemoveEventBlip(string eventId)
        {
            if (activeBlips.TryGetValue(eventId, out EventBlip? eventBlip))
            {
                if (eventBlip.Blip?.Exists() == true)
                {
                    eventBlip.Blip.Delete();
                }
                activeBlips.Remove(eventId);
            }
        }

        /// <summary>
        /// Supprime tous les blips d'un type d'événement spécifique
        /// </summary>
        public void RemoveEventBlipsByType(SpecialEventType eventType)
        {
            var blipsToRemove = activeBlips
                .Where(kvp => kvp.Value.EventType == eventType)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (string eventId in blipsToRemove)
            {
                RemoveEventBlip(eventId);
            }
        }

        /// <summary>
        /// Supprime tous les blips actifs
        /// </summary>
        public void ClearAllBlips()
        {
            foreach (var eventBlip in activeBlips.Values)
            {
                if (eventBlip.Blip?.Exists() == true)
                {
                    eventBlip.Blip.Delete();
                }
            }
            activeBlips.Clear();
        }

        /// <summary>
        /// Obtient les informations de blip pour un type d'événement
        /// </summary>
        private BlipInfo GetBlipInfoForEvent(SpecialEventType eventType)
        {
            return eventType switch
            {
                SpecialEventType.Robbery => new BlipInfo
                {
                    Sprite = BlipSprite.ArmoredTruck,
                    Color = BlipColor.Red,
                    Scale = 0.8f,
                    IsShortRange = false,
                    DefaultName = "Agression en cours",
                    ShouldBlink = true,
                    Duration = TimeSpan.FromMinutes(3)
                },
                SpecialEventType.Accident => new BlipInfo
                {
                    Sprite = BlipSprite.Devin,
                    Color = BlipColor.Orange,
                    Scale = 0.7f,
                    IsShortRange = false,
                    DefaultName = "Accident",
                    ShouldBlink = false,
                    Duration = TimeSpan.FromMinutes(2)
                },
                SpecialEventType.Fight => new BlipInfo
                {
                    Sprite = BlipSprite.GTAOMission,
                    Color = BlipColor.Red2,
                    Scale = 0.7f,
                    IsShortRange = false,
                    DefaultName = "Bagarre",
                    ShouldBlink = true,
                    Duration = TimeSpan.FromMinutes(2)
                },
                SpecialEventType.Medical => new BlipInfo
                {
                    Sprite = BlipSprite.Hospital,
                    Color = BlipColor.White,
                    Scale = 0.8f,
                    IsShortRange = false,
                    DefaultName = "Urgence médicale",
                    ShouldBlink = true,
                    Duration = TimeSpan.FromMinutes(4)
                },
                SpecialEventType.Fire => new BlipInfo
                {
                    Sprite = BlipSprite.Devin,
                    Color = BlipColor.Orange,
                    Scale = 0.8f,
                    IsShortRange = false,
                    DefaultName = "Incendie",
                    ShouldBlink = true,
                    Duration = TimeSpan.FromMinutes(5)
                },
                _ => new BlipInfo
                {
                    Sprite = BlipSprite.Standard,
                    Color = BlipColor.Yellow,
                    Scale = 0.6f,
                    IsShortRange = true,
                    DefaultName = "Événement",
                    ShouldBlink = false,
                    Duration = TimeSpan.FromMinutes(1)
                }
            };
        }

        /// <summary>
        /// Met à jour l'apparence d'un blip selon son âge
        /// </summary>
        private void UpdateBlipAppearance(EventBlip eventBlip)
        {
            if (eventBlip.Blip == null || !eventBlip.Blip.Exists())
                return;

            var age = DateTime.Now - eventBlip.CreationTime;
            var progress = age.TotalSeconds / eventBlip.Duration.TotalSeconds;
            
            // Faire disparaître graduellement le blip
            if (progress > 0.7f)
            {
                eventBlip.Blip.Alpha = (int)(255 * (1.0f - ((progress - 0.7f) / 0.3f)));
            }
            
            // Arrêter le clignotement pour certains événements anciens
            if (progress > 0.5f && eventBlip.Blip.IsFlashing)
            {
                eventBlip.Blip.IsFlashing = false;
            }
        }

        /// <summary>
        /// Affiche une notification pour un événement
        /// </summary>
        private void ShowEventNotification(SpecialEventType eventType, Vector3 position)
        {
            string message = eventType switch
            {
                SpecialEventType.Robbery => "~r~Agression signalée ! Vous pouvez intervenir...",
                SpecialEventType.Accident => "~y~Un accident vient de se produire !",
                SpecialEventType.Fight => "~o~Bagarre en cours dans le secteur !",
                SpecialEventType.Medical => "~w~Urgence médicale signalée !",
                SpecialEventType.Fire => "~o~Incendie détecté !",
                _ => "~b~Événement signalé dans la zone."
            };

            // Utiliser la méthode non obsolète
            GTA.UI.Notification.PostTicker(message, false);
            
            // Calculer la distance approximative
            float distance = Game.Player.Character.Position.DistanceTo(position);
            if (distance > 50f)
            {
                GTA.UI.Notification.PostTicker($"~b~Distance estimée: {distance:F0}m", false);
            }
        }

        /// <summary>
        /// Obtient le nombre d'événements actifs
        /// </summary>
        public int GetActiveEventCount() => activeBlips.Count;

        /// <summary>
        /// Obtient tous les événements actifs d'un type spécifique
        /// </summary>
        public IEnumerable<EventBlip> GetEventsByType(SpecialEventType eventType)
        {
            return activeBlips.Values.Where(eb => eb.EventType == eventType);
        }
    }

    /// <summary>
    /// Informations pour configurer un blip d'événement
    /// </summary>
    internal class BlipInfo
    {
        public BlipSprite Sprite { get; set; }
        public BlipColor Color { get; set; }
        public float Scale { get; set; }
        public bool IsShortRange { get; set; }
        public string DefaultName { get; set; } = string.Empty;
        public bool ShouldBlink { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Représente un blip d'événement actif
    /// </summary>
    public class EventBlip
    {
        public Blip? Blip { get; set; }
        public SpecialEventType EventType { get; set; }
        public Vector3 Position { get; set; }
        public DateTime CreationTime { get; set; }
        public TimeSpan Duration { get; set; }
    }
} 