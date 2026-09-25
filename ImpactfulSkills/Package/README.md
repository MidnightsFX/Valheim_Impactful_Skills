# ImpactfulSkills
This mod aims to make progression in Valheim, especially skill related progression feel impactful and meaningful. 

In the base game, many skills have a significant impact on how powerful your character is, but that is often not felt due to the slow progression and no milestones that provide feedback.

This mod aims to fix that by providing additional functionality, benefits, or bonuses that occur as you level up.

## Reporting issues or feedback
Got a bug to report or just want to chat about the mod? Drop by the discord or github.

[![discord logo](https://i.imgur.com/uE6umQE.png)](https://discord.gg/Dmr9PQTy9m)
[![github logo](https://i.imgur.com/lvbP5OF.png)](https://github.com/MidnightsFX/Valheim_Impactful_Skills)

## Skills and Effects

All of the below effects are **configurable**! Want it to be more powerful or less powerful? Its easy to modify through the config file or an in-game config editor!

### Woodcutting

Woodcutting now improves the amount of wood you get from trees and bushes. Along with the amount of seeds, and resin you get.

Woodcutting also gives you increased _chop_ damage, which makes it faster to cut down trees.

| Woodcutting 0 | Woodcutting 50 | Woodcutting 100 |
| ------------- | ------------- | ------------- |
| ![wood_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Woodcutting_0.gif?raw=true) | ![wood_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Woodcutting_50.gif?raw=true) | ![wood_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Woodcutting_100.gif?raw=true) |


### Mining (Pickaxes)

Pickaxes now improves the amount of ore and stone you get from mining things. Along with providing an AOE mining effect (which strengthens as you level up) when you get to the requisite level (default 50).


| Pickaxes 0 | Pickaxes 50 | Pickaxes 100 |
| ------------- | ------------- | ------------- |
| ![stone_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/pickaxe_0.gif?raw=true) | ![stone_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/pickaxe_50.gif?raw=true) | ![stone_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/pickaxe_100.gif?raw=true) |


### Gathering (Farming)

Gathering increases the bonus number of items you can harvest from any plant. This is partly influenced by luck, and partly influenced by your level.
Additionally at a moderate level of gathering you gain AOE harvesting (default 25) which increases in range with your skill. Gathering skill also increases the range at which you can harvest with the scythe, reaching over 3x as far as AOE harvesting by hand at level 100 (configurable with ScytheHarvestRangeMultiplier).
Multi-planting unlocks at the same level (default 25), and the size of the planting grid grows in steps as your skill rises.
Farming also pays for the cultivator: planting costs full stamina at Farming 0 and nothing at all at Farming 100, and every plant in a grid costs the same as one placed by hand (configurable with PlantingCostStaminaReduction).
At a higher level (default 50) your plants stop caring about the biome they are in, so crops you tend can grow anywhere you can cultivate ground - including the cold of the Mountains and Deep North and the heat of the Ashlands.

| Farming 0 | Farming 50 | Farming 100 |
| ------------- | ------------- | ------------- |
| ![gather_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/farming_0.gif?raw=true) | ![gather_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/farming_50.gif?raw=true) | ![gather_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/farming_100.gif?raw=true) |

### Weapon Skills

All weapon skills now give a corresponding reduction to their stamina cost that is configurable (default 50% at level 100).
Vanilla provides a bonus of 33% at level 100, which is somewhat hidden. This makes it configurable and visibile on item tooltips.
Parrying provides a tiny bit of bonus XP.

| WeaponSkill 0 | WeaponSkill 50 | WeaponSkill 100 |
| ------------- | ------------- | ------------- |
| ![weapon_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sword_0.png?raw=true) | ![weapon_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sword_50.png?raw=true) | ![weapon_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sword_100.png?raw=true) |


### Sneaking

Increases speed based on your sneaking skill.
- Level 50 (configurable) of sneaking starts applying a reduction to your noise generated (similar to how the troll armor works).

| Sneak 0 | Sneak 50 | Sneak 100 |
| ------------- | ------------- | ------------- |
| ![sneak_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sneak_0.gif?raw=true) | ![sneak_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sneak_50.gif?raw=true) | ![sneak_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sneak_100.gif?raw=true) |


### Run

The run skill now increases your movement speed when running. Instead of only reducing stamina cost to run.

| Run 0 | Run 50 | Run 100 |
| ------------- | ------------- | ------------- |
| ![run_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Run_0.gif?raw=true) | ![run_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Run_50.gif?raw=true) | ![run_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Run_100.gif?raw=true) |


### Voyager (new skill)

Voyager increases your potential speed when sailing in a boat. Along with increasing your ability to handle close hauling, when sailing almost against the wind.
Provides a bonus to your rowing speed that scales with player skill also.

- Innate - Multiple players on a boat each provide a small bonus to boat speed (scales with everyones skill levels)
- 25 - Paddle speed increased (scales with level)
- 35 - Damage done to boat reduced (scales with level)
- 50 - Reduces penalty from sailing almost against the wind (scales with level)
- 75 - Boat you are aboard does not recieve damage from impact

| Voyager 0 | Voyager 50 | Voyager 100 |
| ------------- | ------------- | ------------- |
| ![voyager_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Voyager_0.gif?raw=true) | ![voyager_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Voyager_50.gif?raw=true) | ![voyager_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Voyager_100.gif?raw=true) |


### Jump
Jumping now increases your jump height and distance (configurable).
Jumping also increases the distance you can fall before taking fall damage.
Jump also reduces the damage you take from falling.

| Jump 0 | Jump 50 | Jump 100 |
| ------------- | ------------- | ------------- |
| ![jump_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Jump_0.gif?raw=true) | ![jump_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Jump_50.gif?raw=true) | ![voyager_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Jump_100.gif?raw=true) |


### hauling (new skill)

Hauling provides a way to gain innate carry weight, and move large amounts of goods easier.

Hauling is trained by moving goods around: pulling a loaded cart, or covering ground on foot while your inventory is heavily loaded (by default, carrying at least 275 weight). Walking counts just as much as running, and the heavier you are the faster it trains - including when you are over your carry limit, which trains hauling faster than anything else.

| Hauling 0 | Hauling 50 | Hauling 100 |
| ------------- | ------------- | ------------- |
| ![hauling_0](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Hauling_0.gif?raw=true) | ![hauling_50](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Hauling_50.gif?raw=true) | ![hauling_100](https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Hauling_100.gif?raw=true) |


### Cooking

Cooking now causes eaten food to decay slower, allowing it to provide its full bonus for longer.

Cooking several foods in a row builds a streak. Every food you cook adds 10% more cooking XP to the next one, up to double XP, so settling in for a real cooking session is worth far more than making one thing whenever you happen to walk past the cauldron. Crafting at the cauldron counts each item of a multicraft, and taking cooked food off a cooking station or oven counts too. The streak only ends after two minutes without cooking - gathering more ingredients or crafting something else in between does not break it.

Eating trains cooking as well. Every meal grants a little cooking XP based on the health, stamina and eitr it provides, so a hearty meal teaches more than a handful of berries.

### Fishing

Catching a big fish is finally worth something

Now the quality of the fish carries through to the recipe. A quality 5 anglerfish makes 5 fish wraps instead of 1, and the fish based mead bases scale the same way.

Crafting also no longer raids your best catch: recipes now always spend the lowest quality fish that can cover the cost, so a rare fish is only used when it is the only one you are carrying. The crafting panel shows what you will actually get before you commit to the craft.

Recipes that turn fish into gear reward you differently - see Crafting below.

### Crafting

A handful of items carry a quality level that has nothing to do with upgrades: every fish has one, and so does every base Ashlands weapon you have poured resources into. Spend one of those on a recipe that produces equipment and the quality now carries through to what you make, instead of being thrown away.

The quality you get is the average quality of the ingredients you spent, rounded down, and it still respects your crafting station - a level 1 workbench cannot turn out level 4 gear no matter how good your fish were. Twelve prize fish make an upgraded fishing hat; twelve scraps make the same hat the base game gives you. Infusing an Ashlands weapon carries its stars over rather than resetting it to one, which in the base game quietly destroys every upgrade you had put into it.

The crafting panel shows the quality you are about to get before you press craft, and crafting always spends the lowest quality ingredient that covers the recipe, so your best one is only ever used when it is the only one you have.

### Forging (new skill)

Forging improves your crafting stations and your odds at the Forge of Potential, and at higher levels lets you craft equipment that is better than what anyone else can make.

Forging is trained by crafting at the workbench, forge, black forge and galdr table, with extra XP for crafting and upgrading weapons, armor and shields, and for every refinement attempt at the Forge of Potential (more when it succeeds).

- Innate - Better odds at the Forge of Potential (scales with level). At level 100 a refinement succeeds 85% of the time instead of 65%, a failed refinement lowers the item's level instead of destroying it, and an item that does break returns 70% of its materials instead of 35%. The odds are shown when you select an item at the forge.
- 25 - When you use a forge, or workbench it is considered 1 level higher.
- 50 - When you use a black forge, or galdur table it is considered 1 level higher.
- 75 - Masterwork: weapons you craft or upgrade deal 10% more damage, armor gives 10% more armor and shields 10% more block power
- 90 - Lightweight: weapons, armor and shields you craft or upgrade weigh 90% less, and their movement speed penalty is halved. Equipment without a movement penalty gets another 5% damage, armor or block power instead

Masterwork and lightweight are part of the item. They show in its tooltip, stay with it when it is stored, traded or upgraded by anyone, and still apply if your forging skill drops. Which stations get a level, and every bonus and level above, can be changed in the config.

### Blood Magic

Blood magic now gives XP for the shield for damage taken, in addition to 1 xp when the shield is broken.


### Animal Whisper (new skill)

Taming reduces the amount of time it takes to tame creatures. It also multiplies the loot you get from slaughtering tamed creatures, removing the need for massive creature farms. 
The bonus scales off what the animal would actually have dropped, so it accounts for its star level, the world resource rate, and any loot changes from other mods.
Breeding gives XP


### Knowledge Sharing

Expertise in one area of knowledge now allows you to gain skills faster in other areas. Being the best builder in the group no longer means you will never level your combat skills.
Changing your primary weapon type now takes considerably less time.


## Roadmap ideas
Please note these are not ordered or guaranteed to be implemented, but are ideas that are being considered.
I am also always happy to talk ideas and suggestions in the discord!
- Skill announcements, when gaining a skill level that gives you a specific bonus
- Animal whisper allows taming 1 creature permenantly for your character (lvl 100?)
- Localization for all languages

## Suggested Mods to pair with
- [Cartography Skill](https://thunderstore.io/c/valheim/p/Advize/CartographySkill/) - A way to increase your exploration radius
- [Deathlink](https://thunderstore.io/c/valheim/p/MidnightMods/Deathlink/) - Makes item loss, equipment loss and skill loss on death configurable
- [FortifySkillsRedux](https://thunderstore.io/c/valheim/p/Searica/FortifySkillsRedux/) - Provides a system to prevent skill degredation but still provides risk.
