# Hellis Plugin - Oxide Plugin for Rust

A comprehensive C# Oxide plugin for Rust game servers featuring instant, fair 1v1 duels, aim training, multiple weapon modes, and a central lobby system.

## Features

### 🏠 Lobby System
- **Central Hub**: All players spawn in a configured lobby location
- **Automatic Returns**: Return to lobby after matches or leaving queue
- **Easy Navigation**: `/lobby` command or "Return to Lobby" UI button
- **Admin Setup**: `/lobby setpos` to configure spawn location

### 🎯 Aim Training
- Practice accuracy with target shooting
- Track your performance and improve skills
- Command: `/aimtrain`

### ⚔️ 1v1 Instant Duels
- **Queue System**: Join via JOIN QUEUE button (random mode selection)
- **Automatic Matchmaking**: Server matches players instantly
- **Random Weapon Selection**: Each match uses a random weapon (AK-47, SAR, Bow, Revolver)
- **Instant Teleportation**: Both players teleported to dedicated arenas
- **Customizable Loadouts**: Configure weapons, armor, ammo, and meds per mode via config
- **Equal Loadouts**: Both players get identical gear for fair fights
- **Countdown Start**: Short countdown before each round
- **Best-of-X Rounds**: Configurable round system (default: 3)
- **Fast Iteration**: Quick winner/loser screens, auto re-queue option
- **🆕 Multi-Instance Arenas**: Multiple matches can use the same arena simultaneously
- **🆕 Player Isolation**: You only see your opponent, not other dueling pairs

### 🎮 Simple Always-Visible UI
- **JOIN QUEUE Button**: Always visible in top-right corner (green)
- **One-Click Join**: Click to join random mode queue instantly
- **LEAVE Button**: Appears in bottom-right when in queue or match
- **Global Leaderboard**: Always visible in top-left corner
- **Win/Lose Overlay**: Large colored overlay when matches end
- **No Commands Needed**: UI elements always visible, no toggle required
- **Clean Interface**: Minimal, non-intrusive design

### 🏆 Leaderboard
- **Top 10 Rankings**: View best players by win rate
- **Competitive Stats**: Wins, losses, and win percentage
- **Personal Ranking**: See where you stand
- **Commands**: `/leaderboard` or `/top`

### 🛠️ Admin Arena Creation
- **No Preset Arenas**: Start with clean slate
- **In-Game Creation**: Build arenas using commands
- **Multi-Step Process**: Create, set spawn points, save
- **Full Management**: List, delete, teleport to arenas
- **See**: ADMIN_GUIDE.md for complete arena management

### 🔫 Supported Duel Modes
1. **AK-47** - Full auto rifle with metal armor
2. **SAR** - Semi-auto rifle with metal armor
3. **Speargun** - Underwater combat with diving gear
4. **Bow** - Primitive combat with hide armor
5. **Revolver** - Quick draw pistol duels


## Installation

1. Download `HellisPlugin.cs`
2. Place in `oxide/plugins/` directory
3. Server will auto-load the plugin
4. **Configure lobby**: `/lobby setpos` (admin command)
5. **Create arenas**: Use `/arena create` commands (see ADMIN_GUIDE.md)
6. Configure via `oxide/config/HellisPlugin.json` if needed

## Quick Setup (Admins)

1. **Set Lobby Position**:
   ```
   /lobby setpos
   ```
   Stand where you want players to spawn, then run this command.

2. **Create Your First Arena**:
   ```
   /arena create "Arena 1"
   /arena setspawn1
   (move to second position)
   /arena setspawn2
   /arena save
   ```

3. **Ready to Go!** Players can now queue and duel.

## Commands

### Player Commands
- Click **JOIN QUEUE** button (top-right, green) - Join random mode queue
- `/leave` - Leave the current queue or match (returns to lobby)
- `/lobby` - Teleport to lobby
- `/help` - Show all available commands
- `/leaderboard` or `/top` - View top 10 players
- `/toggleleaderboard` - Hide/show the leaderboard UI
- `/aimtrain` - Start aim training session
- `/stats` - View your duel statistics
- `/showui` - Refresh UI elements if not visible
- `/autorequeue [on/off]` - Toggle auto-requeue after matches

**💡 TIP**: Click the green JOIN QUEUE button in the top-right to start dueling!

### Admin Commands
- `/arena create <name>` - Start creating a new arena
- `/arena setspawn1` - Set first spawn point at your current position
- `/arena setspawn2` - Set second spawn point at your current position
- `/arena save` - Save the arena to configuration
- `/arena cancel` - Cancel arena creation
- `/arena list` - List all arenas with their status
- `/arena delete <name>` - Delete an arena
- `/arena tp <name> [1|2]` - Teleport to arena spawn point for testing

### Examples
```
# Join queue
Click JOIN QUEUE button   - Easiest! Join random mode queue
                          - Random weapon selected for each match
                          - 25% chance each: AK47, SAR, Bow, Revolver

# View stats and rankings
/stats                    - View your win/loss record
/leaderboard              - View top 10 players

# Manage auto-requeue
/autorequeue on           - Auto-rejoin queue after matches
/autorequeue off          - Manual requeue (default)

# Admin: Create a new arena
/arena create "CQC Arena 1"
/arena setspawn1          # Stand at first spawn
/arena setspawn2          # Move and stand at second spawn
/arena save               # Save the arena

# Admin: Test an arena
/arena tp "CQC Arena 1" 1  # Teleport to spawn 1
```

## UI System

The plugin includes **always-visible UI elements** for easy access:

### JOIN QUEUE Button (Top Right)
- **Always visible** - Green button in top-right corner
- **One-click join** - Joins random mode queue instantly
- **No commands needed** - Just click and wait for match

### LEAVE Button (Bottom Right)
- **Context-aware** - Appears when in queue or active match
- **Quick exit** - Leave queue or forfeit match with one click
- **Returns to lobby** - Automatically teleports you back

### Global Leaderboard (Top Left)
- **Always visible** - See top 10 players at all times
- **Live updates** - Rankings update after each match
- **Your rank shown** - If outside top 10, shows your position
- **Toggle command** - `/toggleleaderboard` to hide/show

### Win/Lose Overlay
- **Large display** - Centered overlay when match ends
- **Color coded** - Green for WIN, Red for LOSE
- **Shows score** - Final round score displayed
- **Auto-dismiss** - Disappears after 5 seconds

## Configuration

Edit `oxide/config/HellisPlugin.json`:

```json
{
  "Server Name": "Hellis Server",
  "Enable Aim Training": true,
  "Enable 1v1 Duels": true,
  "Enable Speargun Mode": true,
  "Countdown Duration (seconds)": 3,
  "Best of X Rounds": 3,
  "Auto Requeue After Match": true,
  "Max Instances Per Arena": 5,
  "Lobby Position": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "Lobby Position Set": true,
  "Lobby Radius": 50.0,
  "Aim Train Arena Position": { "x": -100.0, "y": 0.0, "z": -100.0 },
  "Aim Train Arena Radius": 30.0,
  "Enable Zone Enforcement": false
}
```

**Note:** Duel arenas are stored in `HellisPlugin_Arenas` data file, not in config. Create arenas using `/arena` commands.

### Customizable Loadouts/Kits

**You can customize what items players get for each weapon mode!**

Edit the `Loadouts` section in `HellisPlugin.json` to change weapons, armor, ammo amounts, and medical supplies for each mode:

```json
"Loadouts": {
  "AK47": {
    "Items": [
      { "ShortName": "rifle.ak", "Amount": 1 },
      { "ShortName": "ammo.rifle", "Amount": 120 },
      { "ShortName": "metal.plate.torso", "Amount": 1 },
      { "ShortName": "metal.facemask", "Amount": 1 },
      { "ShortName": "roadsign.kilt", "Amount": 1 },
      { "ShortName": "syringe.medical", "Amount": 4 }
    ]
  },
  "SAR": {
    "Items": [
      { "ShortName": "rifle.semiauto", "Amount": 1 },
      { "ShortName": "ammo.rifle", "Amount": 96 }
      // ... add more items
    ]
  }
  // ... other modes: Bow, Revolver, Speargun
}
```

**Customization Options:**
- ✅ Change armor types (metal, roadsign, coffee can, etc.)
- ✅ Adjust ammo amounts
- ✅ Add or remove medical supplies
- ✅ Include attachments (scopes, lasers, etc.)
- ✅ Add clothing items
- ✅ Balance kits to your server's preference

**Common Item ShortNames:**
- Weapons: `rifle.ak`, `rifle.semiauto`, `bow.hunting`, `pistol.revolver`, `speargun`
- Ammo: `ammo.rifle`, `ammo.pistol`, `arrow.wooden`, `speargun.spear`
- Armor: `metal.plate.torso`, `metal.facemask`, `coffeecan.helmet`, `roadsign.kilt`
- Medical: `syringe.medical`, `bandage`, `largemedkit`
- Attachments: `weapon.mod.holosight`, `weapon.mod.lasersight`, `weapon.mod.silencer`

Find more item shortnames at: https://www.corrosionhour.com/rust-item-list/

### Multi-Instance Arenas

**New Feature!** Each arena can now host multiple 1v1 matches simultaneously:

- **`Max Instances Per Arena`**: Controls how many matches per arena (default: 5)
- **Player Isolation**: Players in different instances can't see each other
- **Same Location**: All instances use the same physical spawn points
- **Efficient**: With 3 arenas and 5 instances each = 15 concurrent matches!

Example: "Arena 1" can host 5 different 1v1 matches at the same time, with each pair only seeing their opponent.

## Permissions

- `hellisplugin.use` - Allow players to use duel and aimtrain commands
- `hellisplugin.admin` - Admin permissions for arena management commands

Grant permissions via Oxide:
```
oxide.grant user <username> hellisplugin.use
oxide.grant group default hellisplugin.use
oxide.grant user <adminname> hellisplugin.admin
```

## Creating Arenas In-Game (Admins)

Admins can now create arenas directly in-game without editing config files!

### Step-by-Step Arena Creation

1. **Start creation**: `/arena create "My Arena Name"`
2. **Set first spawn**: Walk to where players should spawn, then `/arena setspawn1`
3. **Set second spawn**: Walk to the opponent spawn point, then `/arena setspawn2`
4. **Save arena**: `/arena save` - Arena is now available for duels!

### Arena Management

- **List arenas**: `/arena list` - Shows all arenas and their status
- **Test arena**: `/arena tp "Arena Name" 1` - Teleport to spawn 1 (or 2)
- **Delete arena**: `/arena delete "Arena Name"` - Remove an arena
- **Cancel creation**: `/arena cancel` - Cancel if you made a mistake

### Tips for Arena Placement

- Distance between spawns: 15-30m for CQC, 50-100m for long range
- Ensure flat, open areas with no obstructions
- Place away from monuments and player bases
- Create multiple arenas to handle concurrent matches
- Test with `/arena tp` before finalizing

## How 1v1 Duels Work

### ⚔️ Starting a Duel
1. Player clicks **JOIN QUEUE** button (green, top-right corner)
2. Server adds them to the random mode queue
3. When 2 players are in queue, match starts automatically
4. Random weapon mode selected (AK47, SAR, Bow, or Revolver)
5. Both players teleported to a free arena

### 🔫 Loadouts & Fairness
- Both players get **identical gear** (weapon, armor, meds)
- No looting, no crafting - pure mechanical skill
- Different modes use different weapons (AK, SAR, Speargun, etc.)
- Equal health, equal resources

### 🧠 The Fight
- Countdown timer (default 3 seconds)
- Round begins after countdown
- First to kill opponent wins the round
- Best-of-X format (default: best of 3)

### 🔁 After the Duel
- Winner/loser notification with final score
- Stats updated (wins, losses, rounds)
- Players returned to lobby/spawn
- Optional auto re-queue for fast grinding

## Loadout Details

### AK-47 Mode
- Rifle.AK x1
- 5.56 Ammo x120
- Metal Chest Plate
- Metal Facemask
- Roadsign Kilt
- Medical Syringes x4

### SAR Mode
- Semi-Auto Rifle x1
- 5.56 Ammo x96
- Metal Chest Plate
- Coffee Can Helmet
- Roadsign Kilt
- Medical Syringes x4

### Speargun Mode
- Speargun x1
- Spears x16
- Full Diving Suit (fins, mask, tank, wetsuit)
- Medical Syringes x3

### Bow Mode
- Hunting Bow x1
- Wooden Arrows x60
- Hide Vest
- Bandages x6

### Revolver Mode
- Revolver x1
- Pistol Ammo x64
- Burlap Shirt & Trousers
- Bandages x4

## Statistics Tracking

Players can view their stats with `/stats`:
- Total Matches Played
- Wins / Losses
- Win Rate Percentage
- Rounds Won / Lost

## Requirements

- Oxide Mod installed on Rust server
- Permissions system enabled

## Support

For issues or feature requests, please open an issue on GitHub.

## License

See repository license.
