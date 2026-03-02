# Hellis Plugin - User Guide

## Quick Start

### First Time Joining?

When you connect to the server, you'll see:
```
═══════════════════════════════════════
Welcome to Hellis Duels!
Type /duelui to show/hide the duel menu
Type /help to see all commands
═══════════════════════════════════════
```

**If you don't see the UI panel:**
1. Type `/duelui` - This shows the menu with all buttons
2. Type `/help` - See all available commands

**Auto-reminder:** If you're in lobby without the UI for 30+ seconds, you'll get a friendly reminder!

### Using the UI Panel (Recommended!)

When you join the server, a **UI panel** automatically appears on your screen.

**What you'll see:**
- Your current status (Ready, In Queue, or In Match)
- Buttons for each weapon mode (AK, SAR, Spear, Bow, Rev)
- Leave Queue button
- Stats and Leaderboard buttons
- Your current W-L record

**How to use it:**
1. Click any weapon button (AK, SAR, etc.) to join that queue
2. Click "Leave Queue" if you change your mind
3. Click "Stats" to see your detailed statistics
4. Click "Top" to view the leaderboard
5. Click "Close UI" if you want to hide it (use `/duelui` to show again)

### Joining a Duel (Command Method)
1. Type `/duel <mode>` in chat
2. Available modes: `ak`, `sar`, `speargun`, `bow`, `revolver`
3. Wait for another player to join the same queue
4. You'll be automatically matched and teleported to an arena

Example:
```
/duel ak        → Join AK-47 queue
/duel speargun  → Join Speargun queue
/duelui         → Toggle UI panel
```

### What Happens Next
1. **Teleportation**: You and your opponent teleport to a duel arena
2. **Loadout**: Both receive identical gear (weapon, armor, meds)
3. **Countdown**: 3-second countdown appears
4. **Fight**: First to kill the opponent wins the round
5. **Rounds**: Best of 3 rounds (configurable by admin)
6. **Results**: Winner/loser shown with final score

## All Commands

| Command | Description |
|---------|-------------|
| `/duel <mode>` | Join a duel queue |
| `/leave` | Leave the current queue |
| `/duelui` | Toggle UI panel (recommended!) |
| `/help` | Show all available commands |
| `/lobby` | Teleport to lobby |
| `/leaderboard` or `/top` | View top 10 players |
| `/aimtrain` | Start aim training session |
| `/stats` | View your win/loss statistics |
| `/forfeit` | Forfeit your current match |

💡 **Pro Tip**: Use `/help` anytime to see this list in-game!

## Leaderboard System

View the top players on the server!

**How to access:**
- Type `/leaderboard` or `/top` in chat
- Or click the "Top" button on the UI panel

**Requirements:**
- Must have played at least 3 matches to appear on leaderboard
- Ranked by win rate, then total wins

**What you'll see:**
```
=== LEADERBOARD - TOP 10 ===
1. PlayerName - 15W/3L (83.3%)
2. AnotherPlayer - 12W/4L (75.0%)
3. ProGamer - 20W/8L (71.4%)
...
Your Rank: #15 - 8W/5L (61.5%)
```

## Duel Modes

### AK-47 Mode (`/duel ak`)
**Loadout:**
- AK-47 rifle
- 120 rounds of 5.56 ammo
- Metal chest plate
- Metal facemask
- Roadsign kilt
- 4 medical syringes

**Best for:** Full-auto spray control, close to medium range

### Semi-Auto Rifle (`/duel sar`)
**Loadout:**
- Semi-auto rifle
- 96 rounds of 5.56 ammo
- Metal chest plate
- Coffee can helmet
- Roadsign kilt
- 4 medical syringes

**Best for:** Tap-firing accuracy, medium range

### Speargun (`/duel speargun`)
**Loadout:**
- Speargun
- 16 spears
- Full diving suit (fins, mask, tank, wetsuit)
- 3 medical syringes

**Best for:** Underwater combat, projectile aim

### Bow (`/duel bow`)
**Loadout:**
- Hunting bow
- 60 wooden arrows
- Hide vest
- 6 bandages

**Best for:** Primitive combat, arrow drop practice

### Revolver (`/duel revolver`)
**Loadout:**
- Revolver pistol
- 64 pistol rounds
- Burlap shirt and trousers
- 4 bandages

**Best for:** Quick draw, close range accuracy

## Tips & Strategies

### General Tips
- **Warming Up**: Use `/aimtrain` before dueling to warm up
- **Queue Times**: Popular modes (AK, SAR) have faster queue times
- **Medical Items**: Use your syringes/bandages during the fight!
- **Practice**: Each mode has different mechanics - practice to improve

### Combat Tips
- **Positioning**: Use the arena terrain to your advantage
- **Pacing**: Don't waste all your ammo at once
- **Healing**: Heal between rounds - you get full health reset
- **Aim for Head**: Headshots deal more damage

### Queue System
- You can only be in ONE queue at a time
- Leaving a match counts as a loss
- Stats are tracked automatically
- Auto-requeue may be enabled (check with admin)

## Understanding Stats

View your stats with `/stats`:

```
=== Your Stats ===
Matches: 25
Wins: 15
Losses: 10
Win Rate: 60.0%
Rounds Won: 47
Rounds Lost: 33
```

**What the stats mean:**
- **Matches**: Total completed matches
- **Wins**: Matches you won (best-of-X)
- **Losses**: Matches you lost
- **Win Rate**: Percentage of matches won
- **Rounds Won/Lost**: Individual rounds, not full matches

## FAQ

### Q: How long do I wait in queue?
**A:** As soon as another player joins your mode, the match starts instantly.

### Q: Can I choose my opponent?
**A:** No, matchmaking is automatic and fair (first in queue).

### Q: What if I disconnect during a match?
**A:** Your opponent wins automatically, and the match ends.

### Q: Can I practice alone?
**A:** Yes! Use `/aimtrain` to practice your aim.

### Q: Do I keep my loadout after the match?
**A:** No, all items are removed after the match. Your original inventory is restored.

### Q: Can I spectate matches?
**A:** Not in this version (may be added in future updates).

### Q: What happens if both players die at the same time?
**A:** The round is counted based on who died first (server-side detection).

### Q: Can I duel with friends?
**A:** Queue at the same time in the same mode - you might get matched!

### Q: Is there a ranking system?
**A:** Currently just win/loss tracking. Ranking may be added later.

## Improving Your Skills

### Aim Training
1. Use `/aimtrain` regularly
2. Practice with different weapons
3. Work on headshot accuracy
4. Train movement and strafing

### Duel Practice
1. Start with easier modes (Bow, Revolver)
2. Progress to harder modes (AK, SAR)
3. Learn spray patterns
4. Practice healing during combat
5. Review your stats to see progress

### Common Mistakes
- ❌ Spraying randomly (wastes ammo)
- ❌ Standing still (easy target)
- ❌ Not healing (use those syringes!)
- ❌ Panic shooting (stay calm, aim)

### Pro Tips
- ✅ Strafe while shooting
- ✅ Burst fire for accuracy
- ✅ Use terrain for cover
- ✅ Manage your ammo
- ✅ Heal between engagements

## Etiquette

- **Be Respectful**: No toxic behavior
- **Don't Forfeit Early**: Try your best
- **GG**: Say "gg" (good game) after matches
- **Learn from Losses**: Every match is practice

## Admin Commands

If you have admin permissions (`hellisplugin.admin`), you can manage arenas in-game:

### Creating Arenas

**Step-by-step process:**
1. `/arena create "Arena Name"` - Start creation
2. Move to first spawn location
3. `/arena setspawn1` - Set first spawn
4. Move to second spawn location
5. `/arena setspawn2` - Set second spawn
6. `/arena save` - Save the arena

**Example:**
```
/arena create "Rooftop Arena"
# Move to building rooftop
/arena setspawn1
# Move to opposite rooftop
/arena setspawn2
/arena save
```

### Managing Arenas

| Command | Description |
|---------|-------------|
| `/arena list` | Show all arenas and their status |
| `/arena delete "Name"` | Delete an arena |
| `/arena tp "Name" 1` | Teleport to spawn 1 |
| `/arena tp "Name" 2` | Teleport to spawn 2 |
| `/arena cancel` | Cancel current creation |

### Tips for Arena Placement

- **Distance**: 15-30m for close combat, 50-100m for long range
- **Terrain**: Flat, open areas work best
- **Obstructions**: Clear line of sight between spawns
- **Location**: Away from monuments and bases
- **Testing**: Use `/arena tp` to test before saving
- **Multiple**: Create 2-3+ arenas for concurrent matches

## Need Help?

- Ask server admins for help
- Check `/stats` to track improvement
- Practice in `/aimtrain` mode
- Review loadouts to understand gear
- Use `/arena` commands if you're admin

Good luck and have fun in the Hellis duels! 🎯
