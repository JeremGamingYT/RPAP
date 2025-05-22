using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System;
using System.Collections.Generic;
using System.Drawing;

public class VehicleDeformation : Script
{
    // Valeurs de déformation adaptées pour un maximum de réalisme
    private float baseDeformationValue = 12.0f;
    private float baseCollisionDamageValue = 10.0f;
    private float baseWeaponDamageValue = 6.0f;
    
    // Modèles spécifiques nécessitant des traitements particuliers
    private readonly Dictionary<string, float> specialVehicleMultipliers = new Dictionary<string, float>
    {
        { "RHINO", 0.2f },        // Tank - extrêmement résistant
        { "KHANJALI", 0.15f },    // Tank moderne - encore plus résistant
        { "APC", 0.25f },         // Véhicule blindé
        { "INSURGENT", 0.3f },    // Insurgent
        { "INSURGENT2", 0.3f },   // Insurgent Pick-up
        { "INSURGENT3", 0.3f },   // Insurgent Pick-up Custom
        { "HALFTRACK", 0.35f },   // Half-track
        { "PHANTOM3", 0.4f }      // Phantom Wedge
    };
    
    // Multiplicateurs par type de véhicule pour un comportement réaliste
    private readonly Dictionary<VehicleClass, float> deformationMultipliers = new Dictionary<VehicleClass, float>
    {
        { VehicleClass.Super, 0.7f },          // Supercars - plus résistantes
        { VehicleClass.Sports, 0.8f },         // Sports - assez résistantes
        { VehicleClass.SportsClassics, 1.0f }, // Sport classiques - moyennes
        { VehicleClass.Sedans, 1.2f },         // Berlines - moins résistantes
        { VehicleClass.Compacts, 1.3f },       // Compactes - fragiles
        { VehicleClass.SUVs, 0.9f },           // SUVs - robustes
        { VehicleClass.Coupes, 1.1f },         // Coupés - moyennes-fragiles
        { VehicleClass.Muscle, 1.0f },         // Muscle - moyennes
        { VehicleClass.OffRoad, 0.85f },       // Tout-terrains - robustes
        { VehicleClass.Industrial, 0.7f },     // Industriels - très robustes
        { VehicleClass.Utility, 0.8f },        // Utilitaires - robustes
        { VehicleClass.Commercial, 0.75f },    // Commerciaux - robustes
        { VehicleClass.Military, 0.4f }        // Militaires - très résistants (réduit davantage)
    };
    
    // Suivi du dernier véhicule pour détecter les changements
    private Vehicle? lastVehicle = null;
    
    // Variables pour le suivi des collisions
    private Vector3 lastVelocity = Vector3.Zero;
    private bool wasInVehicle = false;
    
    // Variable pour l'affichage de la vitesse
    private bool showSpeedometer = true;
    
    public VehicleDeformation()
    {
        Tick += OnTick;
        Interval = 0; // Mise à jour aussi rapide que possible pour détecter les collisions
        
        // Afficher une notification au démarrage du script
        Notification.PostTicker("~g~Script de déformation réaliste activé", true);
        Notification.PostTicker("~b~Déformations adaptées par type de véhicule", true);
        Notification.PostTicker("~y~Compteur de vitesse ajouté", true);
    }

    private void OnTick(object sender, EventArgs e)
    {
        Vehicle vehicle = Game.Player.Character.CurrentVehicle;
        
        if (vehicle != null && vehicle.Exists())
        {
            // Afficher le compteur de vitesse
            if (showSpeedometer)
            {
                // Convertir la vitesse en km/h (Velocity.Length() donne la vitesse en m/s)
                float speedKmh = vehicle.Speed * 3.6f; // Conversion en km/h
                
                // Afficher la vitesse en utilisant le HUD natif
                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"~b~{Math.Round(speedKmh, 0)} ~w~km/h");
                Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.93f, 0.92f, 0);
            }
            
            // Calcul de la force d'impact basée sur le changement de vitesse
            Vector3 currentVelocity = vehicle.Velocity;
            Vector3 velocityChange = Vector3.Zero;
            
            if (wasInVehicle)
            {
                velocityChange = lastVelocity - currentVelocity;
            }
            
            // Sauvegarder les valeurs pour la prochaine frame
            lastVelocity = currentVelocity;
            wasInVehicle = true;
            
            // Vérifier si c'est un nouveau véhicule
            bool isNewVehicle = lastVehicle == null || lastVehicle.Handle != vehicle.Handle;
            lastVehicle = vehicle;
            
            // Obtenir le niveau d'armure (0-4)
            int armorLevel = 0;
            if (vehicle.Mods.Contains(VehicleModType.Armor))
            {
                armorLevel = vehicle.Mods[VehicleModType.Armor].Index;
            }
            
            // Calculer les multiplicateurs de déformation en tenant compte du type de véhicule et modèle
            float typeMultiplier = 1.0f;
            
            // Vérifier d'abord si c'est un modèle spécial (RHINO, etc.)
            string modelName = vehicle.Model.ToString();
            if (specialVehicleMultipliers.ContainsKey(modelName))
            {
                typeMultiplier = specialVehicleMultipliers[modelName];
            }
            // Sinon, utiliser le multiplicateur par classe de véhicule
            else if (deformationMultipliers.ContainsKey(vehicle.ClassType))
            {
                typeMultiplier = deformationMultipliers[vehicle.ClassType];
            }
            
            // Pour les véhicules lourds (poids > 5000), augmenter leur résistance
            if (vehicle.HandlingData.Mass > 5000f)
            {
                typeMultiplier *= 0.5f; // Encore plus résistant pour les véhicules lourds
            }
            
            // Calculer les valeurs finales en prenant en compte l'armure
            // Plus l'armure est élevée, plus on augmente la déformation pour compenser
            float armorMultiplier = 1.0f + (armorLevel * 0.5f); // Augmente avec le niveau d'armure
            
            float finalDeformValue = baseDeformationValue * typeMultiplier * armorMultiplier;
            float finalCollisionValue = baseCollisionDamageValue * typeMultiplier * armorMultiplier;
            float finalWeaponValue = baseWeaponDamageValue * typeMultiplier * armorMultiplier;
            
            // Application directe des déformations via HandlingData
            if (vehicle.HandlingData.IsValid)
            {
                vehicle.HandlingData.DeformationDamageMultiplier = finalDeformValue;
                vehicle.HandlingData.CollisionDamageMultiplier = finalCollisionValue;
                vehicle.HandlingData.WeaponDamageMultiplier = finalWeaponValue;
            }
            
            // En cas de collision, appliquer une déformation spécifique
            float impactForce = velocityChange.Length();
            if (impactForce > 3.0f) // Seuil de détection de collision
            {
                // Point d'impact approximatif
                Vector3 position = vehicle.Position;
                Vector3 impactDirection = velocityChange.Normalized;
                Vector3 impactPoint = position + impactDirection * 1.5f;
                
                // Force proportionnelle à l'impact
                float damageForce = Math.Min(impactForce * 2.0f, 100.0f);
                
                // Appliquer des dommages directs au point d'impact
                Function.Call(Hash.SET_VEHICLE_DAMAGE, vehicle.Handle, 
                              impactPoint.X, impactPoint.Y, impactPoint.Z, 
                              damageForce, 15.0f, true);
                
                // Si c'est un véhicule lourd qui heurte quelque chose, endommager les véhicules environnants
                if (vehicle.HandlingData.Mass > 4000f)
                {
                    // Rechercher tous les véhicules à proximité
                    Vehicle[] nearbyVehicles = World.GetNearbyVehicles(vehicle.Position, 10f);
                    foreach (Vehicle nearVehicle in nearbyVehicles)
                    {
                        if (nearVehicle != null && nearVehicle.Handle != vehicle.Handle)
                        {
                            // Calculer la distance
                            float distance = nearVehicle.Position.DistanceTo(vehicle.Position);
                            
                            // Si le véhicule est proche, appliquer une déformation
                            if (distance < 5f)
                            {
                                // Plus le véhicule est proche, plus la force est importante
                                float crushForce = (5f - distance) * 30f;
                                
                                // Direction de l'impact
                                Vector3 crushDirection = (nearVehicle.Position - vehicle.Position).Normalized;
                                Vector3 crushPoint = nearVehicle.Position - crushDirection * 0.5f;
                                
                                // Appliquer une forte déformation au véhicule écrasé
                                Function.Call(Hash.SET_VEHICLE_DAMAGE, nearVehicle.Handle,
                                              crushPoint.X, crushPoint.Y, crushPoint.Z,
                                              crushForce, 30.0f, true);
                                
                                // Réduire la santé du véhicule écrasé
                                nearVehicle.BodyHealth = Math.Max(nearVehicle.BodyHealth - crushForce * 2f, 100f);
                            }
                        }
                    }
                }
                
                // Contourner l'armure pour les véhicules blindés
                if (armorLevel > 0)
                {
                    // Stocker la santé actuelle
                    float currentHealth = vehicle.Health;
                    float currentBodyHealth = vehicle.BodyHealth;
                    
                    // Réduire temporairement la santé pour permettre la déformation
                    vehicle.BodyHealth = Math.Min(vehicle.BodyHealth, 900f);
                    
                    // Appliquer une force d'impact plus grande pour surmonter l'armure
                    Function.Call(Hash.SET_VEHICLE_DAMAGE, vehicle.Handle, 
                                  impactPoint.X, impactPoint.Y, impactPoint.Z, 
                                  damageForce * 3.0f, 25.0f, true);
                    
                    // Restaurer partiellement la santé (pour ne pas endommager trop le moteur)
                    // Mais permettre la déformation visuelle
                    vehicle.Health = (int)(currentHealth * 0.95f);
                }
            }
            
            // Si c'est un nouveau véhicule, initialiser la déformation
            if (isNewVehicle)
            {
                // Réduire légèrement la santé du véhicule pour activer le système de déformation
                vehicle.BodyHealth = Math.Min(vehicle.BodyHealth, 990f);
                
                // Notification pour indiquer que la déformation a été appliquée
                string vehicleType = "standard";
                if (specialVehicleMultipliers.ContainsKey(modelName))
                {
                    vehicleType = "spécial (" + modelName + ")";
                }
                else
                {
                    vehicleType = vehicle.ClassType.ToString();
                }
                
                Notification.PostTicker("Configuration appliquée: ~y~" + vehicle.DisplayName, true);
                Notification.PostTicker("Type: ~b~" + vehicleType + "~w~ Mult: ~y~" + typeMultiplier.ToString("0.0"), true);
            }
            
            // Garder le véhicule légèrement endommagé pour permettre des déformations visibles
            if (vehicle.BodyHealth > 990f && vehicle.IsDriveable)
            {
                vehicle.BodyHealth = 990f;
            }
        }
        else
        {
            // Réinitialiser le suivi du véhicule
            lastVehicle = null;
            wasInVehicle = false;
        }
    }
}