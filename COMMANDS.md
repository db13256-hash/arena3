# Hellis Plugin - Complete Command Reference

**Version:** 1.1.0  
**Last Updated:** 2026-03-03

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

#### `/leave`
Leave the current queue, private room, or forfeit from an active match.

**Usage:**
```
/leave
```

**Permission Required:** None

**Example:**
```
Player: /leave
Server: Left the queue.
```

---

#### `/forfeit`
Forfeit the current match (if you are in one).

**Usage:**
```
/forfeit
```

**Permission Required:** None

**Example:**
```
Player: /forfeit
Server: You forfeited the match.
```

---

#### `/autorequeue [on|off]`
Toggle automatic re-queuing after a match ends.

**Usage:**
```
/autorequeue
/autorequeue on
/autorequeue off
```

**Permission Required:** None

**Description:**
- Without arguments: shows current setting
- `on` / `enable` - automatically re-join the queue after each match
- `off` / `disable` - return to lobby after each match (manual re-queue)

**Example:**
```
Player: /autorequeue on
Server: ✓ Auto-requeue ENABLED - You'll automatically rejoin queue after matches
```

---

#### `/aimtrain`
Start an aim training session.

**Usage:**
```
/aimtrain
```

**Permission Required:** `hellisplugin.use`

**Example:**
```
Player: /aimtrain
Server: Aim Training started! Hit the targets to improve your accuracy.
```

---

### Stats & Leaderboard Commands

#### `/stats`
View your personal duel statistics.

**Usage:**
```
/stats
```

**Permission Required:** None

**Shows:**
- Total matches played
- Wins and losses
- Win rate percentage
- Rounds won and lost

**Example:**
```
Player: /stats
Server:
=== Your Stats ===
Matches: 15
Wins: 10
Losses: 5
Win Rate: 66.7%
Rounds Won: 22
Rounds Lost: 18
```

---

#### `/leaderboard`
View the global leaderboard showing the top 10 players by win rate.

**Usage:**
```
/leaderboard
```

**Aliases:** `/top`

**Permission Required:** None

**Shows:**
- Top 10 players ranked by win rate
- Player name, W-L record, and win percentage
- Your own rank if not in top 10

**Example:**
```
Player: /leaderboard
Server:
=== GLOBAL LEADERBOARD - TOP 10 ===
1. PlayerOne - 15W/2L (88.2%)
2. PlayerTwo - 12W/3L (80.0%)
3. YOU - 10W/5L (66.7%)
```

---

#### `/top`
Alias for `/leaderboard`. See above for details.

---

#### `/toggleleaderboard`
Show or hide the persistent leaderboard UI in the top-left corner.

**Usage:**
```
/toggleleaderboard
```

**Permission Required:** None

**Example:**
```
Player: /toggleleaderboard
Server: Leaderboard UI hidden. Type /toggleleaderboard to show it again.
```

---

### UI Commands

#### `/showui`
Refresh all UI elements (lobby browser + leaderboard). Use this if buttons disappear.

**Usage:**
```
/showui
```

**Permission Required:** None

**Example:**
```
Player: /showui
Server: UI elements refreshed!
```

---

### Navigation Commands

#### `/lobby`
Teleport to the configured lobby spawn point.

**Usage:**
```
/lobby
```

**Permission Required:** None

**Example:**
```
Player: /lobby
Server: Welcome to the lobby!
```

---

### Help Command

#### `/help`
Display all available commands and their descriptions.

**Usage:**
```
/help
```

**Permission Required:** None

---

## Admin Commands

### Arena Management

#### `/arena`
Manage duel arenas (create, set spawns, save, list, delete, teleport, set radius).

**Permission Required:** `hellisplugin.admin`

##### `/arena create <name>`
Start creating a new arena with the given name.

**Usage:**
```
/arena create Arena1
/arena create "CQC Arena 1"
```

---

##### `/arena setspawn1`
Set spawn point 1 at your current position (during arena creation).

**Usage:**
```
/arena setspawn1
```

---

##### `/arena setspawn2`
Set spawn point 2 at your current position (during arena creation).

**Usage:**
```
/arena setspawn2
```

---

##### `/arena save`
Save the arena currently being created.

**Usage:**
```
/arena save
```

**Description:**
- Finalizes arena creation
- Saves to `HellisPlugin_Arenas` data file
- Arena becomes available for matches immediately

---

##### `/arena cancel`
Cancel the current arena creation in progress.

**Usage:**
```
/arena cancel
```

---

##### `/arena list`
List all arenas and their current instance usage.

**Usage:**
```
/arena list
```

**Example output:**
```
=== Arenas ===
Arena 1 - 2/5 instances active
  Spawn 1: (100.0, 0.0, 200.0)
  Spawn 2: (120.0, 0.0, 200.0)
  Zone Radius: 30m
  Active Instances: 0, 3
```

---

##### `/arena delete <name>`
Permanently delete an arena (must not be in use).

**Usage:**
```
/arena delete Arena1
/arena delete "CQC Arena 1"
```

---

##### `/arena tp <name> [1|2]`
Teleport to a spawn point of an arena for testing.

**Usage:**
```
/arena tp Arena1 1
/arena tp Arena1 2
```

---

##### `/arena setradius <name> <radius>`
Set the zone enforcement radius for an arena.

**Usage:**
```
/arena setradius Arena1 30
/arena setradius "CQC Arena 1" 50
```

---

### Lobby Management

#### `/lobby setpos`
Set the lobby spawn position to your current location.

**Usage:**
```
/lobby setpos
```

**Permission Required:** `hellisplugin.admin`

**Example:**
```
Admin: /lobby setpos
Server: Lobby position set to: (0.0, 10.0, 0.0)
```

---

#### `/lobby setradius <radius>`
Set the lobby zone radius.

**Usage:**
```
/lobby setradius 50
```

**Permission Required:** `hellisplugin.admin`

---

### Leaderboard Management

#### `/clearleaderboard [confirm]`
Clear all player statistics from the leaderboard.

**Usage:**
```
/clearleaderboard
/clearleaderboard confirm
```

**Permission Required:** `hellisplugin.admin`

**Description:**
- Without `confirm`: shows a warning prompt
- With `confirm`: permanently deletes all player records

**Example:**
```
Admin: /clearleaderboard confirm
Server: ✅ Leaderboard cleared! Removed 42 player records.
```

---

## Console Commands (UI)

These commands are executed automatically when players interact with UI buttons. They are not intended to be typed manually.

### Queue Buttons (Lobby Browser)

#### `joinqueue.public`
Join the public random-mode queue (AK47 / SAR / Bow / Revolver selected at random).

**Trigger:** Click the **Public — JOIN** button in the lobby browser.

---

#### `joinqueue.ak`
Join the AK47-only public queue.

**Trigger:** Click the **Public AK — JOIN** button.

---

#### `joinqueue.bow`
Join the Bow-only public queue.

**Trigger:** Click the **Public Bow — JOIN** button.

---

#### `joinqueue.spear`
Join the Speargun-only public queue (only visible when Speargun mode is enabled).

**Trigger:** Click the **Speargun — JOIN** button.

---

#### `joinqueue.click`
Join the random-mode queue via the legacy JOIN QUEUE button (Any mode).

**Trigger:** Click the legacy **JOIN QUEUE** button (top-right, if visible).

---

### Leave Button

#### `leavebutton.click`
Leave the current queue or forfeit from an active match.

**Trigger:** Click the **leave** button (bottom-right, visible while in queue or match).

---

### Private Room Buttons

#### `lobby.createroom`
Create a new private room named after your account.

**Trigger:** Click **CREATE ROOM +** in the lobby browser.

---

#### `lobby.requestjoin <roomID>`
Send a join request to a private room.

**Trigger:** Click the **REQUEST** button next to a room listing.

---

#### `lobby.joinroom <roomID>`
Alias for `lobby.requestjoin`. Forwards to the request flow.

---

#### `lobby.accept <playerID>`
Accept a pending join request (room owner only).

**Trigger:** Click **ACCEPT** in the join-request overlay.

---

#### `lobby.decline <playerID>`
Decline a pending join request (room owner only).

**Trigger:** Click **DECLINE** in the join-request overlay.

---

#### `lobby.leaveroom`
Leave your current private room.

**Trigger:** Click the **LEAVE** button inside a room entry.

---

#### `lobby.startmatch`
Manually trigger matchmaking in your room (room owner only).

**Trigger:** Owner action (programmatic, e.g. after accepting a player).

---

#### `lobby.roomguns`
Open the weapon-mode selector for your room (room owner only).

**Trigger:** Click the **GUNS** button inside your room entry.

---

#### `lobby.roomsetmode <mode>`
Set the weapon mode for your room.

**Parameters:**
- `mode` — one of `AK47`, `SAR`, `Bow`, `Revolver`, `Random`, `Speargun`

**Trigger:** Click a mode button inside the weapon-mode selector UI.

---

#### `lobby.closeguns`
Close the weapon-mode selector UI.

**Trigger:** Click the **CLOSE** button in the weapon-mode selector.

---

## Permissions

### Player Permissions

| Permission | Description |
|---|---|
| `hellisplugin.use` | Required to use `/aimtrain` |

All other player commands are available to everyone without any permission.

### Admin Permissions

| Permission | Required For |
|---|---|
| `hellisplugin.admin` | All `/arena` subcommands, `/lobby setpos`, `/lobby setradius`, `/clearleaderboard` |

**Granting permissions via Oxide:**
```
o.grant user <username> hellisplugin.admin
o.grant group <groupname> hellisplugin.admin
o.grant user <username> hellisplugin.use
o.grant group default hellisplugin.use
```

**Revoking permissions:**
```
o.revoke user <username> hellisplugin.admin
o.revoke group <groupname> hellisplugin.admin
```

---

## Quick Examples

### For Players

**Join a random match:**
```
Click the Public — JOIN button in the lobby browser (right side of screen)
```

**Join AK47-only queue:**
```
Click the Public AK — JOIN button
```

**Create a private room:**
```
Click CREATE ROOM + at the bottom of the lobby browser
(Others can then REQUEST to join your room)
```

**Check your stats:**
```
/stats
```

**View top players:**
```
/leaderboard
```

**Return to lobby:**
```
/lobby
```

**Toggle auto-requeue:**
```
/autorequeue on
/autorequeue off
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

**Clear all stats:**
```
/clearleaderboard confirm
```

---

## Troubleshooting

### "I don't see the lobby browser or leaderboard"
```
/showui
```

### "Command not working"
- Check spelling
- Ensure you are not in an active match (some commands are blocked during matches)
- Verify you have the required permission (for admin commands)

### "Can't create arena"
- Ensure you have the `hellisplugin.admin` permission
- Follow all steps: `/arena create` → `/arena setspawn1` → `/arena setspawn2` → `/arena save`

---

## Summary

**Chat Commands:** 14 total
- **Player Commands:** 11 (`/leave`, `/forfeit`, `/autorequeue`, `/aimtrain`, `/stats`, `/leaderboard`, `/top`, `/toggleleaderboard`, `/showui`, `/lobby`, `/help`)
- **Admin Commands:** 12 subcommands (`/arena create/setspawn1/setspawn2/save/cancel/list/delete/tp/setradius`, `/lobby setpos/setradius`, `/clearleaderboard`)

**Console Commands:** 16 (UI internal, triggered by button clicks)

**For Complete Documentation:**
- See `README.md` for overview and configuration
- See `ADMIN_GUIDE.md` for arena management guide
- See `INSTALLATION.md` for setup instructions

---

**Need Help?** Type `/help` in-game for a quick command reference!
