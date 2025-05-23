using System.Collections.Generic;
using GTA.Math;

namespace RealHandlingLib
{
    // ———————————————————————————————————————————————————————————
    // Spécification « IRL » de chaque véhicule
    // ———————————————————————————————————————————————————————————
    public struct Spec
    {
        public float Mass;               // kg
        public float SteeringDeg;        // angle maxi (°)
        public float TractionBiasFront;  // 0 = arrière ; 1 = avant
        public Vector3 CenterOfMass;     // décalage CG
        public float InitialDriveForce;  // 0-1
        public float BrakeForce;         // 0-1
        public float DeformationMult;    // dégâts tôle
        public float CollisionMult;      // dégâts internes

        //  Nouveautés ↓
        public float FuelCapacity;       // litres (réservoir réel)
        public float ConsumptionMult;    // 1 = réference, >1 = glouton
    }

    public static class RealisticVehicleData
    {
        // Valeurs « type » pour ne pas laisser un champ vide
        private const float DefaultTank = 60f;
        private const float DefaultCons = 1.0f;

        public static readonly Dictionary<string, Spec> Specs = new()
        {
            // ——— super / hypercars ——————————————————————————
            ["ADDER"]    = new Spec { Mass=1888f, SteeringDeg=35f, TractionBiasFront=0.52f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.40f, BrakeForce=1.00f, DeformationMult=1.50f, CollisionMult=2.00f, FuelCapacity=100f, ConsumptionMult=1.45f },
            ["TURISMOR"] = new Spec { Mass=1255f, SteeringDeg=35f, TractionBiasFront=0.50f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.36f, BrakeForce=0.95f, DeformationMult=0.35f, CollisionMult=0.55f, FuelCapacity=90f,  ConsumptionMult=1.40f },
            ["T20"]      = new Spec { Mass=1547f, SteeringDeg=35f, TractionBiasFront=0.50f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.36f, BrakeForce=0.95f, DeformationMult=0.40f, CollisionMult=0.60f, FuelCapacity=90f,  ConsumptionMult=1.40f },
            ["ZENTORNO"] = new Spec { Mass=1365f, SteeringDeg=35f, TractionBiasFront=0.52f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.38f, BrakeForce=0.95f, DeformationMult=0.35f, CollisionMult=0.55f, FuelCapacity=90f,  ConsumptionMult=1.45f },
            ["ENTITYXF"] = new Spec { Mass=1450f, SteeringDeg=35f, TractionBiasFront=0.48f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.35f, BrakeForce=0.90f, DeformationMult=0.40f, CollisionMult=0.60f, FuelCapacity=90f,  ConsumptionMult=1.40f },

            // ——— sportives ————————————————————————————————
            ["BANSHEE"]  = new Spec { Mass=1530f, SteeringDeg=35f, TractionBiasFront=0.49f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.32f, BrakeForce=0.90f, DeformationMult=0.50f, CollisionMult=0.70f, FuelCapacity=70f,  ConsumptionMult=1.30f },
            ["COMET2"]   = new Spec { Mass=1475f, SteeringDeg=35f, TractionBiasFront=0.48f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.28f, BrakeForce=0.85f, DeformationMult=0.45f, CollisionMult=0.65f, FuelCapacity=64f,  ConsumptionMult=1.25f },
            ["ELEGY2"]   = new Spec { Mass=1740f, SteeringDeg=35f, TractionBiasFront=0.52f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.34f, BrakeForce=0.90f, DeformationMult=0.45f, CollisionMult=0.65f, FuelCapacity=72f,  ConsumptionMult=1.30f },
            ["MASSACRO"] = new Spec { Mass=1780f, SteeringDeg=35f, TractionBiasFront=0.49f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.30f, BrakeForce=0.90f, DeformationMult=0.50f, CollisionMult=0.70f, FuelCapacity=70f,  ConsumptionMult=1.25f },
            ["SULTANRS"] = new Spec { Mass=1570f, SteeringDeg=35f, TractionBiasFront=0.53f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.31f, BrakeForce=0.90f, DeformationMult=0.50f, CollisionMult=0.70f, FuelCapacity=65f,  ConsumptionMult=1.20f },
            ["STINGERGT"]= new Spec { Mass= 950f, SteeringDeg=38f, TractionBiasFront=0.48f, CenterOfMass=new Vector3(0,0,-0.10f), InitialDriveForce=0.24f, BrakeForce=0.80f, DeformationMult=0.60f, CollisionMult=0.80f, FuelCapacity=50f,  ConsumptionMult=1.05f },

            // ——— berlines / coupés ——————————————————————————
            ["BUFFALO2"]  = new Spec { Mass=1960f, SteeringDeg=35f, TractionBiasFront=0.48f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.25f, BrakeForce=0.80f, DeformationMult=0.65f, CollisionMult=0.85f, FuelCapacity=66f, ConsumptionMult=1.15f },
            ["DOMINATOR"] = new Spec { Mass=1650f, SteeringDeg=35f, TractionBiasFront=0.47f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.27f, BrakeForce=0.80f, DeformationMult=0.60f, CollisionMult=0.80f, FuelCapacity=75f, ConsumptionMult=1.55f },
            ["RUINER"]    = new Spec { Mass=1500f, SteeringDeg=35f, TractionBiasFront=0.47f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.25f, BrakeForce=0.75f, DeformationMult=0.60f, CollisionMult=0.80f, FuelCapacity=68f, ConsumptionMult=1.35f },
            ["PRIMO2"]    = new Spec { Mass=1800f, SteeringDeg=40f, TractionBiasFront=0.60f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.22f, BrakeForce=0.80f, DeformationMult=0.70f, CollisionMult=0.90f, FuelCapacity=65f, ConsumptionMult=1.10f },
            ["SCHAFTER3"] = new Spec { Mass=1950f, SteeringDeg=35f, TractionBiasFront=0.49f, CenterOfMass=new Vector3(0,0,-0.15f), InitialDriveForce=0.29f, BrakeForce=0.88f, DeformationMult=0.65f, CollisionMult=0.85f, FuelCapacity=70f, ConsumptionMult=1.20f },
            ["JACKAL"]    = new Spec { Mass=1650f, SteeringDeg=38f, TractionBiasFront=0.49f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.27f, BrakeForce=0.80f, DeformationMult=0.55f, CollisionMult=0.75f, FuelCapacity=64f, ConsumptionMult=1.15f },

            // ——— compacts ————————————————————————————————
            ["ISSI2"]     = new Spec { Mass=1200f, SteeringDeg=40f, TractionBiasFront=0.58f, CenterOfMass=new Vector3(0,0,-0.12f), InitialDriveForce=0.20f, BrakeForce=0.80f, DeformationMult=0.55f, CollisionMult=0.75f, FuelCapacity=45f, ConsumptionMult=0.85f },

            // ——— SUV / 4×4 ————————————————————————————————
            ["BALLER2"]   = new Spec { Mass=2350f, SteeringDeg=40f, TractionBiasFront=0.53f, CenterOfMass=new Vector3(0,0,-0.20f), InitialDriveForce=0.26f, BrakeForce=0.85f, DeformationMult=0.70f, CollisionMult=0.90f, FuelCapacity=80f, ConsumptionMult=1.35f },
            ["SERRANO"]   = new Spec { Mass=2150f, SteeringDeg=40f, TractionBiasFront=0.53f, CenterOfMass=new Vector3(0,0,-0.20f), InitialDriveForce=0.27f, BrakeForce=0.85f, DeformationMult=0.70f, CollisionMult=0.90f, FuelCapacity=80f, ConsumptionMult=1.30f },
            ["SANDKING"]  = new Spec { Mass=2800f, SteeringDeg=45f, TractionBiasFront=0.54f, CenterOfMass=new Vector3(0,0,-0.25f), InitialDriveForce=0.25f, BrakeForce=0.80f, DeformationMult=0.80f, CollisionMult=1.00f, FuelCapacity=85f, ConsumptionMult=1.55f },
            ["DUBSTA"]    = new Spec { Mass=2600f, SteeringDeg=45f, TractionBiasFront=0.54f, CenterOfMass=new Vector3(0,0,-0.25f), InitialDriveForce=0.26f, BrakeForce=0.80f, DeformationMult=0.80f, CollisionMult=1.00f, FuelCapacity=85f, ConsumptionMult=1.45f },
            ["GRANGER"]   = new Spec { Mass=2560f, SteeringDeg=40f, TractionBiasFront=0.54f, CenterOfMass=new Vector3(0,0,-0.20f), InitialDriveForce=0.24f, BrakeForce=0.80f, DeformationMult=0.75f, CollisionMult=0.95f, FuelCapacity=90f, ConsumptionMult=1.45f },

            // ——— pick-up utilitaires ————————————————————————
            ["BISON"] = new Spec { Mass=2300f, SteeringDeg=40f, TractionBiasFront=0.55f, CenterOfMass=new Vector3(0,0,-0.20f), InitialDriveForce=0.22f, BrakeForce=0.75f, DeformationMult=0.75f, CollisionMult=0.95f, FuelCapacity=90f, ConsumptionMult=1.60f },
            ["RHINO"] = new Spec { Mass=60000f, SteeringDeg=25f, TractionBiasFront=0.5f, CenterOfMass=new Vector3(0,0,-0.30f), InitialDriveForce=0.20f, BrakeForce=0.90f, DeformationMult=0.30f, CollisionMult=5.00f, FuelCapacity=900f, ConsumptionMult = 4.0f},
        };
    }
}