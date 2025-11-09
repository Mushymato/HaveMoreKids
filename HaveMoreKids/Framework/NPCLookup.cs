using System.Diagnostics.CodeAnalysis;
using HaveMoreKids.Framework;
using StardewValley;
using StardewValley.Characters;

internal static class NPCLookup
{
    private static readonly Dictionary<string, NPC> cache = [];

    internal static void Clear() => cache.Clear();

    internal static NPC? GetNonChildNPC(string? npcId)
    {
        if (npcId == null)
        {
            return null;
        }
        if (cache.TryGetValue(npcId, out NPC? npc))
        {
            return npc;
        }
        NPC? match = null;
        Utility.ForEachCharacter(
            npc =>
            {
                if (npc.Name == npcId && npc is not Child && npc.IsVillager && !npc.EventActor)
                {
                    if (npc.currentLocation?.IsActiveLocation() ?? false)
                    {
                        match = npc;
                        return false;
                    }
                }
                return true;
            },
            false
        );
        if (match != null)
        {
            cache[npcId] = match;
        }
        return match;
    }

    internal static NPC? GetNPCParent(string? npcParentId)
    {
        if (
            npcParentId != null
            && npcParentId != KidHandler.Parent_NPC_ADOPT
            && npcParentId != KidHandler.Parent_SOLO_BIRTH
        )
        {
            return GetNonChildNPC(npcParentId);
        }
        return null;
    }
}
