# Data Asset: mushymato.HaveMoreKids/ChildData

Children use the same data model as [normal NPC](https://stardewvalleywiki.com/Modding:NPC_data), just in a different custom `mushymato.HaveMoreKids/ChildData` target and many fields are unused or have a different meaning. The key `mushymato.HaveMoreKids/ChildData` is the kid id.

These fields are used by kids:

| Field | Type | Notes |
| ----- | ---- | ----- |
| `DisplayName` | string | This is the "canon" name for your kid, if a player gives the newborn baby a matching name, then this custom kid will be picked with highest priority. |
| `Gender` | Gender (Male/Female/Undefined) | Affects picking the kid, will attempt to match the randomized gender if possible. |
| `IsDarkSkinned` | bool | Affects picking the kid, will attempt to match the dark skin genetics if possible. |
| `Appearance` | List\<AppearanceData\> | This works just like normal NPC appearances, but newborn/baby/crawler require special `HMK_BABY*` prefix. |

Some fields are set internally by HMK and cannot be changed by content packs:

| Field | Value |
| ----- | ----- |
| `Age` | `Child` |
| `CanBeRomanced` | `false` |
| `Calendar` | `HiddenAlways` |
| `SocialTab` | `HiddenAlways` |
| `EndSlideShow` | `Hidden` |

Other fields are unused by kids, unless they become an NPC which will have a `Data/Character` entry created based on child data with these changes:

| Field | Value |
| ----- | ----- |
| `DisplayName` | The kid's given name will become their display name. |
| `BirthSeason` | The kid's real birthday, will appear on calendar. |
| `BirthDay`  | The kid's real birthday, will appear on calendar. |
| `CanSocialize`  | `TRUE` |
| `SpawnIfMissing` | `true` |

In this case, the other fields apply to NPC version, e.g. setting `Home` will change where they spawn.

#### Appearance

Custom kids in HMK uses the same [appearance system](https://stardewvalleywiki.com/Modding:NPC_data#Appearance_.26_sprite) as regular NPC, with some special quirks for baby sprites.

- Baby appearance entries should have `Id` that begins with `HMK_BABY`. While a child is not yet a toddler, they will use these appearances.
- Toddlers and child NPC uses the same set of appearances, but you can limit an appearance to toddler by using conditions. The special token `KID_ID` will be replaced with this kid's HMK id. Thus if the kid should look different as a child NPC, use `mushymato.HaveMoreKids_CHILD_AGE KID_ID 4` with a lower Precedence.
- If the toddler has lines, they would need a `Portrait` as well.
- A HMK kid must have a least one unconditional baby appearance and one unconditional toddler appearance, which is defined as an appearance with a valid texture in `Sprite` field, applicable both indoor and outdoors, applies to all seasons, and not an island outfit. For case of seasonal outfits, it's recommended to make one season (such as spring) unconditional, then have the seasonal appearances use a lower `Precedence`.
