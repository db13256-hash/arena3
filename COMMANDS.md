# Hellis Plugin - Complete Command Reference

**Version:** 2.0  
**Last Updated:** 2026-02-14

This document provides a comprehensive list of ALL commands available in the Hellis Plugin for Rust servers.

---

## Table of Contents

1. [Player Commands](#player-commands)
2. [Admin Commands](#admin-commands)
3. [Console Commands (UI)](#console-commands-ui)
4. [Permissions](#permissions)
5. [Quick Examples](#quick-examples)

---

## Player Commands

### Queue & Match Commands

#### `/duel <mode>`
Join the 1v1 duel queue for a specific weapon mode.

**Modes:**
- `ak` - AK47 duels
- `sar` - Semi-Automatic Rifle duels
- `spear` - Speargun duels
- `bow` - Bow duels
- `rev` - Revolver duels

**Usage:**
```
/duel ak
/duel sar
/duel spear
/duel bow
/duel rev
```

**Aliases:** None

**Permission Required:** None (available to all players)

**Example:**
```
Player: /duel ak
Server: You've joined the AK47 queue. Waiting for an opponent...
```

---

#### `/leave`
Leave the current queue or forfeit from a match.

**Usage:**
```
/leave
```

**Aliases:** None

**Permission Required:** None

**Example:**
```
Player: /leave
Server: You've left the queue.
```

---

#### `/forfeit`
Forfeit the current match (if you're in one).

**Usage:**
```
/forfeit
```

**Aliases:** None

**Permission Required:** None

**Example:**
```
Player: /forfeit
Server: You've forfeited the match. Returning to lobby...
```

---

#### `/aimtrain`
Join the aim training mode.

**Usage:**
```
/aimtrain
```

**Aliases:** None

**Permission Required:** None

**Example:**
```
Player: /aimtrain
Server: Aim training mode activated!
```

---

### Stats & Leaderboard Commands

#### `/stats`
View your personal duel statistics.

**Usage:**
```
/stats
```

**Aliases:** None

**Permission Required:** None

**Shows:**
- Total matches played
- Wins and losses
- Win rate percentage
- Kills and deaths
- K/D ratio
- Rounds won and lost

**Example:**
```
Player: /stats
Server: 
════════════════════════════════
Your Duel Statistics
════════════════════════════════
Total Matches: 15
Record: 10W - 5L (66.67%)
K/D: 25 kills / 12 deaths (2.08)
Rounds: 22W - 18L
════════════════════════════════
```

---

#### `/leaderboard`
View the global leaderboard showing top 10 players.

**Usage:**
```
/leaderboard
```

**Aliases:** `/top`

**Permission Required:** None

**Shows:**
- Top 10 players ranked by win rate
- Player name, W-L record, and K/D ratio
- Your own rank if not in top 10

**Example:**
```
Player: /leaderboard
Server:
═══════════════════════════════════════
Hellis Duels - Global Leaderboard
═══════════════════════════════════════
1. PlayerOne (15-2) K/D: 7.50
2. PlayerTwo (12-3) K/D: 4.00
3. YOU (10-5) K/D: 2.00
...
```

---

#### `/top`
Alias for `/leaderboard`. See above for details.

---

### UI Commands

#### `/duelui`
Toggle the duel UI panel on/off.

**Usage:**
```
/duelui
```

**Aliases:** None

**Permission Required:** None

**Description:**
- Shows/hides the central duel UI panel
- UI contains quick-join buttons for all modes
- Toggle with one command

**Example:**
```
Player: /duelui
Server: Duel UI shown. Click buttons to join queue!

Player: /duelui (again)
Server: Duel UI hidden. Type /duelui to show it.
```

---

#### `/toggleleaderboard`
Toggle the persistent leaderboard UI in top left corner.

**Usage:**
```
/toggleleaderboard
```

**Aliases:** None

**Permission Required:** None

**Description:**
- Shows/hides the persistent global leaderboard
- Leaderboard displays in top left corner
- Updates in real-time after matches

**Example:**
```
Player: /toggleleaderboard
Server: Leaderboard hidden.

Player: /toggleleaderboard (again)
Server: Leaderboard shown.
```

---

### Navigation Commands

#### `/lobby`
Teleport to the lobby spawn point.

**Usage:**
```
/lobby
```

**Aliases:** None

**Permission Required:** None

**Description:**
- Instantly teleports you to the configured lobby
- Available at any time (not during matches)
- Returns you to the central gathering area

**Example:**
```
Player: /lobby
Server: Teleported to lobby!
```

---

### Help Command

#### `/help`
Display all available commands and their descriptions.

**Usage:**
```
/help
```

**Aliases:** None

**Permission Required:** None

**Shows:**
- All player commands with descriptions
- Admin commands (if you have permission)
- Quick tips and usage notes

**Example:**
```
Player: /help
Server: [Shows formatted command list]
```

---

## Admin Commands

### Arena Management

#### `/arena`
Manage arenas (create, edit, delete, list).

**Permission Required:** `hellisplugin.admin`

**Subcommands:**

##### `/arena create <name>`
Start creating a new arena.

**Usage:**
```
/arena create "Arena 1"
```

**Description:**
- Initiates arena creation process
- Requires setting two spawn points
- Name must be unique

---

##### `/arena setspawn1`
Set the first spawn point at your current position.

**Usage:**
```
/arena setspawn1
```

**Description:**
- Must be used during arena creation
- Records your current position as spawn point 1
- Move to a different location before setting spawn 2

---

##### `/arena setspawn2`
Set the second spawn point at your current position.

**Usage:**
```
/arena setspawn2
```

**Description:**
- Must be used during arena creation
- Records your current position as spawn point 2
- Should face spawn point 1 for proper duels

---

##### `/arena save`
Save the current arena being created.

**Usage:**
```
/arena save
```

**Description:**
- Finalizes arena creation
- Saves to HellisPlugin_Arenas.json
- Arena becomes available for matches immediately

---

##### `/arena cancel`
Cancel the current arena creation.

**Usage:**
```
/arena cancel
```

**Description:**
- Cancels arena creation in progress
- Discards unsaved changes
- Returns you to normal state

---

##### `/arena list`
List all created arenas and their status.

**Usage:**
```
/arena list
```

**Description:**
- Shows all arenas with instance usage
- Displays active instances per arena
- Shows which instances are occupied

**Example:**
```
Admin: /arena list
Server:
Available Arenas:
- Arena 1 (3/5 instances active)
  Active Instances: 0, 2, 4
- Arena 2 (1/5 instances active)
  Active Instances: 1
```

---

##### `/arena delete <name>`
Delete an existing arena.

**Usage:**
```
/arena delete "Arena 1"
```

**Description:**
- Permanently removes the arena
- Cannot be undone
- Arena must not be in use

---

##### `/arena tp <name> [1|2]`
Teleport to an arena for testing.

**Usage:**
```
/arena tp "Arena 1" 1
/arena tp "Arena 1" 2
```

**Description:**
- Teleports you to the specified spawn point
- Useful for testing arena positioning
- Optional spawn number (1 or 2)

---

### Lobby Management

#### `/lobby setpos`
Set the lobby spawn position to your current location.

**Usage:**
```
/lobby setpos
```

**Permission Required:** `hellisplugin.admin`

**Description:**
- Records current position as lobby spawn
- All players spawn here on connect
- Players return here after matches

**Example:**
```
Admin: /lobby setpos
Server: Lobby position set to your current location!
```

---

## Console Commands (UI)

These commands are automatically executed when players click buttons in the UI. They're not meant to be typed manually.

### `duelui.join <mode>`
Internal command executed when clicking weapon mode buttons.

**Parameters:**
- `mode` - Weapon mode (AK, SAR, Spear, Bow, Rev)

**Usage:** Automatic (via UI button clicks)

---

### `duelui.leave`
Internal command executed when clicking "Leave Queue" button.

**Usage:** Automatic (via UI button click)

---

### `duelui.stats`
Internal command executed when clicking "Stats" button.

**Usage:** Automatic (via UI button click)

---

### `duelui.leaderboard`
Internal command executed when clicking "Leaderboard" button.

**Usage:** Automatic (via UI button click)

---

### `duelui.lobby`
Internal command executed when clicking "Return to Lobby" button.

**Usage:** Automatic (via UI button click)

---

### `duelui.close`
Internal command executed when clicking "Close UI" button.

**Usage:** Automatic (via UI button click)

---

## Permissions

### Player Permissions
No permissions required - all player commands are available to everyone.

### Admin Permissions

#### `hellisplugin.admin`
Required for:
- `/arena` (all subcommands)
- `/lobby setpos`

**Granting Permission:**
```
o.grant user <username> hellisplugin.admin
o.grant group <groupname> hellisplugin.admin
```

**Revoking Permission:**
```
o.revoke user <username> hellisplugin.admin
o.revoke group <groupname> hellisplugin.admin
```

---

## Quick Examples

### For Players

**Join a duel:**
```
/duel ak
```

**Check your stats:**
```
/stats
```

**View top players:**
```
/leaderboard
```

**Use the UI instead:**
```
/duelui
(Click [AK] button to join queue)
```

**Return to lobby:**
```
/lobby
```

---

### For Admins

**Set up lobby:**
```
1. Stand at desired lobby location
2. /lobby setpos
```

**Create first arena:**
```
1. /arena create "Arena 1"
2. Stand at first spawn point
3. /arena setspawn1
4. Walk to second spawn point (facing first)
5. /arena setspawn2
6. /arena save
```

**Check arena status:**
```
/arena list
```

**Test arena:**
```
/arena tp "Arena 1" 1
(Check positioning)
/arena tp "Arena 1" 2
(Check other spawn)
```

---

## Configuration

Some command behavior can be modified in `oxide/config/HellisPlugin.json`:

**Auto Show Duel UI:**
```json
"Auto Show Duel UI": true
```
- `true` - Duel UI shows automatically on player connect (default)
- `false` - Players must use `/duelui` to show it manually

---

## Tips & Tricks

### For Players

1. **Use the UI** - Much faster than typing commands
2. **Toggle UIs** - Hide UIs if they're in the way during fights
3. **Check stats regularly** - Track your improvement
4. **Study the leaderboard** - See what K/D ratios top players have

### For Admins

1. **Test arenas** - Use `/arena tp` to verify spawn positions
2. **Multiple arenas** - Create 3-5 arenas for better capacity
3. **Multi-instance** - Each arena supports 5 simultaneous matches
4. **Backup data** - Copy `oxide/data/HellisPlugin_*.json` files regularly

---

## Troubleshooting

### "I don't see the UI buttons"
```
/duelui
```

### "Command not working"
- Check spelling
- Make sure you're not in a match (for some commands)
- Verify you have required permissions (for admin commands)

### "Can't create arena"
- Make sure you have `hellisplugin.admin` permission
- Complete all steps: create → setspawn1 → setspawn2 → save

---

## Summary

**Total Commands:** 18
- **Player Commands:** 11
- **Admin Commands:** 7 (1 main + 6 subcommands)
- **Console Commands:** 6 (UI internal)

**Most Used:**
- `/duel <mode>` - Join queue
- `/stats` - Check stats
- `/leaderboard` - View rankings
- `/duelui` - Toggle UI
- `/lobby` - Return to lobby

**For Complete Documentation:**
- See `README.md` for overview
- See `USER_GUIDE.md` for player guide
- See `ADMIN_GUIDE.md` for admin guide
- See `QUICK_REFERENCE.md` for quick lookup

---

**Need Help?** Type `/help` in-game for a quick command list!
