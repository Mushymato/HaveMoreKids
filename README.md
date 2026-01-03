# Have More Kids (HMK)

A mod that overhauls how pregnancy and children works. It is both a mod that lets players change various aspects of children, and a framework mod that allows content modders to create dialogue, gift taste, and potentially full NPC style kids.

## Author Guide

See [author guide](docs/000-overview.md) for how to make a content pack that adds custom kids.

## Player Facing Features

Have More Kids allows you to have more kids at a pace you can customize.

### More Kids

You can have more kids more frequently, and have them grow up faster (or slower). The parameters can be tuned in the configuration options.

### More Cribs

You can purchase a furniture crib from Robin's shop. When placed in the farmhouse, these count as cribs for the purposes of childbirth. Thus you can raise multiple kids at once, or have a kid without the farmhouse built-in crib.

### Rename Your Kids

You can rename your children via the children registry at Harvey's Clinic. This works on both generic children and custom kids.

### Single Parenthood

Once you have two house upgrades, you can adopt a kid from the children registry at Harvey's Clinic even if you aren't married.
Content packs may also add custom kids that are only available via adoption here.

### Kids Playing Outside

Once the kids reaches toddler age, they will go out to the farm in the morning and wander around the farm, then return to the house by 1900.
By default, the kids will only go out when it is not raining and not winter, custom kids may have special rules.

### Specific Custom Kid

If you have content pack providing custom kids installed, and the author has given them specific "canonical" names, giving the new child one of the canon names will bypass RNG and always choose the specific custom kid.

## Configuration

### General

- `Days Married`: Minimum number of days the player must be married before pregnancy can occur. Vanilla days is 7, only applicable to NPC spouse.
- `Pregnancy Chance`: Changes chance for pregnancy to happen each night, when all other conditions are fufilled.
- `Pregnancy Days`: Time until the child arrives after answering the pregnancy question. Vanilla days is 14, must be changed prior to answering the pregnancy question.
- `Days until Baby`: Number of days until the newborn becomes a baby. Vanilla days is 13.
- `Days until Crawler`: Number of days until the baby becomes a crawler. Vanilla days is 14.
- `Days until Toddler`: Number of days until the crawler becomes a toddler. Vanilla days is 13.
- `Use Single Bed As Child Bed`: Allow children to sleep on single beds instead of special child beds only.
- `Generic Children Boy/Girl Mode`: Decide how gender distribution works for generic children, has no effect on custom kids.
- `Max Children`: Max number of children you can have (i.e. max number of children that will live in your farmhouse).
- `Toddlers Roam on Farm`: Allow all toddlers to roam around on the farm. They will go outside in the morning, and return home by 1900.
- `Per Kid Dark Shrine of Selfishness`: Override Dark Shrine of Selfishness to allow picking a specific kid.

#### Boy/Girl Modes

These are the options of `Generic Children Boy/Girl Mode`.
Changing the option does not reset the gender of existing children, this only affects any following children you have after changing the config.
Custom kids ignore this setting completely and uses the gender defined by the content pack.

- `Alternating`: The first child will be randomly boy or girl, each following child will have the opposite gender as the previous child.
- `Random`: Children gender is random.
- `Boys Only`: All children will be boys.
- `Girls Only`: All children will be girls.

### Content Pack

When there are custom kids installed, you can configure them in the subpages named `<NPC>'s Kids`. Here you can preview the kids for that spouse and enable/disable them as desired. Note that because the content packs are expected to be content patcher mods, they may have their own configuration options too.

There's a config option specifically for the case where spouse has custom kids:

- `Always Allow Generic Children`: When your spouse has custom kids from content packs, generic children will not appear by default unless you enable this config. Besides this setting, `Max Children` still applies when it comes to determining whether you can have another kid.

## Compatibility

- `Free Love`: Compatibility is provided by HMK via harmony patches. Kids will use the parent picked by Free Love using it's pregnancy chance modifiers, but whether they can have kids at all is decided by HMK.
- `Unique Children Talk`: Should work with the first 2 kids, but unknown what happens for 3+. HMK implements a similar feature for content packs to add custom dialogue to kids.
- `Unique Children`: It's unknown which appearance system would take precedence when it comes to custom kids, but generic children should get to have unique children decide their appearance.
- `LittleNPC`: HMK implements a similar feature that allows content packs to add custom kids who grow up into NPC. This feature is automatically disabled if LittleNPC is installed.

## Multiplayer

In order to ensure proper function, all players must install this mod and the same list of content packs for this mod. Compared to content patcher child replacer mods, this mod manages appearances of other player's children in a way that is consistent for all players, and thus all players need to have matching data and textures even if it is not their baby. This is extra important if the feature of letting kid grow up to a child is enabled.

## Translations
- English
- 简体中文

## Installation
- Download and install SMAPI
- Download this mod and extract the archive to the Mods folder.

## Uninstallation

You can remove this mod at any time in your save. After doing so, all kids should revert to generic children.
Removing a content pack for this mod will make the affected kids retry choosing a unique kid appearance on new day. If there are other valid kids for them, this may mean they "change" into a different HMK custom kid that is still installed.
