# Guide d'Utilisation - Système UrbanLife Corrigé

## 🎮 **Contrôles de Test**

### Touches de Debug
- **F11** : Afficher les informations de debug détaillées
- **F10** : Forcer l'activation du système (créer 10 PNJ intelligents)
- **F9** : Réinitialiser complètement le système UrbanLife

## 🔧 **Comment Tester le Système**

### 1. Démarrage Initial
1. Lancez GTA V avec le mod REALIS installé
2. Attendez le message : `"Système de Vie Urbaine Améliorée activé"`
3. Appuyez sur **F11** pour voir l'état du système

### 2. Activation Forcée (pour test)
1. Allez dans une zone avec beaucoup de PNJ (centre-ville)
2. Appuyez sur **F10** pour forcer l'activation
3. Vous devriez voir des messages comme :
   - `"PNJ [ID] activé pour UrbanLife"`
   - `"Force: X PNJ activés"`
   - `"Événement spécial déclenché pour test"`

### 3. Observation des Comportements

#### **Réactions aux Klaxons (Corrigées)**
- Klaxonnez près des PNJ
- **Résultat attendu** : Réactions très subtiles uniquement
- Les PNJ en véhicule **ne sortent plus** de leur voiture
- Simple regard rapide vers vous, puis retour à leurs activités

#### **Approche des PNJ**
- Approchez-vous des PNJ à pied ou en véhicule
- **Résultat attendu** : Pas de réactions excessives
- Les PNJ continuent leurs activités normalement

#### **Événements Spéciaux**
- Observez les PNJ pendant quelques minutes
- Vous pourriez voir :
  - Des PNJ qui changent d'activité
  - Des interactions entre PNJ
  - Des événements rares (bagarre, accident simulé)

## 📊 **Informations de Debug (F11)**

### Statistiques Affichées
```
Smart NPCs: X/30          # Nombre de PNJ intelligents actifs
Active Routines: X        # Routines en cours d'exécution
Noise Sources: X          # Sources de bruit actives
Reserved NPCs: X          # PNJ réservés par UrbanLife
Nearby NPCs: X            # PNJ totaux dans la zone
```

### Messages de Notification
- `"UrbanLife: X nouveaux PNJ activés"` - Nouveaux PNJ ajoutés
- `"X en réaction, Y en véhicule"` - Détails des comportements

## ⚙️ **Configuration Avancée**

### Ajuster la Sensibilité
```csharp
// Dans le code, vous pouvez modifier :
UrbanLifeConfig.SetSensitivityLevel(SensitivityLevel.Low);    // Minimal
UrbanLifeConfig.SetSensitivityLevel(SensitivityLevel.Medium); // Équilibré
UrbanLifeConfig.SetSensitivityLevel(SensitivityLevel.High);   // Plus réactif
```

### Paramètres Modifiables
- **Nombre max de PNJ** : 30 (par défaut)
- **Fréquence de mise à jour** : 2000ms
- **Chance de réaction aux klaxons** : 5% (très faible)
- **Distance de réaction** : 25m pour klaxons

## 🐛 **Résolution de Problèmes**

### Le Système ne S'Active Pas
1. Vérifiez que le message de chargement apparaît
2. Appuyez sur **F10** pour forcer l'activation
3. Allez dans une zone avec plus de PNJ

### Trop de Réactions
1. Appuyez sur **F9** pour réinitialiser
2. Le système est maintenant beaucoup moins intrusif
3. Les réactions aux klaxons sont minimales

### Conflits avec NPCRoadRage
- Le système d'intégration évite automatiquement les conflits
- Les PNJ de police ne sont pas affectés par UrbanLife
- Les incidents de NPCRoadRage ont la priorité

## 📈 **Performances**

### Optimisations Appliquées
- Fréquence réduite des vérifications (250ms → 2000ms)
- Moins de PNJ intelligents simultanés (50 → 30)
- Réactions simplifiées et moins fréquentes
- Auto-nettoyage des PNJ invalides

### Surveillance
- Utilisez **F11** régulièrement pour surveiller les performances
- Le nombre de PNJ actifs ne devrait pas dépasser 30
- Les sources de bruit se nettoient automatiquement

## ✅ **Tests de Validation**

### Test 1 : Klaxons
- [ ] Klaxonner près de PNJ en véhicule → Pas de sortie du véhicule
- [ ] Klaxonner près de PNJ à pied → Regard rapide uniquement
- [ ] Klaxonner répétitivement → Pas de réactions excessives

### Test 2 : Proximité
- [ ] S'approcher de PNJ → Pas de fuite ou arrêt d'activité
- [ ] Rester près de PNJ → Comportement normal maintenu
- [ ] Circulation normale → Pas d'interruption du trafic

### Test 3 : Événements
- [ ] Observer pendant 5-10 minutes → Événements rares et naturels
- [ ] Vérifier les routines → PNJ changent d'activité occasionnellement
- [ ] Tester les réactions → Appropriées aux événements importants

## 🎯 **Résultats Attendus**

### ✅ Corrections Réussies
- **Fini** : PNJ qui sortent de leur véhicule sans raison
- **Fini** : Réactions excessives aux klaxons
- **Fini** : Arrêt d'activités lors d'approches normales
- **Fini** : Conflits avec NPCRoadRage

### ✅ Fonctionnalités Préservées
- Réactions appropriées aux événements violents
- Routines intelligentes pour certains PNJ
- Événements spéciaux rares mais intéressants
- Intégration harmonieuse avec les autres systèmes

Le système UrbanLife est maintenant **stable, performant et non-intrusif** ! 🎉 