using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;

namespace HaveMoreKids.Framework.NightEvents;

public class HMKNewChildEvent : BaseFarmEvent
{
    private int timer = 1500;

    private string? message = null;

    private string? babyName = null;

    internal string? newKidId = null;

    private bool getBabyName = false;

    private bool isAdoptedFromNPC = false;

    private bool naming = false;

    private bool isDarkSkinned = false;

    private bool isTwin = false;

    private NPC? spouse = null;

    /// <inheritdoc />
    public override bool setUp()
    {
        if (newKidId == null)
        {
            return true;
        }

        Game1.player.CanMove = false;
        timer = 1500;

        string childTerm = AssetManager.LoadString("ChildTerm");
        if (newKidId != null && AssetManager.KidDefsByKidId.TryGetValue(newKidId, out KidDefinitionData? kidDef))
        {
            if (kidDef.BirthOrAdoptMessage is not null)
            {
                message = string.Format(TokenParser.ParseText(kidDef.BirthOrAdoptMessage), childTerm);
            }
            isAdoptedFromNPC = kidDef.AdoptedFromNPC != null;
        }
        if (message == null)
        {
            if (spouse == null)
            {
                if (!AssetManager.TryLoadString("BirthMessage_Solo", out message, childTerm))
                {
                    message = Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm);
                }
            }
            else if (
                !AssetManager.TryLoadString(
                    $"BirthMessage_NPC_{spouse.Name}",
                    out message,
                    childTerm,
                    spouse.displayName
                )
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
                        Gender.Undefined => Game1.content.LoadString(
                            "Strings\\Events:BirthMessage_Adoption",
                            childTerm
                        ),
                        _ => throw new NotImplementedException(),
                    };
            }
        }

        isTwin = false;

        return false;
    }

    public bool TryPickKidId()
    {
        Random random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
        spouse = SpouseShim.GetBirthingSpouse(Game1.player);
        if (newKidId == null)
        {
            if (spouse != null)
            {
                isDarkSkinned = random.NextBool(
                    (spouse.hasDarkSkin() ? 0.5 : 0.0) + (Game1.player.hasDarkSkin() ? 0.5 : 0.0)
                );
                newKidId = KidHandler.PickKidId(spouse, darkSkinned: isDarkSkinned);
            }
            else
            {
                if (KidHandler.TryGetAvailableSharedKidIds(out List<string>? sharedKids, Game1.player.hasDarkSkin()))
                {
                    newKidId = KidHandler.PickMostLikelyKidId(sharedKids, isDarkSkinned, null, null);
                }
            }
        }
        return newKidId != null;
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

    private void afterMessageNoNaming()
    {
        getBabyName = true;
        naming = true;
        babyName = "NPC_CHILD_NAME";
    }

    private void TickUpdate_StartNaming()
    {
        Game1.activeClickableMenu = new NamingMenu(returnBabyName, AssetManager.LoadString("BabyNamingTitle"), "");
        naming = true;
    }

    private bool TickUpdate_FinishNaming()
    {
        Child newKid = KidHandler.HaveAKid(
            spouse,
            newKidId,
            isDarkSkinned,
            babyName ?? Dialogue.randomName(),
            out KidDefinitionData? whoseKidForTwin,
            isTwin,
            isAdoptedFromNPC
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
            SpouseShim.SetNPCNewChildDate(Game1.player, spouse, -1);
            spouse.daysAfterLastBirth = 5;

            spouse.shouldSayMarriageDialogue.Value = true;

            int childrenCount = Game1.player.getChildrenCount();

            MarriageDialogueReference? mdrV = null;
            // vanilla fallbacks
            if (childrenCount == 1)
            {
                if (spouse.isAdoptionSpouse())
                {
                    mdrV = new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_Adoption", true, babyName);
                }
                else
                {
                    mdrV = new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_FirstChild", true, babyName);
                }
            }
            else if (childrenCount == 2)
            {
                mdrV = new MarriageDialogueReference(
                    "Data\\ExtraDialogue",
                    "NewChild_SecondChild" + Game1.random.Next(1, 3),
                    true
                );
            }
            if (mdrV != null)
            {
                spouse.shouldSayMarriageDialogue.Value = true;
                spouse.currentMarriageDialogue.Insert(0, mdrV);
            }
        }

        KidHandler.KidEntries_Populate();

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
            if (message != null && !Game1.dialogueUp && Game1.activeClickableMenu == null)
            {
                Game1.drawObjectDialogue(message);
                Game1.afterDialogues = isAdoptedFromNPC ? afterMessageNoNaming : afterMessage;
            }
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
