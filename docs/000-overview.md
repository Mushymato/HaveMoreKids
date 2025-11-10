# Author's Guide: Overview

A Have More Kids (HMK) content pack is a [content patcher](https://www.nexusmods.com/stardewvalley/mods/1915) content pack that provides specific data. This document assumes that you understand the basics of content patcher.

This page covers the pieces needed to create custom kids.

## Index

- Data Models
    - [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md)
    - [`mushymato.HaveMoreKids/ChildData`](./001-model-child_data.md)
- Extensions
    - [Game State Queries](./002-extensions-tokenizable_strings.md)
    - [Triggers and Actions](./002-extensions-triggers_actions.md)
    - [Tokenizable Strings](./002-extensions-tokenizable_strings.md)
    - [Content Patcher Tokens](./002-extensions-content_patcher_tokens.md)
- Misc
    - [Custom Cribs](./003-misc-custom_cribs.md)
    - [Spouse Dialogue](./003-misc-spouse_dialogue.md)

## What makes up a Kid?

A custom kid in HMK can have these features, as long as relevant data is provided.

- Unique look that is tracked on a per kid basis, no need to conditionally edit `Characters/Baby` and `Characters/Toddler`. Their appearance persists even if you divorce your spouse, or if you have multiple spouses via polyamory mods.
- Seasonal outfits via the same `Appearance` system used by vanilla NPC.
- Daily dialogue via dialogue asset.
- Gifting to build friendship.

### Mandatory

There are 2 mandatory assets that defines a kid:

- [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md) defines how this kid can be acquired (through specific parent, any parent, etc.) plus other custom values. See [this page](./001-model-kids.md) for more details.

- [`mushymato.HaveMoreKids/ChildData`](./001-model-child_data.md) is an asset identical to [vanilla NPC data](https://stardewvalleywiki.com/Modding:NPC_data) and mainly serves as a way to utilize NPC like data, especially [`Appearances`](https://stardewvalleywiki.com/Modding:NPC_data#Appearance_.26_sprite), for custom kids. See [this page](./001-model-child_data.md) for more details.

Both are dictionary type data and in both cases the key of the dictionary is what the author guide refers to as "kid id" or `<kidId>`.

### Dialogue

If you want the kid to have dialogue then you additionally need to provide a dialogue asset, just like what a typical NPC needs:

[`Characters/Dialogue/<kidId>`](https://stardewvalleywiki.com/Modding:Dialogue) is the default dialogue asset for the kid, but you can define a custom asset name via [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md) too. Because HMK loads dialogue through vanilla logic, you can also use [`Characters/Dialogue/rainy`](https://stardewvalleywiki.com/Modding:Dialogue#Rain_dialogue).

Things to be aware of:

- You must do a `Load` to initialize the dialogue asset. Generally this is done by loading a `blank.json`.
- You can change the child's dialogue asset name if needed, by setting [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md)'s `DialogueSheetName` field.
- HMK adds a number of [tokenizable strings](./002-extensions-tokenizable_strings.md) for things like parent title and kid display name. Normally they require a kid id to work, but when they appear in a dialogue asset, they will default to using the kid id of the speaking kid if applicable.

### Gifting

If you want the kid to accept gifts then you need to provide a gift taste data, much like a normal NPC:

[`Data/NPCGiftTastes`](https://stardewvalleywiki.com/Modding:Gift_taste_data) is where gift tastes are defined, you can add an entry with `<kidId>` as the key to give your child gift tastes.

Additionally, the dialogue keys for accepting and rejecting items work as well, so as long as you put those in the dialogue asset.

## Where do Kids come from?

In the base game, you may get the baby question from your spouse when:

- You have less than 2 kids
- Your house has a crib that isn't currently occupied
- You are married with >10 hearts with your spouse
- You are lucky that night, and succeeded a 5% roll

After answering 'yes', the baby will arrive in a night event (the New Child event) in 14 days.

HMK allows nearly all aspects of baby acquisition to be customized by the user, and adds 2 new paths:
- Force a new child event to happen at a specific day via [action](./002-extensions-triggers_actions.md).
- Adoption from the adoption registry located in Harvey's Clinic.

### Adoption via Adoption Registry

The player may use the adoption registry to adopt a child at any time. By default, kids marked as `Shared` in [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md) may appear this way, and if there are no matching kids then the baby is a non-custom vanilla baby.

A content pack may have a particular kid available via adoption registry by setting  `CanAdoptFromAdoptionRegistry` in [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md) to a game state query that resolves to true.

## Full NPC Mode

Beyond having the kid move about the house (and optionally roam around on the farm), you can make them eventually grow up into a real NPC.

This works by transforming a `mushymato.HaveMoreKids/ChildData` entry into actual `Data/Characters` entry with a generated internal NPC Id, causing them to spawn in the world. This NPC shares dialogue, appearance, friendship values with the Child, but follows a real NPC schedule.

Thus there are 2 entities that are ostensibly the player's kid.
- Child, in the farmhouse and sometimes farm
- NPC, outside of farm

On a given day, HMK manages whether the Child or the NPC is visible, based on GSQ condition set by the content pack.

### Required Data

- [`mushymato.HaveMoreKids/Kids`](./001-model-child_data.md)'s `IsNPCTodayCondition` field is a [game state query](https://stardewvalleywiki.com/Modding:Game_state_queries) that determines whether the child should be visible as a NPC today. A kid will get a NPC counterpart if this field is not `null` or `"FALSE"`.

- [`mushymato.HaveMoreKids/ChildData`](./001-model-child_data.md)'s `Home` field defines where the NPC version of child will spawn. If you do not add this, then your kid NPC will appear in the middle of town just like how it works for a normal NPC. `Home` is a list, and a NPC may have multiple conditional `Home` entries.

- [`Characters/schedules/<kidId>`](https://stardewvalleywiki.com/Modding:Schedule_data) is the schedule asset for the NPC version of the kid.
    - You must do a `Load` to initialize the schedule asset. Generally this is done by loading a `blank.json`.

Aside from typical NPC schedule features, HMK provides these special features:

### Leaving the Farmhouse

When the Kid NPC's day start location is adjacent to the Farm (e.g. BusStop, Forest, possibly more depending on mods), the Child version will be visible in the farmhouse when you wake up, then leave the house through front door 30 minutes before the first schedule point of the day. Until the, the Child is visible for players to talk to and the NPC is hidden.

When the Kid NPC's day start location is not adjacent to the Farm, they will be invisilbe as Child and visible as NPC from day start.

Day start location can be controlled in one of 2 ways:
1. [`mushymato.HaveMoreKids/ChildData`](./001-model-child_data.md)'s current `Home` entry.
2. A 0 schedule (schedule with `time` value less than 0600).

### Returning to the Farmhouse

There is a special "animation" key `HMK_Home` that is not actually an animation key and instead a hint for HMK to send the kid back to the farmhouse for the rest of the day. This is done by:
1. Overriding the schedule point to instead path to the location indicated by the `Home` field, if the target tile was `0 0`, then also include the tile from the `Home` entry.
2. Once the Kid NPC has reached the `Home` tile, turn the Kid NPC invisible and the Child visible.

Only the first `HMK_Home` marked schedule point does anything.

### Schedule Examples

For example let's say the Kid has this `Home` such that the `Home` tile is X:19 Y:14 in BusStop:
```js
{
  "Id": "Default",
  "Condition": null,
  "Location": "BusStop",
  "Tile": {
    "X": 19,
    "Y": 14
  },
  "Direction": "down"
}
```
And loaded this schedule string for the day: `0700 Town 23 52 2/1010 Town 0 0 HMK_Home`, i.e. they go from home (BusStop) to Town and then back to home.

The behavior will be:
1. The Child will visually leave the farmohouse in the morning, since the `Home` tile is in a map adjacent to the `Farm`.
2. HMK will transform the schedule into equivalent of `0700 Town 23 52 2/1010 BusStop 19 14`.
3. When the Kid NPC completes the `1010 BusStop 19 14` schedule point, they will become invisible while the Child reappears in the farmhouse.

Now if schedule string is a 0 schedule, for example `0000 Mountain 43 23 2/0700 Town 23 52 2/1010 Town 0 0 HMK_Home`:
1. The Child will be invisible from the start of the day, because `Mountain` does not connect to `Farm` (unless another mod changes this).
2. HMK will transform the schedule into equivalent of `0000 Mountain 43 23 2/0700 Town 23 52 2/1010 BusStop 19 14`, because the `Home` tile is unchanged.
3. When the Kid NPC completes the `1010 BusStop 19 14` schedule point, they will become invisible while the Child reappears in the farmhouse.

_Note: Although the location name of `Town` in `1010 Town 0 0 HMK_Home` is always overwritten, it's a good idea to put a real reachable location to avoid spacecore scheduler warnings_

### Dialogue Handling

Everyday, you can talk to both the Child version once, and the NPC version once. To avoid duplicate dialogue, you can use [locational dialogue keys](https://stardewvalleywiki.com/Modding:Dialogue#Location_dialogue) targeting the farmhouse (e.g. `"FarmHouse_Mon"`) to assign dialogue for the Child only, since the NPC version will not enter the farmhouse.

### Adopt NPC as Child

This feature is very closely related to having a Child become an NPC, except it goes the other direction and creates a Child counterpart for a existing normal NPC. The Child will be added to the player's farmhouse as a toddler that share the same sprites as the NPC, skipping the newborn/baby/crawler stages. HMK does not create an entirely new NPC in this case, and simply begins managing the existing vanilla NPC with the same sort of hide/show logic.

There are 2 methods of activating adoption:
- Set [`mushymato.HaveMoreKids/Kids`](./001-model-kids.md)'s `CanAdoptFromAdoptionRegistry` field to a GSQ that evaluates to true. This will add the kid as a custom adoptable kid.
- Use [`mushymato.HaveMoreKids_SetNewChildEvent`](./002-extensions-triggers_actions.md) with special value `Player` as the "spouse".

## Custom Birth Events

You can have a kid via non-standard (i.e. not the vanilla nightly pregnancy question) ways by doing these two things:
1. Set `SpouseWantsChildren` on your Spouse NPC's `Data/Characters` entry to `"FALSE"`, to block the vanilla nightly pregnancy question event.
2. Use [`mushymato.HaveMoreKids_SetNewChildEvent`](./002-extensions-triggers_actions.md) to queue a new child event. Actions can be called from a variety of places, please [refer to the wiki page](https://stardewvalleywiki.com/Modding:Trigger_actions).


## Example Pack

There is a [full example HMK content pack](../ContentPacks/[CP]%20HMK%20Example) which adds 2 kids to spouse NPC of choice, one as a talking and giftable child, and one as a full NPC.
