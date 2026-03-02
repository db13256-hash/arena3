# Hellis Plugin - Installation Guide

## Prerequisites
- Rust game server
- Oxide mod installed and running
- Server admin/owner access

## Installation Steps

### 1. Download the Plugin
Download `HellisPlugin.cs` from this repository.

### 2. Install on Server
1. Locate your Rust server's Oxide directory
2. Navigate to `oxide/plugins/`
3. Copy `HellisPlugin.cs` into the plugins folder
4. The plugin will auto-load (Oxide watches for new files)

### 3. Verify Installation
Check your server console for:
```
Hellis Plugin v1.0.0 loaded - Hellis Server
```

### 4. Configure the Plugin
1. After first load, Oxide creates `oxide/config/HellisPlugin.json`
2. Stop your server
3. Edit the configuration file (see Configuration section below)
4. Start your server

### 5. Set Permissions
Grant players access to use the plugin:

```
oxide.grant group default hellisplugin.use
```

Or grant to individual players:
```
oxide.grant user PlayerName hellisplugin.use
```

## Configuration

Edit `oxide/config/HellisPlugin.json`:

### Basic Settings
```json
{
  "Server Name": "My Hellis Server",
  "Enable Aim Training": true,
  "Enable 1v1 Duels": true,
  "Enable Speargun Mode": true,
  "Countdown Duration (seconds)": 3,
  "Best of X Rounds": 3,
  "Auto Requeue After Match": false
}
```

### Arena Setup

**NEW: You can now create arenas in-game!** (Recommended method)

#### Method 1: In-Game Creation (Recommended)

Requires `hellisplugin.admin` permission.

1. Join your server as admin
2. Find a good location for the first spawn point
3. Use `/arena create "Arena Name"` to start
4. Use `/arena setspawn1` to set first spawn
5. Move to the second spawn location
6. Use `/arena setspawn2` to set second spawn
7. Use `/arena save` to save the arena

The arena is immediately available for duels! No server restart needed.

**Example:**
```
/arena create "CQC Arena 1"
/arena setspawn1        # At first spawn location
/arena setspawn2        # At second spawn location
/arena save             # Done!
/arena list             # Verify it's there
```

#### Method 2: Manual Configuration (Old method)

**IMPORTANT**: You must set arena coordinates for your map!

1. Join your server
2. Find good locations for duel arenas (flat, clear areas)
3. Stand at spawn point 1, type `/pos` to get coordinates
4. Walk to spawn point 2, type `/pos` again
5. Update the config file with these coordinates

Example arena configuration:
```json
"Duel Arena Positions": [
  {
    "Name": "Arena 1",
    "Spawn1": { "x": 100.5, "y": 10.0, "z": -50.2 },
    "Spawn2": { "x": 120.5, "y": 10.0, "z": -50.2 }
  }
]
```

**Tips for Arena Placement:**
- Choose flat, open areas
- Distance between spawns: 15-30 meters for CQC, 50-100m for long range
- Ensure no obstructions between spawn points
- Place arenas away from monuments and player-built bases
- Have at least 2-3 arenas to handle multiple simultaneous matches

### Aim Training Arena
Set a location for aim training:
```json
"Aim Train Arena Position": { "x": -200.0, "y": 15.0, "z": -200.0 }
```

## Reloading the Plugin

After changing configuration:
```
oxide.reload HellisPlugin
```

Or restart the server.

## Troubleshooting

### Plugin Not Loading
- Check console for error messages
- Ensure file is named exactly `HellisPlugin.cs`
- Verify Oxide is properly installed

### Commands Not Working
- Verify permissions are set: `oxide.show perm hellisplugin.use`
- Check player has permission: `oxide.show user PlayerName`

### Players Not Teleporting
- Verify arena coordinates are correct
- Check Y coordinate is at ground level
- Ensure coordinates are on your map (check map size)

### Matches Not Starting
- Ensure at least 2 players are in the same queue
- Check that arenas are available (not all in use)
- Look for errors in console

## Upgrading

1. Stop server
2. Replace `HellisPlugin.cs` with new version
3. Start server (Oxide will auto-reload)
4. Check for new configuration options

## Uninstalling

1. Remove `HellisPlugin.cs` from `oxide/plugins/`
2. Optionally delete `oxide/config/HellisPlugin.json`
3. Remove permissions if desired

## Support

For issues, check:
1. Server console for errors
2. Oxide logs in `oxide/logs/`
3. GitHub issues page
