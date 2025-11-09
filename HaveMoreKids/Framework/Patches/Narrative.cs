using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;

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
            kidId = child.GetHMKAdoptedFromNPCId() ?? child.Name;
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
        string dialogueKeyPrefix = $"HMK_NewChild_{5 - __state}";
        int childrenCount = Game1.player.getChildrenCount();
        Child? mostRecentChild = childrenCount > 0 ? Game1.player.getChildren().Last() : null;
        if (
            AssetManager.TryGetDialogueForChild(
                __instance,
                mostRecentChild,
                dialogueKeyPrefix,
                childrenCount,
                out _,
                minCount: 2
            )
        )
        {
            MarriageDialogueReference marriageDialogueReference =
                new("MarriageDialogue", dialogueKeyPrefix, false, mostRecentChild?.displayName ?? "");
            __instance.currentMarriageDialogue.Clear();
            __instance.shouldSayMarriageDialogue.Value = true;
            __instance.currentMarriageDialogue.Add(marriageDialogueReference);
        }
    }

    private static void EventDefaultCommands_LoadActors_Postfix(Event @event, string[] args, EventContext context)
    {
        @event.actors.RemoveWhere(actor => actor is Child && actor.IsInvisible);
        foreach (NPC actor in @event.actors)
        {
            string? actorName = null;
            if (actor is Child kid && kid.KidDisplayName(allowNull: false) is string displayName)
            {
                kid.displayName = displayName;
                actorName = kid.Name;
            }
            else if (actor.GetHMKChildNPCKidId() is string kidId)
            {
                actorName = kidId;
            }
            if (actorName != null)
            {
                ModEntry.Log($"Push dialogue for HMK {actor.Name} ({actorName})");
                if (@event.TryGetFestivalDialogueForYear(actor, actorName, out var dialogue))
                {
                    actor.setNewDialogue(dialogue);
                }
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
