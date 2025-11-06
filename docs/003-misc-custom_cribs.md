# Misc: Custom Crib Furniture

By default, HMK provides a furniture custom crib that can be purchased from the Carpenter shop. Having these placed in the farmhouse allows the player to have more kids at once.

Content packs can add more cribs by creating a furniture that:
- Has bounding box `3 2`
- Has context tag `hmk_crib`

The texture has a special format that is shaped just like the crib on `Maps/farmhouse_tiles`:

[Crib texture](../ContentPacks/[CP]%20HMK%20Example/assets/crib.png)

The left side is the bottom layer that draws beneath the kid, and the right side draws above the crib. Areas that are black will not be drawn.

See [example here](../ContentPacks/[CP]%20HMK%20Example/data/crib.json).

## Mechanics

1. While a crib furniture is occupied by a kid that hasn't become a toddler yet, player cannot move the crib.

2. Extra cribs are required for twins, player must have 2 free cribs to activate a twin event.
