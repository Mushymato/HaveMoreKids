using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using StardewValley;
using StardewValley.Characters;

namespace HaveMoreKids.Framework;

internal static partial class Patches
{
    private static readonly Regex dialogueTokenizedStringPattern = new(@"\[(HMK_\w+)\]");

    internal static void Apply_Narrative()
    {
        // Allow easier time of using kid actors in events
        harmony.Patch(
            original: AccessTools.DeclaredMethod(
                typeof(Event),
                nameof(Event.getActorByName),
                [typeof(string), typeof(bool).MakeByRefType(), typeof(bool)]
            ),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Event_getActorByName_Postfix))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(
                typeof(Event.DefaultCommands),
                nameof(Event.DefaultCommands.LoadActors)
            ),
            postfix: new HarmonyMethod(typeof(Patches), nameof(EventDefaultCommands_LoadActors_Postfix))
        );
        // Add more spouse dialogue for kid counts
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.marriageDuties)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(NPC_marriageDuties_Prefix)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(NPC_marriageDuties_Postfix))
        );
        // Put in default value for HMK tokenized strings
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Dialogue), "parseDialogueString"),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Dialogue_parseDialogueString_Prefix))
        );
    }

    private static void Dialogue_parseDialogueString_Prefix(Dialogue __instance, ref string masterString)
    {
        string? kidId;
        if (__instance.speaker is Child child)
        {
            kidId = child.Name;
        }
        else
        {
            kidId = __instance.speaker.GetHMKChildNPCKidId();
        }
        if (kidId == null)
        {
            return;
        }
        masterString = dialogueTokenizedStringPattern.Replace(masterString, $"[$1 {kidId}]");
    }

    private static void NPC_marriageDuties_Prefix(NPC __instance, ref int __state)
    {
        __state = __instance.daysAfterLastBirth;
    }

    private static void NPC_marriageDuties_Postfix(NPC __instance, ref int __state)
    {
        if (__state > 0)
        {
            return;
        }
        int childrenCount = Game1.player.getChildrenCount();
        Child? mostRecentChild = childrenCount > 0 ? Game1.player.getChildren().Last() : null;
        if (AssetManager.TryGetDialogueForChild(__instance, mostRecentChild, "HMK_NewChild", childrenCount, out _))
        {
            MarriageDialogueReference marriageDialogueReference =
                new("MarriageDialogue", "HMK_NewChild", false, mostRecentChild?.displayName ?? "");
            __instance.currentMarriageDialogue.Clear();
            __instance.shouldSayMarriageDialogue.Value = true;
            __instance.currentMarriageDialogue.Add(marriageDialogueReference);
        }
    }

    private static void EventDefaultCommands_LoadActors_Postfix(Event @event, string[] args, EventContext context)
    {
        foreach (NPC actor in @event.actors)
        {
            if (actor is Child kid)
            {
                ModEntry.Log($"{kid.Name}: {kid.TilePoint}");
            }
        }
    }

    private static void Event_getActorByName_Postfix(Event __instance, ref string name, ref NPC __result)
    {
        if (name == null || !AssetManager.ChildData.ContainsKey(name))
            return;

        name = KidHandler.FormChildNPCId(name);
        foreach (NPC actor in __instance.actors)
        {
            if (actor.Name == name)
            {
                __result = actor;
                return;
            }
        }
        ModEntry.Log($"Fetching child NPC: {name}");
        if (Game1.getCharacterFromName(name) is NPC childAsNPC)
        {
            __result = new NPC(
                childAsNPC.Sprite.Clone(),
                childAsNPC.Position,
                childAsNPC.FacingDirection,
                childAsNPC.Name
            )
            {
                Birthday_Day = childAsNPC.Birthday_Day,
                Birthday_Season = childAsNPC.Birthday_Season,
                Gender = childAsNPC.Gender,
                Portrait = childAsNPC.Portrait,
                EventActor = true,
                displayName = childAsNPC.displayName,
                drawOffset = childAsNPC.drawOffset,
                TemporaryDialogue = new Stack<Dialogue>(
                    childAsNPC.CurrentDialogue.Select((Dialogue p) => new Dialogue(p))
                ),
                Age = childAsNPC.Age,
            };
            __instance.actors.Add(__result);
        }
    }
}
