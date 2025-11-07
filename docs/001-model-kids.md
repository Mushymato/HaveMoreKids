# Data Asset: mushymato.HaveMoreKids/Kids

Kids metadata in `mushymato.HaveMoreKids/Kids` defines behavior around pregnancy/adoption. This is also how you can create a child version associated with an existing NPC. The key `mushymato.HaveMoreKids/Kids` is the kid id, and should match an entry in `mushymato.HaveMoreKids/ChildData`.

## Structure

| Field | Type | Default | Notes |
| ----- | ---- | ------- | ----- |
| `Parent` | string | _null_ | This is the NPC parent of the kid. If this is set, this kid may appear via night time pregnancy + new child event as long as you are married to that NPC. |
| `Shared` | bool | `false` | When `Parent` is null and this field is true, any spouse can have this kid, including single parent adoption. |
| `AdoptedFromNPC` | string | _null_ | The internal id of the NPC to adopt as a Child. |
| `DefaultEnabled` | bool | `true` | Controls whether this kid is enabled by default in config menu, player can enable/disable the kid there. |
| `Condition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | Controls whether this kid is available from content pack side via GSQ, players cannot affect this check. |
| `IsNPCTodayCondition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | Controls whether the kid will become a full NPC. When this is null/FALSE, the feature is completely disabled for this kid, otherwise the GSQ is evaluated each morning to determine whether the child will "go outside" that day. |
| `Twin` | string | _null_ | The kid id of the twin who will be born during the same new child night event, as long as there's enough cribs. |
| `TwinCondition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | A condition on whether the twin should be born. |
| `TwinMessage` | string | _null_ | A special message to show when a twin is born. |
| `BirthOrAdoptMessage` | string | _null_ | A special message to show for birth/adoption of this kid, accepts tokenized strings. Will be spoken by the parent NPC if kid is already picked ahead of night event via adoption registry or action. |
| `CanAdoptFromAdoptionRegistry` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | If true, this child can be adopted from the adoption registry at Harvey's Clinic. |
| `RoamOnFarmCondition` | string ([Game State Query](https://stardewvalleywiki.com/Modding:Game_state_queries)) | _null_ | If true and the config `Toddlers Roam on Farm` is enabled, the kid will go out to the farm that day. Checked once at 0610 each day. |
| `DialogueSheetName` | string | _null_ | If set, this will be the dialogue asset name used for the Child. The final asset name will be `Characters/Dialogue/<DialogueSheetName OR KidId>`. |

## Samples (Content Patcher)

```json
{
  "Action": "EditData",
  "Target": "mushymato.HaveMoreKids/Kids",
  "Entries": {
    "{{ModId}}_CustomKid1": {
      // At least one of Parent and Shared are required if the kid should be obtainable by night event
      // Otherwise, they can only be obtained in adoption registry (if CanAdoptFromAdoptionRegistry resolves to true) or via trigger action
      "Parent": "<internal id of the NPC parent>",
      "Shared": <true if kid not limited to specific NPC>,
      // If AdoptedFromNPC is specified, Parent and Shared are ignored
      "AdoptedFromNPC": "<internal id of NPC to adopt>",
      // Optional fields
      "Condition": "<game state query for kid availability>",
      "DefaultEnabled": <true if kid should be checked by default in GMCM>,
      "Twin": "<a different kid id>",
      "TwinCondition": "<game state query>",
      "TwinMessage": "<special twin message to show>",
      "BirthOrAdoptMessage": "<special birth or adoption message>",
      "CanAdoptFromAdoptionRegistry": "<game state query>",
      "RoamOnFarmCondition": "<game state query>",
      "DialogueSheetName": "<alternate dialogue sheet name>",
    }
  },
},
```
