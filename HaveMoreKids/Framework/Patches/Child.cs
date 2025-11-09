using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework;

internal record TempCAD(CharacterAppearanceData Data)
{
    readonly string? originalCondition = Data.Condition;
    readonly int originalPrecedence = Data.Precedence;

    public void Restore()
    {
        Data.Condition = originalCondition;
        Data.Precedence = originalPrecedence;
    }
}

internal static partial class Patches
{
    internal const string Condition_KidId = "KID_ID";

    internal static Action<NPC> NPC_ChooseAppearance_Call = null!;
    internal static Func<NPC, Stack<Dialogue>> NPC_loadCurrentDialogue_Call = null!; // coulda used reflection for this one but whatever
    private static bool In_NPC_ChooseAppearance_Call;

    private static void MakeDynamicMethods()
    {
        DynamicMethod dm;
        ILGenerator gen;

        var NPC_ChooseAppearance = AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.ChooseAppearance));
        dm = new DynamicMethod("NPC_ChooseAppearance_Call", typeof(void), [typeof(NPC)]);
        gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldnull);
        gen.Emit(OpCodes.Call, NPC_ChooseAppearance);
        gen.Emit(OpCodes.Ret);
        NPC_ChooseAppearance_Call = dm.CreateDelegate<Action<NPC>>();

        var NPC_loadCurrentDialogue = AccessTools.DeclaredMethod(typeof(NPC), "loadCurrentDialogue");
        dm = new DynamicMethod("NPC_loadCurrentDialogue_Call", typeof(Stack<Dialogue>), [typeof(NPC)]);
        gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Call, NPC_loadCurrentDialogue);
        gen.Emit(OpCodes.Ret);
        NPC_loadCurrentDialogue_Call = dm.CreateDelegate<Func<NPC, Stack<Dialogue>>>();
    }

    internal static void Apply_Child()
    {
        MakeDynamicMethods();

        // Make the child use appearance system
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.reloadSprite)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_reloadSprite_Postfix))
        );
        // Alter rate of child aging
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.dayUpdate)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(Child_dayUpdate_Transpiler))
        );
        // Take over child behavior
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.tenMinuteUpdate)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Child_tenMinuteUpdate_Prefix))
            {
                priority = Priority.VeryHigh,
            }
        );
        // Adjust FarmHouse getChildren/getChildrenCount so that it also looks at Farm
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.getChildren)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FarmHouse_getChildren_Postfix))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.getChildrenCount)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FarmHouse_getChildrenCount_Postfix))
        );
        // Use special child data for the child form
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.GetData)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_GetData_Postfix))
        );
        // Put child in the right crib
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.resetForPlayerEntry)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_resetForPlayerEntry_Postfix))
            {
                // :u
                after = ["mushymato.MMAP"],
            }
        );
        // Child is in a crib
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.isInCrib)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_isInCrib_Postfix))
            {
                // :u
                after = ["mushymato.MMAP"],
            }
        );
        // Talk to the child once every day
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.checkAction)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Child_checkAction_Prefix))
        );
        // Let child have regular npc dialogue
        harmony.Patch(
            original: AccessTools.PropertyGetter(typeof(NPC), nameof(NPC.Dialogue)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(NPC_Dialogue_Transpiler))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.GetDialogueSheetName)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(NPC_GetDialogueSheetName_Postfix))
        );
        harmony.Patch(
            original: AccessTools.PropertyGetter(typeof(NPC), nameof(NPC.CurrentDialogue)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(NPC_CurrentDialogue_Postfix))
        );
        // Let child receive 1 gift a day
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.CanReceiveGifts)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(NPC_CanReceiveGifts_Postfix))
        );
        // Fix display name
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), "translateName"),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_translateName_Postfix))
        );
        // Fix mugshot for Child Age>3
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.getMugShotSourceRect)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_getMugShotSourceRect_Postfix))
        );
        // Fix default texture
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.getTextureName)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(NPC_getTextureName_Postfix))
        );
        // When getRidOfChildren(), summon back any kids roaming in the farm first
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Farmer), nameof(Farmer.getRidOfChildren)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Farmer_getRidOfChildren_Prefix))
        );
        // Change schedule data name
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.getMasterScheduleRawData)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(NPC_getMasterScheduleRawData_Transpiler))
        );
    }

    private static string ModifyScheduleAssetName(NPC npc, string scheduleAssetName)
    {
        if (npc.GetHMKChildNPCKidId() is string kidId)
        {
            return $"{AssetManager.Asset_CharactersSchedule}\\{kidId}";
        }
        return scheduleAssetName;
    }

    private static IEnumerable<CodeInstruction> NPC_getMasterScheduleRawData_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            // IL_0048: ldstr "Mainland"
            // IL_004d: call string [System.Runtime]System.String::Concat(string, string)
            // IL_0052: stloc.0
            matcher.MatchStartForward(
                [new(inst => inst.IsLdloc()), new(OpCodes.Ldstr), new(OpCodes.Call), new(inst => inst.IsStloc())]
            );
            CodeInstruction ldLoc = matcher.Instruction.Clone();
            matcher.Advance(3);
            CodeInstruction stLoc = matcher.Instruction.Clone();
            matcher
                .Advance(2)
                .Insert(
                    [
                        new(OpCodes.Ldarg_0),
                        ldLoc,
                        new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyScheduleAssetName))),
                        stLoc,
                    ]
                );

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in NPC_getMasterScheduleRawData_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void Farmer_getRidOfChildren_Prefix(Farmer __instance)
    {
        KidPathingManager.GoingToTheFarm.Remove(__instance.UniqueMultiplayerID);
        KidPathingManager.ManagedNPCKids.RemoveWhere(kv => kv.Key.idOfParent.Value == __instance.UniqueMultiplayerID);
        KidPathingManager.ReturnKidsToHouse(__instance.getChildren());
    }

    private static void NPC_GetDialogueSheetName_Postfix(NPC __instance, ref string __result)
    {
        string? defaultSheetName = null;
        if (__instance is Child kid)
        {
            if (kid.GetHMKAdoptedFromNPCId() is string npcId)
            {
                defaultSheetName = npcId;
            }
            else
            {
                defaultSheetName = __instance.Name;
            }
        }
        else if (__instance.GetHMKChildNPCKidId() is string kidId)
        {
            defaultSheetName = kidId;
        }

        if (defaultSheetName != null)
        {
            __result = GetHMKDialogueSheet(__instance) ?? defaultSheetName;
        }
    }

    private static string? GetHMKDialogueSheet(NPC npc)
    {
        if (
            npc is Child child
            && child.GetHMKKidDef() is KidDefinitionData kidDef
            && !string.IsNullOrEmpty(kidDef.DialogueSheetName)
        )
        {
            return kidDef.DialogueSheetName;
        }
        return null;
    }

    private static IEnumerable<Child> GetChildrenOnFarm(FarmHouse __instance)
    {
        if (__instance.OwnerId == 0)
        {
            yield break;
        }
        if (__instance.GetParentLocation() is Farm farm)
        {
            foreach (Character chara in farm.characters)
            {
                if (chara is Child kid && kid.idOfParent.Value == __instance.OwnerId)
                {
                    yield return kid;
                }
            }
        }
    }

    private static void FarmHouse_getChildren_Postfix(FarmHouse __instance, ref List<Child> __result)
    {
        __result.AddRange(GetChildrenOnFarm(__instance));
        __result.Sort((kidA, kidB) => kidB.daysOld.Value.CompareTo(kidA.daysOld.Value));
    }

    private static void FarmHouse_getChildrenCount_Postfix(FarmHouse __instance, ref int __result)
    {
        __result += GetChildrenOnFarm(__instance).Count();
    }

    private static IEnumerable<CodeInstruction> Child_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            LocalBuilder locLayerDepth = generator.DeclareLocal(typeof(float));

            // check for the correct new layer depth
            matcher.MatchEndForward(
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, AccessTools.DeclaredProperty(typeof(NPC), nameof(NPC.IsInvisible))),
                    new(OpCodes.Brtrue),
                ]
            );

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Child_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void Child_isInCrib_Postfix(Child __instance, ref bool __result)
    {
        if (!__result)
        {
            CribAssign? cribAssign = CribManager.GetCribAssignment(__instance);
            __result = cribAssign?.IsInCrib() ?? false;
        }
    }

    private static void Child_resetForPlayerEntry_Postfix(Child __instance) => CribManager.PutInACrib(__instance);

    private static void NPC_getTextureName_Postfix(NPC __instance, ref string __result)
    {
        string? npcId = __instance.GetHMKAdoptedFromNPCId();
        if (In_NPC_ChooseAppearance_Call && npcId != null)
        {
            __result = __instance.GetData()?.TextureName ?? npcId;
        }
        else if (__instance is Child)
        {
            if (In_Billboard_GetEventsForDay && npcId == null)
            {
                __result = null!;
            }
        }
        else if (__instance.GetHMKChildNPCKidId() != null)
        {
            __result = __instance.GetData()?.TextureName ?? AssetManager.Asset_DefaultTextureName;
        }
    }

    private static void Child_translateName_Postfix(Child __instance, ref string __result)
    {
        if (__instance.GetHMKAdoptedFromNPCId() is string npcId)
        {
            if (Game1.characterData.TryGetValue(npcId, out CharacterData? data))
            {
                __result = TokenParser.ParseText(data.DisplayName);
            }
        }
        else if (__instance.KidDisplayName() is string displayName)
        {
            __result = displayName;
        }
    }

    private static void Child_getMugShotSourceRect_Postfix(Child __instance, ref Rectangle __result)
    {
        if (__instance.Age > 3)
        {
            __result = __instance.GetData()?.MugShotSourceRect ?? new Rectangle(0, 4, 16, 24);
        }
    }

    private static void NPC_CanReceiveGifts_Postfix(NPC __instance, ref bool __result)
    {
        if (__instance is Child kid && kid.Age >= 3 && Game1.NPCGiftTastes.ContainsKey(kid.Name))
        {
            __result = kid.GetData().CanReceiveGifts;
        }
    }

    private static void NPC_CurrentDialogue_Postfix(NPC __instance, ref Stack<Dialogue> __result)
    {
        if (__instance is Child kid && kid.Age >= 3)
        {
            Game1.npcDialogues.TryGetValue(__instance.Name, out var value);
            value ??= Game1.npcDialogues[__instance.Name] = NPC_loadCurrentDialogue_Call(__instance);
            __result = value;
        }
    }

    private static IEnumerable<CodeInstruction> NPC_Dialogue_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_0018: ldarg.0
            // IL_0019: isinst StardewValley.Characters.Child
            // IL_001e: brfalse.s IL_0029
            matcher
                .MatchEndForward([new(OpCodes.Ldarg_0), new(OpCodes.Isinst, typeof(Child)), new(OpCodes.Brfalse_S)])
                .ThrowIfNotMatch("Did not find 'this is Child'");
            matcher.Opcode = OpCodes.Brtrue_S;
            matcher.Advance(-1).Operand = typeof(NPC);
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Child_Dialogue_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void NPC_Dialogue_Postfix()
    {
        throw new NotImplementedException();
    }

    private static bool Child_checkAction_Prefix(Child __instance, Farmer who, GameLocation l, ref bool __result)
    {
        if (__instance.Age < 3)
        {
            return true;
        }

        if (l is Farm)
            __instance.controller = null;

        if (!who.IsLocalPlayer)
        {
            return true;
        }
        if (who.ActiveObject != null && __instance.tryToReceiveActiveObject(who, probe: true))
        {
            __result = __instance.tryToReceiveActiveObject(who);
            return !__result;
        }

        int hearts = 0;
        if (Game1.player.friendshipData.TryGetValue(__instance.Name, out Friendship friendship))
        {
            hearts = friendship.Points / 250;
        }
        if (!__instance.checkForNewCurrentDialogue(hearts))
        {
            __instance.checkForNewCurrentDialogue(hearts, noPreface: true);
        }

        if (__instance.CurrentDialogue.Count > 0)
        {
            Game1.drawDialogue(__instance);
            who.talkToFriend(__instance); // blocks the vanilla interact
            __instance.faceTowardFarmerForPeriod(4000, 3, faceAway: false, who);
            __result = true;
            return false;
        }
        return true;
    }

    private static void Child_GetData_Postfix(NPC __instance, ref CharacterData __result)
    {
        if (__result != null || __instance.Name == null)
            return;
        if (__instance.GetHMKAdoptedFromNPCId() is string npcId)
        {
            if (Game1.characterData.TryGetValue(npcId, out CharacterData? data))
            {
                __result = data;
            }
        }
        else if (__instance is Child && AssetManager.ChildData.TryGetValue(__instance.Name, out CharacterData? data))
        {
            __result = data;
        }
    }

    private static sbyte ModifyDaysBaby(sbyte days) => ModEntry.Config.TotalDaysBaby;

    private static sbyte ModifyDaysCrawler(sbyte days) => ModEntry.Config.TotalDaysCrawer;

    private static sbyte ModifyDaysToddler(sbyte days) => ModEntry.Config.TotalDaysToddler;

    private static IEnumerable<CodeInstruction> Child_dayUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            // IL_0093: ldfld class Netcode.NetInt StardewValley.Characters.Child::daysOld
            // IL_0098: callvirt instance !0 class Netcode.NetFieldBase`2<int32, class Netcode.NetInt>::get_Value()
            // IL_009d: ldc.i4.s 55

            List<ValueTuple<sbyte, MethodInfo>> patchDays =
            [
                new(55, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyDaysToddler))),
                new(27, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyDaysCrawler))),
                new(13, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyDaysBaby))),
            ];
            foreach ((sbyte days, MethodInfo callFunc) in patchDays)
            {
                matcher
                    .MatchEndForward(
                        [
                            new(OpCodes.Ldfld, AccessTools.Field(typeof(Child), nameof(Child.daysOld))),
                            new(
                                OpCodes.Callvirt
                            // AccessTools.PropertyGetter(typeof(Netcode.NetInt), nameof(Netcode.NetInt.Value))
                            ),
                            new(OpCodes.Ldc_I4_S, days),
                        ]
                    )
                    .ThrowIfNotMatch($"Did not find 'daysOld.Value >= {days}'")
                    .Advance(1)
                    .InsertAndAdvance([new(OpCodes.Call, callFunc)]);
            }

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Child_dayUpdate_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    /// <summary>Alter child behavior so they can go outside to the Farm</summary>
    /// <param name="__instance"></param>
    /// <returns></returns>
    private static bool Child_tenMinuteUpdate_Prefix(Child __instance)
    {
        if (!Game1.IsMasterGame || __instance.IsInvisible || __instance.Age < 3)
        {
            return true;
        }
        return KidPathingManager.TenMinuteUpdate(__instance);
    }

    /// <summary>
    /// Apply appearances on the kid
    /// </summary>
    /// <param name="__instance"></param>
    private static void Child_reloadSprite_Postfix(Child __instance)
    {
        CharacterData? childData = __instance.GetData();
        if (childData?.Appearance is not List<CharacterAppearanceData> appearances || appearances.Count == 0)
        {
            return;
        }
        List<TempCAD> tmpCADs = [];
        foreach (CharacterAppearanceData data in appearances)
        {
            if (__instance.Age < 3)
            {
                tmpCADs.Add(new(data));
                if (data.AppearanceIsBaby())
                {
                    data.Precedence = Math.Min(data.Precedence, -100);
                    data.Condition =
                        data.Condition != null ? data.Condition.Replace(Condition_KidId, __instance.Name) : "TRUE";
                }
                else
                {
                    data.Precedence = Math.Max(data.Precedence, 100);
                    data.Condition = "FALSE";
                }
            }
            else
            {
                if (data.AppearanceIsBaby())
                {
                    tmpCADs.Add(new(data));
                    data.Precedence = Math.Max(data.Precedence, 100);
                    data.Condition = "FALSE";
                }
                else if (data.Condition != null)
                {
                    tmpCADs.Add(new(data));
                    data.Condition = data.Condition.Replace(Condition_KidId, __instance.Name);
                }
            }
        }

        In_NPC_ChooseAppearance_Call = true;
        NPC_ChooseAppearance_Call(__instance);
        In_NPC_ChooseAppearance_Call = false;

        if (__instance.Age < 3)
        {
            __instance.Sprite.SpriteWidth = 22;
            __instance.Sprite.SpriteHeight = __instance.Age == 1 ? 32 : 16;
            __instance.Sprite.currentFrame = 0;
            switch (__instance.Age)
            {
                case 1:
                    __instance.Sprite.currentFrame = 4;
                    break;
                case 2:
                    __instance.Sprite.currentFrame = 32;
                    break;
            }
            __instance.HideShadow = true;
        }
        else
        {
            __instance.Sprite.SpriteWidth = childData.Size.X;
            __instance.Sprite.SpriteHeight = childData.Size.Y;
            __instance.Sprite.currentFrame = 0;
            __instance.HideShadow = false;
            __instance.Breather = childData.Breather && childData.BreathChestRect.HasValue;
        }
        __instance.Sprite.UpdateSourceRect();

        foreach (var tmp in tmpCADs)
            tmp.Restore();

        return;
    }
}
