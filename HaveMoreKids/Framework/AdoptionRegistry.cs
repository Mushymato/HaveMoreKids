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
            )
            {
                string? displayName = null;
                if (
                    def.AdoptedFromNPC != null
                    && Game1.characterData.TryGetValue(def.AdoptedFromNPC, out CharacterData? npcData)
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
