# Extensions: Tokenizable Strings

HMK provides these [tokenizable strings](https://stardewvalleywiki.com/Modding:Tokenizable_strings).

All of them require a kid id as their first argument, but special logic is applied to dialogue spoken by a HMK Child/Child NPC to automatically supply the first argument if not given. This means you can simply write `[HMK_KidName]` in the kid's dialogue and it will automatically become `[HMK_KidName <kidId of speaker>]`. However if you want to use these tokenizable strings elsewere, e.g. to have the spouse to talk about the kid, then you need to explicitly write the full kid id.

## `[HMK_Endearment <kid id>]`

Resolves to the endearment this kid should use for the player.

Endearments are set in the kid's dialogue asset (e.g. `Characters/Dialogue/<kidId>`) via special keys that are checked in-order:

- `HMK_Endearment`: The most prioritized endearment term, ignores gender.
- `HMK_Endearment_<gender>`: One of `HMK_Endearment_Male`, `HMK_Endearment_Female`, and `HMK_Endearment_Undefined`, respects player gender.

For players who are not the parent of the kid (i.e. a farmhand), these keys are checked instead:

- `HMK_Endearment_NonParent`: The most prioritized endearment term, ignores gender.
- `HMK_Endearment_NonParent_<gender>`: One of `HMK_Endearment_NonParent_Male`, `HMK_Endearment_NonParent_Female`, and `HMK_Endearment_NonParent_Undefined`, respects player gender.

If neither key is set, then these vanilla game strings are used:

- Male: `Strings/Characters:Relative_Dad`
- Female: `Strings/Characters:Relative_Mom`
- Undefined: \<player name\>

## `[HMK_EndearmentCap <kid id>]`

Works just like `HMK_Endearment`, but the first letter is capitalized in english.

## `[HMK_KidName <kid id>]`

Resolves to the player given display name for a kid, or empty string if there's no kid with that id in the world.
Does not work for generic kids since they do not have id.

## `[HMK_RandomSiblingName <kid id>]`

Resolves to the name of a random sibling of this kid. 2 kids are considered siblings if they have the same player parent (NPC parent does not matter).
Does work for generic kids that are siblings to this custom kid.

## `[HMK_NPCParentName <kid id>]`

Resolves to kid's NPC parent name, if they have one.
