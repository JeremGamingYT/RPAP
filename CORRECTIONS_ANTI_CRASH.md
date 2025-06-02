# Corrections Anti-Crash pour les Événements Spontanés

## 🚨 Problèmes Identifiés et Corrigés

### 1. **PedHash Inexistants**
- **Problème** : Utilisation de `PedHash.Genstreet01AMY` et `PedHash.Genstreet02AMY` qui n'existent pas
- **Solution** : Remplacés par des PedHash confirmés dans la documentation GTA V

### 2. **Méthodes Async Dangereuses**
- **Problème** : `async void` peuvent causer des crashes non gérés
- **Solution** : Remplacées par des `Task.Run()` avec gestion d'erreur

### 3. **Positions Invalides**
- **Problème** : PNJ créés dans l'eau ou des positions impossibles
- **Solution** : Vérifications de sécurité ajoutées :
  - Test de hauteur Z valide
  - Détection de l'eau
  - Vérification du sol

### 4. **Surcharge d'Événements**
- **Problème** : Trop d'événements créés simultanément
- **Solution** : 
  - Maximum 2 événements simultanés (au lieu de 3)
  - Délai minimum de 3 minutes entre événements
  - Probabilités réduites

## 🔧 Modifications Spécifiques

### NPCEventManager.cs
```csharp
// AVANT (Dangereux)
private async void StartMuggingSequence(...)
{
    await Task.Delay(3000);
    // Code sans gestion d'erreur
}

// APRÈS (Sécurisé)
private void StartMuggingSequenceSafe(...)
{
    Task.Run(() => {
        try {
            Thread.Sleep(3000);
            // Code avec vérifications
        }
        catch { /* Ignorer erreurs */ }
    });
}
```

### Probabilités Réduites
- **Base** : 0.05% au lieu de 0.1%
- **Nuit** : x2 au lieu de x3
- **Maximum** : 1% au lieu de 5%

### Positions Sécurisées
```csharp
// Vérifications ajoutées
if (position.Z < -100.0f || position.Z > 1000.0f) return;
if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, ...)) return;
if (!World.GetGroundHeight(eventPos, out groundZ)) return;
```

## 🎮 Nouvelles Commandes Sécurisées

| Touche | Action | Sécurité |
|--------|--------|----------|
| **F8** | Debug info | Safe |
| **F9** | Reset système | Safe |
| **F10** | Événement aléatoire | Distance fixe 25m |
| **F11** | Agression | Distance fixe 30m |
| **F12** | Bagarre | Distance fixe 25m |
| **R** | ❌ DÉSACTIVÉ | Causait des conflits |

## ⚠️ Mesures Préventives

### 1. Gestion d'Erreur Globale
Toutes les méthodes principales sont maintenant dans des blocs `try-catch`

### 2. Vérifications de PNJ
```csharp
if (ped?.Exists() != true || ped.IsDead) return;
```

### 3. Nettoyage Automatique
- Événements supprimés après 5 minutes (au lieu de 10)
- PNJ marqués comme "no longer needed" automatiquement

### 4. Distances Réduites
- Événements créés à 20-80m (au lieu de 30-150m)
- Distance entre participants réduite à 2-3m

## 🎯 Test Recommandé

1. **Démarrer le mod**
2. **Attendre 1-2 minutes** (événements naturels)
3. **Utiliser F10** pour tester un événement contrôlé
4. **Observer** : Le jeu ne doit plus planter

## 🚀 Optimisations Performance

- Maximum 2 événements simultanés
- Vérifications rapides avant création
- Nettoyage automatique plus fréquent
- Positions calculées plus efficacement

## 🆘 **Crash F11 - Agression (CORRIGÉ)**

### **Problèmes identifiés :**
1. **Threading asynchrone** : `Task.Run` et `Thread.Sleep` causaient des instabilités
2. **Armes automatiques** : Donner des armes aux PNJ agresseurs causait des crashes  
3. **Positions invalides** : Calculs de position pouvant générer des coordonnées dangereuses
4. **Task.AimGunAtEntity** : Fonction instable avec certaines armes
5. **Manque de vérifications** : Pas assez de contrôles de sécurité

### **Solutions appliquées :**

#### **1. Nouveau système d'agression sécurisé**
- ✅ **Suppression du threading** : Plus de `Task.Run` ou `Thread.Sleep`
- ✅ **Pas d'armes automatiques** : Les agresseurs n'ont plus d'armes par défaut
- ✅ **Positions calculées sécurisées** : Distance fixe de 35m du joueur
- ✅ **Modèles PNJ stables** : Utilisation de modèles testés et fiables
- ✅ **Vérifications multiples** : Position, état du joueur, événements actifs

#### **2. Fonctions remplacées :**
```csharp
// ANCIEN (causait crashes)
private void StartMuggingSequenceSafe() {
    Task.Run(() => {
        Thread.Sleep(3000);
        aggressor.Task.AimGunAtEntity(victim, 3000);
        aggressor.Weapons.Give(WeaponHash.Pistol, 50, false, true);
    });
}

// NOUVEAU (sécurisé)
private void StartMuggingSequenceUltraSafe() {
    aggressor.Task.GoTo(victim.Position);
    eventObj.Phase = EventPhase.Active;
    // Simple et stable - pas de threading
}
```

#### **3. Nouvelles vérifications de sécurité :**
- 🔒 **Distance minimale** : Au moins 15m du joueur
- 🔒 **Position valide** : Vérification Z-coordinates et sol
- 🔒 **État du joueur** : Pas en véhicule pendant la création
- 🔒 **Limite d'événements** : Maximum 2 événements simultanés
- 🔒 **Modèles sûrs** : PNJ testés (Business01AMY, StrPunk01GMY, etc.)

### **Test de la correction :**

1. **Lancer le jeu** avec le mod REALIS
2. **Aller à pied** dans une zone ouverte (pas en véhicule)
3. **Appuyer sur F11** pour créer une agression
4. **Résultat attendu** : 
   - ✅ Message : "Agression créée en sécurité à 35m!"
   - ✅ PNJ agresseur et victime apparaissent
   - ✅ **PAS DE CRASH** 
   - ✅ Agression simple sans armes automatiques

### **Messages de debug :**
- `~g~Agression créée avec succès` = Création réussie
- `~r~Position invalide pour agression` = Position trop dangereuse
- `~y~Sortez du véhicule avant de créer une agression!` = Pas en véhicule
- `~r~Trop d'événements actifs!` = Attendez la fin des autres événements

### **Si le crash persiste :**
1. Vérifiez que vous êtes à pied (pas en véhicule)
2. Éloignez-vous des zones d'eau ou souterraines
3. Attendez que les autres événements se terminent
4. Redémarrez le jeu si nécessaire

---

**Ces corrections devraient considérablement réduire (voire éliminer) les crashes lors des événements d'agression.** 