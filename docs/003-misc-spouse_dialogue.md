# Misc: Spouse Dialogue

Because you have more kids, your spouse have more things to say.

## Night Events

By default, HMK changes the 2 child related night events (child question and new child) to display a dialogue box featuring the spouse. You can customize the spoken dialogue in a variety of ways:

### Have Baby Question

HMK checks `Characters/Dialogue/MarriageDialogue_[SpouseNPC]` for a have baby question string. The dialogue key is  `HMK_HaveBabyQuestion_[n]` where `n` is the number of kids you currently have, or `HMK_HaveBabyQuestion` for default value.

If no custom message is set, the default value is the string at `Strings\\Events:HaveBabyQuestion` or `Strings\\Events:HaveBabyQuestion_Adoption` for adoption spouse.

### New Child Message

HMK checks `Characters/Dialogue/MarriageDialogue_[SpouseNPC]` for a new child message string. The dialogue key is  `HMK_BirthMessage_[n]` where `n` is the number of kids you currently have, or `HMK_BirthMessage` for default value.

If the kid is fixed ahead of time from [`mushymato.HaveMoreKids_SetNewChildEvent`](./002-extensions-triggers_actions.md) or from adoption registry, then [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md)'s `BirthOrAdoptMessage` field takes priority. It is spoken by the NPC parent of the kid, or displayed as a message box if there's no NPC parent.

If no custom message is set, the default value is the string at `Strings\\Events:BirthMessage_Adoption` if adopting/player gender is `Unknown`, `Strings\\Events:BirthMessage_PlayerMother` if player gender is `Female`, `Strings\\Events:BirthMessage_SpouseMother` if player gender is `Male`.

Solo new child (e.g. generic adoptions from adoption registry) always uses `Strings\\Events:BirthMessage_Adoption`.

## New Kid Marriage Dialogue

On the first 5 days after having a new child, HMK checks for spouse dialogue in `Characters/Dialogue/MarriageDialogue_[SpouseNPC]`. The dialogue key is  `HMK_NewChild_[d]_[n]` where `d` is the number of days since new child (from 0 to 4) and `n` is the number of kids you currently have, or `HMK_NewChild_[d]` for default value.

