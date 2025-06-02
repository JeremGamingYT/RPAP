using GTA;
using GTA.Native;
using GTA.Math;
using System;

public class NPCRecognition
{
    public Ped NPC { get; private set; }
    public float RecognitionLevel { get; set; } = 0.0f; // 0.0 to 1.0
    public bool HasRecognized { get; set; } = false;
    public DateTime CreationTime { get; private set; }
    public DateTime LastUpdate { get; set; }
    public NPCType NPCType { get; private set; }
    public bool IsActivelyRecognizing { get; set; } = false;
    public Vector3 LastKnownPlayerPosition { get; set; }
    public float SuspicionLevel { get; set; } = 0.0f;
    
    // Recognition modifiers
    public float BaseRecognitionRate { get; private set; }
    public float DistanceMultiplier { get; set; } = 1.0f;
    public float AngleMultiplier { get; set; } = 1.0f;
    public float ObstructionMultiplier { get; set; } = 1.0f;
    
    // Behavioral state
    public NPCBehaviorState BehaviorState { get; set; } = NPCBehaviorState.Normal;
    public DateTime LastBehaviorChange { get; set; }
    
    public NPCRecognition(Ped npc)
    {
        NPC = npc ?? throw new ArgumentNullException(nameof(npc));
        CreationTime = DateTime.Now;
        LastUpdate = DateTime.Now;
        LastBehaviorChange = DateTime.Now;
        
        DetermineNPCType();
        CalculateBaseRecognitionRate();
    }
    
    private void DetermineNPCType()
    {
        int copHash = Function.Call<int>(Hash.GET_HASH_KEY, "COP");
        int securityHash = Function.Call<int>(Hash.GET_HASH_KEY, "SECURITY_GUARD");
        
        if (NPC.RelationshipGroup == copHash)
        {
            NPCType = NPCType.Police;
        }
        else if (NPC.RelationshipGroup == securityHash || IsSecurityNPC())
        {
            NPCType = NPCType.Security;
        }
        else
        {
            NPCType = NPCType.Civilian;
        }
    }
    
    private bool IsSecurityNPC()
    {
        PedHash pedHash = (PedHash)NPC.Model.Hash;
        
        return pedHash == PedHash.Security01SMM ||
               pedHash == PedHash.Bouncer01SMM ||
               pedHash == PedHash.Armoured01SMM ||
               pedHash == PedHash.Armoured02SMM ||
               pedHash == PedHash.ShopKeep01;
    }
    
    private void CalculateBaseRecognitionRate()
    {
        switch (NPCType)
        {
            case NPCType.Police:
                BaseRecognitionRate = 0.025f; // Police are most observant
                break;
            case NPCType.Security:
                BaseRecognitionRate = 0.020f; // Security guards are trained observers
                break;
            case NPCType.Civilian:
                BaseRecognitionRate = 0.010f; // Civilians are least observant
                break;
            default:
                BaseRecognitionRate = 0.015f;
                break;
        }
    }
    
    public void UpdateRecognition(float notorietyBonus, float distance, Vector3 playerPosition)
    {
        LastUpdate = DateTime.Now;
        LastKnownPlayerPosition = playerPosition;
        
        // Calculate all multipliers
        UpdateDistanceMultiplier(distance);
        UpdateAngleMultiplier(playerPosition);
        UpdateObstructionMultiplier(playerPosition);
        
        // Calculate final recognition rate
        float finalRate = BaseRecognitionRate * (1 + notorietyBonus) * 
                         DistanceMultiplier * AngleMultiplier * ObstructionMultiplier;
        
        // Update recognition level
        if (distance <= 25.0f && IsLookingTowards(playerPosition))
        {
            RecognitionLevel = Math.Min(1.0f, RecognitionLevel + finalRate);
            IsActivelyRecognizing = true;
        }
        else
        {
            // Slowly decrease recognition if not actively observing
            RecognitionLevel = Math.Max(0.0f, RecognitionLevel - (finalRate * 0.5f));
            IsActivelyRecognizing = false;
        }
        
        // Update behavior based on recognition level
        UpdateBehavior();
    }
    
    private void UpdateDistanceMultiplier(float distance)
    {
        // Closer distance = better recognition
        if (distance <= 5.0f)
            DistanceMultiplier = 2.0f;
        else if (distance <= 10.0f)
            DistanceMultiplier = 1.5f;
        else if (distance <= 15.0f)
            DistanceMultiplier = 1.0f;
        else if (distance <= 20.0f)
            DistanceMultiplier = 0.7f;
        else
            DistanceMultiplier = 0.3f;
    }
    
    private void UpdateAngleMultiplier(Vector3 playerPosition)
    {
        // Check if NPC is facing towards the player
        Vector3 npcForward = NPC.ForwardVector;
        Vector3 toPlayer = (playerPosition - NPC.Position).Normalized;
        
        float dotProduct = Vector3.Dot(npcForward, toPlayer);
        
        // Facing directly at player = best recognition
        if (dotProduct > 0.8f)
            AngleMultiplier = 1.5f;
        else if (dotProduct > 0.5f)
            AngleMultiplier = 1.0f;
        else if (dotProduct > 0.0f)
            AngleMultiplier = 0.5f;
        else
            AngleMultiplier = 0.1f; // Looking away
    }
    
    private void UpdateObstructionMultiplier(Vector3 playerPosition)
    {
        // Simple line of sight check
        Vector3 npcEyePos = NPC.Position + Vector3.WorldUp * 1.7f; // Eye level
        Vector3 playerEyePos = playerPosition + Vector3.WorldUp * 1.7f;
        
        RaycastResult raycast = World.Raycast(npcEyePos, playerEyePos, IntersectFlags.Map);
        
        if (raycast.DidHit)
        {
            ObstructionMultiplier = 0.2f; // Heavily reduced if obstructed
        }
        else
        {
            ObstructionMultiplier = 1.0f; // Clear line of sight
        }
    }
    
    private bool IsLookingTowards(Vector3 playerPosition)
    {
        Vector3 npcForward = NPC.ForwardVector;
        Vector3 toPlayer = (playerPosition - NPC.Position).Normalized;
        
        float dotProduct = Vector3.Dot(npcForward, toPlayer);
        return dotProduct > 0.3f; // Roughly 70-degree cone
    }
    
    private void UpdateBehavior()
    {
        NPCBehaviorState newState = BehaviorState;
        
        if (HasRecognized)
        {
            switch (NPCType)
            {
                case NPCType.Police:
                    newState = NPCBehaviorState.Investigating;
                    break;
                case NPCType.Security:
                    newState = NPCBehaviorState.Alert;
                    break;
                case NPCType.Civilian:
                    newState = NPCBehaviorState.Suspicious;
                    break;
            }
        }
        else if (RecognitionLevel > 0.7f)
        {
            newState = NPCBehaviorState.Suspicious;
        }
        else if (RecognitionLevel > 0.3f)
        {
            newState = NPCBehaviorState.Observing;
        }
        else
        {
            newState = NPCBehaviorState.Normal;
        }
        
        if (newState != BehaviorState)
        {
            BehaviorState = newState;
            LastBehaviorChange = DateTime.Now;
        }
    }
    
    public bool ShouldShowMarker()
    {
        return RecognitionLevel > 0.05f || IsActivelyRecognizing;
    }
    
    public float GetMarkerIntensity()
    {
        return Math.Min(1.0f, RecognitionLevel + (IsActivelyRecognizing ? 0.2f : 0.0f));
    }
    
    public bool IsValid()
    {
        return NPC != null && NPC.Exists() && !NPC.IsDead;
    }
    
    public bool HasTimedOut(TimeSpan timeout)
    {
        return DateTime.Now - LastUpdate > timeout;
    }
    
    public string GetStatusDescription()
    {
        string status = $"Recognition: {RecognitionLevel:P1} ({BehaviorState})";
        
        if (HasRecognized)
            status += " [RECOGNIZED]";
        if (IsActivelyRecognizing)
            status += " [ACTIVE]";
            
        return status;
    }
}

public enum NPCBehaviorState
{
    Normal,         // NPC is going about their business
    Observing,      // NPC is starting to notice something
    Suspicious,     // NPC is actively watching the player
    Alert,          // NPC is concerned and may take action
    Investigating,  // NPC is actively investigating (police/security)
    Hostile         // NPC is hostile towards the player
} 