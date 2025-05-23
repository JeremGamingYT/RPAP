using System.Collections.Generic;
using GTA.Math;

namespace RealHandlingLib
{
    /// <summary>1 entrée = 1 véhicule GTA.</summary>
    public struct Spec
    {
        public float Mass;
        public float SteeringDeg;          // EN degrés (on convertira + tard)
        public float TractionBiasFront;
        public Vector3 CenterOfMass;       // Décalage (X-Y-Z)
        public float InitialDriveForce;
        public float InitialDriveMaxFlatVel;
        public float BrakeForce;
    }

    /// <summary>Dictionnaire central : MODEL → Specs.</summary>
    public static class RealisticVehicleData
    {
        public static readonly Dictionary<string, Spec> Specs = new()
        {
            ["BANSHEE"] = new Spec
            {
                Mass = 1530f,
                SteeringDeg = 35f,
                TractionBiasFront = 0.50f,
                CenterOfMass = new Vector3(0f, 0f, -0.15f),
                InitialDriveForce = 0.31f,
                InitialDriveMaxFlatVel = 80f,   // ≈ 320 km/h
                BrakeForce = 0.9f
            },

            // ➜ Ajoute ici tous les autres modèles :
            // ["COMET2"] = new Spec { … },
            // ["ADDER"]   = new Spec { … },
        };
    }
}