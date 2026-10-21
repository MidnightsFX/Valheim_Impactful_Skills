ImpactfulSkills

This mod aims to make progression in Valheim, especially skill related progression feel impactful and meaningful.

In the base game, many skills have a significant impact on how powerful your character is, but that is often not felt due to the slow progression and no milestones that provide feedback.

This mod aims to fix that by providing additional functionality, benefits, or bonuses that occur as you level up.


[size=5][b]Reporting issues or feedback[/b][/size]
Got a bug to report or just want to chat about the mod? Drop by the discord or github.

[url=https://discord.gg/Dmr9PQTy9m][img]https://i.imgur.com/uE6umQE.png[/img][/url] [url=https://github.com/MidnightsFX/Valheim_Impactful_Skills][img]https://i.imgur.com/lvbP5OF.png[/img][/url]


[size=5][b]Skills and Effects[/b][/size]

All of the below effects are [b]configurable[/b]! Want it to be more powerful or less powerful? Its easy to modify through the config file or an in-game config editor!

When a skill level-up unlocks one of these features, its name is shown in the middle of the screen, eg "Rockbreaker unlocked!". Server admins can turn this off for everyone with EnableUnlockMessages, and each player can turn it off for themselves with ShowUnlockMessages.

The compendium has a Skill Perks page that lists every skill this mod changes, your level in it, and each of its perks with a short explanation. Perks you have not reached yet are greyed out with the level they unlock at, and anything the server has turned off is left out.

AOE harvesting, AOE mining, rock breaker and multi-planting can all be switched off and on again at any time with the AOE toggle key (N by default).

Bonuses stop growing at their level 100 values, even when gear, food or other mods boost a skill past 100. Server admins can let a skill's bonuses keep growing with its BonusesPastLevel100 setting, eg MiningBonusesPastLevel100.


[size=4][b]Woodcutting[/b][/size]

Woodcutting now improves the amount of wood you get from trees and bushes. Along with the amount of seeds, and resin you get.

Woodcutting also gives you increased [i]chop[/i] damage, which makes it faster to cut down trees.

[b]Woodcutting 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Woodcutting_0.gif?raw=true[/img]
[b]Woodcutting 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Woodcutting_50.gif?raw=true[/img]
[b]Woodcutting 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Woodcutting_100.gif?raw=true[/img]


[size=4][b]Mining (Pickaxes)[/b][/size]

Pickaxes now improves the amount of ore and stone you get from mining things. Along with providing an AOE mining effect (which strengthens as you level up) when you get to the requisite level (default 50).

[list]
[*]Innate - Pickaxe damage grows with your skill, so rocks and ore break faster.[/*]
[*]Innate - Mining a Leviathan down no longer deletes it out from under you, dropping you into the water or lava. It dives and sinks away instead (ProtectLeviathansWhenMined).[/*]
[*]25 - Critical hits: each swing has a 10% chance to deal triple damage (configurable with ChanceForMiningCritHit and CriticalHitDmgMult).[/*]
[*]75 - Rock breaker: each swing has a small chance, up to 5% at level 100, to break the whole vein at once (configurable with RockBreakerMaxChance).[/*]
[/list]

[b]Pickaxes 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/pickaxe_0.gif?raw=true[/img]
[b]Pickaxes 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/pickaxe_50.gif?raw=true[/img]
[b]Pickaxes 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/pickaxe_100.gif?raw=true[/img]


[size=4][b]Gathering (Farming)[/b][/size]

Gathering increases the bonus number of items you can harvest from any plant. This is partly influenced by luck, and partly influenced by your level.
Plants from other mods count too: anything grown from a sapling, its wild counterpart, and wild pickables that drop food are recognised automatically, and server admins can add anything else with GatheringAllowedItems.
From an early level (default 12) a healthy plant shows roughly how long it has left to grow when you look at it. If another mod already shows grow times, each player can turn this off for themselves with EnableFarmingGrowthTimeDisplay.
Additionally at a moderate level of gathering you gain AOE harvesting (default 25) which increases in range with your skill. Gathering skill also increases the range at which you can harvest with the scythe, reaching over 3x as far as AOE harvesting by hand at level 100 (configurable with ScytheHarvestRangeMultiplier).
Multi-planting unlocks at the same level (default 25), and the size of the planting grid grows in steps as your skill rises.
Farming also pays for the cultivator: planting costs full stamina at Farming 0 and nothing at all at Farming 100, and every plant in a grid costs the same as one placed by hand (configurable with PlantingCostStaminaReduction).
At a higher level (default 50) your plants stop caring about the biome they are in, so crops you tend can grow anywhere you can cultivate ground - including the cold of the Mountains and Deep North and the heat of the Ashlands.

[b]Farming 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/farming_0.gif?raw=true[/img]
[b]Farming 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/farming_50.gif?raw=true[/img]
[b]Farming 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/farming_100.gif?raw=true[/img]


[size=4][b]Weapon Skills[/b][/size]

All weapon skills now give a corresponding reduction to their stamina cost that is configurable (default 50% at level 100).
Vanilla provides a bonus of 33% at level 100, which is somewhat hidden. This makes it configurable and visibile on item tooltips.
Parrying provides a tiny bit of bonus XP.
Crossbow hits grant more XP the further away the target is, up to 500% bonus XP at 150m (configurable with CrossbowDistanceXPMaxBonus and CrossbowDistanceXPMaxDistance).
Bow hits also grant more XP for distance, on a sharper curve: nothing inside 15m, then climbing steeply to 800% bonus XP at 100m, since long arrow shots are harder to land (configurable with BowDistanceXPMinDistance, BowDistanceXPMaxDistance, BowDistanceXPMaxBonus and BowDistanceXPCurve).
At crossbow level 50 (configurable), sprinting, jumping and dodging pause your reload instead of restarting it.
Elemental and blood magic weapons that reload get the same pause at level 50 in their magic skill (configurable with EnableMagicReloadPause and MagicReloadPauseLevel).

[list]
[*]Innate - Holding a bow drawn drains less stamina, 50% less at level 100 instead of the game's own 33% (configurable with WeaponSkillBowDrawStaminaCostReduction).[/*]
[*]45 - Quick equip: equipping and putting away an item gets faster with its skill, up to 3x as fast at level 100. This covers every weapon, shields (Blocking), pickaxes and the fishing rod (configurable with WeaponSkillEquipRequiredLevel and WeaponSkillEquipSpeedFactor).[/*]
[/list]

[b]WeaponSkill 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sword_0.png?raw=true[/img]
[b]WeaponSkill 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sword_50.png?raw=true[/img]
[b]WeaponSkill 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sword_100.png?raw=true[/img]


[size=4][b]Blocking[/b][/size]

Blocking makes your shield stronger and blocking less tiring.

[list]
[*]25 - Block power grows with your skill, up to 75% more at level 100 instead of the game's own 50% (configurable with BlockPowerRequiredLevel and BlockPowerFactor).[/*]
[*]40 - Each block gives back part of the stamina it cost, up to half of it at level 100 (configurable with BlockStaminaGainRequiredLevel and BlockStaminaGainFactor). Parries can give stamina back too if the server turns on EnableParryStaminaGain.[/*]
[/list]


[size=4][b]Sneaking[/b][/size]

Increases speed based on your sneaking skill.
[list]
[*]Level 25 (configurable) of sneaking increases the damage of sneak attacks, melee and ranged alike, up to triple the weapon's normal sneak attack bonus at level 100 (configurable with SneakBackstabBonusFactor).[/*]
[*]Level 50 (configurable) of sneaking starts applying a reduction to your noise generated (similar to how the troll armor works).[/*]
[/list]

[b]Sneak 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sneak_0.gif?raw=true[/img]
[b]Sneak 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sneak_50.gif?raw=true[/img]
[b]Sneak 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/sneak_100.gif?raw=true[/img]


[size=4][b]Run[/b][/size]

The run skill now increases your movement speed when running. Instead of only reducing stamina cost to run.

[b]Run 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Run_0.gif?raw=true[/img]
[b]Run 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Run_50.gif?raw=true[/img]
[b]Run 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Run_100.gif?raw=true[/img]


[size=4][b]Swimming[/b][/size]

[list]
[*]25 - Swimming speed grows with your skill (configurable with SwimSpeedRequiredLevel and SwimmingSpeedFactor).[/*]
[*]50 - Swimming uses less stamina, up to 50% less at level 100 (configurable with SwimStaminaReductionLevel and SwimStaminaCostReductionFactor).[/*]
[/list]


[size=4][b]Voyager (new skill)[/b][/size]

Voyager increases your potential speed when sailing in a boat. Along with increasing your ability to handle close hauling, when sailing almost against the wind.
Provides a bonus to your rowing speed that scales with player skill also.

[list]
[*]Innate - Multiple players on a boat each provide a small bonus to boat speed (scales with everyones skill levels)[/*]
[*]Innate - You explore more of the map around you while sailing, up to 4x the radius at level 100 (configurable with VoyagerIncreaseExplorationRadius)[/*]
[*]25 - Paddle speed increased (scales with level)[/*]
[*]35 - Damage done to boat reduced (scales with level)[/*]
[*]50 - Reduces penalty from sailing almost against the wind (scales with level). The ship HUD's wind ring shades in the angles you have won back[/*]
[*]75 - Boat you are aboard does not recieve damage from impact[/*]
[/list]

[b]Voyager 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Voyager_0.gif?raw=true[/img]
[b]Voyager 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Voyager_50.gif?raw=true[/img]
[b]Voyager 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Voyager_100.gif?raw=true[/img]


[size=4][b]Jump[/b][/size]
Jumping now increases your jump height and distance (configurable).
Jumping also increases the distance you can fall before taking fall damage.
Jump also reduces the damage you take from falling.

[b]Jump 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Jump_0.gif?raw=true[/img]
[b]Jump 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Jump_50.gif?raw=true[/img]
[b]Jump 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Jump_100.gif?raw=true[/img]


[size=4][b]Hauling (new skill)[/b][/size]

Hauling provides a way to gain innate carry weight, and move large amounts of goods easier.

[list]
[*]Innate - Carry weight grows with your skill, up to 50 extra at level 100 (configurable with HaulingMaxWeightBonus).[/*]
[*]Innate - Carts you pull get lighter, up to 80% lighter at level 100, so a loaded cart is far easier to move (configurable with HaulingCartMassReduction).[/*]
[/list]

Hauling is trained by moving goods around: pulling a loaded cart, or covering ground on foot while your inventory is heavily loaded (by default, carrying at least 275 weight). Walking counts just as much as running, and the heavier you are the faster it trains - including when you are over your carry limit, which trains hauling faster than anything else.

[b]Hauling 0[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Hauling_0.gif?raw=true[/img]
[b]Hauling 50[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Hauling_50.gif?raw=true[/img]
[b]Hauling 100[/b]
[img]https://github.com/MidnightsFX/Valheim_Impactful_Skills/blob/master/ImpactfulSkills/Art/Hauling_100.gif?raw=true[/img]


[size=4][b]Cooking[/b][/size]

Cooking now causes eaten food to decay slower, allowing it to provide its full bonus for longer.

Cooking several foods in a row builds a streak. Every food you cook adds 10% more cooking XP to the next one, up to double XP, so settling in for a real cooking session is worth far more than making one thing whenever you happen to walk past the cauldron. Crafting at the cauldron counts each item of a multicraft, and taking cooked food off a cooking station or oven counts too. The streak only ends after two minutes without cooking - gathering more ingredients or crafting something else in between does not break it.

Eating trains cooking as well. Every meal grants a little cooking XP based on the health, stamina and eitr it provides, so a hearty meal teaches more than a handful of berries.

[list]
[*]10 - Cooking at the cauldron has a chance to make extra food, up to 40% at level 100, with up to 3 bonus items from a single craft (configurable with RequiredLevelForBonusCookingItems, ChanceForCookingBonusItems and CookingBonusItemMaxAmount).[/*]
[/list]


[size=4][b]Fishing[/b][/size]

Catching a big fish is finally worth something

Now the quality of the fish carries through to the recipe. A quality 5 anglerfish makes 5 fish wraps instead of 1, and the fish based mead bases scale the same way.

Crafting also no longer raids your best catch: recipes now always spend the lowest quality fish that can cover the cost, so a rare fish is only used when it is the only one you are carrying. The crafting panel shows what you will actually get before you commit to the craft.

Recipes that turn fish into gear reward you differently - see Crafting below.


[size=4][b]Crafting[/b][/size]

The crafting skill now pays off well beyond the base game's small bonus.

[list]
[*]25 - Weapons, tools and shields sometimes use no durability when you use them, up to a 25% chance at level 100 (configurable with DurabilitySaveLevel and ChanceForDurabilityLossPrevention).[/*]
[*]50 - Crafting has a chance to make bonus items, up to 30% at level 100, with up to 3 extra from a single craft. Recipes that make several items at once, like arrows or nails, give a quarter of a batch per bonus (configurable with CraftingBonusCraftsLevel, CraftingBonusChance and CraftingMaxBonus).[/*]
[*]75 - Crafting and upgrading have a chance to give back some of the materials, up to a 15% chance to get back 30% of each ingredient at level 100 (configurable with CraftingMaterialReturnsLevel, ChanceForMaterialReturn and MaxCraftingMaterialReturnPercent).[/*]
[/list]

A handful of items carry a quality level that has nothing to do with upgrades: every fish has one, and so does every base Ashlands weapon you have poured resources into. Spend one of those on a recipe that produces equipment and the quality now carries through to what you make, instead of being thrown away.

The quality you get is the average quality of the ingredients you spent, rounded down, and it still respects your crafting station - a level 1 workbench cannot turn out level 4 gear no matter how good your fish were. Twelve prize fish make an upgraded fishing hat; twelve scraps make the same hat the base game gives you. Infusing an Ashlands weapon carries its stars over rather than resetting it to one, which in the base game quietly destroys every upgrade you had put into it.

The crafting panel shows the quality you are about to get before you press craft, and crafting always spends the lowest quality ingredient that covers the recipe, so your best one is only ever used when it is the only one you have.


[size=4][b]Forging (new skill)[/b][/size]

Forging improves your crafting stations and your odds at the Forge of Potential, and at higher levels lets you craft equipment that is better than what anyone else can make.

Forging is trained by crafting at the workbench, forge, black forge and galdr table, with extra XP for crafting and upgrading weapons, armor and shields, and for every refinement attempt at the Forge of Potential (more when it succeeds). Wooden training weapons and Meadows gear give no forging XP, and anything made without metal gives half. Both can be changed in the config.

[list]
[*]Innate - Better odds at the Forge of Potential (scales with level). At level 100 a refinement succeeds 85% of the time instead of 65%, a failed refinement lowers the item's level instead of destroying it, and an item that does break returns 70% of its materials instead of 35%. Refining past level 10 gets harder with every level: lower odds, fewer materials back, and failures increasingly destroy the item. An item above its normal max level returns materials as if it were at that max level. The odds are shown when you select an item at the forge.[/*]
[*]25 - When you use a forge, or workbench it is considered 1 level higher.[/*]
[*]50 - When you use a black forge, or galdur table it is considered 1 level higher.[/*]
[*]75 - Masterwork: weapons you craft or upgrade deal 10% more damage, armor gives 10% more armor and shields 10% more block power[/*]
[*]90 - Lightweight: weapons, armor and shields you craft or upgrade weigh 90% less, and their movement speed penalty is halved. Equipment without a movement penalty gets another 5% damage, armor or block power instead[/*]
[/list]

Masterwork and lightweight are part of the item. They show in its tooltip, stay with it when it is stored, traded or upgraded by anyone, and still apply if your forging skill drops. Which stations get a level, and every bonus and level above, can be changed in the config.

Press the AOE toggle key (N by default) in the crafting window to choose whether what you make comes out lightweight. It is back on each time you log in.

The game now decides each refinement's outcome in advance, separately for every character, item and level. This mod rolls each refinement when you attempt it instead, as the game used to. Set RollRefinementsWhenAttempted to false to use the game's own rolls.

With Reforged Potential installed, forging's odds replace its Upgrade Chance and Break Chance settings. Set EnableRefinementBonus to false to use Reforged Potential's odds instead, along with its own level limits rather than forging's refinement decay.


[size=4][b]Blood Magic[/b][/size]

Blood magic now gives XP for the shield for damage taken, in addition to 1 xp when the shield is broken.

Once your blood magic reaches BloodMagicShieldResetLevel (50 by default), recasting the shield on someone who already has it also restores how much damage it can absorb, not just its duration. Set EnableBloodMagicShieldReset to false to turn this off.


[size=4][b]Elemental Magic[/b][/size]

Once your elemental magic reaches ElementalMagicSpreadReductionLevel (25 by default), projectiles from elemental magic weapons spread less the higher your skill, up to half as much at level 100 (configurable with ElementalMagicProjectileSpreadReduction). This is most noticeable on the Staff of Frost, the one magic weapon that scatters its shots. Set EnableElementalMagicSpreadReduction to false to turn this off.


[size=4][b]Animal Whisper (new skill)[/b][/size]

Taming reduces the amount of time it takes to tame creatures, up to 6x faster at level 100 (AnimalTamingSpeedFactor). In multiplayer it is the player closest to the creature whose skill speeds it up and who gains the taming XP. It also multiplies the loot you get from slaughtering tamed creatures, removing the need for massive creature farms.
The bonus scales off what the animal would actually have dropped, so it accounts for its star level, the world resource rate, and any loot changes from other mods.
Breeding gives XP

While you are taming a creature, its taming progress is shown just above your stamina bar, so you can follow it from a distance. It shows the creature you are looking at, or else the closest one being tamed within TamingProgressDisplayRange (40m by default). Each player can turn this off with EnableTamingProgressDisplay.

You can hand-feed a creature you are taming: look at it and use a food it eats from your hotbar. Each feeding feeds it and adds 2% of its taming at level 0, up to 10% at level 100 (TamingHandFeedMinPercent and TamingHandFeedMaxPercent). A creature can be hand-fed once every 30 seconds by anyone (TamingHandFeedCooldown, which does not change with skill), and a frightened creature refuses the food.

From level 15 (TamingCalmLevel), a creature being tamed is no longer frightened by you while it has food it eats nearby or is still fed, so you can walk up and hand-feed it. Attacking it, or a projectile landing near it, ends this for 60 seconds (TamingCalmAttackedCooldown). Each player's own level counts, so the creature still reacts to less skilled players around it.

From level 75, a creature you tame or breed has a chance to gain an extra star (laid eggs carry it to the creature that hatches). The chance grows with your skill, and in multiplayer it is the player closest to the creature whose skill counts.
Creatures never go past 2 stars this way, unless [url=https://thunderstore.io/c/valheim/p/MidnightMods/StarLevelSystem/]Star Level System[/url] is installed.

Animal handling looks after your bees too:

[list]
[*]Innate - Harvesting honey gives animal handling XP (configurable with BeeHarvestXP).[/*]
[*]15 - Beehives give more honey when you harvest them, up to double at level 100 (configurable with BetterBeesLevel and BeeHoneyOutputIncreaseBySkill).[/*]
[*]25 - Beehives make honey in any biome once you have built or looked at them (configurable with BeeBiomeUnrestrictedLevel).[/*]
[/list]


[size=4][b]Knowledge Sharing[/b][/size]

Expertise in one area of knowledge now allows you to gain skills faster in other areas. Being the best builder in the group no longer means you will never level your combat skills.
Changing your primary weapon type now takes considerably less time.

[list]
[*]Every skill, including skills added by other mods, gets its own XP gain rate setting in the SkillRates section, so server admins can speed up or slow down any skill.[/*]
[/list]


[size=5][b]Console commands[/b][/size]

The new skills work with [font=Courier New]raiseskill[/font] and [font=Courier New]resetskill[/font]. The console splits what you type on spaces, so write a skill name that has a space in it with an underscore instead, eg [font=Courier New]raiseskill Animal_Handling 50[/font]. Tab completion offers the names in this form.
The name of the skill in your own language works, and so do [font=Courier New]AnimalHandling[/font], [font=Courier New]Voyager[/font], [font=Courier New]Hauling[/font] and [font=Courier New]Forging[/font] whatever language the game is in.

The same names work in [url=https://thunderstore.io/c/valheim/p/M2Valheim/SkillsReworked/]SkillsReworked[/url]'s attribute skill lists, eg [font=Courier New]Crafting,Forging,AnimalHandling[/font].


[size=5][b]Roadmap ideas[/b][/size]
Please note these are not ordered or guaranteed to be implemented, but are ideas that are being considered.
I am also always happy to talk ideas and suggestions in the discord!
[list]
[*]Nothing is planned right now, suggestions are welcome![/*]
[/list]


[size=5][b]Suggested Mods to pair with[/b][/size]
[list]
[*][url=https://thunderstore.io/c/valheim/p/Advize/CartographySkill/]Cartography Skill[/url] - A way to increase your exploration radius[/*]
[*][url=https://thunderstore.io/c/valheim/p/MidnightMods/Deathlink/]Deathlink[/url] - Makes item loss, equipment loss and skill loss on death configurable[/*]
[*][url=https://thunderstore.io/c/valheim/p/Searica/FortifySkillsRedux/]FortifySkillsRedux[/url] - Provides a system to prevent skill degredation but still provides risk.[/*]
[/list]

[line]

If you enjoy my mods donations are appreciated. My mods will never be paid and donations are not required.

[url=https://ko-fi.com/midnightsfx][img]https://raster.shields.io/badge/Ko--fi-Support%20me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white[/img][/url]
