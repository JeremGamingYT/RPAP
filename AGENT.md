# Guide de développement pour GTA V Mods

## Meilleures pratiques pour ScriptHookVDotNet V3

### Documentation et ressources
- Consultez toujours la documentation officielle: [ScriptHookVDotNet V3 Documentation](https://nitanmarcel.github.io/scripthookvdotnet/scripting_v3/)
- Référez-vous aux exemples de scripts existants pour comprendre les modèles de conception recommandés
- Utilisez uniquement les versions compatibles : l'ASI et les DLL doivent provenir de la même version SHVDN

### Architecture de base d'un script
```csharp
public class MonScript : Script
{
    public MonScript()
    {
        // Initialisation dans le constructeur
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted; // OBLIGATOIRE pour nettoyer les ressources
    }
    
    private void OnAborted(object sender, EventArgs e)
    {
        // Nettoyage obligatoire des ressources
        // Suppression des entités créées, arrêt des timers, etc.
    }
}
```

### Stabilité et performance

#### Gestion des événements Tick
- **CRITIQUE**: Limitez les opérations dans `OnTick()` - cette méthode est appelée à chaque frame
- Utilisez des intervalles/compteurs pour espacer les opérations coûteuses :
```csharp
private int tickCount = 0;
private const int UPDATE_INTERVAL = 100; // Toutes les 100 frames

private void OnTick(object sender, EventArgs e)
{
    tickCount++;
    if (tickCount % UPDATE_INTERVAL != 0) return;
    
    // Opérations coûteuses ici seulement
}
```

#### Gestion des exceptions
- **OBLIGATOIRE**: Encapsulez TOUT code dans des blocs try/catch
- Les exceptions non gérées peuvent faire crasher le jeu entier
```csharp
private void OnTick(object sender, EventArgs e)
{
    try
    {
        // Votre code ici
    }
    catch (Exception ex)
    {
        UI.Notify($"Erreur dans le script: {ex.Message}");
        // Log l'erreur si possible
    }
}
```

#### Optimisation des performances
- Évitez les `World.GetNearbyPeds()`, `World.GetNearbyVehicles()` appelés trop fréquemment
- Limitez les raycast et les requêtes de collision
- Utilisez `Game.Player.Character.Position` plutôt que de recalculer constamment
- Pré-calculez les distances et positions quand possible
- Évitez les boucles imbriquées dans les méthodes Tick

### Gestion des entités (Peds, Vehicles, Props)

#### Vérifications obligatoires
```csharp
// TOUJOURS vérifier avant d'utiliser une entité
if (ped != null && ped.Exists() && ped.IsAlive)
{
    // Manipuler l'entité
}

// Vérifier la distance avant interaction
float distance = Game.Player.Character.Position.DistanceTo(entity.Position);
if (distance < MAX_INTERACTION_DISTANCE)
{
    // Interaction sécurisée
}
```

#### Nettoyage des entités
- **CRITIQUE**: Supprimez toutes les entités créées dans `OnAborted()`
- Gardez une liste des entités créées pour les nettoyer
```csharp
private List<Ped> createdPeds = new List<Ped>();
private List<Vehicle> createdVehicles = new List<Vehicle>();

private void OnAborted(object sender, EventArgs e)
{
    foreach (var ped in createdPeds)
    {
        if (ped != null && ped.Exists())
            ped.Delete();
    }
    createdPeds.Clear();
}
```

#### Limites et restrictions
- Ne créez pas plus de 10-15 entités simultanément
- Implémentez une zone de sécurité autour du joueur (minimum 5 mètres)
- Utilisez des cooldowns entre les créations d'entités (minimum 1 seconde)
- Vérifiez la charge système avant de créer de nouvelles entités

### Gestion de la mémoire et des ressources

#### Variables et collections
- Utilisez `List<T>` plutôt que des arrays dynamiques
- Nettoyez régulièrement les collections qui grandissent
- Évitez les références circulaires entre objets
```csharp
// Nettoyage périodique
if (tickCount % 1000 == 0) // Toutes les 1000 frames
{
    CleanupDeadReferences();
}
```

#### Gestion des textures et modèles
- Libérez les modèles après utilisation : `model.MarkAsNoLongerNeeded()`
- Ne chargez pas de textures/modèles dans la méthode Tick
- Pré-chargez les ressources dans le constructeur quand possible

### Interactions avec le monde du jeu

#### Manipulation de la physique
- Évitez de modifier directement la vélocité/position des entités trop fréquemment
- Utilisez `Entity.ApplyForce()` plutôt que de modifier directement `Velocity`
- Respectez les limites physiques du moteur (vitesse max, force max)

#### Interface utilisateur
- Limitez les notifications (`UI.Notify()`) - pas plus d'une par seconde
- Évitez les textes trop longs qui peuvent déborder
- Vérifiez que l'UI est disponible avant de l'utiliser

#### Sauvegarde et configuration
- Utilisez des fichiers de configuration simples (INI, JSON)
- Implémentez une sauvegarde périodique automatique
- Gérez les cas où les fichiers de config sont corrompus

### Compatibilité et versions

#### Versions de jeu
- Testez avec différentes versions de GTA V
- Gérez les cas où certaines fonctionnalités ne sont pas disponibles
- Vérifiez la compatibilité avec les mises à jour du jeu

#### Autres mods
- Évitez de modifier les mêmes éléments que d'autres mods populaires
- Implémentez des vérifications de conflit
- Respectez les conventions de nommage communautaires

### Débogage et tests

#### Logging
```csharp
private void LogError(string message)
{
    try
    {
        File.AppendAllText("MonScript.log", $"{DateTime.Now}: {message}\n");
    }
    catch { } // Ne jamais laisser le logging planter le script
}
```

#### Tests
- Testez avec différents niveaux de performance système
- Testez avec d'autres mods installés
- Testez les cas limites (beaucoup d'entités, situations de stress)
- Vérifiez le comportement lors des cinématiques et des missions

### Erreurs courantes à éviter

1. **Ne jamais** manipuler des entités dans les cinématiques
2. **Ne jamais** créer d'entités en boucle infinie
3. **Ne jamais** ignorer les vérifications d'existence des entités
4. **Ne jamais** faire d'opérations lourdes dans OnTick sans limitation
5. **Ne jamais** oublier de nettoyer dans OnAborted
6. **Ne jamais** modifier directement les stats du joueur sans sauvegarde
7. **Ne jamais** utiliser de sleep() ou wait() dans les événements
8. **Ne jamais** accéder aux entités depuis des threads secondaires

### Checklist finale avant publication

- [ ] Tous les try/catch sont en place
- [ ] OnAborted() nettoie toutes les ressources
- [ ] Pas d'opérations lourdes dans OnTick
- [ ] Toutes les entités sont vérifiées avant utilisation
- [ ] Les limites de création d'entités sont respectées
- [ ] Pas de fuites mémoire détectées