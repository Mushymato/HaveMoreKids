using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;

namespace HaveMoreKids.Framework.NightEvents;

public class HMKNewChildEvent : BaseFarmEvent
{
    private int timer = 1500;

    private string? message = null;

    private Dialogue? messageDialogue = null;

    private string? babyName = null;

    internal string? newKidId = null;

    private bool getBabyName = false;

    private bool skipNaming = false;

    private bool naming = false;

    private bool isDarkSkinned = false;

    private bool isTwin = false;

    private NPC? spouse = null;

    internal bool isSoloAdopt = false;

    /// <inheritdoc />
    public override bool setUp()
    {
        Random random = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed);
        if (!isSoloAdopt)
        {
            spouse = SpouseShim.GetBirthingSpouse(Game1.player);
        }

        ModEntry.Log($"HMKNewChildEvent.setUp: '{newKidId}' ({isSoloAdopt})");
        if (newKidId == null)
        {
            if (spouse != null)
            {
                bool? darkSkinRestrict = KidHandler.GetDarkSkinnedRestrict(Game1.player, spouse);
                if (darkSkinRestrict == null)
                {
                    // pick 2 times, once with the rand isDarkSkinned and once unrestricted
                    isDarkSkinned = random.NextBool(
                        (spouse.hasDarkSkin() ? 0.5 : 0.0) + (Game1.player.hasDarkSkin() ? 0.5 : 0.0)
                    );
                    newKidId =
                        KidHandler.PickKidId(spouse, darkSkinned: isDarkSkinned)
                        ?? KidHandler.PickKidId(spouse, darkSkinned: null);
                }
                else
                {
                    // pick 1 time with the invariant isDarkSkinned value
                    isDarkSkinned = darkSkinRestrict.Value;
                    newKidId = KidHandler.PickKidId(spouse, darkSkinned: isDarkSkinned);
                }
            }
            else
            {
                if (KidHandler.TryGetAvailableSharedKidIds(out List<string>? sharedKids, Game1.player.hasDarkSkin()))
                {
                    newKidId = KidHandler.PickMostLikelyKidId(sharedKids, isDarkSkinned, null, null);
                }
            }
        }

        Game1.player.CanMove = false;
        timer = 1500;

        string childTerm = AssetManager.LoadString("ChildTerm");
        if (newKidId != null && AssetManager.KidDefsByKidId.TryGetValue(newKidId, out KidDefinitionData? kidDef))
        {
            if (kidDef.BirthOrAdoptMessage is not null)
            {
                message = string.Format(TokenParser.ParseText(kidDef.BirthOrAdoptMessage), childTerm);
                if (NPCLookup.GetNonChildNPC(kidDef.AdoptedFromNPC) is NPC adoptFrom)
                {
                    messageDialogue = new Dialogue(adoptFrom, "", message);
                }
                else if (spouse != null)
                {
                    messageDialogue = new Dialogue(spouse, "", message);
                }
            }
            skipNaming = kidDef.AdoptedFromNPC != null || kidDef.BirthOrAdoptAsToddler;
        }
        if (message == null)
        {
            if (spouse == null)
            {
                message = Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm);
            }
            else if (
                AssetManager.TryGetDialogueForChild(
                    spouse,
                    null,
                    "HMK_BirthMessage",
                    Game1.player.getChildrenCount(),
                    out messageDialogue
                )
            )
            {
                message = messageDialogue.ToString();
            }
            else
            {
                message =
                    (spouse.isAdoptionSpouse() || isSoloAdopt)
                        ? Game1.content.LoadString("Strings\\Events:BirthMessage_Adoption", childTerm)
                        : spouse.Gender switch
                        {
                            Gender.Male => Game1.content.LoadString(
                                "Strings\\Events:BirthMessage_PlayerMother",
                                childTerm
                            ),
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

    public void returnBabyName(string name)
    {
        babyName = name;
        Game1.exitActiveMenu();
    }

    public void afterMessage()
    {
        Game1.activeClickableMenu = null;
        Game1.currentSpeaker = null;
        getBabyName = true;
    }

    private void afterMessageNoNaming()
    {
        Game1.activeClickableMenu = null;
        Game1.currentSpeaker = null;
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
            isTwin
        );

        if (whoseKidForTwin != null && whoseKidForTwin.Twin != null)
        {
            newKidId = whoseKidForTwin.Twin;
            if (AssetManager.KidDefsByKidId.TryGetValue(newKidId, out KidDefinitionData? kidDef))
            {
                skipNaming = kidDef.AdoptedFromNPC != null || kidDef.BirthOrAdoptAsToddler;
            }
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
                if (messageDialogue != null && messageDialogue.speaker != null)
                {
                    messageDialogue.overridePortrait = AssetManager.GetSpouseSpecialPortrait(
                        messageDialogue.speaker,
                        "HMK_BirthMessage"
                    );
                    messageDialogue.onFinish += skipNaming ? afterMessageNoNaming : afterMessage;
                    messageDialogue.speaker.setNewDialogue(messageDialogue);
                    Game1.drawDialogue(messageDialogue.speaker);
                }
                else
                {
                    Game1.drawObjectDialogue(message);
                    Game1.afterDialogues = skipNaming ? afterMessageNoNaming : afterMessage;
                }
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
                    TriggerActionManager.Raise(GameDelegates.Trigger_NewChild);
                    return true;
                }
            }
        }
        return false;
    }
}
