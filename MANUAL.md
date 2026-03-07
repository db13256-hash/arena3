# Hellis Plugin — Complete Manual

**An Oxide plugin for Rust** | 1v1 duels · aim training · multi-instance arenas · lobby system

---

## Table of Contents

1. [Installation](#installation)
2. [Quick Start for Admins](#quick-start-for-admins)
3. [Lobby System](#lobby-system)
4. [Arena Management](#arena-management)
5. [1v1 Duel System](#1v1-duel-system)
6. [Aim Training](#aim-training)
7. [Leaderboard & Stats](#leaderboard--stats)
8. [UI Overview](#ui-overview)
9. [Permissions](#permissions)
10. [All Commands](#all-commands)
11. [Configuration Reference](#configuration-reference)
12. [Customising Loadouts & Kits](#customising-loadouts--kits)
13. [Multi-Instance Arenas](#multi-instance-arenas)
14. [Troubleshooting](#troubleshooting)

---

## Installation

1. Copy `HellisPlugin.cs` to `oxide/plugins/` on your server.
2. The plugin auto-loads. No restart is required.
3. Grant yourself admin: `oxide.grant user <yourname> hellisplugin.admin`
4. Grant players basic access: `oxide.grant group default hellisplugin.use`
5. Follow [Quick Start for Admins](#quick-start-for-admins) to configure the lobby and create arenas.

---

## Quick Start for Admins

```
# 1. Stand where players should spawn in the lobby, then run:
/lobby setpos

# 2. (Optional) Add more lobby spawn points so players spread out:
/lobby addspawn     ← run this at each additional location

# 3. Create your first duel arena:
/arena create "Arena 1"
/arena setspawn1     ← stand at first spawn, run this
/arena setspawn2     ← walk to second spawn, run this
/arena save

# Done! Players can now click JOIN QUEUE and fight.
```

---

## Lobby System

All players are teleported to the lobby when they connect, finish a match, or type `/lobby`.

### Multiple Spawn Points

The lobby supports **multiple spawn points**. When a player is sent to the lobby, the server picks one at random — this spreads players across the area and prevents congestion.

#### Admin commands (all require `hellisplugin.admin`)

| Command | Description |
|---------|-------------|
| `/lobby setpos` | Set (or replace) spawn point 1 at your current position |
| `/lobby addspawn` | Add an additional spawn point at your current position |
| `/lobby editspawn <n>` | Move spawn point `n` to your current position (update in-place) |
| `/lobby removespawn <n>` | Remove spawn point number `n` (see `/lobby listspawns`) |
| `/lobby listspawns` | List all spawn points with DDraw markers shown in-world |
| `/lobby setradius <r>` | Set the lobby zone radius in metres (default: 10 m) |

#### Example — setting up three lobby spawns

```
# Stand at the first spawn location
/lobby setpos

# Walk a few metres and add two more
/lobby addspawn
/lobby addspawn

# Verify
/lobby listspawns
# Lobby spawn points (3 total):
#   1: (100.5, 10.0, -50.2)
#   2: (105.0, 10.0, -48.7)
#   3: (98.3, 10.0, -52.1)
# Zone radius: 10m
```

#### Player command

| Command | Description |
|---------|-------------|
| `/lobby` | Teleport yourself to a random lobby spawn point |

### Lobby Zone

Players inside the lobby zone cannot deal or receive damage (the `OnEntityTakeDamage` hook blocks it). The zone radius is shared across all lobby spawn points — a player is considered "in the lobby zone" if they are within the configured radius of **any** lobby spawn point.

### Player Invisibility

Players waiting in the lobby are **invisible to each other**. They can only see themselves. This is enforced via the `CanNetworkTo` hook, which also hides any held items (weapons) belonging to hidden players.

---

## Arena Management

### Creating an Arena

```
/arena create "Name"   ← start a builder session
/arena setspawn1       ← stand at first spawn, run this
/arena setspawn2       ← walk to second spawn, run this
/arena save            ← saves the arena immediately
```

An optional lobby spawn and radius can be configured per-arena:

```
/arena setlobbyspawn     ← set a per-arena return-to-lobby position
/arena setlobbyradius 15 ← set the per-arena lobby zone radius (default 10 m)
```

### Editing an Existing Arena

```
/arena edit "Name"     ← loads the arena into the builder session
# Adjust any spawn points or radius
/arena save            ← updates the arena in-place (no duplicate created)
```

### All Arena Commands

| Command | Description |
|---------|-------------|
| `/arena create <name>` | Start a new arena builder session |
| `/arena edit <name>` | Load an existing arena for editing |
| `/arena setspawn1` | Set spawn 1 at your current position |
| `/arena setspawn2` | Set spawn 2 at your current position |
| `/arena setradius <r>` | Set the arena zone radius (metres) |
| `/arena setlobbyspawn` | Set per-arena lobby spawn at your position |
| `/arena setlobbyradius <r>` | Set per-arena lobby zone radius |
| `/arena save` | Save (or update) the arena |
| `/arena cancel` | Cancel the builder session without saving |
| `/arena list` | List all arenas with status and spawn coordinates |
| `/arena delete <name>` | Delete an arena (cannot delete one in use) |
| `/arena tp <name> [1\|2]` | Teleport to arena spawn 1 or 2 for inspection |
| `/arena setkit <name> <kit>` | Restrict an arena to one specific kit/mode |
| `/arena addkit <name> <kit>` | Add an allowed kit to an arena's list |
| `/arena removekit <name> <kit>` | Remove a kit from an arena's allowed list |
| `/arena clearkit <name>` | Clear kit restrictions (allow all kits) |

### Arena Design Tips

| Arena Type | Spawn Distance | Best For |
|------------|---------------|---------|
| CQC | 15–25 m | Revolver, Speargun |
| Medium | 30–50 m | AK-47, SAR |
| Long Range | 60–100 m | SAR, Bow |

Good locations: flat terrain, clear sightlines, away from monuments and player bases, symmetrical layout.  
Bad locations: steep hills, inside player bases, near NPC spawns, unbalanced cover.

### How Many Arenas Do I Need?

Each arena supports up to **5 simultaneous matches** by default (configurable).

| Server Size | Arenas | Concurrent Matches |
|-------------|--------|-------------------|
| Small (1–20 players) | 1–2 | 5–10 |
| Medium (20–50 players) | 2–4 | 10–20 |
| Large (50+ players) | 4–6 | 20–30 |

---

## 1v1 Duel System

### Starting a Match

1. Click the green **JOIN QUEUE** button (top-right corner).
2. The server matches you with another queued player.
3. A random weapon mode is selected (AK-47, SAR, Bow, Revolver — 25% each).
4. Both players are teleported to a free arena instance.
5. A short countdown begins, then the fight starts.

### Winning & Rounds

- First to kill the opponent wins the round.
- Default format: **Best of 3** rounds (configurable).
- After the match ends, both players return to the lobby.

### Leaving a Match

- Click **LEAVE** (bottom-right) or type `/leave` to forfeit.
- You are returned to the lobby immediately.

### Private Rooms

Players can create private 1v1 rooms and invite a specific opponent:

- Click **CREATE ROOM** in the lobby browser, select a kit/mode.
- Share the room with a friend who joins through the lobby browser.
- Only the two matched players can see each other in their arena instance.

---

## Aim Training

```
/aimtrain   ← start an aim training session
/leave      ← exit aim training and return to lobby
```

Shoot targets to practice accuracy. Your session is private — no other players interfere.

---

## Leaderboard & Stats

| Command | Description |
|---------|-------------|
| `/leaderboard` or `/top` | View the top 10 players |
| `/stats` | View your own win/loss record and win rate |
| `/toggleleaderboard` | Hide or show the always-visible leaderboard UI |

Stats tracked per player:
- Total matches played
- Wins / Losses
- Win rate (%)
- Rounds won / lost

---

## UI Overview

### JOIN QUEUE (top-right, green)
Always visible. Click once to join the random-mode queue.

### LEAVE (bottom-right)
Appears when you are in a queue or active match. Click to leave/forfeit and return to lobby.

### Leaderboard (top-left)
Always visible. Shows the top 10 players by win rate. Toggle with `/toggleleaderboard`.

### Win/Lose Overlay (centre)
Large coloured panel shown when a match ends — green for WIN, red for LOSE. Shows the final round score and auto-dismisses after 5 seconds.

### Lobby Browser
Shows all available private rooms, lets you create a new room or join the public queue.

---

## Permissions

| Permission | Description |
|-----------|-------------|
| `hellisplugin.use` | Access to all player commands (duel, aimtrain, stats, etc.) |
| `hellisplugin.admin` | Arena and lobby management commands |

```bash
# Grant player access
oxide.grant group default hellisplugin.use

# Grant admin access
oxide.grant user YourName hellisplugin.admin
```

---

## All Commands

### Player Commands

| Command | Description |
|---------|-------------|
| *(click JOIN QUEUE)* | Join the random-mode public queue |
| `/leave` | Leave queue or forfeit active match |
| `/lobby` | Teleport to a random lobby spawn point |
| `/aimtrain` | Start an aim training session |
| `/leaderboard` / `/top` | View top 10 leaderboard |
| `/stats` | View your personal stats |
| `/toggleleaderboard` | Toggle leaderboard UI visibility |
| `/autorequeue [on\|off]` | Auto-rejoin queue after each match |
| `/showui` | Re-render all UI elements (fixes missing UI) |
| `/help` | In-game command summary |

### Admin Commands

| Command | Description |
|---------|-------------|
| `/lobby setpos` | Set (replace) lobby spawn 1 at your position |
| `/lobby addspawn` | Add a lobby spawn at your position |
| `/lobby editspawn <n>` | Move lobby spawn `n` to your current position |
| `/lobby removespawn <n>` | Remove lobby spawn number `n` |
| `/lobby listspawns` | List all lobby spawns (shows DDraw markers) |
| `/lobby setradius <r>` | Set lobby zone radius (metres) |
| `/arena create <name>` | Start creating a new arena |
| `/arena edit <name>` | Edit an existing arena |
| `/arena setspawn1` | Set arena spawn 1 at your position |
| `/arena setspawn2` | Set arena spawn 2 at your position |
| `/arena setradius <r>` | Set arena zone radius |
| `/arena setlobbyspawn` | Set per-arena lobby spawn at your position |
| `/arena setlobbyradius <r>` | Set per-arena lobby zone radius |
| `/arena save` | Save / update the arena |
| `/arena cancel` | Cancel builder session |
| `/arena list` | List all arenas |
| `/arena delete <name>` | Delete an arena |
| `/arena tp <name> [1\|2]` | Teleport to arena spawn for testing |
| `/arena setkit <name> <kit>` | Restrict arena to one kit |
| `/arena addkit <name> <kit>` | Add kit to arena's allowed list |
| `/arena removekit <name> <kit>` | Remove kit from arena's allowed list |
| `/arena clearkit <name>` | Remove all kit restrictions from arena |
| `/kit save <name>` | Save your current inventory as a new kit/mode |
| `/kit show <name>` | Display a kit's items |
| `/kit list` | List all available kits |
| `/kit reset <name>` | Reset a built-in kit to its defaults |
| `/kit delete <name>` | Delete a custom kit |

---

## Configuration Reference

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
  "Aim Train Arena Position": { "x": -100.0, "y": 0.0, "z": -100.0 },
  "Aim Train Arena Radius": 30.0,
  "Enable Zone Enforcement": false
}
```

> **Lobby positions** are stored separately in the data file `oxide/data/HellisPlugin_Lobby.json` and are managed via `/lobby` commands — do not edit them by hand.

> **Arenas** are stored in `oxide/data/HellisPlugin_Arenas.json` — use `/arena` commands to manage them.

Key settings:

| Setting | Default | Description |
|---------|---------|-------------|
| `Best of X Rounds` | 3 | Rounds per match (odd number recommended) |
| `Countdown Duration (seconds)` | 3 | Countdown before each round starts |
| `Max Instances Per Arena` | 5 | How many simultaneous matches per arena |
| `Enable Zone Enforcement` | false | Kick players who wander outside their zone |

---

## Customising Loadouts & Kits

### In-Game Method (Recommended — no restart needed)

1. Equip yourself with the exact items you want players to receive:
   - Main/belt inventory: weapons, ammo, medical supplies
   - Wear slots: armour and clothing
2. Save as a new kit:
   ```
   /kit save Shotgun
   ```
3. The new mode appears immediately in the private room mode selector.

**Managing kits:**

```
/kit list              ← show all kit names
/kit show Shotgun      ← inspect a kit's items
/kit reset AK47        ← revert a built-in kit to defaults
/kit delete Shotgun    ← remove a custom kit
```

Kit name rules: letters, digits, underscores, dashes only; max 20 characters.

### Manual JSON Method (Requires plugin reload)

Add or edit entries in the `Loadouts` section of `HellisPlugin.json`:

```json
"Loadouts": {
  "AK47": {
    "Items": [
      { "ShortName": "rifle.ak",         "Amount": 1 },
      { "ShortName": "ammo.rifle",        "Amount": 120 },
      { "ShortName": "metal.plate.torso", "Amount": 1 },
      { "ShortName": "metal.facemask",    "Amount": 1 },
      { "ShortName": "roadsign.kilt",     "Amount": 1 },
      { "ShortName": "syringe.medical",   "Amount": 4 }
    ]
  },
  "Shotgun": {
    "Items": [
      { "ShortName": "shotgun.pump",      "Amount": 1 },
      { "ShortName": "ammo.shotgun",      "Amount": 48 },
      { "ShortName": "metal.plate.torso", "Amount": 1 },
      { "ShortName": "metal.facemask",    "Amount": 1 },
      { "ShortName": "syringe.medical",   "Amount": 3 }
    ]
  }
}
```

Then: `oxide.reload HellisPlugin`

### Built-in Kit Reference

| Kit | Weapon | Ammo | Armour | Medical |
|-----|--------|------|--------|---------|
| **AK47** | rifle.ak ×1 | ammo.rifle ×120 | metal plate torso + facemask + roadsign kilt | syringe.medical ×4 |
| **SAR** | rifle.semiauto ×1 | ammo.rifle ×96 | metal plate torso + coffee can helmet + roadsign kilt | syringe.medical ×4 |
| **Bow** | bow.hunting ×1 | arrow.wooden ×60 | hide vest | bandage ×6 |
| **Revolver** | pistol.revolver ×1 | ammo.pistol ×64 | burlap shirt + trousers | bandage ×4 |
| **Speargun** | speargun ×1 | speargun.spear ×16 | full diving suit | syringe.medical ×3 |

### Common Item Short-Names

**Weapons:** `rifle.ak` `rifle.semiauto` `bow.hunting` `pistol.revolver` `speargun` `shotgun.pump` `smg.mp5`

**Ammo:** `ammo.rifle` `ammo.pistol` `arrow.wooden` `speargun.spear` `ammo.shotgun`

**Armour:** `metal.plate.torso` `metal.facemask` `coffeecan.helmet` `roadsign.jacket` `roadsign.kilt` `heavy.plate.jacket`

**Medical:** `syringe.medical` `bandage` `largemedkit`

**Attachments:** `weapon.mod.holosight` `weapon.mod.lasersight` `weapon.mod.silencer`

Full item list: https://www.corrosionhour.com/rust-item-list/

---

## Multi-Instance Arenas

Each physical arena can host multiple 1v1 matches at the same time. Players in different instances are completely isolated — they can only see their own opponent.

### How It Works

- When two players are matched, the server finds an arena with a free instance slot.
- Multiple pairs can use the same spawn points simultaneously.
- The `CanNetworkTo` hook ensures players in different instances (and lobby players) cannot see each other.

### Configuration

```json
"Max Instances Per Arena": 5
```

- **1** → classic single-match-per-arena behaviour
- **5** → default; 3 arenas handles 15 concurrent matches
- **10** → high-capacity servers

### Checking Instance Usage

```
/arena list
```

Output example:
```
Arena 1 — 3/5 instances active
  Spawn 1: (100, 10, -50)
  Spawn 2: (120, 10, -50)
```

---

## Troubleshooting

| Problem | Solution |
|---------|---------|
| Players spawn at world origin (0,0,0) | Lobby not configured — run `/lobby setpos` in-game |
| Arena not listed in `/arena list` | Not saved yet — make sure you ran `/arena save` |
| Players fall through the floor | Spawn Y coordinate too low; use `/arena tp` to check height and re-set spawns |
| Can't delete an arena | Arena is in use — wait for the match to end |
| Players can see each other in lobby | Ensure `CanNetworkTo` hook is not blocked by another plugin |
| UI elements missing | Type `/showui` to re-render all elements |
| Match never starts | No arenas configured, or all arenas are at max instances — add more arenas |
| Kit changes not working | If using JSON method, run `oxide.reload HellisPlugin` after saving the file |

### After a Map Wipe

Arena spawn points are stored as world coordinates — they become invalid after a map wipe or seed change. After wiping:

1. Delete old arenas: `/arena delete <name>` (or clear the data file)
2. Re-create arenas at new locations: `/arena create …`
3. Re-set lobby spawns: `/lobby setpos` (and `/lobby addspawn` for extras)

---

*Hellis Plugin — built for Oxide/uMod on Rust.*
