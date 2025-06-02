using System.Collections.Generic;

namespace RealHandlingLib
{
    public struct IntegritySpec
    {
        public float ExplosionThreshold;   // engineHealth ≤ threshold
        public float FireTimeBeforeBoom;   // temps en flammes avant autorisation
    }

    public static class RealisticVehicleIntegrityData
    {
        public const float DefaultThreshold = -3000f; // explosions presque impossibles
        public const float DefaultFireTime  = 20f;    // brûler longtemps avant de sauter

        public static readonly Dictionary<string,IntegritySpec> Specs = new()
        {
            ["ADDER"]    = new IntegritySpec { ExplosionThreshold = -3200f, FireTimeBeforeBoom = 25f },
            ["TURISMOR"] = new IntegritySpec { ExplosionThreshold = -3200f, FireTimeBeforeBoom = 25f },
            ["T20"]      = new IntegritySpec { ExplosionThreshold = -3300f, FireTimeBeforeBoom = 26f },
            ["ZENTORNO"] = new IntegritySpec { ExplosionThreshold = -3300f, FireTimeBeforeBoom = 26f },

            ["BANSHEE"]  = new IntegritySpec { ExplosionThreshold = -3000f, FireTimeBeforeBoom = 22f },
            ["COMET2"]   = new IntegritySpec { ExplosionThreshold = -3000f, FireTimeBeforeBoom = 22f },
            ["ELEGY2"]   = new IntegritySpec { ExplosionThreshold = -3000f, FireTimeBeforeBoom = 22f },

            ["ISSI2"]    = new IntegritySpec { ExplosionThreshold = -2700f, FireTimeBeforeBoom = 20f },
            ["BALLER2"]  = new IntegritySpec { ExplosionThreshold = -2800f, FireTimeBeforeBoom = 23f },
            ["GRANGER"]  = new IntegritySpec { ExplosionThreshold = -2800f, FireTimeBeforeBoom = 23f },
        };
    }
}