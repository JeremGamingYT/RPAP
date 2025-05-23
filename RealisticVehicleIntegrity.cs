using System.Collections.Generic;
using GTA;
using GTA.Native;
using GTA.Math;
using RealHandlingLib;

namespace RPAP
{
    public class RealisticVehicleIntegrity : Script
    {
        // keep track of burning timers
        private readonly Dictionary<int, float> burningTimers = new();

        public RealisticVehicleIntegrity()
        {
            Tick    += OnTick;
            Interval = 0;          // chaque frame
        }

        private void OnTick(object sender, System.EventArgs e)
        {
            float dt = Game.LastFrameTime;

            foreach (Vehicle v in World.GetAllVehicles())
            {
                if (!IsCandidate(v)) continue;

                // Récupère les règles
                IntegritySpec spec = GetSpec(v);

                // Si la caisse est en flammes, incrémente le chronomètre, sinon reset
                if (v.IsOnFire)
                {
                    burningTimers.TryGetValue(v.Handle, out float t);
                    t += dt;
                    burningTimers[v.Handle] = t;
                }
                else
                {
                    if (burningTimers.ContainsKey(v.Handle))
                        burningTimers.Remove(v.Handle);
                }

                // Empêche l'explosion tant que les conditions ne sont pas réunies
                float engineHp = v.EngineHealth;
                burningTimers.TryGetValue(v.Handle, out float burnTime);

                bool allowExplosion =
                    engineHp <= spec.ExplosionThreshold &&
                    burnTime >= spec.FireTimeBeforeBoom;

                // On joue avec les proofs : désactive/active protection explosion
                Function.Call(Hash.SET_ENTITY_PROOFS,
                              v.Handle,
                              false,      // bullet
                              false,      // fire
                              !allowExplosion, // explosion
                              false,      // collision
                              false, false, false, false);

                // Bonus : si moteur HS mais pas explosion => stop moteur + petites flammes
                if (!allowExplosion && engineHp <= spec.ExplosionThreshold)
                {
                    if (v.IsEngineRunning) v.IsEngineRunning = false;
                    if (!v.IsOnFire) v.EngineHealth = spec.ExplosionThreshold + 10f;
                }
            }
        }

        // ———————————————————————————————————————————————
        private static IntegritySpec GetSpec(Vehicle v)
        {
            return RealisticVehicleIntegrityData.Specs.TryGetValue(v.DisplayName.ToUpper(), out var s)
                   ? s
                   : new IntegritySpec {
                         ExplosionThreshold = RealisticVehicleIntegrityData.DefaultThreshold,
                         FireTimeBeforeBoom = RealisticVehicleIntegrityData.DefaultFireTime
                     };
        }

        private static bool IsCandidate(Vehicle v)
        {
            return v.Exists() && !v.IsDead && !v.IsInvincible;
        }
    }
}