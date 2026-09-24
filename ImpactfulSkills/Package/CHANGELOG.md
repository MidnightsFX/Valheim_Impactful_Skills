**0.19.2**
---
```
- Fixes resource consumption for multiplant not happening with craft from container mods
```

**0.19.1**
---
```
- Update localization for all langues
```

**0.19.0**
---
```
- Fix for potential NPE when mining large areas
- Forging (new skill)
	- Trained by crafting at the workbench, forge, black forge and galdr table, crafting and upgrading equipment, and refining at the Forge of Potential
	- Innate: better odds at the Forge of Potential (level 100: 85% success, failures lower the item's level instead of destroying it, 70% of materials back if it does break). The odds are now shown at the forge
	- 25: workbench and forge count as one level higher
	- 50: black forge and galdr table count as one level higher
	- 75: masterwork equipment, +10% damage, armor or block power
	- 90: lightweight equipment, 90% less weight and half the movement speed penalty (+5% damage, armor or block power when there is no penalty)
	- Masterwork and lightweight stay with the item, including through upgrades
```

**0.18.0**
---
```
- Farming now properly reduces the stamina cost of planting (vanilla reduced stamina costs, and now Impactful matches that)
	- Every plant in a multiplanted grid costs what placing that one plant by hand costs.
	- PlantingCostStaminaReduction now applies to single plants and cultivating ground too, not just the extra plants in a grid, and defaults to 1: planting costs full price at Farming 0 and nothing at Farming 100
- Improves compatibility with ZenBeehives
- Improves consistency of XP gains for the user when harvesting beehives in multiplayer
```

**0.17.1**
---
```
- Bump dependency versions again
```

**0.17.0**
---
```
- Improves XP grants for raising animals, ensures they are properly provided in the local area
- Fixed multiplanted barley and flax being planted too close together and dying before they matured
	- Grid spacing now accounts for the plant's own collider, not just its grow radius, which is what the game actually measures against. Barley and flax grids are wider; every other crop and all tree saplings keep the spacing they had
	- The minimum is enforced on top of FarmingMultiPlantBufferSpace, so existing configs get the fix without being edited
	- An existing field that was already planted too tightly will now show red cells rather than letting you feed more doomed crops into it
- Multiplanting no longer places crops where they would kill a neighbouring plant that has a larger grow radius, such as an ungrown tree sapling
- Vineberries now respect their spacing from other vines and their need for a wall to climb, instead of being placed where they could never grow
- Farming level 50 (FarmingBiomeUnrestrictedLevel) lets your plants grow in any biome, ignoring the biome they were planted in as well as the heat of the Ashlands and the cold of the Mountains and Deep North
	- Cultivated ground, sunlight and grow space are still required, so a crop can still be unhealthy for those reasons
	- Like beehives, the plant keeps the permission once a farmer of that level has tended it, so it survives even while only lower skilled players are around
	- Can be turned off with EnableFarmingBiomeUnrestricted
- AOE harvesting now unlocks at Farming 25 instead of 50, matching multi-planting (does not overwrite existing configs)
- The planting grid now grows in steps instead of one plant at a time (FarmingMultiplantCountIncrement, default 2), so it goes 1 -> 2 -> 4 -> 6 -> 8 as Farming rises and the count stays even for tidier rows
	- Set it to 1 for the old one-at-a-time progression
- Increased required Jotunn version
```

**0.16.2**
---
```
- Makes harvesting, pickables, luck rolls and planting all count towards player statistics
- Voyager impact resistance now only protects the ship you are aboard
```

**0.16.1**
---
```
- Improves quality crafting, and crafting that does not consume ingrediants
```

**0.16.0**
---
```
- Update for Valheim 1.0
- Reduced default gathering extra drops (does not overwrite existing configs)
```

**0.15.0**
---
```
- Animal handling bonus loot is now a multiplier on the loot the creature actually drops
	- The bonus now includes the animal's star level, the world resource rate, and any drops added or rescaled by other loot mods
- A bonus that works out to less than one item is now a chance at one item instead of being rounded away (AnimalHandlingFractionalDropsAsChance)
- How close you have to be to a tamed animal when it dies to earn the slaughter XP and the bonus loot is now configurable instead of a fixed 20 meters (AnimalHandlingLootRange)
- Cooking several foods in a row now builds a streak that grants bonus cooking XP. Each food cooked adds 10% to the XP of the next one, up to double XP (EnableCookingStreak, CookingStreakBonusPerFood, CookingStreakMaxBonus)
	- Crafting at the cauldron counts each item of a multicraft, and taking cooked food off a cooking station or oven counts too. Placing raw food on a station neither builds nor breaks the streak, and neither does collecting something you let burn
	- The streak only ends after two minutes without cooking (CookingStreakTimeout, 0 means it never ends)
	- A notification shows the streak bonus while it climbs, which each player can turn off for themselves (CookingStreakShowText)
- Eating food now grants a small amount of cooking XP, scaled by the total health, stamina and eitr the food provides, so a hearty meal teaches more than a handful of berries (EnableCookingEatXP, CookingEatXPPerFoodStat)
- Updated skill icons to be more vanilla style
```

**0.14.0**
---
```
- Recipes that consume something with a quality level and produce equipment now craft that equipment at a higher quality instead of producing more of it (ScaleCraftedEquipmentQuality, under Crafting)
	- The quality granted is the average quality of the ingredients spent, rounded down, and is still capped by what your crafting station could normally build - twelve good fish make an upgraded fishing hat, twelve poor ones do not
	- Infusing an Ashlands weapon now carries its star level over. Vanilla always handed back a quality 1 weapon, so infusing a fully upgraded one silently threw away three upgrade levels
	- The crafting panel shows the quality you will get before you commit to the craft
- Crafting now always spends the lowest quality ingredient that covers a recipe, for every item that carries a quality level rather than only for fish. Vanilla checks whether you can afford a recipe one quality tier at a time but then removes from any tier, so it could pick your best item purely by where it sat in your inventory
- Fixed the crafting panel showing the wrong amount for quality scaled recipes while multicrafting
```

**0.13.0**
---
```
- Recipes that take a whole fish now produce more the better the fish is. A quality 5 anglerfish makes 5 fish wraps instead of 1, and the same applies to the fish based mead bases (EnableQualityIngredientScaling, QualityIngredientOutputMultiplier)
	- Vanilla only ever paid out for fish quality when turning a fish into raw fish; every other fish recipe ignored it
- Fixes crafting spending a high quality fish when a lower quality one would have done. Vanilla checks whether you can afford a recipe one quality tier at a time, but then removes the items from any tier, so your best catch could be consumed for a quality 1 result. Crafting now always spends the lowest tier that covers the whole recipe, which also fixes the fishing hat quietly eating your rarest fish
- The crafting panel and recipe list now show the amount you will actually get before you craft it
- ScaleNonStackingCraftOutputs turns the bonus off for recipes whose result does not stack, such as the mead bases
- RestrictQualityScalingToFish keeps the bonus to fish only, which matters alongside mods that give quality levels to other materials
```

**0.12.0**
---
```
- Skill gain rate settings are now created for skills added by other mods, not just the vanilla ones. Skills added through Jotunn or SkillManager are detected automatically, and AdditionalSkillNames can be used to add any that are missed
- Skill gain rates can now be set below 1 to slow a skill down, or to 0 to stop it being gained at all
- Skill gain rates now apply to xp granted by mods that raise skills directly instead of through the player, which previously ignored the configured rate
- SharedKnowledgeIgnoreList now accepts skills added by other mods, and no longer requires that your character has already raised the skill for the name to be recognised
- The AOE toggle hotkey now also switches AOE mining and vein breaking on and off, not just area planting and harvesting, so a single node can be taken off a vein without shattering its neighbours. Mining damage, critical hits and bonus drops are unaffected while it is off (AOEToggleHotkey)
```

**0.11.3**
---
```
- Fixes Leviathans and Lava Leviathans being deleted instantly when their last ore node was mined out, which removed the body from under the player and drowned or incinerated them. They now start their dive and sink away instead, leaving time to collect drops and get clear (ProtectLeviathansWhenMined)
- Fixes AOE gathering and weapon equip speed bonuses activating one level later than their configured required level (GatheringRangeRequiredLevel, WeaponSkillEquipRequiredLevel)
- BetterBeesLevel now actually gates the beehive honey production bonus; previously the setting was shown but had no effect
```

**0.11.2**
---
```
- Adds configuration to allow scaling skill gain rate for all skills (default 1.0, 2.0 = double skill gain)
- Fixes a bug where crafting bonuses for specific crafting stations would use the wrong skill and hit a silent fallback for no bonuses, based on player skill levels
```

**0.11.1**
---
```
- Adds extra logging for plant luck rolls
- Bumps required jotunn version to current
```

**0.11.0**
---
```
- Makes the following configurations all client sided, allowing user adjustment on a server without having admin permissions (they are not synced to others on change either)
	- FarmingMultiPlantDistanceBufferModifier
	- FarmingMultiPlantBufferSpace
	- PlantingSnapDistance
	- FarmingSnapStyle
	- EnableSnappingToOtherPlants
- Overhauls plant grid snapping to fix several causes of unexpected snapping
- The grid no longer repositions itself away from your cursor
- With area planting toggled off, a single plant is now spaced and positioned as a single plant rather than as part of a full grid, and snaps to the nearest free spot
- Adds FarmingMultiPlantCenterRows and FarmingMultiPlantCenterColumns to choose whether the grid is centered on your cursor or grows out from it, per axis
- Multiplanted crops are now planted at the rotation shown in the preview, and each gets its own facing like vanilla planting (FarmingMultiPlantRandomRotation, disable for a uniform grid)
- The PreferOtherPlantGrid setting is now re-checked at runtime instead of only at startup
```

**0.10.0**
---
```
- Adds compatibility for SkillTree by M2
- Transpiler improvements to prevent errors with other commonly patched methods
```

**0.9.7**
---
```
- Adds full localization support for valheims supported languages
	- Localization can be edited in `Bepinex/config/ImpactfulSkills`, a restart is required for changes to take effect.
- Hauling movement XP is now granted from a single check (you can gain both at once)
	- Hauling XP from carrying goods no longer requires running, walking counts too.
	- Being over your maximum carry weight now grants hauling XP
	- HaulingMaxLoadRatio caps how much bonus XP an overloaded inventory can provide (defaults to 1.5x)
- HaulingCarryWeightXPMinWeight replaces HaulingCarryWeightXPThreshold, and is a flat inventory weight rather than a percentage (defaults to 275)
- HaulingCarryWeightXPMinDistance is now HaulingXPMinDistance, and applies to cart hauling as well. Set it to 0 to gain hauling XP without moving.
- HaulingCarryWeightXPInterval has been removed, HaulingXPCheckInterval replaces it (frequency of XP check, default 3s)
- Fixes farming multiplant requiring one extra level than defined
```

**0.9.6**
---
```
- Hauling now grants XP while travelling with a heavily loaded inventory, not just while pulling a cart.
	- Configurable via the new HaulingCarryWeightXP settings (defaults to 80% of your maximum carry weight)
	- HaulingCarryWeightXPInterval allows frequency tuning
	- HaulingCarryWeightXPMinDistance allows configuring required distance covered to provide XP
	- HaulingCarryWeightXPThreshold allows configuring the percentage of your carry weight that must be in use to gain XP
	- HaulingCarryWeightXPRate allows tuning specifically XP granted by moving with a heavily loaded inventory
```


**0.9.5**
---
```
- Improves plant grid snapping, new default grid snapping has stronger orientation snapping and will not form diagonals
- Planting grid orientation is now preserved between placements
```

**0.9.4**
---
```
- Fixes Woodcutting, Mining and AnimalWhisper to properly scale drops, resulting in a lower scaling of drops overall
```

**0.9.3**
---
```
- Fixes gathering causing an error when trying to gather from a prefab with a null drop (a pickable that has no pickable...)
- Fixes some mine rocks droping their loot at an incorrect location (center of object, not destroyed leaflet)
- Fixes a concurrent modification exception with mine rocks referencing an already destroyed vein
```

**0.9.2**
---
```
- Rebuild
```

**0.9.1**
---
```
- Add configurable keymap for toggling area planting/harvesting
	- UI Hotkey shown when cultivator is equipped
	- Can be activated any time, feedback message top left
- Improves consistency of XP gains for Voyager
	- Voyager could run into an issue where only one player on a boat would gain XP, that should be fixed
- Provides AnimalWhisper XP when nearby creatures breed
```

**0.9.0**
---
```
- Increases default Voyager skill gain rate by 50%
- Increases default Voyager sailing speed by 50% (now +150%)
- Adds Blocking skill perks (Thanks Leslie!)
	- lvl 25 - block power starts being increased based on skill level (75% vs vanilla 50% at lvl 100, configurable) eg 50% increase
	- lvl 40 - blocking returns stamina (50% of cost returned at level 100, configurable)
```

**0.8.2**
---
```
- Improves ship detection for exploration radius increase for Voyager
- Improves some plant grid edge cases where snapping would prefer to snap to a further away plant
```

**0.8.1**
---
```
- Plant Grid snapping reduced to cardinal directions only (orientation creation of farm still allows a larger degree of freedom)
- Added a toggle to disable the plant grid (sneak key)
- Increased default additional buffer space for plants to .2m (instead of .1m, please note you may need to delete or update your config)
```

**0.8.0**
---
```
- Adds mod detection and automatic deactivation of Impactful skills multiplanting with other grid-plant mods are available (configurable)
- Overhauls multi-plant system
	- Fixes duplicate planting of the first plant when multi-planting
	- Saves rotation of multi-planted entries
	- Allows better snapping to nearby plants
	- Allows snapping to other types of nearby plants
	- Added configuration to allow limiting the size of the planting grid (FarmingMultiplantColumnCount)
- Adds a safety reset when AOE harvest is interrupted (default 10s)
- Improves consistency of AOE harvest reset
```

**0.7.2**
---
```
- Fixes crafting bonuses for stacked item not being applied
- Fixes error while trying to upgrade extremely low cost items
- Enabled bonus item crafting for whole item pieces (such as armor etc, same as vanilla)
- Added missing localization for crafting materials refund
```

**0.7.1**
---
```
- Cooking bonus items now apply to mead bases
- Added seperate configs for Cooking
	- Chance of bonus cooking items
	- Max number of bonus items possible
	- Required level to activate bonus item chance
- Fixes for a potential race condition when mining is exploding a whole vein
```

**0.7.0**
---
```
- Adds multi-planting for the cultivator
	- This can be entirely enabled/disabled
	- Configurable number of max plants
	- Configurable level for when this perk becomes active
	- SNAPPING for same plant types (configurable)
- Adds Jumping skill improvements
	- lvl 10 your jump height can start increasing (vanilla also increases your jump height)
	- Jump modifier is a percentage of vanilla (0% removes jumping), default 125%
	- lvl 25 the distance you can fall before taking damage is increased (configurable, scales)
	- Fall damage reduction based on jumping skill level (default 50% less fall damage at level 100)
```

**0.6.1**
---
```
- Prevents excessive Voyager skill gain in some scenarios
- Added Polish Translation (thanks Jagr)
- Adds compatibility with SNEAKer (removes previous incompatibility)
- Improves compatiblity with MagicPlugin
- Improves compatibility with Crystals Magical
```

**0.6.0**
---
```
- Voyager
	- Adds damage reduction (starts lvl 10, scales with voyager skill)
	- Adds friend paddling (starts lvl 35, uses friends Voyager skill to increase your boat speed) friends must be attached to the boat.
	- Adds impact resistance (enabled at lvl 75, prevents damage to the boat when hitting other objects)
- Hauling (new skill)
	- Adds a small amount of total carry weight over time
	- Reduces the mass of carts when heavily loaded (scales, significantly)
	- Gain XP through moving carts, more xp for a heavier load
```

**0.5.21**
---
```
- Limited bonus crafting chance to valid percentile chance ranges
- Added an option to have fractional rolls for mining allowed to have a chance to spawn. This provides more linear scaling for drops with low chances (like mudpiles)
```

**0.5.20**
---
```
- Added extra safety checks for invalid denylist entries for pickable luck levels and items
- Added a denylist for mining drop modification
- Fixed a bug that could result in 1-2 more bonus item being given than intended when lucky crafting
- Rescaled mining drops with low chance to be less generous than before
- Fixes rockbreaker softlock on rock types not used by default
```

**0.5.19**
---
```
- Fixes mining loot increases not being applied to small destructible rocks
```

**0.5.18**
---
```
- Configuration to individually enable/disable swimming stamina cost reduction
- Configuration to modify the distance that Mining bonuses can be applied at
- Safety fallback, with configuration to reset rockbreaker incase it becomes stuck
- Fixes a bug which would cause 100 skill level to provide no extra mining drops in a very specific scenario
```

**0.5.17**
---
```
- Reduces chances that rockbreaker is not reset with rock destruction
- Reduces config sync strictness to allow players using the mod on servers (friends or dedicated) which do not have the mod to run their own configuration values instead of defaults
- Corrects mining AOE chance becoming unreachable
```

**0.5.16**
---
```
- Fixes hand crafting causing errors when no crafting table is available
```

**0.5.15**
---
```
- Fixes Cooking skill now being used as the skill level gate for bonus crafts when cooking food items (was crafting skill previously)
```

**0.5.14**
---
```
- Fix for excess swimming statmina consumption when stamina cost reduction is not active
```

**0.5.13**
---
```
- Make Voyager more permissive about what counts as being in the ship for skill gain
- Increased velocity requirements for Voyager skill gain, removed bounce velocity consideration (only forward or backward movement gives skill gains)
- Adds Swimming skill bonus:
  - Swimming speed increases with skill level (lvl 25+, configurable)
  - Reduces stamina drain while swimming (lvl 50+, configurable)
```

**0.5.12**
---
```
- Allows Farmings extra drops to work with any pickables, if configured to do so
	- Default configuration still prevents it from working on non-food related pickables
```

**0.5.11**
---
```
- Fixes Animal handling drops triggering for tamed creatures that do not have drops
```

**0.5.10**
---
```
- Additional configurabiliity for Mining drops chance for drops that have a chance to drop
- Configuration option to avoid increasing drops for mining products that are not stone/ore
```

**0.5.9**
---
```
- Fixes broken items not being unequipped when running out of durability
```

**0.5.8**
---
```
- Fixes honey not giving XP for Animal Whisperer when harvested
- Fixes an issue where high levels of Voyager could result in negative steering on some boats
```

**0.5.7**
---
```
- Fixes mining drop chance being overly generous on chance based ores
- Fixes mining drop chance giving extra drops on small trees
```

**0.5.6**
---
```
- Compatibility with ZenBees
- Fixes for bee biome allowance not working in some scenarios
```

**0.5.5**
---
```
- Fixes for Call to Arms
- Fixed an issue with destroying trees that had no drops
- Fixed an issue where Animal whisperer would cause an error on tamable creatures with no drops
- Added increased honey yields for the animal whisperer skill (15)
- Added the ability for beehives to work regardless of biome animal whisper (25)
```

**0.5.2**
---
```
- Fixes crafting bonus chance incorrectly giving a very high chance to get bonus crafts
```

**0.5.1**
---
```
- Fixes accesstools warning
```

**0.5.0**
---
```
- Adds CRAFTING skill bonuses!
  - Crafting provides a chance for additional crafting yields based on skill level (lvl 25+) (now more than 1, scaling with skill and configurable)
  - Crafting can refund partial resource costs sometimes (lvl 50+)
- Mining drop scales are now more linear (not exponential) for drops that have a highly variable chance of dropping
- Adds in more failsafes to prevent AOE mining from becoming disabled in the current session
- Added mining drop increases for small destructible rocks
```

**0.4.0**
---
```
- Better rock breaker per level scaling
- Sneak now provides a damage bonus to backstabs starting at level 25. Configurable enable/disable, level, and damage scale

```

**0.3.19**
---
```
- Added the ability to explode whole ore blocks at a small chance, default starting is lvl 75 with a max chance of 5%
- Improved compatibility for AOE mining with modified ore drops
- Improved performance of mining extremely large rocks
- Improved performance of mining returning drops for extremely large rocks
```

**0.3.18**
---
```
- Improves network synchronization for bonus gathering yields
- Increases vectors that are valid for triggering Voyager skill gains
- Increased default frequency of the voyager skill gain checks
- Increased the internal skill rate for Voyager
```

**0.3.17**
---
```
- Prevents enemy shields from providing blood magic XP
```

**0.3.16**
---
```
- Improves support for loot tables that create creatures, no longer causes errors or additional creature spawns based on luck
- Increases Mining AOE effect speed
```

**0.3.15**
---
```
- Improves support for mods that modify ore drops
```

**0.3.14**
---
```
- Prevents Voyager skill gains while on a boat but not moving
- Fixes a skill gain rate bug for Voyager impacting specific hardware configurations
- Corrects paddle speed scaling bonus
```

**0.3.13**
---
```
- Fixes AOE harvesting being disabled when harvesting only a few items
```

**0.3.12**
---
```
- Fixes a bug where if many items were triggered for AOE harvesting the async harvesting task would not re-enable AOE harvesting afterwards.
```

**0.3.11**
---
```
- Improve compatibility with mods which modify ore drops
```

**0.3.10**
---
```
- Prevents sneaking speed scaling from giving speed while you are over encumbered
- Fixes chain activation of gathering AOE skill on harvestables that are planted close together
```

**0.3.9**
---
```
- Improve compatibility with players who do not have the mod running
- Fixes a bug where gathering AOE harvest would not activate
- Reduced default frequency of Voyager skill gain
```

**0.3.8**
---
```
- Reduces luck when mining low chance yield rocks
- Adds an incompatibility with the "SNEAKr" mod, both mods patch the same thing and will not work together.
```

**0.3.7**
---
```
- Fixes a bug where shared would prevent skill gain in certain scenarios
```

**0.3.6**
---
```
- Fixes crash when mining rocks that are excessively large
```

**0.3.5**
---
```
- Increases flexibility of disallow list for shared XP
- Adds config to optionally disable AOE mining
```

**0.3.4**
---
```
- Reduces default scale of pickaxe AOE mining
- Adds a disallow list for shared XP
- Removes extra logging from stamina reduction
```

**0.3.3**
---
```
- Fixes infinite re-gathering from gathered nodes that are still visible
- Fixes stamina reduction causing returned stamina
```

**0.3.2**
---
```
- Makes Voyager skill gain rate configurable
```

**0.3.1**
---
```
- Fix for potential patch failure on minerock
```

**0.3.0**
---
```
- Initial release
- Adds improvements to Woodcutting, Pickaxe, Farming, Sneaking, Run, Bloodmagic, gathering, and cooking
- Adds new skills for Voyager, AnimalWhisper and sharing of xp between low/high skills
```