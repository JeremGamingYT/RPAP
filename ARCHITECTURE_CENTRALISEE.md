# Architecture Centralisée REALIS

## 🎯 Problèmes Résolus

L'ancienne architecture décentralisée causait plusieurs problèmes critiques :

### ❌ Problèmes Identifiés
- **Conflits entre scripts** : Plusieurs systèmes tentaient de contrôler les mêmes véhicules simultanément
- **Crashes du jeu** : Race conditions et accès concurrents aux entités GTA V
- **Performance dégradée** : Traitements redondants et gaspillage de ressources
- **Gestion d'erreurs insuffisante** : Propagation d'exceptions non gérées
- **Code dupliqué** : Logique similaire répliquée dans plusieurs modules
- **⚠️ NOUVEAU** : Plantages lors des collisions avec le système d'incidents

### ✅ Solutions Apportées
- **Gestionnaire Central d'Événements** : Communication sécurisée entre modules
- **Système de Verrous** : Prévention des conflits d'accès aux véhicules
- **Architecture Event-Driven** : Couplage faible entre les composants
- **Gestion d'Erreurs Robuste** : Isolation des erreurs et récupération automatique
- **Nettoyage Automatique** : Libération proactive des ressources
- **⚠️ NOUVEAU** : Désactivation temporaire du gestionnaire d'incidents problématique

---

## 🏗️ Nouvelle Architecture

### 1. Gestionnaire Central (`CentralEventManager`)

**Responsabilités :**
- Coordination de tous les modules
- Gestion des verrous sur les véhicules
- Système de verrous avec priorité (TrafficAI prioritaire)
- Cooldown de 250 ms entre deux verrous pour éviter le spam
- Distribution des événements
- Nettoyage automatique des ressources

**Avantages :**
- Singleton thread-safe géré par ScriptHookVDotNet
- Prévention des conflits
- Performance optimisée
- Récupération d'erreurs
- Journalisation détaillée des conflits et erreurs (`REALIS.log`)

### 2. Gestionnaire de Trafic Centralisé (`CentralizedTrafficManager`)

**Remplace :** `TrafficIntelligenceManager` (ancien système problématique)

**Améliorations :**
- Utilise le système de verrous central
- Détection de blocage plus intelligente et tolérante
- Actions mesurées et moins agressives
- Gestion d'erreurs complète
- **🆕 NOUVEAU** : Logique améliorée pour les situations complexes (joueur entre deux voies)

**Configuration :**
```csharp
private const float SCAN_RADIUS = 35f;
private const float PROCESSING_INTERVAL = 2f;
private const int MAX_CONCURRENT_PROCESSING = 6;
private const float PLAYER_SAFE_ZONE = 6f;
private const float BLOCKED_TIME_THRESHOLD = 4f * 1.5f; // Plus patient
```

### 3. ~~Gestionnaire d'Incidents Centralisé~~ (`CentralizedIncidentManager`) - **⚠️ TEMPORAIREMENT DÉSACTIVÉ**

**Statut :** **DÉSACTIVÉ** - Causait des plantages lors des collisions

**Raison de la désactivation :**
- Détection agressive des collisions causait des instabilités
- Plantages lors de contacts/frôlements avec d'autres véhicules
- Sera réactivé une fois stabilisé

### 4. Systèmes Supprimés

- **`NPCRoadRage`** : ✅ **COMPLÈTEMENT SUPPRIMÉ**
  - Fichier `NPCRoadRageIntegration.cs` supprimé
  - Toutes les références nettoyées du code
  - Remplacé par le `CentralizedIncidentManager` (temporairement désactivé)

---

## 📊 État Actuel des Systèmes

| Ancien Système | Nouveau Système | Statut |
|---|---|---|
| `TrafficIntelligenceManager` | `CentralizedTrafficManager` | ✅ **Actif et amélioré** |
| `NPCRoadRage` | `CentralizedIncidentManager` | ⚠️ **Temporairement désactivé** |
| Décentralisé | `CentralEventManager` | ✅ **Actif et stable** |
| Scripts isolés | `ScriptCoordinator` | ✅ **Actif et géré par SHVDN** |

---

## 🔧 Améliorations Apportées

### **CentralizedTrafficManager - Logique Intelligente**

1. **Détection de Blocage Améliorée** :
   - Tests multi-directionnels plus tolérants
   - Prise en compte de la vitesse du joueur
   - Vérification des voies de contournement disponibles

2. **Gestion du Joueur Entre Deux Voies** :
   - Détection plus restrictive du blocage réel
   - Tolérance accrue pour les situations complexes
   - Actions plus mesurées et moins fréquentes

3. **Actions Plus Intelligentes** :
   - Klaxon seulement si nécessaire
   - Contournement limité à 1 tentative
   - Délais augmentés pour plus de patience

### **Code Sample - Détection Améliorée** :
```csharp
private bool IsPlayerActuallyBlocking(Vehicle vehicle, Vehicle playerVehicle, float distance, float angle)
{
    // Plus restrictif : le joueur doit vraiment être dans le chemin
    if (distance > 15f) return false; // Distance augmentée
    if (angle < 0.6f) return false;   // Angle plus strict
    
    // Vérifier si le joueur est vraiment stationnaire
    if (playerVehicle.Speed > 2f) return false; // Le joueur bouge
    
    // Vérifier si le véhicule peut facilement contourner
    var leftClear = CanMoveInDirection(vehicle, -vehicle.RightVector, 4f);
    var rightClear = CanMoveInDirection(vehicle, vehicle.RightVector, 4f);
    
    // Si une voie de contournement est libre, ne pas considérer comme bloqué
    if (leftClear || rightClear) return false;
    
    return true;
}
```

---

## 🛡️ Stabilité et Performance

### **Améliorations de Stabilité**
- ✅ Suppression complète du système RoadRage instable
- ✅ Désactivation temporaire du gestionnaire d'incidents problématique
- ✅ Logique de détection de collision améliorée et plus sûre
- ✅ Gestion d'erreurs renforcée partout

### **Optimisations de Performance**
- ✅ Cache intelligent pour les requêtes de véhicules
- ✅ Traitement par lots et limitation de concurrence
- ✅ Nettoyage automatique des ressources
- ✅ Réduction des actions agressives du trafic

---

## 🎮 Expérience Utilisateur

### **Améliorations Notables**
1. **Plus de Plantages** : Système d'incidents désactivé
2. **Trafic Plus Intelligent** : Réactions plus réalistes aux blocages
3. **Moins d'Agressivité** : Actions plus mesurées
4. **Meilleure Tolérance** : Gestion améliorée des situations complexes

### **Contrôles Inchangés**
- F7 : Événements routiers
- F10-F12 : Événements spontanés
- G : Interaction avec événements de panne

---

## 🔮 Prochaines Étapes

1. **Stabilisation du CentralizedIncidentManager**
   - Refactoring de la détection de collision
   - Tests approfondis
   - Réactivation progressive

2. **Nouvelles Fonctionnalités**
   - Système de police amélioré
   - Events plus variés et stables
   - Interface utilisateur améliorée

---

*Dernière mise à jour : Correction des plantages et suppression du système RoadRage obsolète* 