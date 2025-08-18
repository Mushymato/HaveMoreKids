# Have More Kids (HMK)

A mod that let you have more kids, unique kids, talkable and giftable kids, roaming NPC kids, and so on.

This mod's has features related to pregnancy and childbirth that will apply even if you don't have any content packs. These can be changed in-game via GMCM.

## Configuration

- `Days Married`: Minimum number of days the player must be married before pregnancy can occur. Vanilla days is 7, only applicable to NPC spouse.
- `Pregnancy Chance`: Changes chance for pregnancy to happen each night, when all other conditions are fufilled.
- `Pregnancy Days`: Time until the child arrives after answering the pregnancy question. Vanilla days is 14, must be changed prior to answering the pregnancy question.
- `Days until Baby`: Number of days until the newborn becomes a baby. Vanilla days is 13.
- `Days until Crawler`: Number of days until the baby becomes a crawler. Vanilla days is 14.
- `Days until Toddler`: Number of days until the crawler becomes a toddler. Vanilla days is 13.
- `Use Single Bed As Child Bed`: Allow children to sleep on single beds instead of special child beds.
- `Base Max Children`: Max number of children to have, if no custom kids are available for the spouse or shared. When the spouse has custom kids, this setting does nothing.

When you have content packs installed, you can configure which custom kids are enabled through GMCM.

## Compatibility

- `Free Love`: not compat rn (0.6.0), soon perhaps.
- `Unique Children Talk`: HMK has similar feature (allow talking to children), but you should be able to use UCT on a HMK kid without dialogue.
- `LittleNPC`: HMK has similar feature (children grow up to NPC), but you should be able to use LittleNPC with a HMK kid that doesn't have NPC mode enabled.

## Multiplayer

In order to ensure proper function, all players must install this mod and the same list of content packs for this mod. Compared to content patcher child replacer mods, this mod manages appearances of other player's children in a way that is consistent for all players, and thus all players need to have matching data and textures even if it is not their baby. This is extra important if the feature of letting kid grow up to a child is enabled.

## Uninstall

You can remove this mod at any time and if you do so, the kid should revert to vanilla children.
Removing a content pack for this mod will make the affected kids retry choosing a unique kid appearance on new day. Depending on what you have installed, this means they either gain a new HMK kid id or they revert to vanilla.

## For Modders

See [author guide](docs/author-guide.md) for how to make a content pack to have unique children for your spouse.
