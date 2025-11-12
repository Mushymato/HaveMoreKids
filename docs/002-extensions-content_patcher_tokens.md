# Extensions: Content Patcher Tokens

HMK provides these content patcher tokens, and the implementation also results in a change in behavior for the `ChildNames` token.

**Note:** These content patcher tokens updates slightly later than the equivalent [game state queries](./002-extensions-game_state_queries.md). Thus, if you don't need to use CP tokens then it's better to use the GSQs instead.

## mushymato.HaveMoreKids/KidDisplayName

Can be used...
1. Without arguments (`{{mushymato.HaveMoreKids/KidDisplayName}}`): lists all the display names of the player's kids
2. With 1 argument (`{{mushymato.HaveMoreKids/KidDisplayName:<kidId>}}`): list the display name of a specific kid

**Note:** If you need child name in a place that supports [tokenizable strings](https://stardewvalleywiki.com/Modding:Tokenizable_strings) such as dialogue, it is better to use [`[HMK_KidName <kidId>]`](./002-extensions-tokenizable_strings.md).

## mushymato.HaveMoreKids/KidNPCId

Can be used...
1. Without arguments (`{{mushymato.HaveMoreKids/KidNPCId}}`): lists the generated NPC ids for kids.
2. With 1 argument (`{{mushymato.HaveMoreKids/KidNPCId:<kidId>}}`): return the generated NPC id for this kid, if applicable.

**Note:** If you are adding a child NPC to an event, you can directly use the kid id. HMK will automatically pick the kid NPC version if they exist.

## ChildNames

This token is provided by content patcher and normally it lists all the player given names of the player's children. However, due to technical reasons this will actually give the internal kid id for any HMK custom kid.

This can be used to your advantage in that `{{ChildNames|valueAt=0}}` will resolve to the internal kid id of the first kid, such that `{{mushymato.HaveMoreKids/KidNPCId:{{ChildNames|valueAt=0}}}}` will yield the internal NPC id of the first child of the player (if they have one).
