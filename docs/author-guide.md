# Author Guide

Have More Kids allow content pack authors to create unique kids for their NPC.

## Data Model

Children use the same data model as [normal NPC](https://stardewvalleywiki.com/Modding:NPC_data), just in a different custom `mushymato.HaveMoreKids/ChildData` target and many fields are unused or have a different meaning.

These fields are used:

| Field | Type | Notes |
| ----- | ---- | ----- |
| `DisplayName` | string | This is the "canon" name for your kid, if a player name the baby with this name, they will be picked directly. |
| `Gender` | Gender (Male/Female/Undefined) | Affects picking the kid, will attempt to match the randomized gender if possible. |
| `IsDarkSkinned` | bool | Affects picking the kid, will attempt to match the dark skin genetics if possible. |
| `Appearance` | List\<AppearanceData\> | This works just like normal NPC appearances, but newborn/baby/crawler require special `HMK_BABY*` prefix. |
| `CanSocialize` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | Controls whether the kid will become a full NPC. When this is null/FALSE, the feature is completely disabled for this kid, otherwise the GSQ is evaluated each morning to determine whether the child will "go outside" that day. |

Many fields are forced into a specific value, meaning that you cannot change them by `EditData`.

| Field | Value |
| ----- | ----- |
| `Age` | `Child` |
| `CanBeRomanced` | `false` |
| `Calendar` | `HiddenAlways` (note 1: birthday display is controlled by player) |
| `SocialTab` | `HiddenAlways` |
| `EndSlideShow` | `Hidden` |
| `FlowerDanceCanDance` | `false` |
| `PerfectionScore` | `false` |

The remaining fields are either unused or set interally by HMK, for instance birthdays.

### Appearance

Custom kids in HMK uses the same [appearance system](https://stardewvalleywiki.com/Modding:NPC_data#Appearance_.26_sprite) as regular NPC, with some special quirks for baby sprites.

- Baby sprites should have `Id` that begin with `HMK_BABY`. While a child is not yet a toddler, they will use these appearances.
- Toddlers and child NPC uses the same set of appearances, but you can limit an appearance to toddler by using conditions. The special token `KID_ID` will be replaced with this kid's HMK id. Thus if the kid should look different as a child NPC, use `mushymato.HaveMoreKids_CHILD_AGE KID_ID 4` with a lower Precedence.
- If the toddler has lines, they would need a `Portrait` as well.
- A HMK kid must have a least one unconditional baby appearance and one unconditional toddler appearance, which is defined as an appearance with valid Sprite, applicable both indoor and outdoors, and not an island outfit.

### Dialogue & Gift Tastes

You can give the child dialogue by editing `Characters/Dialogue/<ChildId>` and gift tastes by adding a `<ChildId>` entry to `Data/NPCGiftTastes`.
These have same structure as regular [NPC dialogue](https://stardewvalleywiki.com/Modding:Dialogue) and [gift tastes](https://stardewvalleywiki.com/Modding:NPC_data#Gift_tastes).

Gift tastes and dialogue assets are shared between toddler and child NPC.

## Trigger Actions

You can have child birth happen in non-standard ways by doing these two things:
1. Set `SpouseWantsChildren` on your Spouse NPC's `Data/Characters` to `"FALSE"`, to block the vanilla nightly pregnancy question event.
2. Use trigger action `mushymato.HaveMoreKids_SetChildBirth` to queue a birth event. Trigger actions can be called from a variety of places, please [refer to the wiki page](https://stardewvalleywiki.com/Modding:Trigger_actions).

### mushymato.HaveMoreKids_SetChildBirth \<daysUntilBirth\> [childId] [spouse] [message]

- `daysUntilBirth`: Required argument, number of days until child birth happens. Setting 0 here means it happens tonight.
- `childId`: Optional specific unique child, ignored if the child does not exist or had already been taken. Use `Any` to skip this argument.
- `spouse`: Optional specific spouse, only do the action if the spouse matches. Use `Any` to skip this argument. For player couples or solo adoption, use `Player`.
- `message`: Optional HUD message that appear when child birth is successfully set. Can use translation key or LocalizedText.

This trigger action causes guarenteed child birth event to occur this night, or a night in the future, as long as normal can have kids rules pass (NPC.canGetPregnant).

A unique child is not required to use this trigger, though of course the `childId` argument does nothing in that case.

### mushymato.HaveMoreKids_SetChildAge \<childId|childIndex\> \<Age\>

This trigger action alters the age of a child, however, the effects only happens on day started. If the action is called in the middle of a day, you must sleep before changes take place.
Thus, it's recommended to use this action with trigger `DayEnding`.

- `childId|childIndex`: This argument is either a unique child id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.
- `age`: A number corresponding to the age stages
  - 0: Newborn (sleeping baby)
  - 1: Baby (sitting baby)
  - 2: Crawler (crawling around the house)
  - 3: Toddler (running around the house)

## Game State Queries

### mushymato.HaveMoreKids_CHILD_AGE \<childId|childIndex\> \<Age\>

Checks a child is at a certain age.

- `childId|childIndex`: This argument is either a unique child id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.
- `age`: A number corresponding to the age stages
  - 0: Newborn (sleeping baby)
  - 1: Baby (sitting baby)
  - 2: Crawler (crawling around the house)
  - 3: Toddler (running around the house)

### mushymato.HaveMoreKids_HAS_CHILD \<childId|childIndex\>

Check that the player has a particular child.

- `childId|childIndex`: This argument is either a unique child id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.

