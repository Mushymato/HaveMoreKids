using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;

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

    private bool isTwin;
    private NPC? spouse;

    /// <inheritdoc />
    public override bool setUp()
    {
        timer = 1500;
        Random random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
        spouse = Game1.player.getSpouse();
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
            message = spouse.isAdoptionSpouse()
                ? Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm)
                : spouse.Gender switch
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

        isTwin = false;

        return false;
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
        KidHandler.HaveAKid(
            spouse,
            newKidId,
            isDarkSkinned,
            babyName ?? Dialogue.randomName(),
            out WhoseKidData? whoseKidForTwin,
            isTwin
        );

        if (whoseKidForTwin != null)
        {
            newKidId = whoseKidForTwin.Twin;
            isTwin = true;
            babyName = null;
            naming = false;
            getBabyName = false;
            timer = 1000;
            message = null;
            if (whoseKidForTwin.TwinMessage != null)
            {
                message = TokenParser.ParseText(whoseKidForTwin.TwinMessage);
            }
            message ??= AssetManager.LoadString("BirthMessage_Twin");
            return false;
        }

        Game1.stats.checkForFullHouseAchievement(isDirectUnlock: true);
        Game1.playSound("smallSelect");

        // spouse stuff
        if (spouse != null)
        {
            Game1.player.GetSpouseFriendship().NextBirthingDate = null;
            spouse.daysAfterLastBirth = 5;
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
        }

        Game1.morningQueue.Enqueue(() => KidHandler.KidEntries_Populate());

        // remaining message and cleanup
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
        timer -= time.ElapsedGameTime.Milliseconds;
        Game1.fadeToBlackAlpha = 1f;
        if (timer <= 0 && !getBabyName)
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
                if (TickUpdate_FinishNaming())
                {
                    return true;
                }
            }
        }
        return false;
    }
}
