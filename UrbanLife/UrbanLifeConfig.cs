using System;

namespace REALIS.UrbanLife
{
    public static class UrbanLifeConfig
    {
        // Configuration générale
        public static int MaxSmartNPCs = 30;
        public static int UpdateInterval = 2000; // millisecondes
        public static int TickInterval = 250; // millisecondes
        
        // Configuration des réactions aux bruits
        public static class NoiseReaction
        {
            // Portées des différents types de bruits
            public static float GunShotRange = 100.0f;
            public static float ExplosionRange = 300.0f;
            public static float CarHornRange = 25.0f;
            public static float CarCrashRange = 150.0f;
            public static float SirenRange = 200.0f;
            public static float ScreamRange = 75.0f;
            
            // Durées des bruits
            public static float GunShotDuration = 8.0f;
            public static float ExplosionDuration = 15.0f;
            public static float CarHornDuration = 1.5f;
            public static float CarCrashDuration = 10.0f;
            public static float SirenDuration = 12.0f;
            public static float ScreamDuration = 5.0f;
            
            // Chances de réaction de base (avant modifications par personnalité et distance)
            public static float GunShotReactionChance = 0.9f;
            public static float ExplosionReactionChance = 0.95f;
            public static float CarHornReactionChance = 0.05f; // Très faible
            public static float CarCrashReactionChance = 0.8f;
            public static float SirenReactionChance = 0.4f;
            public static float ScreamReactionChance = 0.7f;
        }
        
        // Configuration des PNJ
        public static class NPCBehavior
        {
            // Chances qu'un PNJ devienne "intelligent"
            public static int WealthyNPCChance = 40; // %
            public static int NormalNPCChance = 10; // %
            public static int VehicleNPCAvoidChance = 85; // % chance d'éviter les PNJ en véhicule
            
            // Fréquence de changement de routine
            public static float RoutineChangeChance = 0.03f; // 3% par update
            
            // Distance de réaction minimale au joueur
            public static float PlayerProximityThreshold = 15.0f;
            public static float PlayerProximityReactionChance = 0.02f; // 2% quand joueur proche
        }
        
        // Configuration du debug
        public static class Debug
        {
            public static bool ShowDebugInfo = false;
            public static bool LogReactions = false;
            public static bool ShowNotifications = true;
        }
        
        // Méthode pour ajuster la difficulté
        public static void SetSensitivityLevel(SensitivityLevel level)
        {
            switch (level)
            {
                case SensitivityLevel.Low:
                    NoiseReaction.CarHornReactionChance = 0.01f;
                    NoiseReaction.SirenReactionChance = 0.2f;
                    NPCBehavior.RoutineChangeChance = 0.01f;
                    break;
                    
                case SensitivityLevel.Medium:
                    NoiseReaction.CarHornReactionChance = 0.05f;
                    NoiseReaction.SirenReactionChance = 0.4f;
                    NPCBehavior.RoutineChangeChance = 0.03f;
                    break;
                    
                case SensitivityLevel.High:
                    NoiseReaction.CarHornReactionChance = 0.15f;
                    NoiseReaction.SirenReactionChance = 0.6f;
                    NPCBehavior.RoutineChangeChance = 0.08f;
                    break;
            }
        }
    }
    
    public enum SensitivityLevel
    {
        Low,     // Réactions minimales
        Medium,  // Réactions équilibrées (défaut)
        High     // Réactions plus fréquentes
    }
} 