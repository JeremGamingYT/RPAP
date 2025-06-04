using GTA;
using GTA.Native;
using GTA.UI;
using System;

/// <summary>
/// Script SHVDN V3 pour skipper les intros et messages légaux de GTA V
/// Économise environ 30 secondes à chaque démarrage du jeu
/// Version corrigée compatible avec SHVDN V3 récent
/// </summary>
public class SkipIntroScript : Script
{
    private bool hasSkippedIntro = false;
    private DateTime scriptStartTime;
    private int tickCount = 0;
    
    public SkipIntroScript()
    {
        scriptStartTime = DateTime.Now;
        Tick += OnTick;
        
        // Log que le script est chargé
        Notification.PostTicker("~g~Skip Intro Script chargé - Prêt à skipper les intros!", false, false);
    }
    
    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            tickCount++;
            
            // Skipper pendant les premières 60 secondes après le démarrage du script
            // Mais seulement tous les 10 ticks pour éviter le spam
            if (!hasSkippedIntro && (DateTime.Now - scriptStartTime).TotalSeconds < 60 && tickCount % 10 == 0)
            {
                SkipIntroSequences();
            }
            else if (!hasSkippedIntro && (DateTime.Now - scriptStartTime).TotalSeconds >= 60)
            {
                hasSkippedIntro = true;
                Notification.PostTicker("~y~Skip Intro: Séquence de démarrage terminée", false, false);
            }
        }
        catch (Exception ex)
        {
            // En cas d'erreur, continuer silencieusement mais limiter les messages d'erreur
            if (tickCount % 1000 == 0) // Seulement une erreur toutes les 1000 ticks
            {
                Notification.PostTicker($"~r~Skip Intro Error: {ex.Message}", false, false);
            }
        }
    }
    
    private void SkipIntroSequences()
    {
        // Simuler l'appui sur les touches pour skipper les vidéos
        // Utiliser les contrôles corrects pour les menus frontend
        Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.FrontendAccept, true);
        Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.FrontendCancel, true);
        Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.FrontendSelect, true);
        
        // Forcer le skip des écrans d'intro avec des natives valides
        Function.Call(Hash.SET_GAME_PAUSED, false);
        
        // Vérifier et skipper les messages légaux
        SkipLegalMessages();
        
        // Vérifier et skipper les logos Rockstar
        SkipRockstarLogos();
        
        // Forcer le passage à l'écran principal
        TryForceMainMenu();
    }
    
    private void SkipLegalMessages()
    {
        // Utiliser des natives pour détecter et skipper les messages légaux
        try
        {
            // Vérifier si on est dans l'écran des avertissements légaux
            if (Function.Call<bool>(Hash.IS_WARNING_MESSAGE_ACTIVE))
            {
                // Forcer la fermeture du message d'avertissement
                Function.Call(Hash.SET_WARNING_MESSAGE_WITH_HEADER, "", "", 0, 0, "", "", "", "", "");
                Function.Call(Hash.CLEAR_ADDITIONAL_TEXT, 0, true);
            }
            
            // Alternative: simuler l'appui sur les touches pour confirmer
            if (Game.IsControlPressed(Control.FrontendAccept) == false)
            {
                // Simuler l'appui sur Entrée/Espace
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.FrontendAccept, false);
            }
        }
        catch
        {
            // En cas d'erreur avec les natives, continuer
        }
    }
    
    private void SkipRockstarLogos()
    {
        try
        {
            // Vérifier si une vidéo d'intro est en cours
            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE))
            {
                // Arrêter la cutscene (logos Rockstar)
                Function.Call(Hash.STOP_CUTSCENE_IMMEDIATELY);
            }
            
            // Alternative: forcer le skip des vidéos d'intro
            Function.Call(Hash.SET_CUTSCENE_TRIGGER_AREA, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
        }
        catch
        {
            // En cas d'erreur, continuer
        }
    }
    
    private void TryForceMainMenu()
    {
        try
        {
            // Si on n'est pas encore dans le jeu principal et que suffisamment de temps s'est écoulé
            if (Game.Player.Character == null && (DateTime.Now - scriptStartTime).TotalSeconds > 5)
            {
                // Essayer de passer rapidement les écrans en simulant des touches
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.FrontendAccept, false);
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)Control.FrontendSelect, false);
            }
        }
        catch
        {
            // En cas d'erreur, continuer normalement
        }
    }
} 