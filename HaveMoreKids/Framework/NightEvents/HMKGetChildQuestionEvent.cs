using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Events;

namespace HaveMoreKids.Framework.NightEvents;

public class HMKGetChildQuestionEvent(int whichQuestion) : BaseFarmEvent
{
    private NPC? spouse;
    public bool forceProceed;
    private static Response[] YesNot =>
        [
            new("Yes", Game1.content.LoadString("Strings\\Events:HaveBabyAnswer_Yes")),
            new("Not", Game1.content.LoadString("Strings\\Events:HaveBabyAnswer_No")),
        ];

    /// <inheritdoc />
    public override bool setUp()
    {
        if (whichQuestion == QuestionEvent.pregnancyQuestion)
        {
            if ((spouse = SpouseShim.GetPregnantSpouse(Game1.player)) == null)
            {
                return true;
            }

            int childrenCount = Game1.player.getChildrenCount();

            if (
                !AssetManager.TryGetDialogueForChildCount(
                    spouse,
                    "HMK_HaveBabyQuestion",
                    "",
                    childrenCount,
                    out Dialogue? dialogue,
                    out _
                )
            )
            {
                string translationKey = spouse.isAdoptionSpouse()
                    ? "Strings\\Events:HaveBabyQuestion_Adoption"
                    : "Strings\\Events:HaveBabyQuestion";
                dialogue = new(spouse, translationKey, Game1.content.LoadString(translationKey, Game1.player.Name));
            }
            dialogue.onFinish += () =>
            {
                Game1.currentLocation.createQuestionDialogue(
                    dialogue.dialogues.Last().Text,
                    YesNot,
                    AnswerPregnancyQuestion,
                    spouse
                );
                Game1.messagePause = true;
            };
            spouse.setNewDialogue(dialogue);
            Game1.drawDialogue(spouse);
            return false;
        }
        else if (whichQuestion == QuestionEvent.playerPregnancyQuestion)
        {
            long? value = Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID);
            if (value.HasValue)
            {
                Farmer farmer = Game1.otherFarmers[value.Value];
                if (
                    AssetManager.LoadStringReturnNullIfNotFound("HaveBabyQuestionFarmer", farmer.displayName)
                    is not string question
                )
                {
                    question = Game1.content.LoadString(
                        farmer.IsMale != Game1.player.IsMale
                            ? "Strings\\Events:HavePlayerBabyQuestion"
                            : "Strings\\Events:HavePlayerBabyQuestion_Adoption",
                        farmer.displayName
                    );
                }
                Game1.currentLocation.createQuestionDialogue(question, YesNot, AnswerPlayerPregnancyQuestion);
                Game1.messagePause = true;
                return false;
            }
        }

        return true;
    }

    private void AnswerPregnancyQuestion(Farmer who, string answer)
    {
        if (spouse != null && answer.Equals("Yes"))
        {
            SpouseShim.SetNPCNewChildDate(who, spouse, ModEntry.Config.DaysPregnant);
        }
    }

    private static readonly FieldInfo nextBirthingDateField = AccessTools.DeclaredField(
        typeof(Friendship),
        "nextBirthingDate"
    );
    private static readonly PerScreen<Netcode.NetRef<WorldDate>> nextBirthingDatePS = new();

    private void AnswerPlayerPregnancyQuestion(Farmer who, string answer)
    {
        if (answer.Equals("Yes"))
        {
            long value = Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID)!.Value;
            Farmer receiver = Game1.otherFarmers[value];
            Friendship friendship = Game1.player.team.GetFriendship(Game1.player.UniqueMultiplayerID, value);
            if (nextBirthingDateField.GetValue(friendship) is Netcode.NetRef<WorldDate> nextBirthingDate)
            {
                nextBirthingDatePS.Value = nextBirthingDate;
                if (ModEntry.Config.DaysPregnant != 14)
                {
                    nextBirthingDate.fieldChangeVisibleEvent += OnFieldChangeVisible;
                }
            }
            Game1.player.team.SendProposal(receiver, ProposalType.Baby);
        }
    }

    private void OnFieldChangeVisible(Netcode.NetRef<WorldDate> field, WorldDate oldValue, WorldDate newValue)
    {
        if (field == nextBirthingDatePS.Value && newValue.TotalDays - oldValue.TotalDays == 14)
        {
            ModEntry.Log($"Modifying player pregnancy days");
            nextBirthingDatePS.Value.fieldChangeVisibleEvent -= OnFieldChangeVisible;
            nextBirthingDatePS.Value.Value.TotalDays -= 14;
            nextBirthingDatePS.Value.Value.TotalDays += ModEntry.Config.DaysPregnant;
        }
    }

    /// <inheritdoc />
    public override bool tickUpdate(GameTime time)
    {
        if (forceProceed)
        {
            return true;
        }
        return !Game1.dialogueUp;
    }

    /// <inheritdoc />
    public override void makeChangesToLocation()
    {
        Game1.messagePause = false;
    }
}
