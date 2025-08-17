using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Locations;
using StardewValley.Menus;

namespace HaveMoreKids.Framework.NightEvents;

public class HMKPlayerCoupleBirthingEvent : BaseFarmEvent
{
    private int timer;

    private string? message = null;

    private string? babyName;

    private bool getBabyName;

    private bool naming;

    private readonly FarmHouse farmHouse;

    private readonly long spouseID;

    private readonly Farmer spouse;

    private bool isPlayersTurn;

    private Child? child;

    public HMKPlayerCoupleBirthingEvent()
    {
        spouseID = Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID)!.Value;
        Game1.otherFarmers.TryGetValue(spouseID, out spouse);
        farmHouse = chooseHome();
    }

    private static int SuitableScore(FarmHouse home)
    {
        if (home.upgradeLevel < 2)
            return int.MaxValue;
        return home.getChildrenCount();
    }

    private FarmHouse chooseHome()
    {
        FarmHouse p1Farmhouse = Utility.getHomeOfFarmer(Game1.player);
        FarmHouse p2Farmhouse = Utility.getHomeOfFarmer(spouse);
        return SuitableScore(p1Farmhouse) >= SuitableScore(p2Farmhouse) ? p1Farmhouse : p2Farmhouse;
    }

    /// <inheritdoc />
    public override bool setUp()
    {
        if (spouse == null || farmHouse == null)
        {
            return true;
        }
        Game1.player.CanMove = false;
        Friendship spouseFriendship = Game1.player.GetSpouseFriendship();
        isPlayersTurn =
            spouseFriendship.Proposer != Game1.player.UniqueMultiplayerID == (farmHouse.getChildrenCount() % 2 == 0);

        string childTerm = AssetManager.LoadString("ChildTerm");
        message = Game1.player.Gender switch
        {
            Gender.Female => Game1.content.LoadString("Strings\\Events:BirthMessage_PlayerMother", childTerm),
            Gender.Male => Game1.content.LoadString("Strings\\Events:BirthMessage_SpouseMother", childTerm),
            Gender.Undefined => Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm),
            _ => throw new NotImplementedException(),
        };

        return false;
    }

    public void returnBabyName(string name)
    {
        babyName = name;
        Game1.exitActiveMenu();
    }

    public void afterMessage()
    {
        if (isPlayersTurn)
        {
            getBabyName = true;
            double num = spouse.hasDarkSkin() ? 0.5 : 0.0;
            num += Game1.player.hasDarkSkin() ? 0.5 : 0.0;

            Random random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
            bool isDarkSkinned = random.NextDouble() < num;

            if (
                KidHandler.TryGetAvailableSharedKidIds(out List<string>? sharedKids)
                && KidHandler.PickMostLikelyKidId(sharedKids, isDarkSkinned, null, null) is string newKidId
                && AssetManager.ChildData.TryGetValue(newKidId, out CharacterData? childData)
            )
            {
                child = KidHandler.ApplyKidId(
                    "PLAYER_COUPLE",
                    new("Baby", childData.Gender == Gender.Male, childData.IsDarkSkinned, Game1.player),
                    true,
                    "Baby",
                    newKidId
                );
            }
            else
            {
                bool isMale;
                if (farmHouse.getChildrenCount() == 0)
                {
                    isMale = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).NextBool();
                }
                else
                {
                    isMale = farmHouse.getChildren().Last().Gender == Gender.Female;
                }
                child = new Child("Baby", isMale, isDarkSkinned, Game1.player);
            }
            farmHouse.characters.Add(child);
            KidHandler.ChildToNPC_Check();

            child.Age = 0;
            child.Position = new Vector2(16f, 4f) * 64f + new Vector2(0f, -24f);

            Game1.player.stats.checkForFullHouseAchievement(isDirectUnlock: true);
            Game1.player.GetSpouseFriendship().NextBirthingDate = null;
        }
        else
        {
            Game1.afterDialogues = delegate
            {
                getBabyName = true;
            };
            Game1.drawObjectDialogue(AssetManager.LoadString("SpouseNaming", spouse.Name));
        }
    }

    /// <inheritdoc />
    public override bool tickUpdate(GameTime time)
    {
        Game1.player.CanMove = false;
        timer += time.ElapsedGameTime.Milliseconds;
        Game1.fadeToBlackAlpha = 1f;
        if (timer > 1500 && !getBabyName)
        {
            if (message != null && !Game1.dialogueUp && Game1.activeClickableMenu == null)
            {
                Game1.drawObjectDialogue(message);
                Game1.afterDialogues = afterMessage;
            }
        }
        else if (getBabyName)
        {
            if (!isPlayersTurn || child == null)
            {
                Game1.globalFadeToClear();
                return true;
            }
            if (!naming)
            {
                Game1.activeClickableMenu = new NamingMenu(
                    returnBabyName,
                    AssetManager.LoadString("BabyNamingTitle"),
                    ""
                );
                naming = true;
            }
            if (!string.IsNullOrEmpty(babyName) && babyName.Length > 0)
            {
                string text = babyName;
                List<NPC> allCharacters = Utility.getAllCharacters();
                if (child.KidDisplayName() is not null)
                {
                    child.displayName = text;
                }
                else
                {
                    child.Name = HMKBirthingEvent.AntiNameCollision(text);
                }
                Game1.playSound("smallSelect");
                if (Game1.keyboardDispatcher != null)
                {
                    Game1.keyboardDispatcher.Subscriber = null;
                }
                Game1.globalFadeToClear();
                return true;
            }
        }
        return false;
    }
}
