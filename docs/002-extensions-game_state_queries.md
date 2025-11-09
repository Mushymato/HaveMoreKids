# Extensions: Game State Queries

HMK provides these [game state queries](https://stardewvalleywiki.com/Modding:Game_state_queries).

## mushymato.HaveMoreKids_WILL_HAVE_CHILD [spouseName] [kidId]

Check that you will have a child.

- `spouseName`: A specific spouse name to check for, can use `Any` for anyone, and `Player` for player adoptions.
- `kidId`: A specific kid id to check for.

## mushymato.HaveMoreKids_HAS_CHILD \<kidId|childIndex\>

Check that the player has a particular child, works for both generic and custom kids.

- `kidId|childIndex`: This argument is either a kid id, or an index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.

## mushymato.HaveMoreKids_HAS_ADOPTED_NPC \<NPCId\>

Check that the player has a child who is adopted from specific NPC. Only applicable to `AdoptedFromNPC` kids.

- `NPCId`: This argument is the NPC Id.

## mushymato.HaveMoreKids_CHILD_AGE \<kidId|childIndex\> \<Age\>

Checks a child is at a certain age, works for both generic and custom kids. For generic kids their player given name is considered the kid id.

- `kidId|childIndex`: This argument is either a kid id, or an index for child in order of birth in the format of `#N`, e.g. `#0` for the first born child.
- `age`: A number corresponding to the age stages
  - 0: Newborn (sleeping baby)
  - 1: Baby (sitting baby)
  - 2: Crawler (crawling around the house)
  - 3: Toddler (running around the house)
  - 4: Child NPC (went outside today as NPC)

Age increases from 0 to 3 as days pass, but a given kid can switch between Toddler mode and Child NPC mode depending on [`mushymato.HaveMoreKids/Kids`](./001-model-child_data.md)'s `IsNPCTodayCondition`.

HMK also provides the [`mushymato.HaveMoreKids_SetChildAge`](./002-extensions-triggers_actions.md) action which may change a child's age.
