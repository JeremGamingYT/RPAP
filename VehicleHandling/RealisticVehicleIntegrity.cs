using System.Collections.Generic;
using GTA;
using GTA.Native;
using RealHandlingLib;

namespace RPAP
{
    public class RealisticVehicleIntegrity : Script
    {
        private readonly Dictionary<int,float> burningTimers = new();

        public RealisticVehicleIntegrity()
        {
            Tick    += OnTick;
            Interval = 0;
        }

        private void OnTick(object sender,System.EventArgs e)
        {
            float dt = Game.LastFrameTime;

            foreach(Vehicle v in World.GetAllVehicles())
            {
                if(!IsCandidate(v)) continue;

                IntegritySpec spec = GetSpec(v);

                if(v.IsOnFire)
                {
                    burningTimers.TryGetValue(v.Handle,out float t);
                    t += dt;
                    burningTimers[v.Handle] = t;
                }
                else
                {
                    if(burningTimers.ContainsKey(v.Handle))
                        burningTimers.Remove(v.Handle);
                }

                v.EngineHealth = v.EngineHealth < spec.ExplosionThreshold
                                 ? spec.ExplosionThreshold      // évite de descendre trop bas
                                 : v.EngineHealth;

                burningTimers.TryGetValue(v.Handle,out float burnTime);

                bool allowExplosion =
                    v.EngineHealth <= spec.ExplosionThreshold &&
                    burnTime >= spec.FireTimeBeforeBoom;

                Function.Call(Hash.SET_ENTITY_PROOFS,
                              v.Handle,
                              false,false,!allowExplosion,false,
                              false,false,false,false);
            }
        }

        private static IntegritySpec GetSpec(Vehicle v) =>
            RealisticVehicleIntegrityData.Specs.TryGetValue(v.DisplayName.ToUpper(),out var s)
            ? s
            : new IntegritySpec {
                  ExplosionThreshold = RealisticVehicleIntegrityData.DefaultThreshold,
                  FireTimeBeforeBoom = RealisticVehicleIntegrityData.DefaultFireTime
              };

        private static bool IsCandidate(Vehicle v) =>
            v.Exists() && !v.IsDead && !v.IsInvincible;
    }
}