# Extensions: Triggers & Actions

HMK provides these [triggers and actions](https://stardewvalleywiki.com/Modding:Trigger_actions).

## Trigger: mushymato.HaveMoreKids_NewChild

Trigger for use with `Data/TriggerActions`, raised at the end of a new child night event that results in a child.

## Trigger: mushymato.HaveMoreKids_Adoption

Trigger for use with `Data/TriggerActions`, raised when adoption via adoption registry has just occurred. Does not fire any other means of new child.

## Trigger: mushymato.HaveMoreKids_Doved

Trigger for use with `Data/TriggerActions`, raised when the Dark Shrine of Selfishness is used.

## Action: mushymato.HaveMoreKids_SetNewChildEvent \<daysUntilNewChild\> [kidId] [spouse] [message]

Action that causes guarenteed child birth event to occur this night, or a night in the future, as long as normal can-have-kids checks pass.

You can have a kid via non-standard (i.e. not the vanilla nightly pregnancy question) ways by doing these two things:
1. Set `SpouseWantsChildren` on your Spouse NPC's `Data/Characters` entry to `"FALSE"`, to block the vanilla nightly pregnancy question event.
2. Use this action to queue a new child event. Actions can be called from a variety of places, please [refer to the wiki page](https://stardewvalleywiki.com/Modding:Trigger_actions).

- `daysUntilNewChild`: Required argument, number of days until new child arrives. Setting 0 here means it happens tonight.
- `kidId`: Optional kid id for a specific custom kid, ignored if the custom kid does not exist or had already been taken. A kid specified this way ignores `Parent` and `Shared` settings, but not `Condition`. Use `Any` to skip this argument.
- `spouse`: Optional specific spouse, only do the action if the spouse matches. Use `Any` to skip this argument. For player couples or solo adoption, use `Player`.
- `message`: Optional HUD message that appears when child birth is successfully set. Can use translation key or `[LocalizedText <translation key>]`.

A custom kid is not required to use this trigger, and the `kidId` argument does nothing in that case.

## Action: mushymato.HaveMoreKids_SetChildAge \<kidId\> \<age\>

Action that alters the age of a child. Since effects of age is checked and applied at start of day, if the action is called in the middle of a day then the player must sleep before changes take place. Thus, it's recommended to use this action with trigger `DayEnding`.

- `kidId|childIndex`: This argument is either a unique kid id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.
- `age`: A number corresponding to the age stages
  - 0: Newborn (sleeping baby)
  - 1: Baby (sitting baby)
  - 2: Crawler (crawling around the house)
  - 3: Toddler (running around the house)
  - 4: Child NPC (Toddler + roaming NPC)
