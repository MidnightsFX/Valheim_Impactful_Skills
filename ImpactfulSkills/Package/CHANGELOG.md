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