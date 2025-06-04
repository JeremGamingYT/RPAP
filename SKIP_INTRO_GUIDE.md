# Guide : Skipper les Intros et Écrans de Chargement GTA V

Ce guide vous explique comment **éliminer les intros agaçantes** et **économiser ~30 secondes** à chaque lancement de GTA V.

## 🚀 Solutions Disponibles

### Solution 1: Mod ASI (Le plus simple et efficace)

**SkipIntro** par grasmanek94 - Mod ASI qui fonctionne au niveau du moteur du jeu.

#### Avantages:
- ✅ **Le plus efficace** - Skip complet des logos Rockstar ET messages légaux
- ✅ **Ne modifie aucun fichier du jeu**
- ✅ **Compatible toutes versions** de GTA V
- ✅ **Installation simple**

#### Installation:
1. **Télécharger le mod:**
   - GitHub: https://github.com/grasmanek94/SkipIntro
   - GTA5-Mods: https://www.gta5-mods.com/tools/gtav-no-intro-video-xeramon

2. **Installer ScriptHookV** (ASI Loader):
   - Télécharger: http://www.dev-c.com/gtav/scripthookv/
   - Copier `ScriptHookV.dll` et `dinput8.dll` dans le dossier GTA V

3. **Installer SkipIntro:**
   - Copier `SkipIntro.asi` dans le dossier GTA V (à côté de `GTA5.exe`)

#### Structure finale:
```
📁 GTA V/
├── GTA5.exe
├── ScriptHookV.dll
├── dinput8.dll
└── SkipIntro.asi
```

---

### Solution 2: Script SHVDN V3 (Personnalisable)

**Script C# personnalisé** utilisant ScriptHookVDotNet V3.

#### Avantages:
- ✅ **Code source disponible** et modifiable
- ✅ **Intégrable** avec d'autres mods SHVDN
- ✅ **Personnalisable** - vous pouvez ajuster le comportement
- ⚠️ Nécessite SHVDN V3

#### Installation:
1. **Installer SHVDN V3:**
   - Télécharger: https://github.com/scripthookvdotnet/scripthookvdotnet/releases
   - Installer `ScriptHookVDotNet.asi` et `ScriptHookVDotNet3.dll`

2. **Utiliser le script:**
   - Copier `SkipIntroScript.cs` dans le dossier `scripts/`
   - Ou compiler en `.dll` et placer dans `scripts/`

#### Structure finale:
```
📁 GTA V/
├── GTA5.exe
├── ScriptHookV.dll
├── dinput8.dll
├── ScriptHookVDotNet.asi
├── ScriptHookVDotNet3.dll
└── 📁 scripts/
    └── SkipIntroScript.cs (ou .dll)
```

---

### Solution 3: Méthode Manuelle (Fichiers vidéo)

**Remplacer/supprimer** les fichiers vidéo d'intro.

#### Avantages:
- ✅ **Simple** conceptuellement
- ✅ **Pas de dépendances** externes

#### Inconvénients:
- ❌ **Modifie les fichiers du jeu** (risque de ban online)
- ❌ **Ne supprime PAS les messages légaux**
- ❌ **Peut causer des problèmes** de vérification d'intégrité

#### Méthode (NON RECOMMANDÉE):
1. Naviguer vers `GTA V/x64/movies/`
2. Renommer `rockstar_logos.bik` en `rockstar_logos.bik.backup`
3. Créer un fichier vide nommé `rockstar_logos.bik`

---

## 🎯 Recommandation

**Utilisez la Solution 1 (SkipIntro ASI)** car:
- C'est la plus efficace et complète
- Ne modifie aucun fichier du jeu
- Installation simple
- Marche sur toutes les versions

## 🔧 Dépannage

### Le mod ne fonctionne pas?

1. **Vérifiez l'ASI Loader:**
   ```
   📁 GTA V/
   ├── dinput8.dll ✅
   └── ScriptHookV.dll ✅
   ```

2. **Vérifiez les logs:**
   - `ScriptHookV.log` - doit mentionner le chargement d'ASI
   - `asiloader.log` - doit montrer le chargement réussi

3. **Version compatible:**
   - ScriptHookV doit être à jour pour votre version de GTA V
   - Si GTA V a été mis à jour, attendez la MAJ de ScriptHookV

### Erreurs communes:

- **"MSVCP140.dll manquant":** Installer Visual C++ Redistributable 2019
- **"Le jeu ne démarre pas":** Vérifier que ScriptHookV est compatible avec votre version
- **"ASI ne se charge pas":** Vérifier que `dinput8.dll` est bien présent

## 📝 Notes Importantes

- ⚠️ **Online:** Les mods ASI peuvent être détectés online - utilisez seulement en mode histoire
- 🔄 **Mises à jour:** Après une MAJ de GTA V, vérifiez la compatibilité des mods
- 💾 **Sauvegarde:** Gardez toujours une sauvegarde propre du jeu

## 📚 Ressources

- **ScriptHookV:** http://www.dev-c.com/gtav/scripthookv/
- **SHVDN V3:** https://github.com/scripthookvdotnet/scripthookvdotnet
- **GTA5-Mods:** https://www.gta5-mods.com/
- **GTAForums:** https://gtaforums.com/

---

*Fin des galères avec les intros! 🎉* 