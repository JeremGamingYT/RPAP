# NPCRoadRage Script - Changelog

## Version 1.27.1 - NPCIntelligence Build Fix (2025-06-03)

### 🛠️ Correction de build
- Utilisation correcte de `Game.Player.Wanted` pour ajuster le niveau de recherche
- Suppression d'un appel invalide `IsFiringWeapon` sur `Ped`

## Version 1.27 - Enhanced NPC Intelligence (2025-06-02)

### 🤖 PNJ plus intelligents hors traffic
- Nouveau dossier `NPCIntelligence` avec gestionnaire dédié.
- Les PNJ fuient ou appellent la police lorsqu'ils sont menacés.


## Version 1.26 - Intelligent Traffic AI (2025-06-01)

### 🚗 PNJ plus dynamiques dans les embouteillages
- Nouveau dossier `TrafficAI` avec un gestionnaire dédié.
- Les véhicules PNJ bloqués klaxonnent après quelques secondes.
- Ils tentent ensuite de contourner l'obstacle pour continuer leur route.

## Version 1.24.1 - Compilation Fixes (2025-05-30)

### 🔧 **Correctifs de compilation pour SHVDN v3**

#### **Erreurs corrigées**
- ❌ `Hash.SET_PED_ALERT_LEVEL` - Hash inexistant supprimé
- ❌ `Hash.SET_VEHICLE_TYRE_BURST_MULTIPLIER` - Hash inexistant supprimé  
- ❌ `World.GetEntityByHandle<Ped>()` - Remplacé par dictionnaire de références stockées
- ⚠️ `TaskInvoker.GoTo()` obsolète - Remplacé par `FollowNavMeshTo()`

#### **Warnings supprimés**
- ⚠️ **CS8600 Nullable warnings** : `Ped` → `Ped?` pour les références nullable
- ⚠️ `VehicleDrivingFlags.AvoidVehicles` → `SwerveAroundAllVehicles`
- ⚠️ `VehicleDrivingFlags.AvoidObjects` → `SteerAroundObjects` 
- ⚠️ `VehicleDrivingFlags.AvoidPeds` → `SteerAroundPeds`

#### **Améliorations techniques**
- ✅ **Dictionnaire de références** : `_trackedPeds` pour éviter `GetEntityByHandle`
- ✅ **Nettoyage amélioré** : Suppression des références stockées lors du cleanup
- ✅ **Types nullable corrects** : Utilisation de `Ped?` pour éviter les warnings CS8600
- ✅ **Compatibilité totale** : Scripts compilent sans erreur ni warning avec SHVDN v3

### 📦 **Résultat**
- ✅ **Compilation parfaite** : 0 erreur, 0 warning
- ✅ **API à jour** : Toutes les méthodes obsolètes remplacées
- ✅ **Types sécurisés** : Gestion correcte des références nullable
- ✅ **Performance optimisée** : Utilisation des nouvelles APIs recommandées

## Version 1.25 - 2025-01-XX - URGENT: Fixed False "Player Left Scene" Messages

### 🚨 CRITICAL BUG FIX
- **FIXED: False "Player left the scene" messages** - Major issue where police would incorrectly claim player fled
- **Root Cause**: Distance check was comparing player position to police VEHICLE instead of incident location or officers
- **Solution**: Multi-layered distance checking system

### 🛠️ Technical Fix Details
- **Improved Distance Logic**: Now checks distance to incident, vehicle, AND nearest officer
- **Smarter Thresholds**: Different tolerance levels for different elements:
  - Incident: 60m tolerance (1.5x normal)
  - Vehicle: 80m tolerance (2x normal) 
  - Officers: 40m tolerance (normal)
- **Interaction Protection**: Won't trigger "fled scene" during active police interaction
- **Player Must Be Far From ALL**: Only triggers if player is far from incident AND vehicle AND officers

### 🔧 Additional Improvements
- **CheckPoliceArrival Enhanced**: Distance check increased from 60m to 80m
- **Interaction State Awareness**: Won't trigger during `_policeInteractionActive` or `_makingPoliceDecision`
- **Better Logging**: More detailed distance information in logs for debugging

### 🎯 Result
- ✅ **No more false "fled scene" messages** when staying near incident
- ✅ **Police interactions complete properly** without premature termination
- ✅ **More realistic behavior** - only triggers when truly fleeing far away
- ✅ **Better tolerance for movement** during natural interaction flow

## Version 1.24 - 2025-01-XX - Police AI Navigation Improvements & NPCAIEnhancer Removal

### 🔥 MAJOR CHANGES
- **REMOVED NPCAIEnhancer.cs** - Supprimé complètement car il interfère avec l'IA de la police et cause des problèmes de navigation
- **Police Navigation Completely Overhauled** - Système de navigation police entièrement revu pour plus de fiabilité

### 🚨 Police AI Improvements
- **Shorter spawn distances**: Police spawn now between 60-120m instead of 80-150m for faster arrival
- **Simplified navigation checks**: Reduced from every tick to every 20 seconds to prevent over-correction
- **Better stuck detection**: More sensitive stuck detection (0.3f speed threshold vs 0.5f)
- **Faster fallback mechanisms**: Emergency teleportation after 1.5 minutes instead of 3 minutes
- **Improved spawn point selection**: 16 spawn attempts with better positioning strategies
- **Enhanced initial navigation**: Better driving flags and speed settings for initial dispatch
- **More flexible arrival criteria**: Police can arrive when within 30m or 50m+slow speed

### 🛠️ Technical Improvements
- **Reduced navigation retries**: Max 2 attempts instead of 3 for faster resolution
- **Better driver abilities**: Enhanced police officer driving skills and aggressiveness settings
- **Optimized correction strategies**: Simplified 3-step navigation correction process
- **Improved vehicle properties**: Better initial vehicle state setup for reliable navigation

### 🐛 Bug Fixes
- Fixed police getting stuck in endless navigation loops
- Fixed police spawning too far away and being unable to reach incident
- Fixed compilation errors with invalid ScriptHookV.NET properties
- Removed conflicting AI enhancement that interfered with police behavior

### 📈 Performance
- Reduced excessive navigation checks from every tick to periodic intervals
- Simplified navigation logic for better performance
- Less aggressive pathfinding corrections to reduce CPU usage

## Version 1.23 - Major Bug Fixes (2025-05-30)

### 🚨 Résolution des problèmes majeurs

#### 1. **Police qui se téléporte directement vers le joueur**
- **Problème** : La police utilisait des mécanismes d'urgence trop agressifs qui causaient une téléportation immédiate
- **Solution** :
  - Augmentation du timeout de dispatch de 90s à 300s (5 minutes)
  - Augmentation du timeout de sortie de véhicule de 10s à 15s
  - Augmentation du timeout d'approche de 15s à 30s
  - Vérifications de navigation seulement toutes les 15 secondes au lieu de chaque tick
  - Délai de détection de blocage passé de 8s à 30s
  - Réduction du nombre de tentatives de redirection de 5 à 3
  - Téléportation d'urgence seulement après 3+ minutes au lieu de 90s
  - La police ne se téléporte maintenant qu'en dernier recours absolu

#### 2. **Comportement "en toupie" des officiers de police**
- **Problème** : Les officiers étaient re-taskés à chaque tick, causant un comportement erratique et des rotations sur place
- **Solution** :
  - Ajout de vérifications temporelles pour empêcher le re-tasking constant
  - Les officiers ne reçoivent de nouvelles tâches qu'au maximum toutes les 2-5 secondes
  - Ajout d'un dictionnaire de timing individuel pour chaque officier
  - Prévention des boucles de tâches infinies
  - Les officiers s'arrêtent correctement une fois proches du joueur

#### 3. **Menu de réponse invisible**
- **Problème** : Le menu interactif pour choisir la réponse aux questions de police n'était pas visible
- **Solution** :
  - Réécriture complète de `DrawResponseMenu` avec plusieurs méthodes d'affichage
  - Ajout de notifications persistantes pour le titre du menu
  - Ajout de rendu de texte direct avec coordonnées fixes à l'écran
  - Multiples méthodes de backup pour une meilleure compatibilité
  - Surbrillance visuelle pour l'option sélectionnée
  - Affichage des instructions de contrôle
  - Le menu devrait maintenant être visible quelle que soit la version de SHVDN

### 🔧 Améliorations de gestion mémoire
- **Ajout du nettoyage des dictionnaires de timing lors des resets**
- **Prévention des fuites mémoire dues aux données de timing accumulées**
- **Amélioration des procédures de gestion d'état et de nettoyage**

### 📋 Résumé des changements de configuration

#### Timeouts augmentés :
- `_policeDispatchTimeoutDuration` : 90s → 300s
- `_policeExitVehicleTimeoutDuration` : 10s → 15s  
- `_policeApproachTimeoutDuration` : 15s → 30s

#### Navigation moins agressive :
- `PoliceStuckSpeedThreshold` : 0.8f → 0.5f (moins sensible)
- `PoliceProgressThreshold` : 2.0f → 3.0f (plus tolérant)
- `MaxNavigationRetries` : 5 → 3 tentatives
- `ForceArrivalDistance` : 60.0f → 40.0f

#### Nouvelles variables de gestion :
- `_lastOfficerTaskTime` : Contrôle global du timing des tâches
- `_officerLastTaskedTime` : Dictionnaire individuel par officier
- `_lastMenuDisplayTime` : Gestion de l'affichage du menu

### 🎯 Résultat attendu
Avec ces corrections, vous devriez maintenant avoir :
1. ✅ Une police qui arrive de manière réaliste par la route sans téléportation
2. ✅ Des officiers qui se déplacent normalement sans faire "la toupie"
3. ✅ Un menu de réponse clairement visible et interactif
4. ✅ Des performances améliorées et une gestion mémoire optimisée

### 🔍 Tests recommandés
1. Provoquer une collision avec un NPC et attendre l'appel de police
2. Vérifier que la police arrive par la route de manière naturelle
3. Observer le comportement des officiers lors de leur approche
4. Tester la visibilité et l'interaction avec le menu de réponse 

# CHANGELOG - REALIS Mod

## [Version 1.4.0] - 2024-12-19 - CORRECTION MAJEURE URBANLIFE

### 🛠️ **CORRECTIONS CRITIQUES - Système UrbanLife**

#### **Problème Résolu : PNJ sortant de leur véhicule**
- ✅ **CORRIGÉ** : Les PNJ ne sortent plus de leur véhicule quand le joueur klaxonne
- ✅ **CORRIGÉ** : Les PNJ ne s'arrêtent plus de bouger quand le joueur s'approche
- ✅ **CORRIGÉ** : Réactions excessives aux bruits mineurs

#### **Optimisations de Performance**
- 🚀 Réduction de la fréquence des vérifications (100ms → 250ms)
- 🚀 Intervalle d'update augmenté (1000ms → 2000ms)
- 🚀 Nombre max de PNJ intelligents réduit (50 → 30)
- 🚀 Auto-nettoyage des PNJ invalides

#### **Améliorations du Système de Réaction**
- 🔧 Chance de réaction aux klaxons : 30% → 5%
- 🔧 Portée des klaxons réduite : 50m → 25m
- 🔧 Durée des klaxons réduite : 3.0s → 1.5s
- 🔧 Protection spéciale pour les PNJ en véhicule
- 🔧 Gestion intelligente de la proximité du joueur

#### **Nouveau Système d'Intégration**
- 🆕 **UrbanLifeIntegration.cs** : Évite les conflits avec NPCRoadRage
- 🆕 Réservation intelligente des PNJ
- 🆕 Détection automatique des PNJ occupés
- 🆕 Protection des PNJ de police et d'urgence

#### **Contrôles de Debug Ajoutés**
- 🎮 **F11** : Affichage des informations de debug détaillées
- 🎮 **F10** : Activation forcée du système (test)
- 🎮 **F9** : Réinitialisation complète du système

#### **Corrections Techniques**
- 🔧 Correction des erreurs de compilation SHVDN
- 🔧 Mise à jour des API obsolètes
- 🔧 Correction des signatures de méthodes
- 🔧 Ajout des classes manquantes (RoutineSteps, SpecialEventType)

### 📁 **Nouveaux Fichiers**
- `UrbanLife/UrbanLifeConfig.cs` - Configuration centralisée
- `UrbanLife/UrbanLifeIntegration.cs` - Système d'intégration
- `UrbanLife/README_CORRECTIONS.md` - Documentation des corrections
- `UrbanLife/GUIDE_UTILISATION.md` - Guide d'utilisation

### 🎯 **Résultat Final**
Le système UrbanLife est maintenant **stable, performant et non-intrusif**. Les PNJ se comportent naturellement sans réactions excessives, tout en conservant les fonctionnalités d'amélioration de l'IA pour les événements importants.

---

## [Version 1.3.x] - Versions Précédentes

### 🚗 **NPCRoadRage System**
- Système de réaction des PNJ aux collisions
- Gestion des interactions avec la police
- Menu de réponses interactif

### 🏛️ **Criminal Record System**  
- Système d'enregistrement des crimes
- Intégration avec les forces de l'ordre
- Persistance des données criminelles

### ⛽ **Fuel System**
- Système de carburant réaliste
- Gestion de la consommation
- Stations-service fonctionnelles

### 🚙 **Vehicle Handling**
- Amélioration du comportement des véhicules
- Système d'usure des pneus
- Intégrité réaliste des véhicules

---

**Note** : Cette version 1.4.0 résout les problèmes majeurs signalés avec le système UrbanLife. Le mod est maintenant stable et prêt pour une utilisation normale. 