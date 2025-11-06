# Extensions: Content Patcher Tokens

HMK provides these content patcher tokens, and the implementation also results in a change in behavior for the `ChildNames` token.

## mushymato.HaveMoreKids/KidDisplayName

Can be used...
1. Without arguments (`{{mushymato.HaveMoreKids/KidDisplayName}}`): lists all the display names of the player's kids
2. With 1 argument (`{{mushymato.HaveMoreKids/KidDisplayName:<kidId>}}`): list the display name of a specific kid

**Note:** If you need child name in a place that supports [tokenizable strings](https://stardewvalleywiki.com/Modding:Tokenizable_strings) such as dialogue, it is better to use [`[HMK_KidName <kidId>]`](./002-extensions-tokenizable_strings.md).

## mushymato.HaveMoreKids/KidNPCId

Can be used...
1. Without arguments (`{{mushymato.HaveMoreKids/KidNPCId}}`): lists the generated NPC ids for kids.
2. With 1 argument (`{{mushymato.HaveMoreKids/KidNPCId:<kidId>}}`): return the generated NPC id for this kid, if applicable.

## ChildNames

This token is provided by content patcher and normally it lists all the player given names of the player's children. However, due to technical reasons this will actually give the internal kid id for any HMK custom kid.

This can be used to your advantage in that `{{ChildNames|valueAt=0}}` will resolve to the internal kid id of the first kid, such that `{{mushymato.HaveMoreKids/KidNPCId:{{ChildNames|valueAt=0}}}}` will yield the internal NPC id of the first child of the player (if they have one).
