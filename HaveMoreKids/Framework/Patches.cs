using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HaveMoreKids.Framework.BirthingEvents;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.GameData.Characters;
using StardewValley.Locations;
using StardewValley.Objects;

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

internal static class Patches
{
    internal const string Condition_KidId = "KID_ID";
    internal static Action<NPC> NPC_ChooseAppearance_Call = null!;
    internal static Func<NPC, Stack<Dialogue>> NPC_loadCurrentDialogue_Call = null!; // coulda used reflection for this one but whatever

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

    internal static void Apply()
    {
        MakeDynamicMethods();

        Harmony harmony = new(ModEntry.ModId);
        // Change pregnancy chance
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.pickPersonalFarmEvent)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Prefix)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Transpiler)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Postfix))
        );
        // Change pregnancy time needed
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(QuestionEvent), "answerPregnancyQuestion"),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(QuestionEvent_answerPregnancyQuestion_Transpiler))
        );
        // Allow pregnancy past the second child
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.canGetPregnant)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(NPC_canGetPregnant_Transpiler))
        );
        // Allow pregnancy past the second child for players
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.playersCanGetPregnantHere)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_playersCanGetPregnantHere_Transpiler))
        );
        // Make the child use appearance system
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.reloadSprite)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Child_reloadSprite_Prefix))
        );
        // Alter rate of child aging
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.dayUpdate)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(Child_dayUpdate_Transpiler))
        );
        // Use special child data for the child form
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.GetData)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_GetData_Postfix))
        );
        // Talk to the child once every day
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.checkAction)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Child_checkAction_Prefix))
        );
        // Let child have regular npc dialogue
        harmony.Patch(
            original: AccessTools.PropertyGetter(typeof(NPC), nameof(NPC.Dialogue)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(Child_Dialogue_Transpiler))
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
        // Let child sleep on single beds
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.GetChildBed)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(FarmHouse_GetChildBed_Postfix))
        );
    }

    private static void FarmHouse_GetChildBed_Postfix(FarmHouse __instance, ref BedFurniture __result)
    {
        if (ModEntry.Config.UseSingleBedAsChildBed)
            __result ??= __instance.GetBed(BedFurniture.BedType.Single);
    }

    private static void NPC_CanReceiveGifts_Postfix(NPC __instance, ref bool __result)
    {
        if (__instance is Child kid && kid.Age >= 3)
        {
            __result = true;
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

    private static IEnumerable<CodeInstruction> Child_Dialogue_Transpiler(
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

    private static bool Child_checkAction_Prefix(Child __instance, Farmer who, GameLocation l, ref bool __result)
    {
        if (__instance.Age >= 3 && who.IsLocalPlayer)
        {
            ModEntry.Log(__instance.displayName);
            if (who.ActiveObject != null && __instance.tryToReceiveActiveObject(who, probe: true))
            {
                __result = __instance.tryToReceiveActiveObject(who);
                return !__result;
            }
            if (__instance.CurrentDialogue.Count > 0)
            {
                Game1.drawDialogue(__instance);
                who.talkToFriend(__instance); // blocks the vanilla interact
                __instance.faceTowardFarmerForPeriod(4000, 3, faceAway: false, who);
                __result = true;
                return false;
            }
        }
        return true;
    }

    private static void Child_GetData_Postfix(NPC __instance, ref CharacterData __result)
    {
        if (__result != null)
            return;
        if (__instance is Child && AssetManager.ChildData.TryGetValue(__instance.Name, out CharacterData? data))
        {
            __result = data;
        }
    }

    private static sbyte ModifyDaysBaby(sbyte days) => (sbyte)ModEntry.Config.DaysBaby;

    private static sbyte ModifyDaysCrawler(sbyte days) =>
        (sbyte)(ModEntry.Config.DaysBaby + ModEntry.Config.DaysCrawler);

    private static sbyte ModifyDaysToddler(sbyte days) =>
        (sbyte)(ModEntry.Config.DaysBaby + ModEntry.Config.DaysCrawler + ModEntry.Config.DaysToddler);

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

    /// <summary>
    /// Apply appearances on the kid
    /// </summary>
    /// <param name="__instance"></param>
    private static bool Child_reloadSprite_Prefix(Child __instance)
    {
        if (__instance.currentLocation == null)
            return true;
        CharacterData? characterData = __instance.GetData();
        if (characterData?.Appearance is not List<CharacterAppearanceData> appearances || appearances.Count == 0)
        {
            return true;
        }
        List<TempCAD> tmpCADs = [];
        foreach (var data in appearances)
        {
            if (__instance.Age < 3)
            {
                tmpCADs.Add(new(data));
                if (data.Id.StartsWith(AssetManager.Appearances_Prefix_Baby))
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
                if (data.Id.StartsWith(AssetManager.Appearances_Prefix_Baby))
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
        NPC_ChooseAppearance_Call(__instance);
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
            __instance.Sprite.SpriteWidth = characterData.Size.X;
            __instance.Sprite.SpriteHeight = characterData.Size.Y;
            __instance.Sprite.currentFrame = 0;
            __instance.HideShadow = false;
        }
        __instance.Sprite.UpdateSourceRect();

        foreach (var tmp in tmpCADs)
            tmp.Restore();

        return false;
    }

    private static sbyte ModifyDaysPregnant(sbyte days) => (sbyte)ModEntry.Config.DaysPregnant;

    private static IEnumerable<CodeInstruction> QuestionEvent_answerPregnancyQuestion_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            matcher
                .MatchStartForward([new(OpCodes.Ldc_I4_S, (sbyte)14)])
                .ThrowIfNotMatch("Did not find '14'")
                .Advance(1)
                .Insert([new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyDaysPregnant)))]);
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in QuestionEvent_answerPregnancyQuestion_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    /// <summary>
    /// Check should have kids for NPC that have unique kids
    /// If they don't have unique kids, fall down to the base kid count logic
    /// </summary>
    /// <param name="spouse"></param>
    /// <param name="children"></param>
    /// <returns></returns>
    private static bool ShouldHaveKids(NPC spouse, List<Child> children)
    {
        if (AssetManager.PickKidId(spouse) != null)
            return children.All(child => child.Age > 2);
        return false;
    }

    private static int ModifyBaseKidCount(int count) => ModEntry.Config.BaseMaxChildren;

    /// <summary>
    /// Change can get pregnant count to check for number of kids available
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> NPC_canGetPregnant_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            // IL_00b0: ldloc.3
            // IL_00b1: callvirt instance int32 class [System.Collections]System.Collections.Generic.List`1<class StardewValley.Characters.Child>::get_Count()
            // IL_00b6: brfalse.s IL_00d3
            CodeMatcher matcher = new(instructions, generator);
            matcher
                .MatchEndForward(
                    [
                        new(inst => inst.IsLdloc()),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<Child>), nameof(List<Child>.Count))
                        ),
                        new(OpCodes.Brfalse_S),
                        new(inst => inst.IsLdloc()),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'children.Count != 0'");
            CodeInstruction ldlocChildren = matcher.InstructionAt(-3);
            ldlocChildren = new(ldlocChildren.opcode, ldlocChildren.operand);
            matcher
                .CreateLabel(out Label lbl)
                .Insert(
                    [
                        new(OpCodes.Ldarg_0),
                        ldlocChildren,
                        new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ShouldHaveKids))),
                        new(OpCodes.Brfalse_S, lbl),
                        new(OpCodes.Ldc_I4_1),
                        new(OpCodes.Ret),
                    ]
                );
            matcher
                .MatchEndForward(
                    [
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<Child>), nameof(List<Child>.Count))
                        ),
                        new(OpCodes.Ldc_I4_2),
                        new(OpCodes.Bge_S),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'children.Count < 2'")
                .InsertAndAdvance(
                    [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyBaseKidCount)))]
                );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in NPC_canGetPregnant_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    /// <summary>
    /// Check all kids are of right arge
    /// </summary>
    /// <param name="children"></param>
    /// <returns></returns>
    private static bool ShouldHaveKidsPlayer(List<Child> children)
    {
        return children.All(child => child.Age > 2);
    }

    private static IEnumerable<CodeInstruction> Utility_playersCanGetPregnantHere_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            matcher
                .MatchEndForward(
                    [
                        new(
                            OpCodes.Callvirt,
                            AccessTools.DeclaredMethod(typeof(FarmHouse), nameof(FarmHouse.getChildrenCount))
                        ),
                        new(OpCodes.Ldc_I4_2),
                        new(OpCodes.Bge_S),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'farmHouse.getChildrenCount() < 2'")
                .InsertAndAdvance(
                    [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyBaseKidCount)))]
                );

            matcher
                .MatchEndForward(
                    [
                        new(inst => inst.IsLdloc()),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<Child>), nameof(List<Child>.Count))
                        ),
                        new(OpCodes.Ldc_I4_2),
                        new(OpCodes.Bge_S),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'children.Count < 2'");
            CodeInstruction ldlocChildren = matcher.InstructionAt(-3);
            ldlocChildren = new(ldlocChildren.opcode, ldlocChildren.operand);
            matcher.InsertAndAdvance(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyBaseKidCount)))]
            );
            matcher
                .MatchEndForward(
                    [
                        new(inst => inst.IsLdloc()),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<Child>), nameof(List<Child>.Count))
                        ),
                        new(OpCodes.Brfalse_S),
                        new(inst => inst.IsLdloc()),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'children.Count != 0'")
                .Insert(
                    [
                        ldlocChildren,
                        new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ShouldHaveKidsPlayer))),
                        new(OpCodes.Ret),
                    ]
                );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Utility_playersCanGetPregnantHere_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static float ModifyPregnancyChance(float originalValue)
    {
        ModEntry.LogOnce($"Modify pregnancy chance: {originalValue} -> {ModEntry.Config.PregnancyChance / 100f}");
        return ModEntry.Config.PregnancyChance / 100f;
    }

    private static bool Utility_pickPersonalFarmEvent_Prefix(ref FarmEvent __result)
    {
        if (Game1.weddingToday)
        {
            return true;
        }
        if (Game1.player.stats.Get(Quirks.Stats_daysUntilBirth) == 1)
        {
            __result = new HMKBirthingEvent();
            return false;
        }
        return true;
    }

    /// <summary>Change pregnancy chance to configured number.</summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> Utility_pickPersonalFarmEvent_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            matcher
                .MatchEndForward(
                    [
                        new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Random), nameof(Random.NextDouble))),
                        new(OpCodes.Ldc_R8, 0.05),
                    ]
                )
                .ThrowIfNotMatch("Did not find first 'random.NextDouble() < 0.05'")
                .Advance(1)
                .Insert([new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(ModifyPregnancyChance)))])
                .MatchEndForward(
                    [
                        new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Random), nameof(Random.NextDouble))),
                        new(OpCodes.Ldc_R8, 0.05),
                    ]
                )
                .ThrowIfNotMatch("Did not find second 'random.NextDouble() < 0.05'")
                .Advance(1)
                .Insert([new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(ModifyPregnancyChance)))]);
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Utility_pickPersonalFarmEvent_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void Utility_pickPersonalFarmEvent_Postfix(ref FarmEvent __result)
    {
        ModEntry.Log($"Utility_pickPersonalFarmEvent_Postfix {__result}");
        if (__result is BirthingEvent)
        {
            __result = new HMKBirthingEvent();
        }
        else if (__result is PlayerCoupleBirthingEvent)
        {
            __result = new HMKPlayerCoupleBirthingEvent();
        }
    }
}
