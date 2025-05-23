using System.Collections.Generic;

namespace RealHandlingLib
{
    /// <summary>
    ///  Spécifie à partir de quel seuil un véhicule PEUT exploser
    ///  (engineHealth <= ExplosionThreshold  &&  véhicule en flammes depuis FireTimeBeforeBoom)
    /// </summary>
    public struct IntegritySpec
    {
        public float ExplosionThreshold;   // ex : -2000  (GTA vanilla : ~-4000)
        public float FireTimeBeforeBoom;   // secondes de feu avant autorisation d'explosion
    }

    public static class RealisticVehicleIntegrityData
    {
        // Valeurs par défaut (si non listé)
        public const float DefaultThreshold = -1500f;
        public const float DefaultFireTime  = 12f;

        public static readonly Dictionary<string, IntegritySpec> Specs = new()
        {
            // ——— hyper/super —------------------------------------
            ["ADDER"]    = new IntegritySpec { ExplosionThreshold = -1800f, FireTimeBeforeBoom = 10f },
            ["TURISMOR"] = new IntegritySpec { ExplosionThreshold = -1800f, FireTimeBeforeBoom = 10f },
            ["T20"]      = new IntegritySpec { ExplosionThreshold = -1900f, FireTimeBeforeBoom = 11f },
            ["ZENTORNO"] = new IntegritySpec { ExplosionThreshold = -1900f, FireTimeBeforeBoom = 11f },

            // ——— sport / coupé —-----------------------------------
            ["BANSHEE"]  = new IntegritySpec { ExplosionThreshold = -1600f, FireTimeBeforeBoom = 9f  },
            ["COMET2"]   = new IntegritySpec { ExplosionThreshold = -1600f, FireTimeBeforeBoom = 9f  },
            ["ELEGY2"]   = new IntegritySpec { ExplosionThreshold = -1600f, FireTimeBeforeBoom = 9f  },

            // ——— berlines / compacts / SUV —------------------------
            ["ISSI2"]    = new IntegritySpec { ExplosionThreshold = -1200f, FireTimeBeforeBoom = 8f  },
            ["BALLER2"]  = new IntegritySpec { ExplosionThreshold = -1400f, FireTimeBeforeBoom = 10f },
            ["GRANGER"]  = new IntegritySpec { ExplosionThreshold = -1400f, FireTimeBeforeBoom = 10f },
        };
    }
}