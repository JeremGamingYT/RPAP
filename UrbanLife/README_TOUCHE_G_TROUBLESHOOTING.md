# Guide de Dépannage - Système d'Escorte (Touche G) - VERSION AMÉLIORÉE

## Problème Résolu : Le PNJ sort immédiatement du véhicule

### Correctifs Appliqués (Version Renforcée)

1. **Vérification du même véhicule** : Le système vérifie maintenant que le PNJ est dans le MÊME véhicule que le joueur
2. **Protection renforcée contre les sorties involontaires** : Application multiple des propriétés anti-sortie
3. **Période de grâce étendue** : 5 secondes d'attente (augmenté de 3 secondes) après montée dans le véhicule
4. **Surveillance automatique** : Nouveau système qui détecte et corrige automatiquement les sorties involontaires
5. **Gestion robuste des erreurs** : Système de récupération amélioré si le PNJ sort malgré tout

### NOUVELLES FONCTIONNALITÉS (Version 2.0)

#### Surveillance Automatique
- **MonitorPassengerStability()** : Fonction qui surveille en permanence les PNJ transportés
- **Détection automatique** : Si un PNJ sort involontairement, il est automatiquement remis dans le véhicule
- **Logs automatiques** : Fichier `UrbanLife_passenger_auto_fix.log` pour tracker les corrections

#### Protections Renforcées
- **Application multiple** : Les propriétés de protection sont appliquées plusieurs fois avec des délais
- **Réapplication périodique** : Toutes les 10 secondes pendant le transport
- **Protection pendant la période de grâce** : Surveillance active même pendant les 5 premières secondes

### Comment Utiliser le Système

1. **Approchez-vous** d'un véhicule en panne (blip jaune)
2. **Appuyez sur G** pour proposer votre aide
3. **Le PNJ vous suit à pied** - des instructions apparaissent à l'écran
4. **Montez dans votre véhicule** - le PNJ montera automatiquement
5. **Attendez 5 secondes** - période de stabilisation (NOUVEAU)
6. **Conduisez vers la destination** marquée en jaune sur la mini-map
7. **Le PNJ descend** automatiquement arrivé à destination

### Diagnostic Automatique

Si des problèmes persistent, le système génère automatiquement plusieurs fichiers de diagnostic :
- **UrbanLife_passenger_debug.log** : États détaillés du joueur, PNJ, véhicules
- **UrbanLife_passenger_auto_fix.log** : Corrections automatiques appliquées (NOUVEAU)
- **UrbanLife_monitor_error.log** : Erreurs du système de surveillance (NOUVEAU)
- **UrbanLife_transport_crash.log** : Erreurs de transport pour debug

### Messages du Système

- **Vert** : Succès (montée, arrivée à destination)
- **Jaune** : Instructions ou avertissements
- **Rouge** : Erreurs critiques OU corrections automatiques (NOUVEAU)
- **Bleu** : Informations d'aide

### Propriétés de Protection du PNJ (Renforcées)

Quand le PNJ monte dans votre véhicule, il obtient ces protections :
- `BlockPermanentEvents = true` : Ignore les événements externes
- `CanBeDraggedOutOfVehicle = false` : Ne peut pas être sorti de force
- `CanBeKnockedOffBike = false` : Ne tombe pas des motos
- `KnockOffVehicleType = Never` : Ne sort jamais involontairement
- `CanBeTargetted = false` : Évite les interactions externes (NOUVEAU)

**NOUVEAU** : Ces propriétés sont maintenant appliquées :
- **Immédiatement** après l'ordre d'entrée
- **50ms plus tard** pour confirmation
- **100ms plus tard** pour sécurité
- **Toutes les 10 secondes** pendant le transport
- **Automatiquement** si le PNJ sort involontairement

### En Cas de Problème

1. **Vérifiez** que vous êtes dans un véhicule avec des sièges libres
2. **Attendez** la période de grâce de 5 secondes après montée
3. **Observez** les messages de correction automatique (rouge)
4. **Consultez** les fichiers de diagnostic si disponible
5. **Réessayez** avec un autre événement de panne

### Commandes de Debug Avancées

Si des problèmes persistent, le système génère automatiquement des informations de debug. Vous pouvez également ajouter temporairement ces lignes dans le code :

```csharp
// Debug de base
GTA.UI.Screen.ShowSubtitle($"Debug: Phase {roadEvent.Phase}, Driver in vehicle: {driver?.IsInVehicle()}", 2000);

// Debug avancé avec surveillance
GTA.UI.Screen.ShowSubtitle($"Monitor: Transport events: {transportEvents.Count}, Auto-fixes applied: {autoFixCount}", 3000);
```

### Résolution des Problèmes Connus

**Si le PNJ sort encore involontairement :**
1. Le système de surveillance automatique devrait le détecter et le corriger
2. Vérifiez le fichier `UrbanLife_passenger_auto_fix.log` pour voir si les corrections sont appliquées
3. Si le problème persiste, il pourrait y avoir un conflit avec un autre mod

**Si le jeu plante pendant le transport :**
1. Consultez `UrbanLife_transport_crash.log` pour les détails
2. Les nouvelles protections devraient éviter la plupart des crashes
3. Le système a des fallbacks multiples pour terminer proprement les événements

### Améliorations Techniques (Version 2.0)

- **Réduction des Script.Wait()** : Optimisation des performances
- **Gestion d'erreurs renforcée** : Try-catch multiples avec fallbacks
- **Système de logging avancé** : Traçabilité complète des problèmes
- **Monitoring en temps réel** : Surveillance continue sans impact performance 