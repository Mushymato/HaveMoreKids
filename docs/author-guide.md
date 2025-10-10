# Author Guide

Have More Kids (HMK) allow content pack authors to create unique kids for their NPC. For full example, see [the example pack](../ContentPacks/[CP]%20HMK%20Example).

## Data Models

To make a custom kid, you need a child data entry in `mushymato.HaveMoreKids/ChildData` and a kid metadata entry in `mushymato.HaveMoreKids/Kids`.

### mushymato.HaveMoreKids/ChildData

Children use the same data model as [normal NPC](https://stardewvalleywiki.com/Modding:NPC_data), just in a different custom `mushymato.HaveMoreKids/ChildData` target and many fields are unused or have a different meaning. The key `mushymato.HaveMoreKids/ChildData` is the kid id.

These fields are used:

| Field | Type | Notes |
| ----- | ---- | ----- |
| `DisplayName` | string | This is the "canon" name for your kid, if a player name the baby with this name, they will be picked directly. |
| `Gender` | Gender (Male/Female/Undefined) | Affects picking the kid, will attempt to match the randomized gender if possible. |
| `IsDarkSkinned` | bool | Affects picking the kid, will attempt to match the dark skin genetics if possible. |
| `Appearance` | List\<AppearanceData\> | This works just like normal NPC appearances, but newborn/baby/crawler require special `HMK_BABY*` prefix. |
| `CanSocialize` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | Controls whether the kid will become a full NPC. When this is null/FALSE, the feature is completely disabled for this kid, otherwise the GSQ is evaluated each morning to determine whether the child will "go outside" that day. |
| `CustomFields["mushymato.HaveMoreKids/GoOutsideCondition"]` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | When Toddler Roam on Farm is enabled, this controls whether the toddler will go outside and roam on the farm that day. In this case, the entity on the farm is the actual `Child` and not a NPC clone. |

Many fields are forced into a specific value, meaning that you cannot change them by `EditData`.

| Field | Value |
| ----- | ----- |
| `Age` | `Child` |
| `CanBeRomanced` | `false` |
| `Calendar` | `HiddenAlways` |
| `SocialTab` | `HiddenAlways` |
| `EndSlideShow` | `Hidden` |
| `FlowerDanceCanDance` | `false` |
| `PerfectionScore` | `false` |

The remaining fields are either unused or set interally by HMK, for instance birthdays are set to the real birthday of the child in the current save.

#### Appearance

Custom kids in HMK uses the same [appearance system](https://stardewvalleywiki.com/Modding:NPC_data#Appearance_.26_sprite) as regular NPC, with some special quirks for baby sprites.

- Baby sprites should have `Id` that begin with `HMK_BABY`. While a child is not yet a toddler, they will use these appearances.
- Toddlers and child NPC uses the same set of appearances, but you can limit an appearance to toddler by using conditions. The special token `KID_ID` will be replaced with this kid's HMK id. Thus if the kid should look different as a child NPC, use `mushymato.HaveMoreKids_CHILD_AGE KID_ID 4` with a lower Precedence.
- If the toddler has lines, they would need a `Portrait` as well.
- A HMK kid must have a least one unconditional baby appearance and one unconditional toddler appearance, which is defined as an appearance with valid Sprite, applicable both indoor and outdoors, and not an island outfit.

#### Dialogue & Gift Tastes

You can give the child dialogue by editing `Characters/Dialogue/<KidId>` and gift tastes by adding a `<KidId>` entry to `Data/NPCGiftTastes`.
These have same structure as regular [NPC dialogue](https://stardewvalleywiki.com/Modding:Dialogue) and [gift tastes](https://stardewvalleywiki.com/Modding:NPC_data#Gift_tastes).

Gift tastes and dialogue assets are shared between toddler and child NPC.

#### Kid NPC Mode

When `CanSocialize` is not always false, HMK will generate a NPC counterpart to a toddler aged kid. These NPCs can have full schedules, loaded from  `Characters/schedules/<KidId>`. They still get dialogue from `Characters/Dialogue/<KidId>` as well.

Every morning, `CanSocialize` is reevaluated, if the result is true then the NPC counterpart of the kid will be visible that day and the Child version is invisible. Conversely if the result is false, the NPC version of the kid will be invisible and the Child version visible.

### mushymato.HaveMoreKids/Kids

Kids metadata in `mushymato.HaveMoreKids/Kids` defines behavior around pregnancy/adoption. This is also how you can create a child version associated with an existing NPC. The key `mushymato.HaveMoreKids/Kids` is the kid id, and should match an entry in `mushymato.HaveMoreKids/ChildData`.

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `Parent` | string | _null_ | This is the NPC parent of the kid, if they should have one. |
| `Shared` | bool | `false` | When `Parent` is null and this field is true, any spouse can have this kid. |
| `DefaultEnabled` | bool | `true` | If this kid should be enabled by default in the config. |
| `Condition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | Controls whether the kid is available from content pack side. |
| `Twin` | string | The kid id of the twin who will be born at the same time as this kid, as long as there's enough cribs. |
| `TwinCondition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | A condition on whether the twin should be born. |
| `TwinMessage` | string | _null_ | A special message to show when a twin is born. |
| `AdoptedFromNPC` | string | _null_ | The internal id of the NPC to adopt as a Child. |
| `BirthOrAdoptMessage` | string | _null_ | A special message to show for birth/adoption of this kid. |

#### Parent vs Shared

When `AdoptedFromNPC` is set, this kid can only appear through trigger action `mushymato.HaveMoreKids_SetNewChildEvent`.

When `Parent` is set, this kid can only appear with that specific NPC spouse.

When `Shared` is set, you this kid can appear with any NPC spouse, as well as player pairings.

When none of the above are set, this kid can only appear through trigger action `mushymato.HaveMoreKids_SetNewChildEvent`.

#### Twins

Content packs can define a kid as the twin of another kid such that if one is born, the other will be immediately born in the same night event, so as long as there are enough cribs.

Players can have more cribs by placing custom crib furniture.

#### Adopt from NPC

Content packs can specify a kid counterpart to an existing NPC, essentially creating a way to adopt them. Kids adopted this way begin as a toddler.

This works very similar to Kid NPC Mode, but instead of `CanSocialize` the daily visible/invisible check uses `Data/Characters` custom field `mushymato.HaveMoreKids/IsNPCToday`.

See [example here](../ContentPacks/[CP]%20Jas%20Adoption/data/content.json).

## Custom Crib Furniture

By default, HMK provides a furniture custom crib that can be purchased from the Carpenter shop. Having these placed in the farmhouse allows you to have more kids at once.

Content packs can add more cribs by creating a furniture that:
- Has bounding box `3 2`
- Has context tag `hmk_crib`

See [example here](../ContentPacks/[CP]%20HMK%20Example/data/crib.json).

While a crib furniture is occupied by a kid that hasn't become a toddler yet, player cannot move the crib.

## Content Patcher Tokens

HMK provides these 2 content patcher tokens:
- `mushymato.HaveMoreKids/KidNPCId:<kidId>`: yields the internal ID of the NPC counterpart of this kid, if there is one. When used without `kidId`, this token yields all the id of all NPC kids.
- `mushymato.HaveMoreKids/KidDisplayName`: yields ordered list of display names for kids of the player.

In addition, the content patcher built-in token `ChildNames` behaves differently on HMK, it now yields the internal kid id which is separated from display name. Use `mushymato.HaveMoreKids/KidDisplayName` for the display name.

## Trigger Actions

You can have a kid via non-standard (i.e. not the vanilla nightly pregnancy question) ways by doing these two things:
1. Set `SpouseWantsChildren` on your Spouse NPC's `Data/Characters` to `"FALSE"`, to block the vanilla nightly pregnancy question event.
2. Use trigger action `mushymato.HaveMoreKids_SetNewChildEvent` to queue a new child event. Trigger actions can be called from a variety of places, please [refer to the wiki page](https://stardewvalleywiki.com/Modding:Trigger_actions).

This is also how you can have a child without being married.

### mushymato.HaveMoreKids_SetNewChildEvent \<daysUntilNewChild\> [kidId] [spouse] [message]

- `daysUntilNewChild`: Required argument, number of days until child birth happens. Setting 0 here means it happens tonight.
- `kidId`: Optional specific unique child, ignored if the child does not exist or had already been taken. A kid specified this way ignores `Parent` and `Shared` settings, but not `Condition`. Use `Any` to skip this argument.
- `spouse`: Optional specific spouse, only do the action if the spouse matches. Use `Any` to skip this argument. For player couples or solo adoption, use `Player`.
- `message`: Optional HUD message that appear when child birth is successfully set. Can use translation key or LocalizedText.

This trigger action causes guarenteed child birth event to occur this night, or a night in the future, as long as normal can have kids rules pass (NPC.canGetPregnant).

A unique child is not required to use this trigger, though of course the `kidId` argument does nothing in that case.


### mushymato.HaveMoreKids_SetChildAge \<KidId|childIndex\> \<Age\>

This trigger action alters the age of a child, however, the effects only happens on day started. If the action is called in the middle of a day, you must sleep before changes take place.
Thus, it's recommended to use this action with trigger `DayEnding`.

- `KidId|childIndex`: This argument is either a unique child id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.
- `age`: A number corresponding to the age stages
  - 0: Newborn (sleeping baby)
  - 1: Baby (sitting baby)
  - 2: Crawler (crawling around the house)
  - 3: Toddler (running around the house)

## Game State Queries

### mushymato.HaveMoreKids_CHILD_AGE \<KidId|childIndex\> \<Age\>

Checks a child is at a certain age.

- `KidId|childIndex`: This argument is either a unique child id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.
- `age`: A number corresponding to the age stages
  - 0: Newborn (sleeping baby)
  - 1: Baby (sitting baby)
  - 2: Crawler (crawling around the house)
  - 3: Toddler (running around the house)

### mushymato.HaveMoreKids_HAS_CHILD \<KidId|childIndex\>

Check that the player has a particular child.

- `KidId|childIndex`: This argument is either a unique child id, or a index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.

