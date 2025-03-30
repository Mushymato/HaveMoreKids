# Author Guide

Have More Kids allow content pack authors to create unique kids for their NPC.

_Note: For the 2 example mods in this repository, `[CP] Senlets` is using assets from "Lurking in the Dark", and `[CP] Lumisteria Toddlers` is using assets from "Toddlers like parents - Content Patcher". Need to copy the assets from respective mod to work._

## Adding a new Child

See [Senlet.json]([CP] Senlets/Data/Senlet.json) for full example.

To give the spouse NPC a custom child, add the custom field `mushymato.HaveMoreKids/Kid.<ChildId>`.

e.g.
```json
{
  "Action": "EditData",
  "Target": "Data/Characters",
  "TargetField": [
    "SenS",
    "CustomFields"
  ],
  "Entries": {
    // the value marks this child as enabled or disabled by default in config
    "mushymato.HaveMoreKids/Kid.<ChildId>": true
  }
},
```

The `ChildId` must correspond to an entry in `mushymato.HaveMoreKids/ChildData`, which have the same structure as [Data/Characters](https://stardewvalleywiki.com/Modding:NPC_data#Main_data). Since children are not real NPC, most fields are unused.

Here are the actually used fields:

| Field | Type | Notes |
| ----- | ---- | ----- |
| `DisplayName` | string | This is the "canon" name for your child, if you name your baby with this name, they will be picked directly. |
| `Gender` | Gender (Male/Female/Undefined) | Overrides the random gender picked. |
| `IsDarkSkinned` | bool | If true, prioritize this child if the base baby got assigned dark skin. |
| `Appearance` | List<AppearanceData> | Place to actually put your baby/toddler sprites, more on this in following section. |

### Appearance

This mod makes your child use NPC appearances.

- Baby sprites should have `Id` that begin with `mushymato.HaveMoreKids_Baby`. This forces those appearance entry to be prioritized while child's is not a toddler.
- Toddler sprites works like [normal NPC appearance](https://stardewvalleywiki.com/Modding:NPC_data#Appearance_.26_sprite). Though of course, children do not leave farm house.

Toddler appearances should have `Portrait` set if you wish to give the child dialogue, otherwise their dialogue will appear but without portrait.

### Dialogue & Gift Tastes

You can give the child dialogue by editing `Characters/Dialogue/<ChildId>` and gift tastes by adding a `<ChildId>` entry to `Data/NPCGiftTastes`.
These have same structure as regular [NPC dialogue](https://stardewvalleywiki.com/Modding:Dialogue) and [gift tastes](https://stardewvalleywiki.com/Modding:NPC_data#Gift_tastes).

## Special Birth Trigger

You can have child birth happen in non-standard ways by doing these two things:
1. Set `SpouseWantsChildren` on your Spouse NPC's `Data/Characters` to `"FALSE"`, to block the vanilla nightly pregnancy question event.
2. Use trigger action `mushymato.HaveMoreKids_SetChildBirth` to queue a birth event. Trigger actions can be called from a variety of places, please [refer to the wiki page](https://stardewvalleywiki.com/Modding:Trigger_actions).

### Trigger Action mushymato.HaveMoreKids_SetChildBirth \<daysUntilBirth\> [childId] [spouse] [message]

- `daysUntilBirth`: Required argument, number of days until child birth happens. Setting 0 here means it happens tonight.
- `childId`: Optional specific unique child, ignored if the child does not exist or had already been taken. Use `Any` to skip this argument.
- `spouse`: Optional specific spouse, only do the action if the spouse matches. Use `Any` to skip this argument. For player couples or solo adoption, use `Player`.
- `message`: Optional HUD message that appear when child birth is successfully set. Can use translation key or LocalizedText.

This trigger action causes guarenteed child birth event to occur this night, or a night in the future, as long as normal can have kids rules pass (NPC.canGetPregnant).

A unique child is not required to use this trigger, though of course the `childId` argument does nothing in that case.

## Console Commands

### hmk-set_ages \<age\>

Set the age of all children in the house, in a way that will actually stick. Need to sleep before their sprites/behaviors update.
