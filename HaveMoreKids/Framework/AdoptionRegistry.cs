using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData.Characters;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal static class AdoptionRegistry
{
    internal const string Action_ShowAdoption = $"{ModEntry.ModId}_ShowAdoption";

    internal static void Register()
    {
        GameLocation.RegisterTileAction(Action_ShowAdoption, TileShowAdoption);
    }

    private static bool TileShowAdoption(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (Game1.player.HouseUpgradeLevel <= 1)
        {
            Game1.drawObjectDialogue(AssetManager.LoadString("Adoption_CantAdoptYet"));
            return false;
        }

        List<KeyValuePair<string, string>> responses =
        [
            new("Adoption_Generic", AssetManager.LoadString("Adoption_Generic", ModEntry.Config.DaysPregnant)),
        ];
        GameStateQueryContext ctx = new(location, farmer, null, null, null);
        foreach ((string kidId, KidDefinitionData def) in AssetManager.KidDefsByKidId)
        {
            if (
                KidHandler.KidEntries.ContainsKey(kidId)
                || def.AdoptedFromNPC != null && KidHandler.KidEntries.Values.Any(entry => entry.KidNPCId == kidId)
            )
            {
                continue;
            }
            if (
                def.CanAdoptFromAdoptionRegistry != null
                && GameStateQuery.CheckConditions(def.CanAdoptFromAdoptionRegistry, ctx)
                && !Game1.getAllFarmers().Any(player => player.NextKidId() is string nextKidId && nextKidId == kidId)
            )
            {
                string? displayName = null;
                if (
                    def.AdoptedFromNPC != null
                    && KidHandler.GetNonChildNPCByName(def.AdoptedFromNPC) is NPC npc
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
                responses.Add(
                    new(
                        $"Adoption_{kidId}",
                        AssetManager.LoadStringReturnNullIfNotFound(
                            "Adoption_Specific",
                            displayName,
                            ModEntry.Config.DaysPregnant
                        )
                    )
                );
            }
        }
        location.ShowPagedResponses(AssetManager.LoadString("Adoption_Prompt"), responses, OnResponse);
        return true;
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
                "Player",
                AssetManager.LoadString("Adoption_Done", ModEntry.Config.DaysPregnant),
                Gender.Undefined
            );
            return;
        }
        string kidId = obj.AsSpan()[9..].ToString();
        GameDelegates.DoSetNewChildEvent(
            out _,
            ModEntry.Config.DaysPregnant,
            kidId,
            "Player",
            AssetManager.LoadString("Adoption_Done", ModEntry.Config.DaysPregnant),
            Gender.Undefined
        );
    }
}
