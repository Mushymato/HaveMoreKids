using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace HaveMoreKids.Framework.ExtraFeatures;

internal static class ChildrenRegistry
{
    internal const string Action_ChildrenRegistry = $"{ModEntry.ModId}_ChildrenRegistry";

    internal static void Register()
    {
        GameLocation.RegisterTileAction(Action_ChildrenRegistry, TileChildrenRegistry);
    }

    private static bool TileChildrenRegistry(GameLocation location, string[] args, Farmer farmer, Point point)
    {
        if (!farmer.getChildren().Any(kid => kid.GetHMKAdoptedFromNPCId() is null))
        {
            return TileShowAdoption(location, farmer);
        }
        location.createQuestionDialogue(
            AssetManager.LoadString("Registry_Prompt"),
            [
                new("hmk_cr_adoption", AssetManager.LoadString("Registry_Adoption")),
                new("hmk_cr_renamer", AssetManager.LoadString("Registry_Rename")),
                new("hmk_cr_cancel", Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel")),
            ],
            (who, response) =>
            {
                switch (response)
                {
                    case "hmk_cr_renamer":
                        TileShowRenamer(location, farmer);
                        break;
                    case "hmk_cr_adoption":
                        TileShowAdoption(location, farmer);
                        break;
                }
            },
            speaker: null
        );
        return true;
    }

    #region rename
    private static void TileShowRenamer(GameLocation location, Farmer farmer)
    {
        Dictionary<string, Child> kids = [];
        List<KeyValuePair<string, string>> responsePairs = [];
        foreach (Child kid in farmer.getChildren())
        {
            if (kid.GetHMKAdoptedFromNPCId() is not null)
                continue;
            responsePairs.Add(new(kid.Name, kid.displayName));
            kids[kid.Name] = kid;
        }
        location.ShowPagedResponses(
            AssetManager.LoadString("Rename_Prompt"),
            responsePairs,
            (response) => ShowKidRenameMenu(kids[response], farmer),
            addCancel: true
        );
    }

    private static void ShowKidRenameMenu(Child kid, Farmer farmer)
    {
        Game1.activeClickableMenu = new NamingMenu(
            (s) =>
            {
                string oldName = kid.displayName;
                kid.SetChildDisplayName(s);
                Game1.exitActiveMenu();
                Farmer.canMoveNow(farmer);
                Game1.addHUDMessage(
                    HUDMessage.ForCornerTextbox(
                        AssetManager.LoadString("Rename_Child_Message", oldName, kid.displayName)
                    )
                );
            },
            AssetManager.LoadString("Remame_Child_Label", kid.displayName),
            kid.displayName
        );
    }
    #endregion

    #region adoption
    private static bool TileShowAdoption(GameLocation location, Farmer farmer)
    {
        if (farmer.HouseUpgradeLevel < 2)
        {
            Game1.drawObjectDialogue(AssetManager.LoadString("Adoption_CantAdoptYet_BiggerHouse"));
            return false;
        }

        List<KeyValuePair<string, string>> responses = [];
        bool haveCribs = CribManager.HasAvailableCribs(Utility.getHomeOfFarmer(farmer));
        bool underMaxChildren = Patches.UnderMaxChildrenCount(farmer);

        if (haveCribs && underMaxChildren)
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
                    if (!haveCribs && underMaxChildren)
                    {
                        continue;
                    }
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
            if (!haveCribs)
                Game1.drawObjectDialogue(AssetManager.LoadString("Adoption_CantAdoptYet_NoCrib"));
            else if (!underMaxChildren)
                Game1.drawObjectDialogue(AssetManager.LoadString("Adoption_CantAdoptYet_MaxChildren"));
            return false;
        }

        location.ShowPagedResponses(AssetManager.LoadString("Adoption_Prompt"), responses, OnAdoptionResponse);
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

    private static void OnAdoptionResponse(string obj)
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
        TriggerActionManager.Raise(GameDelegates.Trigger_Adoption);
    }
    #endregion
}
