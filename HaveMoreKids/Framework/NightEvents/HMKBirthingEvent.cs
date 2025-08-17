using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Menus;

namespace HaveMoreKids.Framework.NightEvents;

public class HMKBirthingEvent : BaseFarmEvent
{
    private int timer;

    private string? message;

    private string? babyName;

    private string? newKidId;

    private bool getBabyName;

    private bool naming;

    private bool isDarkSkinned;

    /// <inheritdoc />
    public override bool setUp()
    {
        Random random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
        NPC? spouse = Game1.player.getSpouse();
        Game1.player.CanMove = false;
        if (spouse != null)
        {
            isDarkSkinned = random.NextBool(
                (spouse.hasDarkSkin() ? 0.5 : 0.0) + (Game1.player.hasDarkSkin() ? 0.5 : 0.0)
            );
            newKidId = KidHandler.PickKidId(
                spouse,
                darkSkinned: random.NextBool(
                    (spouse.hasDarkSkin() ? 0.5 : 0.0) + (Game1.player.hasDarkSkin() ? 0.5 : 0.0)
                )
            );
        }
        else
        {
            isDarkSkinned = random.NextBool(Game1.player.hasDarkSkin() ? 0.75 : 0.25);
            if (KidHandler.TryGetAvailableSharedKidIds(out List<string>? sharedKids))
            {
                newKidId = KidHandler.PickMostLikelyKidId(sharedKids, isDarkSkinned, null, null);
            }
        }

        string childTerm = AssetManager.LoadString("ChildTerm");
        if (spouse == null)
        {
            if (!AssetManager.TryLoadString("BirthMessage_Solo", out message, childTerm))
            {
                message = Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm);
            }
        }
        else if (
            !AssetManager.TryLoadString($"BirthMessage_NPC_{spouse.Name}", out message, childTerm, spouse.displayName)
        )
        {
            if (spouse.isAdoptionSpouse())
            {
                message = Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm);
            }
            else
            {
                message = spouse.Gender switch
                {
                    Gender.Male => Game1.content.LoadString("Strings\\Events:BirthMessage_PlayerMother", childTerm),
                    Gender.Female => Game1.content.LoadString(
                        "Strings\\Events:BirthMessage_SpouseMother",
                        childTerm,
                        spouse.displayName
                    ),
                    Gender.Undefined => Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm),
                    _ => throw new NotImplementedException(),
                };
            }
        }

        return false;
    }

    public static string AntiNameCollision(string name)
    {
        HashSet<string> npcIds = Utility.getAllCharacters().Select(npc => npc.Name).ToHashSet();
        while (npcIds.Contains(name))
        {
            name += " ";
        }
        return name;
    }

    public void returnBabyName(string name)
    {
        babyName = name;
        Game1.exitActiveMenu();
    }

    public void afterMessage()
    {
        getBabyName = true;
    }

    public void TickUpdate_CheckForName()
    {
        if (message != null && !Game1.dialogueUp && Game1.activeClickableMenu == null)
        {
            Game1.drawObjectDialogue(message);
            Game1.afterDialogues = afterMessage;
        }
    }

    private void TickUpdate_StartNaming()
    {
        Game1.activeClickableMenu = new NamingMenu(returnBabyName, AssetManager.LoadString("BabyNamingTitle"), "");
        naming = true;
    }

    private bool TickUpdate_FinishNaming()
    {
        babyName ??= Dialogue.randomName();
        NPC spouse = Game1.player.getSpouse();
        List<NPC> allCharacters = Utility.getAllCharacters();

        // create and add kid
        Child newKid;
        if (newKidId == null && KidHandler.PickForSpecificKidId(spouse, babyName) is string specificKidName)
        {
            newKidId = specificKidName;
        }

        if (newKidId != null && AssetManager.ChildData.TryGetValue(newKidId, out CharacterData? childData))
        {
            newKid = KidHandler.ApplyKidId(
                spouse.Name,
                new(babyName, childData.Gender == Gender.Male, childData.IsDarkSkinned, Game1.player),
                true,
                babyName,
                newKidId
            );
        }
        else
        {
            bool isMale;
            if (Game1.player.getNumberOfChildren() == 0)
            {
                isMale = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).NextBool();
            }
            else
            {
                isMale = Game1.player.getChildren().Last().Gender == Gender.Female;
            }
            newKid = new(AntiNameCollision(babyName), isMale, isDarkSkinned, Game1.player);
        }
        newKid.Age = 0;
        newKid.Position = new Vector2(16f, 4f) * 64f + new Vector2(0f, -24f);
        Utility.getHomeOfFarmer(Game1.player).characters.Add(newKid);
        KidHandler.ChildToNPC_Check();

        // spouse stuff
        Game1.stats.checkForFullHouseAchievement(isDirectUnlock: true);
        Game1.playSound("smallSelect");
        spouse.daysAfterLastBirth = 5;
        Game1.player.GetSpouseFriendship().NextBirthingDate = null;
        int childrenCount = Game1.player.getChildrenCount();

        spouse.shouldSayMarriageDialogue.Value = true;
        if (childrenCount == 1)
        {
            if (spouse.isAdoptionSpouse())
            {
                spouse.currentMarriageDialogue.Insert(
                    0,
                    new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_Adoption", true, babyName)
                );
            }
            else
            {
                spouse.currentMarriageDialogue.Insert(
                    0,
                    new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_FirstChild", true, babyName)
                );
            }
        }
        else if (childrenCount == 2)
        {
            spouse.currentMarriageDialogue.Insert(
                0,
                new MarriageDialogueReference(
                    "Data\\ExtraDialogue",
                    "NewChild_SecondChild" + Game1.random.Next(1, 3),
                    true
                )
            );
        }
        else if (
            AssetManager.LoadMarriageDialogueReference($"NewChild_{childrenCount}")
            is MarriageDialogueReference marriageDialogueChildCount
        )
        {
            spouse.currentMarriageDialogue.Insert(0, marriageDialogueChildCount);
        }
        else if (
            AssetManager.LoadMarriageDialogueReference("NewChild_Generic")
            is MarriageDialogueReference marriageDialogueGeneral
        )
        {
            spouse.currentMarriageDialogue.Insert(0, marriageDialogueGeneral);
        }
        else
        {
            spouse.shouldSayMarriageDialogue.Value = false;
        }

        // remaining message and cleanup
        Game1.morningQueue.Enqueue(
            delegate
            {
                string text2 =
                    Game1.getCharacterFromName(Game1.player.spouse)?.GetTokenizedDisplayName() ?? Game1.player.spouse;
                Game1.Multiplayer.globalChatInfoMessage(
                    "Baby",
                    Lexicon.capitalize(Game1.player.Name),
                    text2,
                    Lexicon.getTokenizedGenderedChildTerm(newKid.Gender == Gender.Male),
                    Lexicon.getTokenizedPronoun(newKid.Gender == Gender.Male),
                    newKid.displayName
                );
            }
        );
        if (Game1.keyboardDispatcher != null)
        {
            Game1.keyboardDispatcher.Subscriber = null;
        }
        Game1.player.Position = Utility.PointToVector2(Utility.getHomeOfFarmer(Game1.player).GetPlayerBedSpot()) * 64f;
        Game1.globalFadeToClear();

        return true;
    }

    /// <inheritdoc />
    public override bool tickUpdate(GameTime time)
    {
        Game1.player.CanMove = false;
        timer += time.ElapsedGameTime.Milliseconds;
        Game1.fadeToBlackAlpha = 1f;
        if (timer > 1500 && !getBabyName)
        {
            TickUpdate_CheckForName();
        }
        else if (getBabyName)
        {
            if (!naming)
            {
                TickUpdate_StartNaming();
            }
            if (!string.IsNullOrEmpty(babyName) && babyName.Length > 0)
            {
                return TickUpdate_FinishNaming();
            }
        }
        return false;
    }
}
