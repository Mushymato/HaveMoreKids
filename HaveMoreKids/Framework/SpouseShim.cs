using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using HaveMoreKids.Integration;
using StardewModdingAPI;
using StardewValley;

namespace HaveMoreKids.Framework;

internal static class SpouseShim
{
    internal static readonly string[] FL_ModIds = ["aedenthorn.FreeLove", "ApryllForever.PolyamorySweetLove"];
    private static IFreeLoveAPI? FL_api = null;
    internal static Type? FL_modType = null;
    private static FieldInfo? FL_lastPregnantSpouse = null;
    private static FieldInfo? FL_lastBirthingSpouse = null;

    #region GetSpouses
    private static IEnumerable<NPC> GetSpouses_Vanilla(Farmer farmer)
    {
        if (farmer.getSpouse() is NPC spouse)
            yield return spouse;
    }

    private static IEnumerable<NPC> GetSpouses_FL(Farmer farmer)
    {
        if (FL_api?.GetSpouses(farmer).Values is IEnumerable<NPC> spouses)
        {
            foreach (NPC spouse in spouses)
            {
                if (spouse != null)
                {
                    yield return spouse;
                }
            }
        }
    }

    private static Func<Farmer, IEnumerable<NPC>> GetSpouses_Func = GetSpouses_Vanilla;

    internal static IEnumerable<NPC> GetSpouses(Farmer farmer) => GetSpouses_Func(farmer);
    #endregion

    #region GetBirthingSpouse GetPregnantSpouse
    private static NPC? GetMainSpouse_Vanilla(Farmer farmer)
    {
        return farmer.getSpouse();
    }

    private static NPC? GetPregnantSpouse_FL(Farmer farmer)
    {
        return (NPC?)(FL_lastPregnantSpouse?.GetValue(null) ?? GetMainSpouse_Vanilla(farmer));
    }

    private static Func<Farmer, NPC?> GetPregnantSpouse_Func = GetMainSpouse_Vanilla;

    internal static NPC? GetPregnantSpouse(Farmer farmer) => GetPregnantSpouse_Func(farmer);

    private static NPC? GetBirthingSpouse_FL(Farmer farmer)
    {
        return (NPC?)(FL_lastBirthingSpouse?.GetValue(null) ?? GetMainSpouse_Vanilla(farmer));
    }

    private static Func<Farmer, NPC?> GetBirthingSpouse_Func = GetMainSpouse_Vanilla;

    internal static NPC? GetBirthingSpouse(Farmer farmer) => GetBirthingSpouse_Func(farmer);

    #endregion

    internal static bool TryGetNPCFriendship(
        Farmer player,
        NPC npc,
        [NotNullWhen(true)] out Friendship? spouseFriendship
    )
    {
        return player.friendshipData.TryGetValue(npc.Name, out spouseFriendship) && spouseFriendship != null;
    }

    internal static void SetNPCNewChildDate(Farmer player, NPC spouse, int daysUntilNewChild)
    {
        if (TryGetNPCFriendship(player, spouse, out Friendship? spouseFriendship))
        {
            if (daysUntilNewChild <= -1)
            {
                spouseFriendship.NextBirthingDate = null;
            }
            else
            {
                WorldDate worldDate = new(Game1.Date);
                worldDate.TotalDays += daysUntilNewChild;
                spouseFriendship.NextBirthingDate = worldDate;
            }
        }
    }

    internal static void Register(IModHelper helper)
    {
        try
        {
            foreach (string modId in FL_ModIds)
            {
                FL_api = helper.ModRegistry.GetApi<IFreeLoveAPI>(modId);
                IModInfo? modInfo = ModEntry.help.ModRegistry.Get(modId);
                IMod? FL_mod = null;
                if (modInfo?.GetType().GetProperty("Mod")?.GetValue(modInfo) is IMod mod)
                {
                    FL_mod = mod;
                }
                if (FL_api != null && FL_mod != null)
                {
                    ModEntry.Log($"Got FL mod: {modId}");
                    GetSpouses_Func = GetSpouses_FL;
                    FL_modType = FL_mod.GetType();
                    FL_lastPregnantSpouse = AccessTools.DeclaredField(FL_modType, "lastPregnantSpouse");
                    GetPregnantSpouse_Func = GetPregnantSpouse_FL;
                    FL_lastBirthingSpouse = AccessTools.DeclaredField(FL_modType, "lastBirthingSpouse");
                    GetBirthingSpouse_Func = GetBirthingSpouse_FL;
                    Patches.Apply_PregnancyFL();
                    break;
                }
            }
        }
        catch
        {
            return;
        }
    }
}
