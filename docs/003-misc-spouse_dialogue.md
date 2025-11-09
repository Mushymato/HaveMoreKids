# Misc: Spouse Dialogue

Because you have more kids, your spouse have more things to say.

## Night Events

By default, HMK changes the 2 child related night events (child question and new child) to display a dialogue box featuring the spouse. You can customize the spoken dialogue in a variety of ways:

### Have Baby Question

HMK checks `Characters/Dialogue/MarriageDialogue_[SpouseNPC]` for a have baby question string. The dialogue key is `HMK_HaveBabyQuestion_[n]` where `n` is the number of kids you currently have, or `HMK_HaveBabyQuestion` for default value.

If no custom message is set, the default value is the string at `Strings\\Events:HaveBabyQuestion` or `Strings\\Events:HaveBabyQuestion_Adoption` for adoption spouse.

A special portrait can be used here if loaded to `Portraits/<textureName>_HaveBabyQuestion`.

### New Child Message

HMK checks `Characters/Dialogue/MarriageDialogue_[SpouseNPC]` for a new child message string. The dialogue key is `HMK_BirthMessage_[n]` where `n` is the number of kids you currently have, or `HMK_BirthMessage` for default value.

If the kid is fixed ahead of time from [`mushymato.HaveMoreKids_SetNewChildEvent`](./002-extensions-triggers_actions.md) or from adoption registry, then [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md)'s `BirthOrAdoptMessage` field takes priority. It is spoken by the NPC parent of the kid, or displayed as a message box if there's no NPC parent.

If no custom message is set, the default value is the string at `Strings\\Events:BirthMessage_Adoption` if adopting/player gender is `Unknown`, `Strings\\Events:BirthMessage_PlayerMother` if player gender is `Female`, `Strings\\Events:BirthMessage_SpouseMother` if player gender is `Male`.

Solo new child (e.g. generic adoptions from adoption registry) always uses `Strings\\Events:BirthMessage_Adoption`.

A special portrait can be used here if loaded to `Portraits/<textureName>_BirthMessage`.

## New Kid Marriage Dialogue

On the first 5 days after having a new child, HMK checks for spouse dialogue in `Characters/Dialogue/MarriageDialogue_[SpouseNPC]`.

These dialogue keys begin with `HMK_NewChild_[d]` where `d` is the number of days since new child (from 0 to 4). There are these variants:
1. `HMK_NewChild_[d]_[mostRecentKidId]` where `mostRecentKidId` is the custom kid id of the newest (youngest) kid.
2. `HMK_NewChild_[d]_[n]` where `n` is the number of kids you currently have.
3. `HMK_NewChild_[d]`, the default value. Note that if you have only 1 or 2 kids, the vanilla `OneKid_[d]` and `TwoKids_[d]` take precedence.
