using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace REALIS.Common
{
    /// <summary>
    /// Gestionnaire central d'événements pour coordonner tous les modules et éviter les conflits.
    /// Architecture event-driven qui permet une communication sécurisée entre modules.
    /// </summary>
    public sealed class CentralEventManager : Script
    {
        // Instance statique pour accès global - gérée par ScriptHookVDotNet
        private static CentralEventManager? _instance;
        public static CentralEventManager? Instance => _instance;

        // Event System
        private readonly ConcurrentDictionary<REALISEventType, List<IEventHandler>> _eventHandlers = new();
        private readonly ConcurrentQueue<GameEvent> _eventQueue = new();
        private readonly Dictionary<int, VehicleState> _vehicleStates = new();
        private readonly HashSet<int> _lockedVehicles = new();
        private readonly Dictionary<int, DateTime> _lastEventTime = new();

        // Configuration
        private const float CLEANUP_INTERVAL = 15f; // secondes
        private const float MAX_EVENT_AGE = 30f; // secondes
        private const int MAX_EVENTS_PER_TICK = 10;
        private DateTime _lastCleanup = DateTime.Now;

        // Constructeur public par défaut pour ScriptHookVDotNet
        public CentralEventManager()
        {
            // Définit l'instance statique lors de l'instantiation par ScriptHookVDotNet
            _instance = this;
            
            Tick += OnTick;
            Interval = 100; // 10 FPS pour réactivité
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                ProcessEventQueue();
                UpdateVehicleStates();
                PeriodicCleanup();
            }
            catch (Exception ex)
            {
                SafeLogError($"CentralEventManager error: {ex.Message}");
            }
        }

        #region Event System
        
        public void RegisterHandler(REALISEventType eventType, IEventHandler handler)
        {
            if (!_eventHandlers.ContainsKey(eventType))
                _eventHandlers[eventType] = new List<IEventHandler>();
            
            if (!_eventHandlers[eventType].Contains(handler))
                _eventHandlers[eventType].Add(handler);
        }

        public void UnregisterHandler(REALISEventType eventType, IEventHandler handler)
        {
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
                handlers.Remove(handler);
        }

        public void FireEvent(GameEvent gameEvent)
        {
            if (gameEvent == null) return;
            
            gameEvent.Timestamp = DateTime.Now;
            _eventQueue.Enqueue(gameEvent);
        }

        private void ProcessEventQueue()
        {
            int processed = 0;
            while (_eventQueue.TryDequeue(out var gameEvent) && processed < MAX_EVENTS_PER_TICK)
            {
                if (IsEventTooOld(gameEvent)) continue;
                
                ProcessEvent(gameEvent);
                processed++;
            }
        }

        private void ProcessEvent(GameEvent gameEvent)
        {
            if (!_eventHandlers.TryGetValue(gameEvent.Type, out var handlers)) return;

            foreach (var handler in handlers.ToList()) // Copie pour éviter modifications concurrentes
            {
                try
                {
                    if (handler.CanHandle(gameEvent))
                        handler.Handle(gameEvent);
                }
                catch (Exception ex)
                {
                    SafeLogError($"Handler error for {gameEvent.Type}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Vehicle Management

        public bool TryLockVehicle(int vehicleHandle, string requesterId)
        {
            if (_lockedVehicles.Contains(vehicleHandle)) return false;
            
            _lockedVehicles.Add(vehicleHandle);
            UpdateVehicleState(vehicleHandle, vs => vs.LockedBy = requesterId);
            return true;
        }

        public void UnlockVehicle(int vehicleHandle)
        {
            _lockedVehicles.Remove(vehicleHandle);
            UpdateVehicleState(vehicleHandle, vs => vs.LockedBy = null);
        }

        public bool IsVehicleLocked(int vehicleHandle) => _lockedVehicles.Contains(vehicleHandle);

        public VehicleState GetVehicleState(int vehicleHandle)
        {
            if (!_vehicleStates.TryGetValue(vehicleHandle, out var state))
            {
                state = new VehicleState { Handle = vehicleHandle };
                _vehicleStates[vehicleHandle] = state;
            }
            return state;
        }

        private void UpdateVehicleState(int vehicleHandle, Action<VehicleState> updateAction)
        {
            var state = GetVehicleState(vehicleHandle);
            updateAction(state);
            state.LastUpdate = DateTime.Now;
        }

        private void UpdateVehicleStates()
        {
            var vehiclesToRemove = new List<int>();

            foreach (var kvp in _vehicleStates.ToList())
            {
                try
                {
                    var vehicle = World.GetAllVehicles().FirstOrDefault(v => v.Handle == kvp.Key);
                    if (vehicle == null || !vehicle.Exists())
                    {
                        vehiclesToRemove.Add(kvp.Key);
                        continue;
                    }

                    // Mise à jour de l'état du véhicule
                    var state = kvp.Value;
                    state.Position = vehicle.Position;
                    state.Speed = vehicle.Speed;
                    state.IsStuck = vehicle.Speed < 0.5f && state.Speed < 0.5f;
                    state.LastUpdate = DateTime.Now;
                }
                catch
                {
                    vehiclesToRemove.Add(kvp.Key);
                }
            }

            foreach (var handle in vehiclesToRemove)
            {
                _vehicleStates.Remove(handle);
                _lockedVehicles.Remove(handle);
            }
        }

        #endregion

        #region Utilities

        private void PeriodicCleanup()
        {
            if ((DateTime.Now - _lastCleanup).TotalSeconds < CLEANUP_INTERVAL) return;

            try
            {
                CleanupOldVehicleStates();
                CleanupOldEvents();
                _lastCleanup = DateTime.Now;
            }
            catch (Exception ex)
            {
                SafeLogError($"Cleanup error: {ex.Message}");
            }
        }

        private void CleanupOldVehicleStates()
        {
            var cutoff = DateTime.Now.AddSeconds(-MAX_EVENT_AGE);
            var toRemove = _vehicleStates
                .Where(kvp => kvp.Value.LastUpdate < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var handle in toRemove)
            {
                _vehicleStates.Remove(handle);
                _lockedVehicles.Remove(handle);
            }
        }

        private void CleanupOldEvents()
        {
            var cutoff = DateTime.Now.AddSeconds(-MAX_EVENT_AGE);
            _lastEventTime.Keys.Where(k => _lastEventTime[k] < cutoff).ToList()
                .ForEach(k => _lastEventTime.Remove(k));
        }

        private bool IsEventTooOld(GameEvent gameEvent)
        {
            return (DateTime.Now - gameEvent.Timestamp).TotalSeconds > MAX_EVENT_AGE;
        }

        private void SafeLogError(string message)
        {
            try
            {
                GTA.UI.Notification.PostTicker($"~r~[REALIS] {message}", false);
            }
            catch
            {
                // Même le logging peut échouer, on ignore silencieusement
            }
        }

        #endregion

        #region Safe Shutdown

        public void Dispose()
        {
            try
            {
                _eventHandlers.Clear();
                _vehicleStates.Clear();
                _lockedVehicles.Clear();
                _lastEventTime.Clear();
            }
            catch (Exception ex)
            {
                SafeLogError($"Disposal error: {ex.Message}");
            }
        }

        #endregion
    }

    #region Data Classes

    public class VehicleState
    {
        public int Handle { get; set; }
        public Vector3 Position { get; set; }
        public float Speed { get; set; }
        public bool IsStuck { get; set; }
        public string? LockedBy { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;
        public Dictionary<string, object> CustomData { get; } = new();
    }

    public abstract class GameEvent
    {
        public REALISEventType Type { get; protected set; }
        public DateTime Timestamp { get; set; }
        public Vector3 Position { get; set; }
        public Dictionary<string, object> Data { get; } = new();
    }

    public class VehicleEvent : GameEvent
    {
        public int VehicleHandle { get; set; }
        public int DriverHandle { get; set; }
    }

    public class CollisionEvent : VehicleEvent
    {
        public CollisionEvent()
        {
            Type = REALISEventType.Collision;
        }
        
        public int OtherVehicleHandle { get; set; }
        public float ImpactForce { get; set; }
        public CollisionSeverity Severity { get; set; }
    }

    public class TrafficBlockEvent : VehicleEvent
    {
        public TrafficBlockEvent()
        {
            Type = REALISEventType.TrafficBlock;
        }
        
        public float BlockedDuration { get; set; }
        public Vector3 BlockingPosition { get; set; }
    }

    public enum REALISEventType
    {
        Collision,
        TrafficBlock,
        VehicleStuck,
        PoliceCall,
        VehicleDestroyed
    }

    public enum CollisionSeverity
    {
        Minor,
        Moderate,
        Severe
    }

    public interface IEventHandler
    {
        bool CanHandle(GameEvent gameEvent);
        void Handle(GameEvent gameEvent);
    }

    #endregion
} 