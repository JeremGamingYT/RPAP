using GTA;
using System;
using REALIS.TrafficAI;
using REALIS.UrbanLife;
using RPAP;

namespace REALIS.Common
{
    /// <summary>
    /// Coordinateur centralisé et robuste pour tous les modules REALIS.
    /// Utilise la nouvelle architecture event-driven pour éviter les conflits.
    /// </summary>
    public class ScriptCoordinator : Script
    {
        public ScriptCoordinator()
        {
            GTA.UI.Notification.PostTicker("~g~[REALIS] Démarrage de l'architecture centralisée...", false);
            
            try
            {
                // Configure les événements
                SetupEventHandlers();

                Tick += OnTick;
                Interval = 2000; // Coordonne toutes les 2 secondes

                GTA.UI.Notification.PostTicker("~g~[REALIS] Architecture centralisée initialisée avec succès!", false);
            }
            catch (Exception ex)
            {
                GTA.UI.Notification.PostTicker($"~r~[REALIS] Erreur d'initialisation: {ex.Message}", false);
            }
        }

        private void SetupEventHandlers()
        {
            try
            {
                // Attendre que le gestionnaire central soit disponible
                if (CentralEventManager.Instance == null)
                    return;
                
                // Configure la communication entre les systèmes via les événements
                CentralEventManager.Instance.RegisterHandler(REALISEventType.Collision, new CollisionEventLogger());
                CentralEventManager.Instance.RegisterHandler(REALISEventType.TrafficBlock, new TrafficEventLogger());
                CentralEventManager.Instance.RegisterHandler(REALISEventType.PoliceCall, new PoliceCallLogger());
            }
            catch (Exception ex)
            {
                SafeLogError($"Event handler setup error: {ex.Message}");
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                // Monitoring et coordination des systèmes
                MonitorSystemHealth();
                CoordinateSystemInteractions();
            }
            catch (Exception ex)
            {
                SafeLogError($"Coordinator error: {ex.Message}");
            }
        }

        private void MonitorSystemHealth()
        {
            try
            {
                // Vérifie l'état du gestionnaire central
                if (CentralEventManager.Instance == null)
                {
                    GTA.UI.Notification.PostTicker("~r~[REALIS] Gestionnaire central non disponible!", false);
                    return;
                }

                // Statistiques de base (peut être étendues)
                var player = Game.Player.Character;
                if (player?.CurrentVehicle != null)
                {
                    var vehicleState = CentralEventManager.Instance.GetVehicleState(player.CurrentVehicle.Handle);
                    if (vehicleState.LockedBy != null)
                    {
                        // Le véhicule du joueur est verrouillé par un système, ce qui pourrait indiquer un problème
                        vehicleState.LockedBy = null; // Libère le véhicule du joueur
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogError($"Health monitoring error: {ex.Message}");
            }
        }

        private void CoordinateSystemInteractions()
        {
            try
            {
                // Coordination intelligente entre les systèmes
                // Par exemple, si un incident est en cours, réduire l'agressivité du trafic AI
                
                // Cette logique peut être étendue selon les besoins
            }
            catch (Exception ex)
            {
                SafeLogError($"System coordination error: {ex.Message}");
            }
        }

        private void SafeLogError(string message)
        {
            try
            {
                GTA.UI.Notification.PostTicker($"~r~[REALIS Coordinator] {message}", false);
            }
            catch
            {
                // Ignore silencieusement si le logging échoue
            }
        }

        public void Dispose()
        {
            try
            {
                GTA.UI.Notification.PostTicker("~y~[REALIS] Arrêt de l'architecture centralisée...", false);
                
                // Nettoyage minimal - les scripts se nettoient automatiquement
                GTA.UI.Notification.PostTicker("~g~[REALIS] Arrêt terminé avec succès!", false);
            }
            catch (Exception ex)
            {
                SafeLogError($"Disposal error: {ex.Message}");
            }
        }
    }

    #region Event Handlers pour Logging et Coordination

    /// <summary>
    /// Gestionnaire d'événements pour logger les collisions
    /// </summary>
    internal class CollisionEventLogger : IEventHandler
    {
        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent is CollisionEvent;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                if (gameEvent is CollisionEvent collision)
                {
                    var severity = collision.Severity == CollisionSeverity.Severe ? "~r~Grave" :
                                  collision.Severity == CollisionSeverity.Moderate ? "~o~Modérée" : "~y~Légère";
                    
                    GTA.UI.Notification.PostTicker($"~b~[Collision] {severity}~w~ détectée", false);
                }
            }
            catch
            {
                // Ignore les erreurs de logging
            }
        }
    }

    /// <summary>
    /// Gestionnaire d'événements pour logger les blocages de trafic
    /// </summary>
    internal class TrafficEventLogger : IEventHandler
    {
        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent is TrafficBlockEvent;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                if (gameEvent is TrafficBlockEvent traffic)
                {
                    var isPlayerBlocking = traffic.Data.ContainsKey("IsPlayerBlocking") && 
                                         (bool)traffic.Data["IsPlayerBlocking"];
                    
                    if (isPlayerBlocking)
                    {
                        GTA.UI.Notification.PostTicker("~o~[Trafic] Vous bloquez la circulation", false);
                    }
                }
            }
            catch
            {
                // Ignore les erreurs de logging
            }
        }
    }

    /// <summary>
    /// Gestionnaire d'événements pour logger les appels de police
    /// </summary>
    internal class PoliceCallLogger : IEventHandler
    {
        public bool CanHandle(GameEvent gameEvent)
        {
            return gameEvent.Type == REALISEventType.PoliceCall;
        }

        public void Handle(GameEvent gameEvent)
        {
            try
            {
                GTA.UI.Notification.PostTicker("~r~[Police] Incident signalé aux autorités", false);
            }
            catch
            {
                // Ignore les erreurs de logging
            }
        }
    }

    #endregion
}
