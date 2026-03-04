# Hellis Plugin - Admin Guide

## Admin Permissions

Grant yourself admin permissions:
```
oxide.grant user YourUsername hellisplugin.admin
```

Or grant to an admin group:
```
oxide.grant group admin hellisplugin.admin
```

## Arena Management Commands

### Creating Arenas In-Game

The easiest way to create arenas is to do it in-game while standing at the actual locations.

#### Step-by-Step Arena Creation

1. **Start the process**
   ```
   /arena create "Arena Name"
   ```
   - Use descriptive names like "CQC Arena 1", "Long Range", "Rooftop Duels"
   - Spaces in names are allowed

2. **Set First Spawn Point**
   - Walk to where you want the first player to spawn
   - Face the direction they should be looking
   - Type: `/arena setspawn1`
   - You'll see confirmation with coordinates

3. **Set Second Spawn Point**
   - Walk to where the opponent should spawn
   - Typically facing toward spawn 1
   - Type: `/arena setspawn2`
   - You'll see confirmation with coordinates

4. **Save the Arena**
   ```
   /arena save
   ```
   - Arena is immediately available for duels
   - Automatically saved to config file
   - No server restart required

#### Example Session
```
/arena create "Desert Arena 1"
# Walk to location 1
/arena setspawn1
# Confirmation: "Spawn point 1 set at (100.5, 10.2, -50.3)"

# Walk to location 2 (about 20-30 meters away)
/arena setspawn2
# Confirmation: "Spawn point 2 set at (120.8, 10.1, -50.5)"

/arena save
# Confirmation: "Arena 'Desert Arena 1' saved successfully!"
```

### Managing Existing Arenas

#### List All Arenas
```
/arena list
```
Shows:
- Arena names
- Current status (Available or IN USE)
- Spawn point coordinates

#### Delete an Arena
```
/arena delete "Arena Name"
```
- Cannot delete arenas currently in use
- Removed from config file automatically
- No server restart needed

#### Teleport to Arenas (Testing)
```
/arena tp "Arena Name" 1    # Teleport to spawn 1
/arena tp "Arena Name" 2    # Teleport to spawn 2
/arena tp "Arena Name"      # Defaults to spawn 1
```

Use this to:
- Test spawn positions before finalizing
- Check arena sightlines
- Verify no obstructions
- Test arena balance

#### Cancel Arena Creation
```
/arena cancel
```
- Cancels if you made a mistake during creation
- Discards any spawn points you've set
- No changes saved

## Arena Design Best Practices

### Distance Guidelines

| Arena Type | Distance | Weapon Modes |
|------------|----------|--------------|
| CQC (Close) | 15-25m | Revolver, Speargun |
| Medium | 30-50m | AK, SAR |
| Long Range | 60-100m | SAR, Bow |

### Location Selection

**Good Locations:**
- ✅ Flat, level terrain
- ✅ Clear sightlines between spawns
- ✅ Away from monuments (avoid NPC interference)
- ✅ Away from player bases (prevent griefing)
- ✅ Symmetrical layout (fairness)
- ✅ Natural cover available but not obstructing

**Bad Locations:**
- ❌ Steep hills or slopes
- ❌ Inside player bases
- ❌ Near monuments with NPCs
- ❌ Cluttered with rocks/trees between spawns
- ❌ One spawn has clear advantage
- ❌ Near radiation zones

### Testing Your Arenas

1. **Create the arena** using the commands above
2. **Teleport test**:
   ```
   /arena tp "Your Arena" 1
   /arena tp "Your Arena" 2
   ```
3. **Check both spawns**:
   - Can you see the other spawn clearly?
   - Is terrain level?
   - Any obstacles blocking shots?
   - Is it balanced (no advantage to either side)?

4. **Test with another admin** (if available):
   - Both queue for same mode
   - Actually fight in the arena
   - Check for any issues

### Recommended Arena Count

**With Multi-Instance Support:**

Each arena can host multiple matches simultaneously (default: 5 instances per arena).

- **Small Server (1-20 players)**: 1-2 arenas (5-10 concurrent matches)
- **Medium Server (20-50 players)**: 2-4 arenas (10-20 concurrent matches)
- **Large Server (50+ players)**: 4-6 arenas (20-30 concurrent matches)

**Calculation:** `Total Capacity = Number of Arenas × Max Instances Per Arena`

Example: 3 arenas × 5 instances = 15 simultaneous 1v1 matches (30 players fighting)

### Multi-Instance Arena System

**NEW FEATURE:** Arenas now support multiple simultaneous matches!

#### How It Works
- Each physical arena can host up to 5 matches at once (configurable)
- Players in different instances are isolated - they can only see their opponent
- All instances use the same spawn point locations
- Network isolation prevents "ghost fights" visibility

#### Configuration
```json
"Max Instances Per Arena": 5
```

Set this value to control how many matches each arena can host:
- **1**: Traditional single match per arena (old behavior)
- **5**: Default, allows 5 concurrent matches per arena
- **10**: High capacity servers, 10 matches per arena

#### Benefits
- **Fewer arenas needed**: 2-3 arenas can handle 10-15 matches
- **No queue buildup**: More match capacity without creating more arenas
- **Efficient use of space**: Reuse good arena locations
- **Player isolation**: Clean 1v1 experience, no distractions

#### Arena Status
Use `/arena list` to see instance usage:
```
Arena 1 - 3/5 instances active
  Spawn 1: (100, 10, -50)
  Spawn 2: (120, 10, -50)
  Active Instances: 0, 2, 4
```

This shows Arena 1 has 3 out of 5 instances in use (instances #0, #2, and #4).

More arenas = more simultaneous matches = less queue time

## Configuration Management

### Automatic Config Updates

When you create/delete arenas in-game:
- Config file (`oxide/config/HellisPlugin.json`) is automatically updated
- Changes persist across server restarts
- No manual editing needed

### Manual Configuration (Alternative)

You can still manually edit the config if preferred:

```json
"Duel Arena Positions": [
  {
    "Name": "Arena 1",
    "Spawn1": { "x": 100.5, "y": 10.0, "z": -50.2 },
    "Spawn2": { "x": 120.5, "y": 10.0, "z": -50.2 }
  },
  {
    "Name": "Long Range Arena",
    "Spawn1": { "x": 200.0, "y": 15.0, "z": 100.0 },
    "Spawn2": { "x": 280.0, "y": 15.0, "z": 100.0 }
  }
]
```

After manual edits:
```
oxide.reload HellisPlugin
```

## Troubleshooting

### Arena Not Available
- Check `/arena list` to see if it exists
- Verify it's not "IN USE"
- Check coordinates are valid for your map

### Players Falling Through Floor
- Arena spawn Y coordinate is too low
- Teleport to location, check actual height
- Update spawn points with correct Y value

### Arena Too Easy/Hard
- Adjust distance between spawns
- Change terrain (more/less cover)
- Create mode-specific arenas

### Can't Delete Arena
- Arena is currently in use by a match
- Wait for match to end
- Or kick players from match first

## Advanced Tips

### Theme-Based Arenas
Create themed arenas for variety:
- **Rooftop Duels**: On building tops
- **Desert Standoff**: Open desert areas
- **Forest Fight**: Light forest areas
- **Compound Clash**: Near compounds (empty)

### Mode-Specific Arenas
Consider creating arenas optimized for specific modes:
- **Speargun Arena**: Near water or shallow pools
- **Long Range**: For SAR with 60-80m distance
- **CQC**: For Revolver with 15-20m distance

### Regular Maintenance
- Review arena list monthly
- Remove unused arenas
- Add new arenas based on player feedback
- Test arenas after map wipes

## Customizing Loadouts/Kits

You can fully customize what items players receive for each weapon mode, and **create entirely new custom modes**. The easiest way is to use the **in-game `/kit` commands** — no JSON editing or server restart required.

### In-Game Kit Creation (Recommended)

1. **Grant yourself admin** (if not already done):
   ```
   oxide.grant user YourUsername hellisplugin.admin
   ```

2. **Equip yourself** with exactly the items you want players to receive for a mode.  
   Put weapons + ammo in your main inventory; put armor in your wear slots.

3. **Save the kit** with one command — use any name you like:
   ```
   /kit save Shotgun
   ```
   The plugin reads your entire inventory (main + belt + wear) and saves it.  
   Changes take effect immediately — **no reload needed**.  
   The new mode will instantly appear in the **private room mode selector** for all players.

4. **Verify** what was saved:
   ```
   /kit show Shotgun
   ```

5. **List** all kit names (built-in + custom):
   ```
   /kit list
   ```

6. **Reset** a built-in kit back to its defaults at any time:
   ```
   /kit reset AK47
   ```

7. **Delete** a custom kit you no longer want:
   ```
   /kit delete Shotgun
   ```

#### Example: Creating a Shotgun mode
```
# Equip: shotgun.pump (x1), ammo.shotgun (x48), metal.plate.torso, metal.facemask, syringe.medical (x3)
/kit save Shotgun
# ✅ Kit 'Shotgun' saved with 5 item(s). It now appears in the private room mode selector.

/kit list
# Available kits: AK47, SAR, Bow, Revolver, Speargun, Shotgun
```

Players creating private rooms will immediately see "Shotgun" as a chooseable mode.

### Kit Name Rules

Custom kit names must:
- Contain only **letters, digits, underscores (`_`) or dashes (`-`)**
- Be **20 characters or less**

### Where Custom Modes Appear

- ✅ **Private room mode selector** (GUNS button in room list)  
- ✅ **CREATE ROOM buttons** at the bottom of the lobby browser  
- ❌ **Public queues** — custom modes are for private rooms only

### Manual Configuration (Alternative)

You can also directly edit `oxide/config/HellisPlugin.json` if you prefer:

1. **Stop your server** (or be ready to reload the plugin)

2. **Edit** `oxide/config/HellisPlugin.json`

3. **Find the Loadouts section and add your new mode:**
   ```json
   "Loadouts": {
     "AK47": { ... },
     "Shotgun": {
       "Items": [
         { "ShortName": "shotgun.pump", "Amount": 1 },
         { "ShortName": "ammo.shotgun", "Amount": 48 },
         { "ShortName": "metal.plate.torso", "Amount": 1 }
       ]
     }
   }
   ```

4. **Reload plugin**: `oxide.reload HellisPlugin`

### Customization Examples

**Example 1: Give More Ammo**
```json
"AK47": {
  "Items": [
    { "ShortName": "rifle.ak", "Amount": 1 },
    { "ShortName": "ammo.rifle", "Amount": 240 },  // Changed from 120
    ...
  ]
}
```

**Example 2: Add a Scope**
```json
"AK47": {
  "Items": [
    { "ShortName": "rifle.ak", "Amount": 1 },
    { "ShortName": "weapon.mod.holosight", "Amount": 1 },  // Added
    { "ShortName": "ammo.rifle", "Amount": 120 },
    ...
  ]
}
```

**Example 3: Better Armor**
```json
"SAR": {
  "Items": [
    { "ShortName": "rifle.semiauto", "Amount": 1 },
    { "ShortName": "ammo.rifle", "Amount": 96 },
    { "ShortName": "heavy.plate.jacket", "Amount": 1 },  // Changed from metal.plate.torso
    { "ShortName": "metal.facemask", "Amount": 1 },      // Upgraded
    { "ShortName": "heavy.plate.pants", "Amount": 1 },   // Upgraded
    { "ShortName": "syringe.medical", "Amount": 4 }
  ]
}
```

### Common Item ShortNames

**Weapons:** `rifle.ak`, `rifle.semiauto`, `bow.hunting`, `pistol.revolver`, `speargun`

**Ammo:** `ammo.rifle`, `ammo.pistol`, `arrow.wooden`, `speargun.spear`

**Armor:** `metal.plate.torso`, `metal.facemask`, `roadsign.jacket`, `roadsign.kilt`, `heavy.plate.jacket`

**Medical:** `syringe.medical`, `bandage`, `largemedkit`

**Attachments:** `weapon.mod.holosight`, `weapon.mod.lasersight`, `weapon.mod.silencer`

**Find more:** https://www.corrosionhour.com/rust-item-list/

### Balancing Tips

- **Keep it fair**: Both players get identical loadouts
- **Test thoroughly**: Play matches with each loadout
- **Listen to feedback**: Players know what feels balanced
- **Medical balance**: More meds = longer fights

## Common Questions

**Q: How many arenas should I create?**
A: Start with 3-5, add more if queue times are long.

**Q: Can I create arenas on different map sizes?**
A: Yes, but you'll need to recreate them after map wipes/changes.

**Q: Do arenas persist after server restart?**
A: Yes, they're saved in the config file.

**Q: Can I rename an arena?**
A: Delete and recreate with new name, or manually edit config.

**Q: What if I make a mistake?**
A: Use `/arena cancel` before saving, or delete and recreate.

## Monitoring

Keep an eye on:
- Arena usage via `/arena list`
- Player complaints about specific arenas
- Queue times (add arenas if too long)
- Match fairness (one side winning too much?)

## Getting Help

If you encounter issues:
1. Check server console for errors
2. Verify permissions are set correctly
3. Test with `/arena tp` commands
4. Review this guide
5. Check plugin GitHub for updates

---

Good luck managing your Hellis dueling server! 🎮
