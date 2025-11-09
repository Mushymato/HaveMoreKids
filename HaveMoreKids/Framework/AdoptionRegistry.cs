using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace HaveMoreKids.Framework;

internal static class AdoptionRegistry
{
    internal const string Action_ShowAdoption = $"{ModEntry.ModId}_ShowAdoption";
    internal const string Trigger_Adoption = $"{ModEntry.ModId}_Adoption";

    internal static void Register()
    {
        GameLocation.RegisterTileAction(Action_ShowAdoption, TileShowAdoption);
        TriggerActionManager.RegisterTrigger(Trigger_Adoption);
    }

    private static bool TileShowAdoption(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (Game1.player.HouseUpgradeLevel < 2)
        {
            Game1.drawObjectDialogue(AssetManager.LoadString("Adoption_CantAdoptYet_BiggerHouse"));
            return false;
        }

        List<KeyValuePair<string, string>> responses = [];

        if (CribManager.HasAvailableCribs(Utility.getHomeOfFarmer(Game1.player)))
        {
            responses.Add(
                new("Adoption_Generic", FormAdoptionOptionText("Adoption_Generic", ModEntry.Config.DaysPregnant, ""))
            );
        }

        GameStateQueryContext ctx = new(location, farmer, null, null, null);
        foreach ((string kidId, KidDefinitionData kidDef) in AssetManager.KidDefsByKidId)
        {
            if (
                KidHandler.KidEntries.ContainsKey(kidId)
                || kidDef.AdoptedFromNPC != null && KidHandler.KidEntries.Values.Any(entry => entry.KidNPCId == kidId)
            )
            {
                continue;
            }
            if (
                kidDef.CanAdoptFromAdoptionRegistry != null
                && GameStateQuery.CheckConditions(kidDef.CanAdoptFromAdoptionRegistry, ctx)
                && !Game1.getAllFarmers().Any(player => player.NextKidId() is string nextKidId && nextKidId == kidId)
            )
            {
                string? displayName = null;
                if (
                    kidDef.AdoptedFromNPC != null
                    && kidDef.AdoptedFromNPC == kidId
                    && NPCLookup.GetNonChildNPC(kidDef.AdoptedFromNPC) is NPC npc
                    && npc.GetData() is CharacterData npcData
                )
                {
                    displayName = TokenParser.ParseText(npcData.DisplayName);
                }
                else if (AssetManager.ChildData.TryGetValue(kidId, out CharacterData? kidData))
                {
                    displayName = TokenParser.ParseText(kidData.DisplayName);
                }
                else
                {
                    ModEntry.Log($"No data found for '{kidId}', skipping", LogLevel.Error);
                    continue;
                }
                int daysToAdopt = kidDef.DaysFromAdoptionRegistry ?? ModEntry.Config.DaysPregnant;
                responses.Add(
                    new($"Adoption_{kidId}", FormAdoptionOptionText("Adoption_Specific", daysToAdopt, displayName))
                );
            }
        }
        if (responses.Count == 0)
        {
            Game1.drawObjectDialogue(AssetManager.LoadString("Adoption_CantAdoptYet_NoCrib"));
            return false;
        }

        location.ShowPagedResponses(AssetManager.LoadString("Adoption_Prompt"), responses, OnResponse);
        return true;
    }

    private static string FormAdoptionOptionText(string translationKey, int daysToAdopt, string displayName)
    {
        return AssetManager.LoadStringReturnNullIfNotFound(
            translationKey,
            AssetManager.LoadString(
                daysToAdopt switch
                {
                    0 => "Adoption_Time_Tonight",
                    1 => "Adoption_Time_Day",
                    _ => "Adoption_Time_Days",
                },
                daysToAdopt
            ),
            displayName
        );
    }

    private static void OnResponse(string obj)
    {
        if (!obj.StartsWith("Adoption_"))
            return;
        if (obj == "Adoption_Generic")
        {
            GameDelegates.DoSetNewChildEvent(
                out _,
                ModEntry.Config.DaysPregnant,
                null,
                GameDelegates.PLAYER_PARENT,
                AssetManager.LoadString("Adoption_Done")
            );
        }
        else
        {
            string kidId = obj.AsSpan()[9..].ToString();
            if (AssetManager.KidDefsByKidId.TryGetValue(kidId, out KidDefinitionData? kidDef))
            {
                GameDelegates.DoSetNewChildEvent(
                    out _,
                    kidDef.DaysFromAdoptionRegistry ?? ModEntry.Config.DaysPregnant,
                    kidId,
                    GameDelegates.PLAYER_PARENT,
                    AssetManager.LoadString("Adoption_Done"),
                    skipHomeCheck: kidDef.AdoptedFromNPC != null
                );
            }
            else
            {
                return;
            }
        }
        TriggerActionManager.Raise(Trigger_Adoption);
    }
}
