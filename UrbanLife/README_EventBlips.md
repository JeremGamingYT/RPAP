# Système d'Événements sur la Mini-Map

## Vue d'ensemble

Le nouveau système `EventBlipManager` affiche automatiquement les événements spéciaux sur votre mini-map dans GTA V, vous permettant de voir en temps réel les incidents qui se produisent autour de vous. **Les événements se créent maintenant uniquement dans un rayon de 100m maximum du joueur** pour une expérience plus immersive !

## Types d'événements affichés

### 🔴 Agressions/Vols (Robbery) - **40% des événements**
- **Icône** : Camion blindé (rouge)
- **Durée** : 3 minutes
- **Clignotant** : Oui
- **Comportement** : Le voleur menace la victime avec un couteau, la victime lève les mains, puis le voleur s'enfuit
- **Distance** : Créé entre 3-8m de la victime pour une visibilité optimale

### 🔴 Bagarres (Fight) - **20% des événements**
- **Icône** : Mission GTA Online (rouge foncé)
- **Durée** : 2 minutes
- **Clignotant** : Oui
- **Comportement** : Les PNJs se provoquent puis se battent réellement
- **Recherche** : Trouve automatiquement un adversaire dans un rayon de 15m

### 🟠 Accidents (Accident) - **15% des événements**
- **Icône** : Devin (orange)
- **Durée** : 2 minutes
- **Clignotant** : Non
- **Comportement** : Animation de chute plus visible et durable

### ⚪ Urgences médicales (Medical) - **15% des événements**
- **Icône** : Hôpital (blanc)
- **Durée** : 4 minutes
- **Clignotant** : Oui
- **Comportement** : Animation de malaise avec cris de douleur périodiques pour attirer l'attention

### 🟠 Incendies (Fire) - **10% des événements**
- **Icône** : Devin (orange)
- **Durée** : 5 minutes
- **Clignotant** : Oui
- **Comportement** : Feu réel créé dans le jeu, PNJ panique et s'enfuit

## Fonctionnalités

### ✨ Nouvelles améliorations
- **Événements proches uniquement** : Maximum 100m du joueur
- **Comportements réalistes** : PNJs avec animations et séquences d'actions
- **Notifications avec distance** : Affichage de la distance exacte
- **Probabilités ajustées** : Événements plus visibles favorisés
- **Séquences asynchrones** : Actions programmées dans le temps

### Intégration NPCRoadRage
- Détection automatique des accidents de voiture
- Affichage des véhicules endommagés sur la mini-map
- Synchronisation avec les appels à la police

### Apparence dynamique
- Les blips deviennent plus transparents avec le temps
- Le clignotement s'arrête après 50% de la durée
- Suppression automatique après expiration

## Contrôles

| Touche | Action |
|--------|--------|
| **F9** | Réinitialiser le système UrbanLife |
| **F10** | Forcer l'activation du système + créer un événement test |
| **F11** | Afficher les informations de debug (inclut le nombre de blips actifs) |
| **L** | Effacer tous les blips d'événements de la mini-map |
| **R** | 🆕 **Forcer la création d'un événement à proximité** (pour tests) |

## Configuration

Le système est automatiquement initialisé avec `UrbanLifeMain` et ne nécessite aucune configuration manuelle.

### Probabilités d'événements
```csharp
// Dans CheckForSpecialEvents()
if (random.NextDouble() < 0.0005f) // 0.05% de chance par update
```

### Distance maximale des événements
```csharp
// Dans CheckForSpecialEvents()
var nearbySmartNPCs = smartNPCs.Where(npc => 
    npc.Ped.Position.DistanceTo(playerPos) <= 100.0f && // 100m maximum
    npc.CanTriggerSpecialEvent()).ToList();
```

## Utilisation en jeu

### 🎮 Mode normal
1. **Démarrage automatique** : Le système démarre automatiquement avec UrbanLife
2. **Observation** : Surveillez votre mini-map pour les nouveaux blips d'événements (max 100m)
3. **Intervention** : Rendez-vous sur les lieux pour intervenir ou observer
4. **Notifications** : Recevez des alertes avec la distance exacte

### 🧪 Mode test
1. **Appuyez sur R** pour forcer la création d'un événement proche
2. **Appuyez sur F10** pour activer le système et créer un événement test
3. **Appuyez sur L** pour nettoyer tous les blips

## Intégration avec d'autres mods

Le système s'intègre automatiquement avec :
- **NPCRoadRage** : Affichage des accidents de circulation
- **UrbanLife SmartNPCs** : Événements générés par les PNJs intelligents (max 100m)
- **NoiseReactionManager** : Réactions aux bruits violents

## Comportements des événements

### 🔴 Agression (Robbery)
1. Voleur apparaît à 3-8m de la victime
2. Voleur équipe un couteau et court vers la victime
3. Voleur menace la victime pendant 3 secondes
4. Victime lève les mains pendant 5 secondes
5. Voleur s'enfuit vers une position aléatoire
6. Nettoyage automatique après 10 secondes

### 🔴 Bagarre (Fight)
1. Recherche d'un adversaire dans un rayon de 15m
2. Les deux PNJs se tournent l'un vers l'autre
3. Tension pendant 1.5 secondes
4. Combat réel entre les deux PNJs

### ⚪ Urgence médicale (Medical)
1. PNJ tombe au sol avec animation de malaise
2. Cris de douleur toutes les 3 secondes (5 fois)
3. Animation continue jusqu'à intervention ou expiration

## Notes techniques

- **Performance** : Optimisé avec vérifications asynchrones
- **Sécurité** : Gestion complète des erreurs et null-checks
- **Réalisme** : Animations et sons appropriés pour chaque événement
- **Visibilité** : Événements conçus pour être facilement observables

## Dépannage

### Les événements sont trop rares
- Utilisez la touche **R** pour forcer la création d'événements
- Assurez-vous d'avoir des SmartNPCs actifs (F11 pour vérifier)

### Les PNJs ne bougent pas pendant les événements
- Vérifiez que vous êtes bien proche (moins de 100m)
- Les actions peuvent avoir un délai de 1-3 secondes
- Certains événements nécessitent du temps pour se développer

### Problèmes de performance
- Le système est optimisé avec des tâches asynchrones
- Probabilité très faible pour éviter le spam (0.05%)
- Nettoyage automatique des ressources

---

*Développé par REALIS - Urban Life System v2.1 - Événements Proches et Réalistes* 