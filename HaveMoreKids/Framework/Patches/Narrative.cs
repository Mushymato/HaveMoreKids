using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
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
        // Make sure the draw loop doesn't crash sigh
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Game1), nameof(Game1.drawDialogueBox), []),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Game1_drawDialogueBox_Prefix))
        );
    }

    private static void Game1_drawDialogueBox_Prefix()
    {
        if (Game1.currentSpeaker != null && Game1.currentSpeaker.CurrentDialogue.Count == 0)
        {
            ModEntry.Log("Game1.currentSpeaker.CurrentDialogue is empty! Force end current dialogue to prevent crash.", LogLevel.Warn);
            Game1.currentSpeaker = null;
            Game1.dialogueUp = false;
            Game1.currentDialogueCharacterIndex = 0;
        }
    }

    private static void Dialogue_parseDialogueString_Prefix(Dialogue __instance, ref string masterString)
    {
        string? kidId;
        if (__instance.speaker is Child child)
        {
            // some shenanigans cause this to get called between saving???
            kidId = child.KidHMKId();
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
            MarriageDialogueReference marriageDialogueReference = new(
                "MarriageDialogue",
                dialogueKeyPrefix,
                false,
                mostRecentChild?.displayName ?? ""
            );
            __instance.currentMarriageDialogue.Clear();
            __instance.shouldSayMarriageDialogue.Value = true;
            __instance.currentMarriageDialogue.Add(marriageDialogueReference);
        }
    }

    private static void EventDefaultCommands_LoadActors_Postfix(Event @event, string[] args, EventContext context)
    {
        if (!@event.isFestival)
            return;
        @event.actors.RemoveWhere(actor => actor is Child && actor.IsInvisible);
        int stationaryKidsCount = 0;
        List<Point>? kidCanStand = null;
        foreach (NPC actor in @event.actors)
        {
            string? actorName = null;
            if (actor is Child kid && kid.KidDisplayName(allowNull: false) is string displayName)
            {
                kid.displayName = displayName;
                actorName = kid.Name;
                if (
                    AssetManager.KidDefsByKidId.TryGetValue(kid.Name, out KidDefinitionData? kidDef)
                    && (kidDef.FestivalBehaviour?.TryGetValue(@event.id, out KidFestivalBehaviour? behaviour) ?? false)
                    && behaviour != null
                )
                {
                    if (behaviour.IsStationary)
                    {
                        kid.IsWalkingInSquare = false;
                        kid.Halt();
                    }
                    if (behaviour.Position is Vector3 pos)
                    {
                        kid.setTilePosition((int)pos.X, (int)pos.Y);
                        kid.faceDirection((int)pos.Z);
                    }
                    else if (behaviour.IsStationary)
                    {
                        // avoid collisions as much as possible
                        if (stationaryKidsCount > 0)
                        {
                            kidCanStand ??= KidPathingManager.TileStandableBFS(
                                context.Location,
                                kid.TilePoint,
                                5,
                                Game1.player.getChildrenCount(),
                                collisionMask: CollisionMask.All
                            );
                            if (kidCanStand.Count > stationaryKidsCount)
                            {
                                kid.setTilePosition(
                                    kidCanStand[stationaryKidsCount].X,
                                    kidCanStand[stationaryKidsCount].Y
                                );
                            }
                        }
                        stationaryKidsCount++;
                    }
                }
            }
            else if (actor.GetHMKChildNPCKidId() is string kidId)
            {
                actorName = kidId;
            }
            if (actorName != null)
            {
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
        ModEntry.Log($"Fetching child as NPC: {name}");
        if (NPCLookup.GetNonChildNPC(name) is NPC childAsNPC)
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
