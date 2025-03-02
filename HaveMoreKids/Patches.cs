using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;

namespace HaveMoreKids;

internal record TempCAD(CharacterAppearanceData Data)
{
    readonly string? originalCondition = Data.Condition;

    public void Restore() => Data.Condition = originalCondition;
}

internal static class Patches
{
    internal static string Child_ModData_DisplayName => $"{ModEntry.ModId}/DisplayName";
    internal static Action<NPC> NPC_ChooseAppearance_Call = MakeDynamicMethod();

    internal static void Apply()
    {
        Harmony harmony = new(ModEntry.ModId);
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.pickPersonalFarmEvent)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(Utility_pickPersonalFarmEvent_Transpiler))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.canGetPregnant)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(NPC_canGetPregnant_Transpiler))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(BirthingEvent), nameof(BirthingEvent.tickUpdate)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(BirthingEvent_tickUpdate_Transpiler))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Child), nameof(Child.reloadSprite)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(Child_reloadSprite_Prefix))
        );
        harmony.Patch(
            original: AccessTools.PropertyGetter(typeof(Character), nameof(Character.displayName)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_displayName_Postfix))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.GetData)),
            postfix: new HarmonyMethod(typeof(Patches), nameof(Child_GetData_Postfix))
        );
    }

    private static Action<NPC> MakeDynamicMethod()
    {
        var NPC_ChooseAppearance = AccessTools.DeclaredMethod(typeof(NPC), nameof(NPC.ChooseAppearance));
        var dm = new DynamicMethod("NPC_ChooseAppearance_Call", typeof(void), [typeof(NPC)]);
        var gen = dm.GetILGenerator();
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Ldnull);
        gen.Emit(OpCodes.Call, NPC_ChooseAppearance);
        gen.Emit(OpCodes.Ret);
        return dm.CreateDelegate<Action<NPC>>();
    }

    private static void Child_GetData_Postfix(NPC __instance, ref CharacterData __result)
    {
        if (__result != null)
            return;
        if (__instance is Child && NPC.TryGetData($"{ModEntry.ModId}_{__instance.Name}", out CharacterData data))
        {
            __result = data;
        }
    }

    /// <summary>
    /// Restore the display name for the child
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="__result"></param>
    private static void Child_displayName_Postfix(Character __instance, ref string __result)
    {
        if (__instance is Child && __instance.modData.TryGetValue(Child_ModData_DisplayName, out string displayName))
            __result = displayName;
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
        if (characterData?.Appearance is not List<CharacterAppearanceData> appearances)
        {
            return true;
        }
        List<TempCAD> tmpCADs = [];
        string prefixSetTrue;
        string prefixSetFalse;
        if (__instance.Age < 3)
        {
            prefixSetTrue = $"{ModEntry.ModId}_Baby";
            prefixSetFalse = $"{ModEntry.ModId}_Toddler";
        }
        else
        {
            prefixSetTrue = $"{ModEntry.ModId}_Toddler";
            prefixSetFalse = $"{ModEntry.ModId}_Baby";
        }
        foreach (var data in appearances)
        {
            if (data.Id.StartsWith(prefixSetFalse))
            {
                tmpCADs.Add(new(data));
                data.Condition = "FALSE";
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
            __instance.HideShadow = false;
        }
        else
        {
            __instance.Sprite.SpriteWidth = 16;
            __instance.Sprite.SpriteHeight = 32;
            __instance.Sprite.currentFrame = 0;
            __instance.HideShadow = true;
        }
        __instance.Sprite.UpdateSourceRect();

        foreach (var tmp in tmpCADs)
            tmp.Restore();

        return false;
    }

    private static Child ModifyKid(Child newKid, NPC spouse)
    {
        if (spouse.GetKidIds() is not string[] kidIds)
            return newKid;
        HashSet<string> children = Game1.player.getChildren().Select(child => child.Name).ToHashSet();
        string[] availableKidIds = kidIds.Where(id => !children.Contains(id)).ToArray();
        newKid.modData[Child_ModData_DisplayName] = newKid.Name;
        newKid.Name = availableKidIds[
            Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).Next(availableKidIds.Length)
        ];
        newKid.reloadSprite(onlyAppearance: true);
        ModEntry.Log($"Assigned {newKid.Name} to {spouse.Name} and {Game1.player.UniqueMultiplayerID}'s child.");
        return newKid;
    }

    /// <summary>
    /// Change kid internal name to the character data entry id
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="generator"></param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> BirthingEvent_tickUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_014a: callvirt instance class StardewValley.NPC StardewValley.Farmer::getSpouse()
            // IL_014f: stloc.3
            // IL_0150: ldloc.3
            matcher
                .MatchEndForward(
                    [
                        new(OpCodes.Callvirt, AccessTools.Method(typeof(Farmer), nameof(Farmer.getSpouse))),
                        new(inst => inst.IsStloc()),
                        new(inst => inst.IsLdloc()),
                    ]
                )
                .ThrowIfNotMatch("Did not find NPC spouse = Game1.player.getSpouse();");
            OpCode ldlocSpouse = matcher.Opcode;

            // IL_025c: newobj instance void StardewValley.Characters.Child::.ctor(string, bool, bool, class StardewValley.Farmer)
            matcher
                .MatchEndForward(
                    [
                        new(
                            OpCodes.Newobj,
                            AccessTools.Constructor(
                                typeof(Child),
                                [typeof(string), typeof(bool), typeof(bool), typeof(Farmer)]
                            )
                        ),
                        new(OpCodes.Stfld),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'Child baby = new Child();'");
            object babyfld = matcher.Operand;
            // IL_02ba: ldfld class StardewValley.Characters.Child StardewValley.Events.BirthingEvent/'<>c__DisplayClass11_0'::baby
            // IL_02bf: callvirt instance void class Netcode.NetCollection`1<class StardewValley.NPC>::Add(!0)
            matcher.MatchEndForward(
                [
                    new(OpCodes.Ldfld, babyfld),
                    new(
                        OpCodes.Callvirt,
                        AccessTools.Method(typeof(Netcode.NetCollection<NPC>), nameof(Netcode.NetCollection<NPC>.Add))
                    ),
                ]
            );
            matcher
                .Insert(
                    [
                        new(ldlocSpouse),
                        new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(ModifyKid))),
                    ]
                )
                .ThrowIfNotMatch("Did not find 'Utility.getHomeOfFarmer(Game1.player).characters.Add(baby);'");
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in BirthingEvent_tickUpdate_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static bool ShouldHaveKids(NPC spouse, List<Child> children)
    {
        if (spouse.GetKidIds() is string[] kidIds)
            return kidIds.Length > children.Count && children.Last().Age > 2;
        return false;
    }

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
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in NPC_canGetPregnant_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    /// <summary>Change pregnancy chance to 100%.</summary>
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
                .ThrowIfNotMatch("Did not find 'random.NextDouble() < 0.05'");
            matcher.Operand = 1.0;
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in Utility_pickPersonalFarmEvent_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }
}
