using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hellis Plugin", "Arena2", "1.1.0")]
    [Description("Arena duel plugin with random mode selection, global leaderboard, customizable loadouts, and statistics tracking")]
    class HellisPlugin : RustPlugin
    {
        #region Fields
        
        private QueueManager queueManager;
        private ArenaManager arenaManager;
        private LoadoutManager loadoutManager;
        private AimTrainManager aimTrainManager;
        private Dictionary<ulong, ActiveMatch> activeMatches = new Dictionary<ulong, ActiveMatch>();
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        private Dictionary<ulong, ArenaBuilder> arenaBuilders = new Dictionary<ulong, ArenaBuilder>();
        private HashSet<ulong> defeatedThisTick = new HashSet<ulong>(); // Track defeated players to prevent race conditions
        private HashSet<ulong> activeLeaderboardUIs = new HashSet<ulong>(); // Track players with leaderboard UI
        private HashSet<ulong> activeJoinButtons = new HashSet<ulong>(); // Track players with join button
        private HashSet<ulong> activeLeaveButtons = new HashSet<ulong>(); // Track players with leave button
        private HashSet<ulong> autoRequeueOptOut = new HashSet<ulong>(); // Track players who opted out of auto-requeue
        private List<ArenaConfig> arenas = new List<ArenaConfig>(); // Arena storage (stored in data file, not config)
        
        // Lobby browser system
        private Dictionary<QueueType, List<ulong>> queuesByType = new Dictionary<QueueType, List<ulong>>();
        private Dictionary<string, PrivateRoom> privateRooms = new Dictionary<string, PrivateRoom>();
        
        #endregion
        
        #region Configuration
        
        private Configuration config;
        
        // Configuration class - stores all server settings
        // Note: Player data is stored in HellisPlugin_PlayerData
        // Note: Arena data is stored in HellisPlugin_Arenas
        // Note: Loadouts can be customized per weapon mode (see ADMIN_GUIDE.md)
        public class Configuration
        {
            [JsonProperty("Server Name")]
            public string ServerName = "Hellis Server";
            
            [JsonProperty("Enable Aim Training")]
            public bool EnableAimTraining = true;
            
            [JsonProperty("Enable 1v1 Duels")]
            public bool EnableDuels = true;
            
            [JsonProperty("Enable Speargun Mode")]
            public bool EnableSpeargun = true;
            
            [JsonProperty("Countdown Duration (seconds)")]
            public int CountdownDuration = 3;
            
            [JsonProperty("Best of X Rounds")]
            public int BestOfRounds = 3;
            
            [JsonProperty("Auto Requeue After Match")]
            public bool AutoRequeue = true;
            
            [JsonProperty("Max Instances Per Arena")]
            public int MaxInstancesPerArena = 5;
            
            // NOTE: Lobby configuration has been moved to ArenaConfig (HellisPlugin_Arenas data file)
            // NOTE: Duel arenas are now stored in HellisPlugin_Arenas data file, not in config
            
            [JsonProperty("Aim Train Arena Position")]
            public Vector3 AimTrainArena = new Vector3(-100, 0, -100);
            
            [JsonProperty("Aim Train Arena Radius")]
            public float AimTrainArenaRadius = 30f;
            
            [JsonProperty("Enable Zone Enforcement")]
            public bool EnableZoneEnforcement = false;
            
            [JsonProperty("Leaderboard Time Window (minutes)")]
            public int LeaderboardTimeWindowMinutes = 5;
            
            [JsonProperty("UI Auto-Refresh Interval (seconds, 0 = disabled)")]
            public int UIRefreshInterval = 30;
            
            [JsonProperty("Loadouts")]
            public Dictionary<string, LoadoutConfig> Loadouts = GetDefaultLoadouts();
            
            private static Dictionary<string, LoadoutConfig> GetDefaultLoadouts()
            {
                return new Dictionary<string, LoadoutConfig>
                {
                    ["AK47"] = new LoadoutConfig
                    {
                        Items = new List<LoadoutItemConfig>
                        {
                            new LoadoutItemConfig { ShortName = "rifle.ak", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "ammo.rifle", Amount = 120 },
                            new LoadoutItemConfig { ShortName = "metal.plate.torso", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "metal.facemask", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "roadsign.kilt", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "syringe.medical", Amount = 4 }
                        }
                    },
                    ["SAR"] = new LoadoutConfig
                    {
                        Items = new List<LoadoutItemConfig>
                        {
                            new LoadoutItemConfig { ShortName = "rifle.semiauto", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "ammo.rifle", Amount = 96 },
                            new LoadoutItemConfig { ShortName = "metal.plate.torso", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "coffeecan.helmet", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "roadsign.kilt", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "syringe.medical", Amount = 4 }
                        }
                    },
                    ["Speargun"] = new LoadoutConfig
                    {
                        Items = new List<LoadoutItemConfig>
                        {
                            new LoadoutItemConfig { ShortName = "speargun", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "speargun.spear", Amount = 16 },
                            new LoadoutItemConfig { ShortName = "diving.fins", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "diving.mask", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "diving.tank", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "diving.wetsuit", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "syringe.medical", Amount = 3 }
                        }
                    },
                    ["Bow"] = new LoadoutConfig
                    {
                        Items = new List<LoadoutItemConfig>
                        {
                            new LoadoutItemConfig { ShortName = "bow.hunting", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "arrow.wooden", Amount = 60 },
                            new LoadoutItemConfig { ShortName = "attire.hide.vest", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "bandage", Amount = 6 }
                        }
                    },
                    ["Revolver"] = new LoadoutConfig
                    {
                        Items = new List<LoadoutItemConfig>
                        {
                            new LoadoutItemConfig { ShortName = "pistol.revolver", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "ammo.pistol", Amount = 64 },
                            new LoadoutItemConfig { ShortName = "burlap.shirt", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "burlap.trousers", Amount = 1 },
                            new LoadoutItemConfig { ShortName = "bandage", Amount = 4 }
                        }
                    }
                };
            }
        }
        
        // Loadout configuration for a specific weapon mode
        // Each mode (AK47, SAR, Bow, Revolver, Speargun) has its own loadout
        // Customize items, amounts, armor, and attachments via config (see ADMIN_GUIDE.md)
        public class LoadoutConfig
        {
            [JsonProperty("Items")]
            public List<LoadoutItemConfig> Items = new List<LoadoutItemConfig>();
        }
        
        // Individual item in a loadout
        // ShortName: Rust item shortname (e.g., "rifle.ak", "ammo.rifle", "metal.plate.torso")
        // Amount: Quantity to give player
        public class LoadoutItemConfig
        {
            [JsonProperty("ShortName")]
            public string ShortName;
            
            [JsonProperty("Amount")]
            public int Amount;
            
            [JsonProperty("SkinID")]
            public ulong SkinID = 0;
        }
        
        // Arena configuration - defines duel arena spawn points
        // Stored in HellisPlugin_Arenas data file (not in main config)
        public class ArenaConfig
        {
            public string Name;
            public Vector3 Spawn1;
            public Vector3 Spawn2;
            
            [JsonProperty("Radius")]
            public float Radius = 30f;
            
            // Lobby configuration (moved from main config)
            public Vector3 LobbyPosition;
            public float LobbyRadius = 10f;
            public bool LobbyPositionSet;
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new Exception();
                }
            }
            catch
            {
                // Only write error config if it's not null
                if (config != null)
                {
                    Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                }
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
                SaveConfig(); // Only save when creating new default config
            }
        }
        
        protected override void LoadDefaultConfig() => config = new Configuration();
        
        protected override void SaveConfig() => Config.WriteObject(config);
        
        #endregion
        
        #region Hooks
        
        private void Init()
        {
            queueManager = new QueueManager(config.EnableSpeargun);
            arenaManager = new ArenaManager(arenas, config.MaxInstancesPerArena);
            loadoutManager = new LoadoutManager(config);
            aimTrainManager = new AimTrainManager();
            
            // Initialize lobby browser queues
            foreach (QueueType queueType in Enum.GetValues(typeof(QueueType)))
            {
                queuesByType[queueType] = new List<ulong>();
            }
            
            // Migration: Move lobby from old config to arena data (one-time)
            // This happens automatically when plugin loads with old config
            // Note: We can't access config.LobbyPosition here since it's been removed
            // Migration is handled by preserving data through ArenaConfig loading
            
            permission.RegisterPermission("hellisplugin.use", this);
            permission.RegisterPermission("hellisplugin.admin", this);
            
            // Load persistent player data
            LoadData();
            
            Puts($"Hellis Plugin v1.1.0 loaded - {config.ServerName}");
            Puts($"Multi-instance arenas enabled: {config.MaxInstancesPerArena} instances per arena");
            
            // Auto-save player data every 5 minutes
            timer.Every(300f, () => SaveData());
            
            // Refresh UI for all connected players after config loads (fixes missing buttons after reload)
            timer.Once(2f, () => RefreshAllPlayerUI());
            
            // Auto-refresh leaderboard for rolling 5-minute window (if enabled)
            if (config.UIRefreshInterval > 0)
            {
                timer.Repeat(config.UIRefreshInterval, 0, () => RefreshAllPlayerUI());
            }
            
            // Cleanup old stat events every 60 seconds to keep data manageable
            timer.Repeat(60f, 0, () => CleanupOldEvents());
            
            // Zone enforcement check every 5 seconds (if enabled)
            if (config.EnableZoneEnforcement)
            {
                timer.Every(5f, () => CheckZoneEnforcement());
                Puts("Zone enforcement enabled - players will be kept in designated zones");
            }
        }
        
        private void OnServerInitialized()
        {
            if (config.EnableAimTraining)
            {
                Puts("Aim Training mode enabled");
            }
            
            if (config.EnableDuels)
            {
                Puts("1v1 Duel mode enabled");
            }
            
            timer.Every(1f, () => ProcessMatchmaking());
        }
        
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer player && player != null)
            {
                // Check if player is in an active match
                if (activeMatches.ContainsKey(player.userID))
                {
                    // Check if player already defeated this tick (prevents multiple headshots race condition)
                    if (defeatedThisTick.Contains(player.userID))
                    {
                        // Player is already being defeated - ignore all subsequent damage
                        info.damageTypes = new Rust.DamageTypeList();
                        info.DoHitEffects = false;
                        info.HitMaterial = 0;
                        return true; // Block all damage
                    }
                    
                    // Calculate if this damage would bring player below 10 HP threshold
                    float totalDamage = info?.damageTypes?.Total() ?? 0f;
                    
                    // Intercept at 10 HP threshold to prevent death screen and ensure clean reset
                    // This also prevents bleeding/poison from killing the player after the hit
                    if (player.health <= 10f || player.health - totalDamage <= 10f)
                    {
                        // Player is below/would be below 10 HP - intercept and reset!
                        var match = activeMatches[player.userID];
                        
                        // Mark player as defeated IMMEDIATELY to prevent race conditions with rapid hits
                        defeatedThisTick.Add(player.userID);
                        
                        // Cancel all damage to prevent any effects
                        info.damageTypes = new Rust.DamageTypeList();
                        info.DoHitEffects = false;
                        info.HitMaterial = 0;
                        
                        // Handle the "defeat" with full reset IMMEDIATELY (not NextTick!)
                        // This prevents race conditions where multiple headshots could kill the player
                        // before the deferred action runs
                        HandleMatchPlayerDefeat(player, match);
                        
                        // Clear the defeated flag after a short delay to allow for round reset
                        timer.Once(0.5f, () => defeatedThisTick.Remove(player.userID));
                        
                        // Prevent the damage from being applied
                        return true;
                    }
                }
            }
            
            return null; // Allow normal damage processing
        }
        
        private void HandleMatchPlayerDefeat(BasePlayer player, ActiveMatch match)
        {
            if (player == null || !player.IsConnected) return;
            
            // FULL RESET - Set health to maximum (100 HP by default)
            player.health = player.MaxHealth();
            
            // STOP ALL BLEEDING - Critical to prevent death after reset
            player.metabolism.bleeding.value = 0f;
            
            // CLEAR ALL DAMAGE OVER TIME EFFECTS
            player.metabolism.poison.value = 0f;
            player.metabolism.radiation_level.value = 0f;
            player.metabolism.radiation_poison.value = 0f;
            
            // RESET METABOLISM - Full health state
            player.metabolism.calories.value = player.metabolism.calories.max;
            player.metabolism.hydration.value = player.metabolism.hydration.max;
            player.metabolism.temperature.value = 32f; // Normal body temperature
            player.metabolism.wetness.value = 0f;
            player.metabolism.dirtyness.value = 0f;
            player.metabolism.oxygen.value = player.metabolism.oxygen.max;
            
            // Make player invisible/frozen during transition
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            
            // Clear inventory to prevent item usage during transition
            player.inventory.Strip();
            
            // Record the defeat for match logic
            match.OnPlayerDeath(player.userID);
            
            // Note: Kills/Deaths are tracked in UpdatePlayerStats based on round scores
            // to avoid double-counting
            
            // Check if round/match should end
            CheckMatchEnd(match);
        }
        
        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            // CRITICAL: Prevent death screen for match players
            // This is a failsafe in case damage interception somehow fails
            if (player != null && activeMatches.ContainsKey(player.userID))
            {
                // Player should NEVER actually die during a match due to damage interception
                // But if they somehow do, prevent the death and death screen
                var match = activeMatches[player.userID];
                
                // Record the defeat
                match.OnPlayerDeath(player.userID);
                
                // Check match end
                CheckMatchEnd(match);
                
                // PREVENT DEATH - cancel the death event and death screen
                // Returning false prevents the death from processing
                return false;
            }
            
            // Allow normal deaths for non-match players
            return null;
        }
        
        // OnPlayerRespawn hook removed - no longer needed!
        // With damage interception, players never die during matches, so they never respawn.
        // Players are frozen and reset in place instead.
        // This also prevents conflicts with other spawn plugins like SpawnCore.
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            
            // Remove from queue if queued (old system)
            queueManager.LeaveQueue(player.userID);
            
            // Remove from new queue system (Phase 3)
            LeaveQueueInternal(player, false);
            
            // Clean up private rooms (Phase 4)
            CleanupPlayerRooms(player.userID);
            
            // End match if in one
            if (activeMatches.ContainsKey(player.userID))
            {
                var match = activeMatches[player.userID];
                EndMatch(match, true);
            }
            
            // Clean up UIs
            DestroyLobbyBrowser(player); // Phase 2: Use lobby browser
            DestroyLeaderboardUI(player);
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            // Teleport to lobby and show UI after a small delay to ensure player is fully loaded
            timer.Once(2f, () =>
            {
                if (player != null && player.IsConnected)
                {
                    TeleportToLobby(player);
                    
                    // Phase 2: Show lobby browser instead of simple button
                    ShowLobbyBrowser(player);
                    
                    ShowLeaderboardUI(player); // Show persistent leaderboard (always)
                    
                    // Send welcome and instructions
                    SendReply(player, "═══════════════════════════════════════");
                    SendReply(player, "Welcome to Hellis Duels!");
                    SendReply(player, "Use the lobby browser to join or create matches!");
                    SendReply(player, "Type /help to see all commands");
                    
                    if (arenaManager.GetAllArenas().Count == 0)
                    {
                        if (permission.UserHasPermission(player.UserIDString, "hellisplugin.admin"))
                        {
                            SendReply(player, "No arenas found! Use /arena create to set up arenas");
                        }
                        else
                        {
                            SendReply(player, "No arenas available yet. Ask an admin to create some!");
                        }
                    }
                    SendReply(player, "═══════════════════════════════════════");
                }
            });
        }
        
        private void TeleportToLobby(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            if (arenaManager.IsLobbySet())
            {
                player.Teleport(arenaManager.GetLobbyPosition());
                SendReply(player, "Welcome to the lobby!");
            }
            else
            {
                SendReply(player, "⚠ Lobby not configured. Admin: use /lobby setpos to set lobby location.");
            }
        }
        
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            // Player visibility isolation for multi-instance arenas
            // Players in a match can only see their opponent
            if (entity is BasePlayer player)
            {
                // Check if the player is in an active match
                if (activeMatches.ContainsKey(player.userID))
                {
                    var match = activeMatches[player.userID];
                    
                    // Allow visibility to opponent in the same match
                    if (target.userID == match.Player1ID || target.userID == match.Player2ID)
                    {
                        return null; // Allow default behavior (visible)
                    }
                    
                    // Hide from all other players
                    return false;
                }
                
                // Check if the target is in an active match
                if (activeMatches.ContainsKey(target.userID))
                {
                    var match = activeMatches[target.userID];
                    
                    // Only show to their opponent
                    if (player.userID == match.Player1ID || player.userID == match.Player2ID)
                    {
                        return null; // Allow default behavior (visible)
                    }
                    
                    // Hide from all other players
                    return false;
                }
            }
            
            return null; // Default behavior
        }
        
        #endregion
        
        #region Commands
        
        // /duel command removed - use JOIN QUEUE button instead (always random mode)
        
        [ChatCommand("leave")]
        private void LeaveCommand(BasePlayer player, string command, string[] args)
        {
            // Handle active match (forfeit)
            if (activeMatches.ContainsKey(player.userID))
            {
                var match = activeMatches[player.userID];
                EndMatch(match, false, player.userID);
                SendReply(player, "You forfeited the match.");
                return;
            }

            // Try to leave old queue system
            bool leftOldQueue = queueManager.LeaveQueue(player.userID);

            // Try to leave Phase 3 queue system
            bool inNewQueue = GetPlayerQueueType(player.userID).HasValue;
            LeaveQueueInternal(player, false);

            if (leftOldQueue || inNewQueue)
            {
                SendReply(player, "Left the queue.");
                DestroyLeaveButton(player);
                TeleportToLobby(player);
                ShowLobbyBrowser(player);
            }
            else
            {
                SendReply(player, "You're not in queue.");
            }
        }
        
        [ChatCommand("aimtrain")]
        private void AimTrainCommand(BasePlayer player, string command, string[] args)
        {
            if (!config.EnableAimTraining)
            {
                SendReply(player, "Aim training is currently disabled.");
                return;
            }
            
            if (!permission.UserHasPermission(player.UserIDString, "hellisplugin.use"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }
            
            StartAimTraining(player);
        }
        
        [ChatCommand("stats")]
        private void StatsCommand(BasePlayer player, string command, string[] args)
        {
            if (!playerData.ContainsKey(player.userID))
            {
                SendReply(player, "No stats yet. Play some matches!");
                return;
            }
            
            var data = playerData[player.userID];
            SendReply(player, $"=== Your Stats ===\n" +
                             $"Matches: {data.TotalMatches}\n" +
                             $"Wins: {data.Wins}\n" +
                             $"Losses: {data.Losses}\n" +
                             $"Win Rate: {data.WinRate:F1}%\n" +
                             $"Rounds Won: {data.RoundsWon}\n" +
                             $"Rounds Lost: {data.RoundsLost}");
        }
        
        [ChatCommand("leaderboard")]
        private void LeaderboardCommand(BasePlayer player, string command, string[] args)
        {
            if (playerData.Count == 0)
            {
                SendReply(player, "No stats yet. Be the first to duel!");
                return;
            }
            
            // Sort by win rate, then by total wins - GLOBAL (no minimum matches)
            var topPlayers = playerData
                .OrderByDescending(p => p.Value.WinRate)
                .ThenByDescending(p => p.Value.Wins)
                .Take(10)
                .ToList();
            
            var message = "=== GLOBAL LEADERBOARD - TOP 10 ===\n";
            int rank = 1;
            foreach (var entry in topPlayers)
            {
                var playerName = covalence.Players.FindPlayerById(entry.Key.ToString())?.Name ?? "Unknown";
                var stats = entry.Value;
                message += $"{rank}. {playerName} - {stats.Wins}W/{stats.Losses}L ({stats.WinRate:F1}%)\n";
                rank++;
            }
            
            // Show player's own rank if not in top 10
            if (playerData.ContainsKey(player.userID))
            {
                var playerRank = playerData
                    .OrderByDescending(p => p.Value.WinRate)
                    .ThenByDescending(p => p.Value.Wins)
                    .ToList()
                    .FindIndex(p => p.Key == player.userID) + 1;
                
                if (playerRank > 10)
                {
                    var yourStats = playerData[player.userID];
                    message += $"\nYour Rank: #{playerRank} - {yourStats.Wins}W/{yourStats.Losses}L ({yourStats.WinRate:F1}%)";
                }
            }
            
            SendReply(player, message);
        }
        
        [ChatCommand("top")]
        private void TopCommand(BasePlayer player, string command, string[] args)
        {
            LeaderboardCommand(player, command, args);
        }
        
        [ChatCommand("toggleleaderboard")]
        private void ToggleLeaderboardCommand(BasePlayer player, string command, string[] args)
        {
            if (activeLeaderboardUIs.Contains(player.userID))
            {
                DestroyLeaderboardUI(player);
                SendReply(player, "Leaderboard UI hidden. Type /toggleleaderboard to show it again.");
            }
            else
            {
                ShowLeaderboardUI(player);
                SendReply(player, "Leaderboard UI shown. Type /toggleleaderboard to hide it.");
            }
        }
        
        [ChatCommand("showui")]
        private void ShowUICommand(BasePlayer player, string command, string[] args)
        {
            ShowLobbyBrowser(player); // Show lobby browser instead of old button
            ShowLeaderboardUI(player);
            SendReply(player, "UI elements refreshed!");
            SendReply(player, "Lobby Browser: Right side");
            SendReply(player, "Leaderboard: Top left");
            SendReply(player, "If you still don't see them, you may need to enable your cursor with F1 menu.");
        }
        
        // Helper method to refresh UI for all connected players (used after config reloads)
        private void RefreshAllPlayerUI()
        {
            int refreshed = 0;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                // Skip players in active matches (they don't see lobby UI anyway)
                if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
                    continue;
                
                ShowLobbyBrowser(player); // Show lobby browser instead of old button
                ShowLeaderboardUI(player);
                refreshed++;
            }
            if (refreshed > 0)
            {
                Puts($"Refreshed UI for {refreshed} player(s) in lobby");
            }
        }
        
        // Rolling stat calculation methods - calculate stats from events within time window
        private int GetRecentKills(PlayerData data, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return data.StatEvents.Count(e => e.Type == "Kill" && e.Timestamp >= cutoff);
        }
        
        private int GetRecentDeaths(PlayerData data, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return data.StatEvents.Count(e => e.Type == "Death" && e.Timestamp >= cutoff);
        }
        
        private int GetRecentWins(PlayerData data, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return data.StatEvents.Count(e => e.Type == "Win" && e.Timestamp >= cutoff);
        }
        
        private int GetRecentLosses(PlayerData data, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return data.StatEvents.Count(e => e.Type == "Loss" && e.Timestamp >= cutoff);
        }
        
        // Cleanup old stat events to prevent data bloat
        private void CleanupOldEvents()
        {
            var cutoff = DateTime.Now.AddMinutes(-config.LeaderboardTimeWindowMinutes);
            int totalRemoved = 0;
            
            foreach (var kvp in playerData)
            {
                int before = kvp.Value.StatEvents.Count;
                kvp.Value.StatEvents.RemoveAll(e => e.Timestamp < cutoff);
                totalRemoved += (before - kvp.Value.StatEvents.Count);
            }
            
            if (totalRemoved > 0)
            {
                Puts($"Cleaned up {totalRemoved} old stat event(s)");
                SavePlayerData();
            }
        }
        
        [ChatCommand("autorequeue")]
        private void AutoRequeueCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                // Show current status
                bool enabled = !autoRequeueOptOut.Contains(player.userID);
                SendReply(player, $"Auto-requeue is currently {(enabled ? "ENABLED" : "DISABLED")}");
                SendReply(player, "Use '/autorequeue on' or '/autorequeue off' to change");
                return;
            }
            
            string action = args[0].ToLower();
            if (action == "on" || action == "enable")
            {
                autoRequeueOptOut.Remove(player.userID);
                SendReply(player, "✓ Auto-requeue ENABLED - You'll automatically rejoin queue after matches");
            }
            else if (action == "off" || action == "disable")
            {
                autoRequeueOptOut.Add(player.userID);
                SendReply(player, "✓ Auto-requeue DISABLED - Click JOIN QUEUE manually after matches");
            }
            else
            {
                SendReply(player, "Usage: /autorequeue [on/off]");
            }
        }
        
        [ChatCommand("clearleaderboard")]
        private void ClearLeaderboardCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            // Admin check
            if (!permission.UserHasPermission(player.UserIDString, "hellisplugin.admin"))
            {
                SendReply(player, "You must be an admin to use this command.");
                return;
            }
            
            // Require confirmation
            if (args == null || args.Length == 0 || args[0] != "confirm")
            {
                SendReply(player, "⚠️ WARNING: This will clear ALL player statistics!");
                SendReply(player, "Type '/clearleaderboard confirm' to proceed.");
                return;
            }
            
            // Clear all player data
            int count = playerData.Count;
            playerData.Clear();
            SavePlayerData();
            RefreshAllPlayerUI();
            
            Puts($"{player.displayName} cleared all leaderboard data ({count} players)");
            SendReply(player, $"✅ Leaderboard cleared! Removed {count} player records.");
        }
        
        [ChatCommand("help")]
        private void HelpCommand(BasePlayer player, string command, string[] args)
        {
            SendReply(player, "═══════════════════════════════════════");
            SendReply(player, "Hellis Duels - Available Commands");
            SendReply(player, "═══════════════════════════════════════");
            SendReply(player, "");
            SendReply(player, "─── Queue Commands ───");
            SendReply(player, "Click JOIN QUEUE button (top-right) to join!");
            SendReply(player, "/leave - Leave the queue or match");
            SendReply(player, "/autorequeue [on/off] - Toggle auto-requeue after matches");
            SendReply(player, "");
            SendReply(player, "─── Arena Commands ───");
            SendReply(player, "/lobby - Teleport to lobby");
            SendReply(player, "");
            SendReply(player, "─── Stats Commands ───");
            SendReply(player, "/stats - View your statistics");
            SendReply(player, "/leaderboard or /top - View top players");
            SendReply(player, "/toggleleaderboard - Show/hide leaderboard UI");
            SendReply(player, "/showui - Refresh UI elements (if not visible)");
            SendReply(player, "");
            
            if (permission.UserHasPermission(player.UserIDString, "hellisplugin.admin"))
            {
                SendReply(player, "─── Admin Commands ───");
                SendReply(player, "/arena create <name> - Start creating arena");
                SendReply(player, "/arena setspawn1 - Set first spawn");
                SendReply(player, "/arena setspawn2 - Set second spawn");
                SendReply(player, "/arena save - Save the arena");
                SendReply(player, "/arena list - List all arenas");
                SendReply(player, "/arena delete <name> - Delete arena");
                SendReply(player, "/arena setradius <name> <radius> - Set arena zone radius");
                SendReply(player, "/lobby setpos - Set lobby position");
                SendReply(player, "/lobby setradius <radius> - Set lobby zone radius");
                SendReply(player, "/clearleaderboard - Clear all leaderboard data (requires confirm)");
                SendReply(player, "");
            }
            
            SendReply(player, "═══════════════════════════════════════");
            SendReply(player, "TIP: Click the green JOIN QUEUE button to start dueling!");
            SendReply(player, "═══════════════════════════════════════");
        }
        
        [ChatCommand("forfeit")]
        private void ForfeitCommand(BasePlayer player, string command, string[] args)
        {
            if (activeMatches.ContainsKey(player.userID))
            {
                var match = activeMatches[player.userID];
                EndMatch(match, false, player.userID);
                SendReply(player, "You forfeited the match.");
            }
            else
            {
                SendReply(player, "You're not in a match.");
            }
        }
        
        [ChatCommand("lobby")]
        private void LobbyCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                TeleportToLobby(player);
                return;
            }
            
            if (!permission.UserHasPermission(player.UserIDString, "hellisplugin.admin"))
            {
                SendReply(player, "You don't have permission to configure the lobby.");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "setpos":
                    arenaManager.SetLobby(player.transform.position, arenaManager.GetLobbyRadius());
                    SaveArenas();
                    SendReply(player, $"Lobby position set to: {player.transform.position}");
                    break;
                    
                case "setradius":
                    if (args.Length < 2)
                    {
                        SendReply(player, $"Current lobby radius: {arenaManager.GetLobbyRadius()}m\nUsage: /lobby setradius <radius>");
                        return;
                    }
                    
                    float lobbyRadius;
                    if (float.TryParse(args[1], out lobbyRadius) && lobbyRadius > 0)
                    {
                        arenaManager.SetLobby(arenaManager.GetLobbyPosition(), lobbyRadius);
                        SaveArenas();
                        SendReply(player, $"Lobby radius set to: {lobbyRadius}m");
                    }
                    else
                    {
                        SendReply(player, "Invalid radius. Please enter a positive number.");
                    }
                    break;
                    
                default:
                    SendReply(player, "Lobby Commands:\n" +
                                     "/lobby - Teleport to lobby\n" +
                                     "/lobby setpos - Set lobby position (Admin)\n" +
                                     "/lobby setradius <radius> - Set lobby zone radius (Admin)");
                    break;
            }
        }
        
        [ChatCommand("arena")]
        private void ArenaCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hellisplugin.admin"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }
            
            if (args.Length == 0)
            {
                SendReply(player, "Arena Commands:\n" +
                                 "/arena create <name> - Start creating a new arena\n" +
                                 "/arena setspawn1 - Set first spawn point\n" +
                                 "/arena setspawn2 - Set second spawn point\n" +
                                 "/arena save - Save the arena\n" +
                                 "/arena cancel - Cancel arena creation\n" +
                                 "/arena list - List all arenas\n" +
                                 "/arena delete <name> - Delete an arena\n" +
                                 "/arena setradius <name> <radius> - Set arena zone radius\n" +
                                 "/arena tp <name> [1|2] - Teleport to arena spawn");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "create":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /arena create <name>");
                        return;
                    }
                    CreateArena(player, string.Join(" ", args.Skip(1)));
                    break;
                    
                case "setspawn1":
                    SetSpawn1(player);
                    break;
                    
                case "setspawn2":
                    SetSpawn2(player);
                    break;
                    
                case "save":
                    SaveArena(player);
                    break;
                    
                case "cancel":
                    CancelArena(player);
                    break;
                    
                case "list":
                    ListArenas(player);
                    break;
                    
                case "delete":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /arena delete <name>");
                        return;
                    }
                    DeleteArena(player, string.Join(" ", args.Skip(1)));
                    break;
                    
                case "tp":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /arena tp <name> [1|2]");
                        return;
                    }
                    int spawnNum = 1;
                    string arenaName;
                    if (args.Length >= 3 && int.TryParse(args[args.Length - 1], out spawnNum))
                    {
                        // Last arg is spawn number
                        arenaName = string.Join(" ", args.Skip(1).Take(args.Length - 2));
                    }
                    else
                    {
                        // No spawn number, use all remaining args as name
                        arenaName = string.Join(" ", args.Skip(1));
                    }
                    TeleportToArena(player, arenaName, spawnNum);
                    break;
                    
                case "setradius":
                    if (args.Length < 3)
                    {
                        SendReply(player, "Usage: /arena setradius <name> <radius>");
                        return;
                    }
                    
                    string arenaNameForRadius = string.Join(" ", args.Skip(1).Take(args.Length - 2));
                    float arenaRadius;
                    
                    if (!float.TryParse(args[args.Length - 1], out arenaRadius) || arenaRadius <= 0)
                    {
                        SendReply(player, "Invalid radius. Please enter a positive number.");
                        return;
                    }
                    
                    var arenaToUpdate = arenas.FirstOrDefault(a => a.Name.Equals(arenaNameForRadius, StringComparison.OrdinalIgnoreCase));
                    if (arenaToUpdate == null)
                    {
                        SendReply(player, $"Arena '{arenaNameForRadius}' not found.");
                        return;
                    }
                    
                    arenaToUpdate.Radius = arenaRadius;
                    SaveArenas();
                    SendReply(player, $"Arena '{arenaNameForRadius}' radius set to: {arenaRadius}m");
                    break;
                    
                default:
                    SendReply(player, "Unknown command. Use /arena for help.");
                    break;
            }
        }
        
        #endregion
        
        #region Matchmaking
        
        private void ProcessMatchmaking()
        {
            // Process old queue system
            var matches = queueManager.TryMatchAll();
            
            foreach (var match in matches)
            {
                var player1 = BasePlayer.FindByID(match.Player1ID);
                var player2 = BasePlayer.FindByID(match.Player2ID);
                
                if (player1 == null || player2 == null) continue;
                
                StartDuel(player1, player2, match.Mode);
            }
            
            // Process new queue system (Phase 3)
            ProcessAllQueues();
        }
        
        private void StartDuel(BasePlayer player1, BasePlayer player2, DuelMode mode)
        {
            var arena = arenaManager.GetAvailableArena();
            if (arena == null)
            {
                SendReply(player1, "No arenas available. Please wait.");
                SendReply(player2, "No arenas available. Please wait.");
                queueManager.JoinQueue(player1.userID, player1.displayName, mode);
                queueManager.JoinQueue(player2.userID, player2.displayName, mode);
                return;
            }
            
            // Get instance ID for this match
            int instanceId = arena.GetNextInstanceId();
            if (instanceId == -1)
            {
                SendReply(player1, "No arena instances available. Please wait.");
                SendReply(player2, "No arena instances available. Please wait.");
                queueManager.JoinQueue(player1.userID, player1.displayName, mode);
                queueManager.JoinQueue(player2.userID, player2.displayName, mode);
                return;
            }
            
            var match = new ActiveMatch(player1.userID, player2.userID, mode, arena, instanceId, config.BestOfRounds);
            activeMatches[player1.userID] = match;
            activeMatches[player2.userID] = match;
            
            // Clean up any arrows from previous matches
            var arrows1 = new List<BaseEntity>();
            foreach (var child in player1.children)
            {
                if (child != null && child.ShortPrefabName != null && child.ShortPrefabName.Contains("arrow"))
                    arrows1.Add(child);
            }
            foreach (var arrow in arrows1) arrow.Kill();
            
            var arrows2 = new List<BaseEntity>();
            foreach (var child in player2.children)
            {
                if (child != null && child.ShortPrefabName != null && child.ShortPrefabName.Contains("arrow"))
                    arrows2.Add(child);
            }
            foreach (var arrow in arrows2) arrow.Kill();
            
            // Teleport players to arena
            TeleportPlayer(player1, arena.Spawn1);
            TeleportPlayer(player2, arena.Spawn2);
            
            // Apply loadouts
            GiveLoadout(player1, mode);
            GiveLoadout(player2, mode);
            
            // Hide join button and show leave button during match
            DestroyJoinButton(player1);
            DestroyJoinButton(player2);
            ShowLeaveButton(player1);
            ShowLeaveButton(player2);
            
            // Start countdown (silent - no chat messages)
            timer.Once(config.CountdownDuration, () => StartRound(match));
        }
        
        private void StartRound(ActiveMatch match)
        {
            match.StartRound();
            
            var player1 = BasePlayer.FindByID(match.Player1ID);
            var player2 = BasePlayer.FindByID(match.Player2ID);
            
            // CRITICAL: Unfreeze players so they can move/aim/shoot
            if (player1 != null)
            {
                player1.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                player1.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
                SendReply(player1, "FIGHT!");
            }
            
            if (player2 != null)
            {
                player2.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                player2.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
                SendReply(player2, "FIGHT!");
            }
        }
        
        private void CheckMatchEnd(ActiveMatch match)
        {
            if (match.IsFinished())
            {
                EndMatch(match, false);
            }
            else if (match.IsRoundFinished())
            {
                // Start next round - reset both players without death/respawn
                var player1 = BasePlayer.FindByID(match.Player1ID);
                var player2 = BasePlayer.FindByID(match.Player2ID);
                
                if (player1 != null)
                {
                    SendReply(player1, $"Round {match.CurrentRound}/{match.BestOfRounds} - Score: {match.Player1Score}-{match.Player2Score}");
                    ResetPlayerForNextRound(player1, match.Arena.Spawn1, match.Mode);
                }
                
                if (player2 != null)
                {
                    SendReply(player2, $"Round {match.CurrentRound}/{match.BestOfRounds} - Score: {match.Player1Score}-{match.Player2Score}");
                    ResetPlayerForNextRound(player2, match.Arena.Spawn2, match.Mode);
                }
                
                timer.Once(config.CountdownDuration, () => StartRound(match));
            }
        }
        
        private void ResetPlayerForNextRound(BasePlayer player, Vector3 spawnPos, DuelMode mode)
        {
            if (player == null || !player.IsConnected) return;
            
            // Restore player from spectating/frozen state
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            
            // Reset health
            player.health = player.MaxHealth();
            
            // Teleport to spawn point
            TeleportPlayer(player, spawnPos);
            
            // Clear and give fresh loadout
            player.inventory.Strip();
            GiveLoadout(player, mode);
            
            // Reset metabolism
            player.metabolism.Reset();
        }
        
        private void EndMatch(ActiveMatch match, bool disconnect, ulong forfeitPlayerID = 0)
        {
            var player1 = BasePlayer.FindByID(match.Player1ID);
            var player2 = BasePlayer.FindByID(match.Player2ID);
            
            ulong winnerID = match.GetWinner();
            if (forfeitPlayerID != 0)
            {
                winnerID = forfeitPlayerID == match.Player1ID ? match.Player2ID : match.Player1ID;
            }
            
            // Update stats
            UpdatePlayerStats(match.Player1ID, winnerID == match.Player1ID, match);
            UpdatePlayerStats(match.Player2ID, winnerID == match.Player2ID, match);
            
            // Notify players
            if (player1 != null)
            {
                if (disconnect)
                {
                    SendReply(player1, "Match ended - opponent disconnected");
                }
                else
                {
                    bool player1Won = winnerID == match.Player1ID;
                    SendReply(player1, player1Won ? "YOU WIN!" : "YOU LOSE!");
                    SendReply(player1, $"Final Score: {match.Player1Score}-{match.Player2Score}");
                    
                    // Show win/lose UI overlay
                    ShowWinLoseUI(player1, player1Won, match.Player1Score, match.Player2Score);
                }
                
                // Return to spawn or re-queue
                ReturnPlayerToLobby(player1);
                
                if (config.AutoRequeue && !disconnect && !autoRequeueOptOut.Contains(player1.userID))
                {
                    queueManager.JoinQueue(player1.userID, player1.displayName, DuelMode.Any);
                    SendReply(player1, "✓ Auto-requeued for random match!");
                }
                else if (config.AutoRequeue && autoRequeueOptOut.Contains(player1.userID))
                {
                    SendReply(player1, "Auto-requeue disabled. Click JOIN QUEUE to play again.");
                }
            }
            
            if (player2 != null)
            {
                if (disconnect)
                {
                    SendReply(player2, "Match ended - opponent disconnected");
                }
                else
                {
                    bool player2Won = winnerID == match.Player2ID;
                    SendReply(player2, player2Won ? "YOU WIN!" : "YOU LOSE!");
                    SendReply(player2, $"Final Score: {match.Player1Score}-{match.Player2Score}");
                    
                    // Show win/lose UI overlay
                    ShowWinLoseUI(player2, player2Won, match.Player2Score, match.Player1Score);
                }
                
                ReturnPlayerToLobby(player2);
                
                if (config.AutoRequeue && !disconnect && !autoRequeueOptOut.Contains(player2.userID))
                {
                    queueManager.JoinQueue(player2.userID, player2.displayName, DuelMode.Any);
                    SendReply(player2, "✓ Auto-requeued for random match!");
                }
                else if (config.AutoRequeue && autoRequeueOptOut.Contains(player2.userID))
                {
                    SendReply(player2, "Auto-requeue disabled. Click JOIN QUEUE to play again.");
                }
            }
            
            // Release arena instance
            arenaManager.ReleaseArenaInstance(match.Arena, match.InstanceID);
            
            // Remove from active matches
            activeMatches.Remove(match.Player1ID);
            activeMatches.Remove(match.Player2ID);
            
            // Refresh UI for both players
            timer.Once(0.5f, () =>
            {
                if (player1 != null && player1.IsConnected)
                {
                    DestroyLeaveButton(player1);
                    ShowLobbyBrowser(player1); // Show lobby browser instead
                }
                if (player2 != null && player2.IsConnected)
                {
                    DestroyLeaveButton(player2);
                    ShowLobbyBrowser(player2); // Show lobby browser instead
                }
            });
            
            // Refresh leaderboard for all players after stats update
            timer.Once(1f, () => RefreshAllLeaderboards());
            
            // Save player data after match ends
            SavePlayerData();
        }
        
        #endregion
        
        #region Aim Training
        
        private void StartAimTraining(BasePlayer player)
        {
            TeleportPlayer(player, config.AimTrainArena);
            
            // Give bow and arrows for aim training
            player.inventory.Strip();
            GiveItem(player, "bow.hunting", 1);
            GiveItem(player, "arrow.wooden", 100);
            
            SendReply(player, "Aim Training started! Hit the targets to improve your accuracy.");
            
            aimTrainManager.StartSession(player.userID);
        }
        
        #endregion
        
        #region Helpers
        
        private void TeleportPlayer(BasePlayer player, Vector3 position)
        {
            if (player == null) return;
            player.Teleport(position);
        }
        
        private void ReturnPlayerToLobby(BasePlayer player)
        {
            if (player == null) return;
            
            // Restore player from any spectating/frozen state
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            
            // Reset health
            player.health = player.MaxHealth();
            
            // Clear inventory
            player.inventory.Strip();
            
            // Reset metabolism
            player.metabolism.Reset();
            
            // Clean up arrows stuck in player after bow matches
            var arrows = new List<BaseEntity>();
            foreach (var child in player.children)
            {
                if (child != null && child.ShortPrefabName != null && child.ShortPrefabName.Contains("arrow"))
                {
                    arrows.Add(child);
                }
            }
            foreach (var arrow in arrows)
            {
                arrow.Kill();
            }
            if (arrows.Count > 0)
            {
                Puts($"Removed {arrows.Count} arrow(s) from {player.displayName}");
            }
            
            // Teleport to lobby if configured, otherwise use default spawn
            if (arenaManager.IsLobbySet())
            {
                player.Teleport(arenaManager.GetLobbyPosition());
            }
            else
            {
                // Find a safe spawn point or use default
                var spawnPoint = ServerMgr.FindSpawnPoint();
                if (spawnPoint != null)
                {
                    player.Teleport(spawnPoint.pos);
                }
            }
        }
        
        private void GiveLoadout(BasePlayer player, DuelMode mode)
        {
            if (player == null) return;
            
            player.inventory.Strip();
            player.metabolism.calories.value = 500;
            player.metabolism.hydration.value = 250;
            player.health = 100;
            
            var loadout = loadoutManager.GetLoadout(mode);
            
            // Two-pass approach: Give ammo and equipment first, then weapons
            // This ensures ammo is available when weapons auto-load
            
            // First pass: Give all non-weapon items (ammo, armor, medical, etc.)
            foreach (var loadoutItem in loadout.Items)
            {
                var itemDef = ItemManager.FindItemDefinition(loadoutItem.ShortName);
                if (itemDef != null && itemDef.category != ItemCategory.Weapon)
                {
                    GiveItem(player, loadoutItem.ShortName, loadoutItem.Amount, loadoutItem.SkinID);
                }
            }
            
            // Second pass: Give weapons (ammo is now available for auto-loading)
            foreach (var loadoutItem in loadout.Items)
            {
                var itemDef = ItemManager.FindItemDefinition(loadoutItem.ShortName);
                if (itemDef != null && itemDef.category == ItemCategory.Weapon)
                {
                    GiveItem(player, loadoutItem.ShortName, loadoutItem.Amount, loadoutItem.SkinID);
                }
            }
        }
        
        private void GiveItem(BasePlayer player, string shortName, int amount, ulong skinID = 0)
        {
            var item = ItemManager.CreateByName(shortName, amount, skinID);
            if (item != null)
            {
                // Check if item is wearable and auto-equip it
                if (item.info.category == ItemCategory.Attire)
                {
                    // Move to wear container (auto-equips)
                    if (!item.MoveToContainer(player.inventory.containerWear, -1, true))
                    {
                        // If wear slot is full, put in main inventory
                        player.GiveItem(item);
                    }
                }
                else
                {
                    // Not wearable, put in main inventory
                    player.GiveItem(item);
                    
                    // Auto-load weapons with ammo
                    if (item.info.category == ItemCategory.Weapon)
                    {
                        var held = item.GetHeldEntity() as BaseProjectile;
                        if (held != null)
                        {
                            // Find ammo in player's inventory
                            var ammoType = held.primaryMagazine.ammoType;
                            if (ammoType != null)
                            {
                                var ammoItem = player.inventory.FindItemByItemID(ammoType.itemid);
                                if (ammoItem != null && ammoItem.amount > 0)
                                {
                                    // Load the weapon to full capacity
                                    int ammoNeeded = held.primaryMagazine.capacity;
                                    int ammoToLoad = Math.Min(ammoNeeded, ammoItem.amount);
                                    
                                    held.primaryMagazine.contents = ammoToLoad;
                                    ammoItem.UseItem(ammoToLoad);
                                    held.SendNetworkUpdateImmediate();
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private DuelMode ParseDuelMode(string mode)
        {
            switch (mode.ToLower())
            {
                case "ak":
                case "ak47":
                    return DuelMode.AK47;
                case "sar":
                case "semiauto":
                    return DuelMode.SAR;
                case "speargun":
                case "spear":
                    return DuelMode.Speargun;
                case "bow":
                    return DuelMode.Bow;
                case "revolver":
                case "rev":
                    return DuelMode.Revolver;
                default:
                    return DuelMode.None;
            }
        }
        
        private void UpdatePlayerStats(ulong playerID, bool won, ActiveMatch match)
        {
            if (!playerData.ContainsKey(playerID))
            {
                playerData[playerID] = new PlayerData();
            }
            
            var data = playerData[playerID];
            data.TotalMatches++;
            data.LastMatchTime = DateTime.Now; // Update timestamp for leaderboard filtering
            
            if (won)
            {
                data.Wins++;
                data.StatEvents.Add(new StatEvent { Type = "Win", Timestamp = DateTime.Now });
            }
            else
            {
                data.Losses++;
                data.StatEvents.Add(new StatEvent { Type = "Loss", Timestamp = DateTime.Now });
            }
            
            if (playerID == match.Player1ID)
            {
                data.RoundsWon += match.Player1Score;
                data.RoundsLost += match.Player2Score;
                data.Kills += match.Player1Score;  // Keep legacy field for compatibility
                data.Deaths += match.Player2Score;
                
                // Record individual kill events (one per round won)
                for (int i = 0; i < match.Player1Score; i++)
                    data.StatEvents.Add(new StatEvent { Type = "Kill", Timestamp = DateTime.Now });
                
                // Record individual death events (one per round lost)
                for (int i = 0; i < match.Player2Score; i++)
                    data.StatEvents.Add(new StatEvent { Type = "Death", Timestamp = DateTime.Now });
            }
            else
            {
                data.RoundsWon += match.Player2Score;
                data.RoundsLost += match.Player1Score;
                data.Kills += match.Player2Score;
                data.Deaths += match.Player1Score;
                
                // Record individual kill events (one per round won)
                for (int i = 0; i < match.Player2Score; i++)
                    data.StatEvents.Add(new StatEvent { Type = "Kill", Timestamp = DateTime.Now });
                
                // Record individual death events (one per round lost)
                for (int i = 0; i < match.Player1Score; i++)
                    data.StatEvents.Add(new StatEvent { Type = "Death", Timestamp = DateTime.Now });
            }
            
            data.WinRate = (float)data.Wins / data.TotalMatches * 100f;
        }
        
        private void UpdateWinRate(ulong playerID)
        {
            if (playerData.ContainsKey(playerID))
            {
                var data = playerData[playerID];
                if (data.TotalMatches > 0)
                {
                    data.WinRate = (float)data.Wins / data.TotalMatches * 100f;
                }
                else
                {
                    data.WinRate = 0f;
                }
            }
        }
        
        #endregion
        
        #region Zone Management
        
        private bool IsInLobbyZone(BasePlayer player)
        {
            if (player == null || !arenaManager.IsLobbySet()) return true;
            
            float distance = Vector3.Distance(player.transform.position, arenaManager.GetLobbyPosition());
            return distance <= arenaManager.GetLobbyRadius();
        }
        
        private bool IsInArenaZone(BasePlayer player, Arena arena)
        {
            if (player == null || arena == null) return true;
            
            // Calculate center point between two spawns
            Vector3 arenaCenter = (arena.Spawn1 + arena.Spawn2) / 2;
            
            float distance = Vector3.Distance(player.transform.position, arenaCenter);
            return distance <= arena.Radius;
        }
        
        private bool IsInAimTrainZone(BasePlayer player)
        {
            if (player == null) return true;
            
            float distance = Vector3.Distance(player.transform.position, config.AimTrainArena);
            return distance <= config.AimTrainArenaRadius;
        }
        
        private void CheckZoneEnforcement()
        {
            if (!config.EnableZoneEnforcement) return;
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                // Check if player is in an active match
                if (activeMatches.ContainsKey(player.userID))
                {
                    var match = activeMatches[player.userID];
                    var arena = match.Arena;
                    
                    if (arena != null && !IsInArenaZone(player, arena))
                    {
                        // Player left arena during match - teleport back to arena center
                        Vector3 arenaCenter = (arena.Spawn1 + arena.Spawn2) / 2;
                        player.Teleport(arenaCenter);
                        SendReply(player, "You left the arena zone and have been teleported back!");
                    }
                }
                else if (!queueManager.IsQueued(player.userID))
                {
                    // Player is not in match or queue - should be in lobby
                    if (!IsInLobbyZone(player))
                    {
                        TeleportToLobby(player);
                        SendReply(player, "You left the lobby zone and have been teleported back!");
                    }
                }
            }
        }
        
        #endregion
        
        #region UI Management
        
        
        #region UI - Join Queue Button
        
        private void ShowJoinButton(BasePlayer player)
        {
            if (player == null) return;
            
            DestroyJoinButton(player); // Clean up any existing button
            
            var elements = new CuiElementContainer();
            
            // Main button panel - top right corner, green for visibility
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.6 0.2 0.9" }, // Green
                RectTransform = { AnchorMin = "0.85 0.90", AnchorMax = "0.98 0.98" },
                CursorEnabled = false  // Don't capture cursor - allows camera movement
            }, "Hud", "JoinQueueButton");
            
            // Title
            elements.Add(new CuiLabel
            {
                Text = { Text = "JOIN QUEUE", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
            }, "JoinQueueButton");
            
            // Subtitle
            elements.Add(new CuiLabel
            {
                Text = { Text = "(Random Mode)", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.9 0.9 0.9 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5" }
            }, "JoinQueueButton");
            
            // Clickable button
            elements.Add(new CuiButton
            {
                Button = { Command = "joinqueue.click", Color = "0 0 0 0" }, // Transparent overlay
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, "JoinQueueButton");
            
            CuiHelper.AddUi(player, elements);
            activeJoinButtons.Add(player.userID);
            
            Puts($"Showed join button to {player.displayName} ({player.userID})");
        }
        
        private void DestroyJoinButton(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "JoinQueueButton");
            activeJoinButtons.Remove(player.userID);
        }
        
        private void ShowLeaveButton(BasePlayer player)
        {
            if (player == null) return;
            if (activeLeaveButtons.Contains(player.userID)) return;
            
            var elements = new CuiElementContainer();
            
            // Main button panel - bottom right area (slightly left to avoid health UI), orange/red for leave action
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.8 0.3 0.2 0.9" }, // Orange/Red
                RectTransform = { AnchorMin = "0.70 0.02", AnchorMax = "0.83 0.10" },
                CursorEnabled = false  // Don't capture cursor
            }, "Hud", "LeaveButton");
            
            // Title
            elements.Add(new CuiLabel
            {
                Text = { Text = "LEAVE", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
            }, "LeaveButton");
            
            // Subtitle
            elements.Add(new CuiLabel
            {
                Text = { Text = "Match/Queue", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.9 0.9 0.9 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5" }
            }, "LeaveButton");
            
            // Clickable button
            elements.Add(new CuiButton
            {
                Button = { Command = "leavebutton.click", Color = "0 0 0 0" }, // Transparent overlay
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, "LeaveButton");
            
            CuiHelper.AddUi(player, elements);
            activeLeaveButtons.Add(player.userID);
        }
        
        private void DestroyLeaveButton(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "LeaveButton");
            activeLeaveButtons.Remove(player.userID);
        }
        
        // ============================================
        // PHASE 2: LOBBY BROWSER UI
        // ============================================
        
        private void ShowLobbyBrowser(BasePlayer player)
        {
            if (player == null) return;
            
            DestroyLobbyBrowser(player); // Clean up any existing UI
            
            var elements = new CuiElementContainer();
            
            // Main panel - right side, dark gray background
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.17 0.17 0.17 0.95" }, // Dark gray #2B2B2B
                RectTransform = { AnchorMin = "0.70 0.15", AnchorMax = "0.98 0.85" },
                CursorEnabled = true
            }, "Overlay", "LobbyBrowser");
            
            // Header: "TEAM MATCHES" - Cyan
            elements.Add(new CuiLabel
            {
                Text = { Text = "TEAM MATCHES", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0 0.8 0.82 1" }, // Cyan #00CED1
                RectTransform = { AnchorMin = "0.05 0.92", AnchorMax = "0.95 0.98" }
            }, "LobbyBrowser", "LobbyBrowser.Header");
            
            // Mode indicator - Top left, smaller text
            elements.Add(new CuiLabel
            {
                Text = { Text = "1v1 Mode", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0 0.8 0.82 1" },
                RectTransform = { AnchorMin = "0.05 0.88", AnchorMax = "0.40 0.92" }
            }, "LobbyBrowser");
            
            // ========== PUBLIC SECTION ==========
            float publicStartY = 0.78f;
            
            // "PUBLIC" header
            elements.Add(new CuiLabel
            {
                Text = { Text = "PUBLIC", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = $"0.05 {publicStartY}", AnchorMax = $"0.95 {publicStartY + 0.05f}" }
            }, "LobbyBrowser");
            
            // Separator line under PUBLIC header
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0.8 0.82 0.3" },
                RectTransform = { AnchorMin = $"0.05 {publicStartY - 0.005f}", AnchorMax = $"0.95 {publicStartY}" }
            }, "LobbyBrowser");
            
            // Public queue items
            float queueY = publicStartY - 0.08f;
            
            // Public (General) queue
            int publicCount = queuesByType.ContainsKey(QueueType.Public) ? queuesByType[QueueType.Public].Count : 0;
            AddQueueEntry(elements, "LobbyBrowser", "Public", $"({publicCount} Players)", queueY, "joinqueue.public");
            
            // Public AK queue
            queueY -= 0.08f;
            int akCount = queuesByType.ContainsKey(QueueType.PublicAK) ? queuesByType[QueueType.PublicAK].Count : 0;
            AddQueueEntry(elements, "LobbyBrowser", "Public AK", $"({akCount} Players)", queueY, "joinqueue.ak");
            
            // Public SAR queue
            queueY -= 0.08f;
            int sarCount = queuesByType.ContainsKey(QueueType.PublicSAR) ? queuesByType[QueueType.PublicSAR].Count : 0;
            AddQueueEntry(elements, "LobbyBrowser", "Public SAR", $"({sarCount} Players)", queueY, "joinqueue.sar");
            
            // Public Bow queue
            queueY -= 0.08f;
            int bowCount = queuesByType.ContainsKey(QueueType.PublicBow) ? queuesByType[QueueType.PublicBow].Count : 0;
            AddQueueEntry(elements, "LobbyBrowser", "Public Bow", $"({bowCount} Players)", queueY, "joinqueue.bow");
            
            // Public Revolver queue
            queueY -= 0.08f;
            int revCount = queuesByType.ContainsKey(QueueType.PublicRevolver) ? queuesByType[QueueType.PublicRevolver].Count : 0;
            AddQueueEntry(elements, "LobbyBrowser", "Public Revolver", $"({revCount} Players)", queueY, "joinqueue.rev");
            
            // Public Speargun queue (shown only when enabled)
            if (config.EnableSpeargun)
            {
                queueY -= 0.08f;
                int spearCount = queuesByType.ContainsKey(QueueType.PublicSpeargun) ? queuesByType[QueueType.PublicSpeargun].Count : 0;
                AddQueueEntry(elements, "LobbyBrowser", "Speargun", $"({spearCount} Players)", queueY, "joinqueue.spear");
            }
            
            // ========== PRIVATE ROOMS SECTION ==========
            // Calculate available space: clamp so private section doesn't collide with queue entries
            float privateStartY = Math.Max(queueY - 0.06f, 0.20f);
            
            // "PRIVATE ROOMS" header
            elements.Add(new CuiLabel
            {
                Text = { Text = "PRIVATE ROOMS", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = $"0.05 {privateStartY}", AnchorMax = $"0.95 {privateStartY + 0.05f}" }
            }, "LobbyBrowser");
            
            // Separator line under PRIVATE ROOMS header
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0.8 0.82 0.3" },
                RectTransform = { AnchorMin = $"0.05 {privateStartY - 0.005f}", AnchorMax = $"0.95 {privateStartY}" }
            }, "LobbyBrowser");
            
            // Private room listings — cap at 2 entries to keep CREATE ROOM button visible
            float roomY = privateStartY - 0.08f;
            int roomCount = 0;
            foreach (var room in privateRooms.Values.Take(2))
            {
                AddRoomEntry(elements, "LobbyBrowser", room, roomY);
                roomY -= 0.07f;
                roomCount++;
            }
            
            // If no rooms, show message
            if (roomCount == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text = { Text = "No private rooms available", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
                    RectTransform = { AnchorMin = $"0.05 {roomY}", AnchorMax = $"0.95 {roomY + 0.06f}" }
                }, "LobbyBrowser");
            }
            
            // "CREATE ROOM +" button at bottom
            float createButtonY = 0.05f;
            elements.Add(new CuiButton
            {
                Button = { Command = "lobby.createroom", Color = "0 0.8 0.82 0.8" }, // Cyan button
                RectTransform = { AnchorMin = $"0.10 {createButtonY}", AnchorMax = $"0.90 {createButtonY + 0.06f}" },
                Text = { Text = "CREATE ROOM +", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "LobbyBrowser");
            
            CuiHelper.AddUi(player, elements);
        }
        
        private void AddQueueEntry(CuiElementContainer elements, string parent, string queueName, string playerCount, float yPos, string command)
        {
            // Background panel for queue entry
            string entryName = $"{parent}.Queue.{queueName.Replace(" ", "")}";
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 0.8" }, // Slightly darker background
                RectTransform = { AnchorMin = $"0.05 {yPos}", AnchorMax = $"0.95 {yPos + 0.07f}" }
            }, parent, entryName);
            
            // Queue name (top half of the entry)
            elements.Add(new CuiLabel
            {
                Text = { Text = queueName, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0.50", AnchorMax = "0.65 1" }
            }, entryName);
            
            // Player count (bottom half of the entry)
            elements.Add(new CuiLabel
            {
                Text = { Text = playerCount, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.05 0", AnchorMax = "0.65 0.50" }
            }, entryName);
            
            // JOIN button
            elements.Add(new CuiButton
            {
                Button = { Command = command, Color = "0 0.8 0.82 1" }, // Cyan
                RectTransform = { AnchorMin = "0.70 0.15", AnchorMax = "0.95 0.85" },
                Text = { Text = "JOIN", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, entryName);
        }
        
        private void AddRoomEntry(CuiElementContainer elements, string parent, PrivateRoom room, float yPos)
        {
            // Background panel for room entry
            string entryName = $"{parent}.Room.{room.RoomID}";
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 0.8" },
                RectTransform = { AnchorMin = $"0.05 {yPos}", AnchorMax = $"0.95 {yPos + 0.06f}" }
            }, parent, entryName);
            
            // Room info: "(X) PlayerName"
            string roomText = $"({room.PlayerIDs.Count}) {room.OwnerName}";
            elements.Add(new CuiLabel
            {
                Text = { Text = roomText, FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0", AnchorMax = "0.60 1" }
            }, entryName);
            
            // JOIN button
            elements.Add(new CuiButton
            {
                Button = { Command = $"lobby.joinroom {room.RoomID}", Color = "0 0.8 0.82 1" },
                RectTransform = { AnchorMin = "0.70 0.15", AnchorMax = "0.95 0.85" },
                Text = { Text = "JOIN", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, entryName);
        }
        
        private void DestroyLobbyBrowser(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "LobbyBrowser");
        }
        
        // ============================================
        // END PHASE 2
        // ============================================
        
        [ConsoleCommand("leavebutton.click")]
        private void LeaveButtonClickCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            // Check if player is in queue
            if (queueManager.IsQueued(player.userID))
            {
                queueManager.LeaveQueue(player.userID);
                SendReply(player, "You left the queue.");
                DestroyLeaveButton(player);
                TeleportToLobby(player);
                return;
            }
            
            // Check if player is in active match
            if (activeMatches.ContainsKey(player.userID))
            {
                var match = activeMatches[player.userID];
                
                // Determine opponent
                ulong opponentID = (match.Player1ID == player.userID) ? match.Player2ID : match.Player1ID;
                var opponent = BasePlayer.FindByID(opponentID);
                
                // Player forfeits - opponent wins
                SendReply(player, "You forfeited the match.");
                
                if (opponent != null && opponent.IsConnected)
                {
                    SendReply(opponent, "Opponent forfeited. YOU WIN!");
                }
                
                // Update winner stats (opponent) - ensure data exists
                if (!playerData.ContainsKey(opponentID))
                {
                    playerData[opponentID] = new PlayerData();
                }
                playerData[opponentID].Wins++;
                playerData[opponentID].TotalMatches++;
                UpdateWinRate(opponentID);
                
                // Update loser stats (forfeiter) - ensure data exists
                if (!playerData.ContainsKey(player.userID))
                {
                    playerData[player.userID] = new PlayerData();
                }
                playerData[player.userID].Losses++;
                playerData[player.userID].TotalMatches++;
                UpdateWinRate(player.userID);
                
                // Clean up match
                activeMatches.Remove(player.userID);
                activeMatches.Remove(opponentID);
                match.Arena.ReleaseInstance(match.InstanceID);
                
                // Return both players to lobby
                DestroyLeaveButton(player);
                TeleportToLobby(player);
                ShowLobbyBrowser(player); // Show lobby browser instead
                
                if (opponent != null && opponent.IsConnected)
                {
                    DestroyLeaveButton(opponent);
                    TeleportToLobby(opponent);
                    ShowLobbyBrowser(opponent); // Show lobby browser instead
                }
                
                SavePlayerData();
                
                // Refresh leaderboard to show updated stats
                timer.Once(1f, () => RefreshAllLeaderboards());
                
                return;
            }
            
            // Not in queue or match - just send to lobby
            DestroyLeaveButton(player);
            TeleportToLobby(player);
        }
        
        private DuelMode GetRandomMode()
        {
            // For JOIN QUEUE button, use Any mode to enable cross-mode matching
            return DuelMode.Any;
        }
        
        [ConsoleCommand("joinqueue.click")]
        private void JoinQueueClickCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            // Check if already in queue
            if (queueManager.IsQueued(player.userID))
            {
                SendReply(player, "You're already in queue!");
                return;
            }
            
            // Check if in active match
            if (activeMatches.ContainsKey(player.userID))
            {
                SendReply(player, "You're already in a match!");
                return;
            }
            
            // Join Any mode queue for cross-mode matching
            var mode = DuelMode.Any;
            queueManager.JoinQueue(player.userID, player.displayName, mode);
            
            // Show leave button when in queue
            ShowLeaveButton(player);
            
            SendReply(player, "Joined queue for random match!");
            SendReply(player, "Searching for opponent...");
        }
        
        // Phase 2: Queue-specific join commands
        [ConsoleCommand("joinqueue.public")]
        private void JoinQueuePublicCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            JoinQueueByType(player, QueueType.Public);
        }
        
        [ConsoleCommand("joinqueue.ak")]
        private void JoinQueueAKCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            JoinQueueByType(player, QueueType.PublicAK);
        }
        
        [ConsoleCommand("joinqueue.sar")]
        private void JoinQueueSARCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            JoinQueueByType(player, QueueType.PublicSAR);
        }
        
        [ConsoleCommand("joinqueue.bow")]
        private void JoinQueueBowCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            JoinQueueByType(player, QueueType.PublicBow);
        }
        
        [ConsoleCommand("joinqueue.rev")]
        private void JoinQueueRevCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            JoinQueueByType(player, QueueType.PublicRevolver);
        }
        
        [ConsoleCommand("joinqueue.spear")]
        private void JoinQueueSpearCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (!config.EnableSpeargun)
            {
                SendReply(player, "Speargun mode is currently disabled.");
                return;
            }
            
            JoinQueueByType(player, QueueType.PublicSpeargun);
        }
        
        [ConsoleCommand("lobby.createroom")]
        private void CreateRoomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(player, "Usage: lobby.createroom <roomName>");
                SendReply(player, "Example: lobby.createroom My Epic Room");
                return;
            }
            
            var roomName = string.Join(" ", arg.Args);
            CreateRoom(player, roomName);
        }
        
        [ConsoleCommand("lobby.joinroom")]
        private void JoinRoomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(player, "Usage: lobby.joinroom <roomID>");
                SendReply(player, "Example: lobby.joinroom A7K3M9");
                return;
            }
            
            var roomID = arg.Args[0].ToUpper();
            JoinRoom(player, roomID);
        }
        
        [ConsoleCommand("lobby.leaveroom")]
        private void LeaveRoomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            var roomID = GetPlayerRoom(player.userID);
            if (roomID == null)
            {
                SendReply(player, "You're not in any room!");
                return;
            }
            
            LeaveRoom(player, roomID);
        }
        
        [ConsoleCommand("lobby.startmatch")]
        private void StartMatchCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            var roomID = GetPlayerRoom(player.userID);
            if (roomID == null)
            {
                SendReply(player, "You're not in any room!");
                return;
            }
            
            StartRoomMatch(player, roomID);
        }
        
        #endregion
        
        #region Phase 3 - Queue Management
        
        private void JoinQueueByType(BasePlayer player, QueueType queueType)
        {
            if (player == null) return;
            
            // Check if player is already in a match
            if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
            {
                SendReply(player, "You're already in a match!");
                return;
            }
            
            // Check if player is already in a queue
            var currentQueue = GetPlayerQueueType(player.userID);
            if (currentQueue.HasValue)
            {
                if (currentQueue.Value == queueType)
                {
                    SendReply(player, "You're already in this queue!");
                    return;
                }
                // Remove from current queue
                LeaveQueueInternal(player, false);
            }
            
            // Add to specified queue
            if (!queuesByType.ContainsKey(queueType))
            {
                queuesByType[queueType] = new List<ulong>();
            }
            
            queuesByType[queueType].Add(player.userID);
            
            string queueName = queueType == QueueType.Public ? "Public" :
                              queueType == QueueType.PublicAK ? "AK" :
                              queueType == QueueType.PublicSAR ? "SAR" :
                              queueType == QueueType.PublicBow ? "Bow" :
                              queueType == QueueType.PublicRevolver ? "Revolver" :
                              queueType == QueueType.PublicSpeargun ? "Speargun" : "Unknown";
            
            SendReply(player, $"Joined {queueName} queue! Waiting for opponent...");
            ShowLobbyBrowser(player);
        }
        
        private void LeaveQueueInternal(BasePlayer player, bool updateUI = true)
        {
            if (player == null) return;
            
            bool wasInQueue = false;
            foreach (var queue in queuesByType.Values)
            {
                if (queue.Remove(player.userID))
                {
                    wasInQueue = true;
                }
            }
            
            if (wasInQueue && updateUI)
            {
                SendReply(player, "Left the queue.");
                ShowLobbyBrowser(player);
            }
        }
        
        private QueueType? GetPlayerQueueType(ulong playerID)
        {
            foreach (var kvp in queuesByType)
            {
                if (kvp.Value.Contains(playerID))
                {
                    return kvp.Key;
                }
            }
            return null;
        }
        
        private int GetQueuePlayerCount(QueueType queueType)
        {
            if (queuesByType.ContainsKey(queueType))
            {
                return queuesByType[queueType].Count;
            }
            return 0;
        }
        
        private void ProcessAllQueues()
        {
            foreach (QueueType queueType in Enum.GetValues(typeof(QueueType)))
            {
                TryMatchPlayersInQueue(queueType);
            }
        }
        
        private void TryMatchPlayersInQueue(QueueType queueType)
        {
            if (!queuesByType.ContainsKey(queueType)) return;
            
            var queue = queuesByType[queueType];
            if (queue.Count < 2) return;
            
            // Get first two players
            var player1ID = queue[0];
            var player2ID = queue[1];
            
            var player1 = BasePlayer.FindByID(player1ID);
            var player2 = BasePlayer.FindByID(player2ID);
            
            if (player1 == null || player2 == null)
            {
                // Remove disconnected players
                if (player1 == null) queue.Remove(player1ID);
                if (player2 == null) queue.Remove(player2ID);
                return;
            }
            
            // Remove both from queue
            queue.RemoveAt(0);
            queue.RemoveAt(0);
            
            // Determine mode based on queue type
            DuelMode mode;
            switch (queueType)
            {
                case QueueType.PublicAK:
                    mode = DuelMode.AK47;
                    break;
                case QueueType.PublicSAR:
                    mode = DuelMode.SAR;
                    break;
                case QueueType.PublicBow:
                    mode = DuelMode.Bow;
                    break;
                case QueueType.PublicRevolver:
                    mode = DuelMode.Revolver;
                    break;
                case QueueType.PublicSpeargun:
                    mode = DuelMode.Speargun;
                    break;
                case QueueType.Public:
                default:
                    // Random mode for public queue — include Speargun when enabled
                    var modeList = new List<DuelMode> { DuelMode.AK47, DuelMode.SAR, DuelMode.Bow, DuelMode.Revolver };
                    if (config.EnableSpeargun) modeList.Add(DuelMode.Speargun);
                    mode = modeList[UnityEngine.Random.Range(0, modeList.Count)];
                    break;
            }
            
            // Create the match using existing StartDuel method
            StartDuel(player1, player2, mode);
        }
        
        #endregion
        
        #region Phase 4 - Private Rooms
        
        private void CreateRoom(BasePlayer player, string roomName)
        {
            if (player == null) return;
            
            // Validate room name
            if (string.IsNullOrWhiteSpace(roomName) || roomName.Length < 3 || roomName.Length > 20)
            {
                SendReply(player, "Room name must be between 3 and 20 characters!");
                return;
            }
            
            // Check if player is already in a match
            if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
            {
                SendReply(player, "You cannot create a room while in a match!");
                return;
            }
            
            // Check if player is already in a room
            var existingRoom = GetPlayerRoom(player.userID);
            if (existingRoom != null)
            {
                SendReply(player, "You're already in a room! Leave it first with /lobby.leaveroom");
                return;
            }
            
            // Generate unique room ID
            string roomID;
            do
            {
                roomID = GenerateRoomID();
            } while (privateRooms.ContainsKey(roomID));
            
            // Create the room
            var room = new PrivateRoom
            {
                RoomID = roomID,
                RoomName = roomName,
                OwnerID = player.userID,
                OwnerName = player.displayName,
                PlayerIDs = new List<ulong> { player.userID },
                MaxPlayers = 2,
                Mode = DuelMode.AK47, // Default, can be changed
                Created = DateTime.Now
            };
            
            privateRooms[roomID] = room;
            
            // Remove from queue if queued
            LeaveQueueInternal(player, false);
            
            SendReply(player, $"Room '{roomName}' created with ID: {roomID}");
            SendReply(player, $"Share this ID with friends: {roomID}");
            SendReply(player, "Start match when 2 players with: /lobby.startmatch");
            
            // Update UI for all players
            UpdateRoomsList();
        }
        
        private void JoinRoom(BasePlayer player, string roomID)
        {
            if (player == null || string.IsNullOrWhiteSpace(roomID)) return;
            
            // Check if room exists
            if (!privateRooms.ContainsKey(roomID))
            {
                SendReply(player, $"Room {roomID} does not exist!");
                return;
            }
            
            var room = privateRooms[roomID];
            
            // Check if player is already in a match
            if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
            {
                SendReply(player, "You cannot join a room while in a match!");
                return;
            }
            
            // Check if player is already in this room
            if (room.PlayerIDs.Contains(player.userID))
            {
                SendReply(player, "You're already in this room!");
                return;
            }
            
            // Check if player is in another room
            var existingRoom = GetPlayerRoom(player.userID);
            if (existingRoom != null)
            {
                SendReply(player, "You're already in another room! Leave it first with /lobby.leaveroom");
                return;
            }
            
            // Check if room is full
            if (room.IsFull())
            {
                SendReply(player, $"Room '{room.RoomName}' is full!");
                return;
            }
            
            // Remove from queue if queued
            LeaveQueueInternal(player, false);
            
            // Add player to room
            room.PlayerIDs.Add(player.userID);
            
            SendReply(player, $"Joined room '{room.RoomName}' ({room.PlayerIDs.Count}/{room.MaxPlayers})");
            
            // Notify all room members
            foreach (var playerID in room.PlayerIDs)
            {
                var p = BasePlayer.FindByID(playerID);
                if (p != null && p.userID != player.userID)
                {
                    SendReply(p, $"{player.displayName} joined your room");
                }
            }
            
            // Update UI for all players
            UpdateRoomsList();
        }
        
        private void LeaveRoom(BasePlayer player, string roomID, bool updateUI = true)
        {
            if (player == null || string.IsNullOrWhiteSpace(roomID)) return;
            
            if (!privateRooms.ContainsKey(roomID)) return;
            
            var room = privateRooms[roomID];
            
            if (!room.PlayerIDs.Contains(player.userID)) return;
            
            // Remove player from room
            room.PlayerIDs.Remove(player.userID);
            
            SendReply(player, $"Left room '{room.RoomName}'");
            
            // Notify remaining players
            foreach (var playerID in room.PlayerIDs)
            {
                var p = BasePlayer.FindByID(playerID);
                if (p != null)
                {
                    SendReply(p, $"{player.displayName} left the room");
                }
            }
            
            // Handle owner leaving
            if (player.userID == room.OwnerID && room.PlayerIDs.Count > 0)
            {
                // Transfer ownership to next player
                room.OwnerID = room.PlayerIDs[0];
                var newOwner = BasePlayer.FindByID(room.OwnerID);
                if (newOwner != null)
                {
                    room.OwnerName = newOwner.displayName;
                    SendReply(newOwner, "You are now the room owner");
                }
            }
            
            // Close room if empty
            if (room.PlayerIDs.Count == 0)
            {
                privateRooms.Remove(roomID);
            }
            
            if (updateUI)
            {
                UpdateRoomsList();
            }
        }
        
        private void StartRoomMatch(BasePlayer owner, string roomID)
        {
            if (owner == null || string.IsNullOrWhiteSpace(roomID)) return;
            
            if (!privateRooms.ContainsKey(roomID))
            {
                SendReply(owner, "Room does not exist!");
                return;
            }
            
            var room = privateRooms[roomID];
            
            // Check if player is the owner
            if (owner.userID != room.OwnerID)
            {
                SendReply(owner, "Only the room owner can start the match!");
                return;
            }
            
            // Check if room has exactly 2 players
            if (room.PlayerIDs.Count != 2)
            {
                SendReply(owner, $"Need exactly 2 players to start! ({room.PlayerIDs.Count}/2)");
                return;
            }
            
            // Get both players
            var player1 = BasePlayer.FindByID(room.PlayerIDs[0]);
            var player2 = BasePlayer.FindByID(room.PlayerIDs[1]);
            
            if (player1 == null || player2 == null)
            {
                SendReply(owner, "One or more players are not available!");
                return;
            }
            
            // Select random mode
            var modes = new[] { DuelMode.AK47, DuelMode.SAR, DuelMode.Bow, DuelMode.Revolver };
            var mode = modes[UnityEngine.Random.Range(0, modes.Length)];
            
            // Start the match
            StartDuel(player1, player2, mode);
            
            // Close the room
            privateRooms.Remove(roomID);
            
            // Update UI
            UpdateRoomsList();
        }
        
        private string GetPlayerRoom(ulong playerID)
        {
            foreach (var room in privateRooms.Values)
            {
                if (room.PlayerIDs.Contains(playerID))
                {
                    return room.RoomID;
                }
            }
            return null;
        }
        
        private void CleanupPlayerRooms(ulong playerID)
        {
            var roomsToRemove = new List<string>();
            
            foreach (var room in privateRooms.Values)
            {
                if (room.PlayerIDs.Contains(playerID))
                {
                    room.PlayerIDs.Remove(playerID);
                    
                    // Handle owner leaving
                    if (playerID == room.OwnerID && room.PlayerIDs.Count > 0)
                    {
                        room.OwnerID = room.PlayerIDs[0];
                        var newOwner = BasePlayer.FindByID(room.OwnerID);
                        if (newOwner != null)
                        {
                            room.OwnerName = newOwner.displayName;
                            SendReply(newOwner, "You are now the room owner");
                        }
                    }
                    
                    // Mark for removal if empty
                    if (room.PlayerIDs.Count == 0)
                    {
                        roomsToRemove.Add(room.RoomID);
                    }
                }
            }
            
            // Remove empty rooms
            foreach (var roomID in roomsToRemove)
            {
                privateRooms.Remove(roomID);
            }
            
            if (roomsToRemove.Count > 0)
            {
                UpdateRoomsList();
            }
        }
        
        private void UpdateRoomsList()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected)
                {
                    ShowLobbyBrowser(player);
                }
            }
        }
        
        private string GenerateRoomID()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new System.Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        
        #endregion
        
        private void ShowLeaderboardUI(BasePlayer player)
        {
            if (player == null) return;
            
            DestroyLeaderboardUI(player); // Clean up any existing leaderboard UI
            
            var elements = new CuiElementContainer();
            
            // Main panel - top left corner (narrower with bigger text)
            var mainPanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.85" },
                RectTransform = { AnchorMin = "0.01 0.70", AnchorMax = "0.19 0.99" },
                CursorEnabled = false
            }, "Hud", "LeaderboardUI");
            
            // Title
            elements.Add(new CuiLabel
            {
                Text = { Text = "🏆 GLOBAL LEADERBOARD", FontSize = 16, Align = TextAnchor.UpperCenter, Color = "1 0.8 0 1" },
                RectTransform = { AnchorMin = "0.05 0.92", AnchorMax = "0.95 0.98" }
            }, mainPanel);
            
            // Get top 10 players - Filter by time window (last X minutes)
            var cutoffTime = DateTime.Now.AddMinutes(-config.LeaderboardTimeWindowMinutes);
            var topPlayers = playerData
                .Where(p => p.Value.LastMatchTime >= cutoffTime) // Only show players with recent matches
                .OrderByDescending(p => p.Value.WinRate)
                .ThenByDescending(p => p.Value.Wins)
                .Take(10)
                .ToList();
            
            // Display leaderboard entries
            float startY = 0.88f;
            float entryHeight = 0.08f;
            int rank = 1;
            
            if (topPlayers.Count == 0)
            {
                // No recent matches - show message
                elements.Add(new CuiLabel
                {
                    Text = { Text = $"No matches in the\nlast {config.LeaderboardTimeWindowMinutes} minutes", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = "0.05 0.40", AnchorMax = "0.95 0.60" }
                }, mainPanel);
            }
            
            foreach (var entry in topPlayers)
            {
                var playerName = covalence.Players.FindPlayerById(entry.Key.ToString())?.Name ?? "Unknown";
                if (playerName.Length > 12) playerName = playerName.Substring(0, 12); // Truncate long names
                
                var stats = entry.Value;
                
                // Calculate K/D ratio from rolling stats (last X minutes only)
                int recentKills = GetRecentKills(stats, config.LeaderboardTimeWindowMinutes);
                int recentDeaths = GetRecentDeaths(stats, config.LeaderboardTimeWindowMinutes);
                float kd = recentDeaths > 0 ? (float)recentKills / recentDeaths : recentKills;
                
                // Get recent wins/losses for display
                int recentWins = GetRecentWins(stats, config.LeaderboardTimeWindowMinutes);
                int recentLosses = GetRecentLosses(stats, config.LeaderboardTimeWindowMinutes);
                
                string entryText = $"{rank}. {playerName} ({recentWins}-{recentLosses}) K/D: {kd:F2}";
                
                // Highlight current player
                string textColor = (entry.Key == player.userID) ? "1 1 0 1" : "0.9 0.9 0.9 1";
                
                elements.Add(new CuiLabel
                {
                    Text = { Text = entryText, FontSize = 12, Align = TextAnchor.UpperLeft, Color = textColor },
                    RectTransform = { AnchorMin = $"0.05 {startY - entryHeight}", AnchorMax = $"0.95 {startY}" }
                }, mainPanel);
                
                startY -= entryHeight;
                rank++;
                
                if (rank > 10) break; // Only show top 10
            }
            
            // Footer with player's rank if not in top 10
            if (playerData.ContainsKey(player.userID))
            {
                var playerRank = playerData
                    .Where(p => p.Value.LastMatchTime >= cutoffTime) // Use same time filter
                    .OrderByDescending(p => p.Value.WinRate)
                    .ThenByDescending(p => p.Value.Wins)
                    .ToList()
                    .FindIndex(p => p.Key == player.userID) + 1;
                
                if (playerRank > 10)
                {
                    var yourStats = playerData[player.userID];
                    // Calculate from rolling stats
                    int yourRecentKills = GetRecentKills(yourStats, config.LeaderboardTimeWindowMinutes);
                    int yourRecentDeaths = GetRecentDeaths(yourStats, config.LeaderboardTimeWindowMinutes);
                    float yourKd = yourRecentDeaths > 0 ? (float)yourRecentKills / yourRecentDeaths : yourRecentKills;
                    
                    int yourRecentWins = GetRecentWins(yourStats, config.LeaderboardTimeWindowMinutes);
                    int yourRecentLosses = GetRecentLosses(yourStats, config.LeaderboardTimeWindowMinutes);
                    string footerText = $"Your Rank: #{playerRank}\n{yourRecentWins}W-{yourRecentLosses}L | K/D: {yourKd:F2}";
                    
                    elements.Add(new CuiLabel
                    {
                        Text = { Text = footerText, FontSize = 11, Align = TextAnchor.LowerCenter, Color = "1 1 0 1" },
                        RectTransform = { AnchorMin = "0.05 0.02", AnchorMax = "0.95 0.10" }
                    }, mainPanel);
                }
            }
            
            CuiHelper.AddUi(player, elements);
            activeLeaderboardUIs.Add(player.userID);
        }
        
        private void DestroyLeaderboardUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "LeaderboardUI");
            activeLeaderboardUIs.Remove(player.userID);
        }
        
        private void RefreshAllLeaderboards()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected && activeLeaderboardUIs.Contains(player.userID))
                {
                    ShowLeaderboardUI(player);
                }
            }
        }
        
        private void ShowWinLoseUI(BasePlayer player, bool didWin, int yourScore, int opponentScore)
        {
            if (player == null) return;
            
            DestroyWinLoseUI(player); // Clean up any existing overlay
            
            var elements = new CuiElementContainer();
            
            // Background overlay - semi-transparent
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.7" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = false
            }, "Overlay", "WinLoseOverlay");
            
            // Main result panel - center of screen
            string panelColor = didWin ? "0.2 0.8 0.2 0.95" : "0.8 0.2 0.2 0.95"; // Green for win, Red for lose
            elements.Add(new CuiPanel
            {
                Image = { Color = panelColor },
                RectTransform = { AnchorMin = "0.35 0.40", AnchorMax = "0.65 0.60" },
                CursorEnabled = false
            }, "WinLoseOverlay", "WinLosePanel");
            
            // Win/Lose text - large and bold
            string resultText = didWin ? "YOU WIN!" : "YOU LOSE!";
            elements.Add(new CuiLabel
            {
                Text = { Text = resultText, FontSize = 40, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1" }
            }, "WinLosePanel");
            
            // Score text
            string scoreText = $"Final Score: {yourScore} - {opponentScore}";
            elements.Add(new CuiLabel
            {
                Text = { Text = scoreText, FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5" }
            }, "WinLosePanel");
            
            CuiHelper.AddUi(player, elements);
            
            // Auto-dismiss after 5 seconds
            timer.Once(5f, () => DestroyWinLoseUI(player));
        }
        
        private void DestroyWinLoseUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "WinLoseOverlay");
        }
        
        #endregion
        
        #region Arena Management
        
        private void CreateArena(BasePlayer player, string name)
        {
            if (arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're already creating an arena. Use /arena cancel to cancel.");
                return;
            }
            
            // Check if arena with this name already exists
            if (arenaManager.ArenaExists(name))
            {
                SendReply(player, $"Arena '{name}' already exists. Choose a different name.");
                return;
            }
            
            arenaBuilders[player.userID] = new ArenaBuilder { Name = name };
            SendReply(player, $"Creating arena '{name}'.\n" +
                             "Step 1: Move to the first spawn point and use /arena setspawn1\n" +
                             "Step 2: Move to the second spawn point and use /arena setspawn2\n" +
                             "Step 3: Use /arena save to save the arena");
        }
        
        private void SetSpawn1(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not creating an arena. Use /arena create <name> first.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            builder.Spawn1 = player.transform.position;
            SendReply(player, $"Spawn point 1 set at {builder.Spawn1}\n" +
                             "Now move to the second spawn point and use /arena setspawn2");
        }
        
        private void SetSpawn2(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not creating an arena. Use /arena create <name> first.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            
            if (builder.Spawn1 == Vector3.zero)
            {
                SendReply(player, "You need to set spawn point 1 first using /arena setspawn1");
                return;
            }
            
            builder.Spawn2 = player.transform.position;
            SendReply(player, $"Spawn point 2 set at {builder.Spawn2}\n" +
                             "Arena is ready! Use /arena save to save it.");
        }
        
        private void SaveArena(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not creating an arena. Use /arena create <name> first.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            
            if (builder.Spawn1 == Vector3.zero || builder.Spawn2 == Vector3.zero)
            {
                SendReply(player, "You need to set both spawn points first.\n" +
                                 "Use /arena setspawn1 and /arena setspawn2");
                return;
            }
            
            // Add to configuration
            var arenaConfig = new ArenaConfig
            {
                Name = builder.Name,
                Spawn1 = builder.Spawn1,
                Spawn2 = builder.Spawn2
            };
            
            // Add to arena list
            arenas.Add(arenaConfig);
            SaveArenas(); // Save arenas to data file
            
            // Add to arena manager
            arenaManager.AddArena(arenaConfig);
            
            SendReply(player, $"Arena '{builder.Name}' saved successfully!\n" +
                             $"Spawn 1: {builder.Spawn1}\n" +
                             $"Spawn 2: {builder.Spawn2}");
            
            arenaBuilders.Remove(player.userID);
        }
        
        private void CancelArena(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not creating an arena.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            arenaBuilders.Remove(player.userID);
            SendReply(player, $"Cancelled creation of arena '{builder.Name}'");
        }
        
        private void ListArenas(BasePlayer player)
        {
            var arenas = arenaManager.GetAllArenas();
            
            if (arenas.Count == 0)
            {
                SendReply(player, "No arenas configured yet.");
                return;
            }
            
            var message = "=== Arenas ===\n";
            foreach (var arena in arenas)
            {
                int activeInstances = arena.ActiveInstances.Count;
                int maxInstances = arena.MaxInstances;
                string status = activeInstances == 0 ? "Available" : $"{activeInstances}/{maxInstances} instances active";
                
                // Find the arena config to get radius
                var arenaConfig = arenas.FirstOrDefault(a => a.Name == arena.Name);
                float radius = arenaConfig != null ? arenaConfig.Radius : 30f;
                
                message += $"{arena.Name} - {status}\n";
                message += $"  Spawn 1: {arena.Spawn1}\n";
                message += $"  Spawn 2: {arena.Spawn2}\n";
                message += $"  Zone Radius: {radius}m\n";
                
                if (activeInstances > 0)
                {
                    message += $"  Active Instances: {string.Join(", ", arena.ActiveInstances)}\n";
                }
            }
            
            SendReply(player, message);
        }
        
        private void DeleteArena(BasePlayer player, string name)
        {
            if (!arenaManager.ArenaExists(name))
            {
                SendReply(player, $"Arena '{name}' not found.");
                return;
            }
            
            // Check if arena is in use
            if (arenaManager.IsArenaInUse(name))
            {
                SendReply(player, $"Arena '{name}' is currently in use. Cannot delete.");
                return;
            }
            
            // Remove from arena list
            arenas.RemoveAll(a => a.Name == name);
            SaveArenas(); // Save arenas to data file
            
            // Remove from manager
            arenaManager.RemoveArena(name);
            
            SendReply(player, $"Arena '{name}' deleted successfully.");
        }
        
        private void TeleportToArena(BasePlayer player, string name, int spawnNum)
        {
            var arena = arenaManager.GetArenaByName(name);
            
            if (arena == null)
            {
                SendReply(player, $"Arena '{name}' not found.");
                return;
            }
            
            Vector3 position = spawnNum == 2 ? arena.Spawn2 : arena.Spawn1;
            TeleportPlayer(player, position);
            SendReply(player, $"Teleported to {arena.Name} - Spawn {spawnNum}");
        }
        
        #endregion
        
        #region Data Storage
        
        private void LoadPlayerData()
        {
            try
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("HellisPlugin_PlayerData") 
                             ?? new Dictionary<ulong, PlayerData>();
                Puts($"Loaded player data for {playerData.Count} players");
            }
            catch (Exception ex)
            {
                Puts($"Error loading player data: {ex.Message}");
                playerData = new Dictionary<ulong, PlayerData>();
            }
        }
        
        private void SavePlayerData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject("HellisPlugin_PlayerData", playerData);
            }
            catch (Exception ex)
            {
                Puts($"Error saving player data: {ex.Message}");
            }
        }
        
        private void LoadArenas()
        {
            try
            {
                arenas = Interface.Oxide.DataFileSystem.ReadObject<List<ArenaConfig>>("HellisPlugin_Arenas") 
                            ?? new List<ArenaConfig>();
                
                if (arenas.Count > 0)
                {
                    arenaManager = new ArenaManager(arenas, config.MaxInstancesPerArena);
                    Puts($"Loaded {arenas.Count} arenas from data file");
                }
                else
                {
                    Puts("No arenas found in data file");
                }
            }
            catch (Exception ex)
            {
                Puts($"Error loading arenas: {ex.Message}");
            }
        }
        
        private void SaveArenas()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject("HellisPlugin_Arenas", arenas);
                Puts($"Saved {arenas.Count} arenas to data file");
            }
            catch (Exception ex)
            {
                Puts($"Error saving arenas: {ex.Message}");
            }
        }
        
        private void LoadData()
        {
            LoadPlayerData();
            LoadArenas();
        }
        
        private void SaveData()
        {
            SavePlayerData();
        }
        
        private void Unload()
        {
            // Save all data on plugin unload
            SavePlayerData();
            SaveArenas();
            Puts("HellisPlugin unloaded - all data saved");
        }
        
        #endregion
        
        #region Classes
        
        public enum DuelMode
        {
            None,
            AK47,
            SAR,
            Speargun,
            Bow,
            Revolver,
            Any  // For random queue matchmaking
        }
        
        private class ModeButton
        {
            public string Label { get; set; }
            public DuelMode Mode { get; set; }
        }
        
        public class QueueManager
        {
            private Dictionary<DuelMode, List<QueueEntry>> queues = new Dictionary<DuelMode, List<QueueEntry>>();
            private Dictionary<ulong, DuelMode> playerQueues = new Dictionary<ulong, DuelMode>();
            private bool enableSpeargun;
            
            public QueueManager(bool enableSpeargun = false)
            {
                this.enableSpeargun = enableSpeargun;
                queues[DuelMode.AK47] = new List<QueueEntry>();
                queues[DuelMode.SAR] = new List<QueueEntry>();
                queues[DuelMode.Speargun] = new List<QueueEntry>();
                queues[DuelMode.Bow] = new List<QueueEntry>();
                queues[DuelMode.Revolver] = new List<QueueEntry>();
                queues[DuelMode.Any] = new List<QueueEntry>();
            }
            
            public void JoinQueue(ulong playerID, string playerName, DuelMode mode)
            {
                if (playerQueues.ContainsKey(playerID)) return;
                
                queues[mode].Add(new QueueEntry { PlayerID = playerID, PlayerName = playerName, Mode = mode });
                playerQueues[playerID] = mode;
            }
            
            public bool LeaveQueue(ulong playerID)
            {
                if (!playerQueues.ContainsKey(playerID)) return false;
                
                var mode = playerQueues[playerID];
                queues[mode].RemoveAll(x => x.PlayerID == playerID);
                playerQueues.Remove(playerID);
                return true;
            }
            
            public bool IsQueued(ulong playerID)
            {
                return playerQueues.ContainsKey(playerID);
            }
            
            public int GetQueuePosition(ulong playerID)
            {
                if (!playerQueues.ContainsKey(playerID)) return -1;
                
                var mode = playerQueues[playerID];
                return queues[mode].FindIndex(x => x.PlayerID == playerID);
            }
            
            public List<MatchPair> TryMatchAll()
            {
                var matches = new List<MatchPair>();
                
                foreach (var mode in queues.Keys.ToList())
                {
                    // Special handling for Any mode - match players and randomly select game mode
                    if (mode == DuelMode.Any)
                    {
                        while (queues[mode].Count >= 2)
                        {
                            var p1 = queues[mode][0];
                            var p2 = queues[mode][1];
                            
                            queues[mode].RemoveAt(0);
                            queues[mode].RemoveAt(0);
                            
                            playerQueues.Remove(p1.PlayerID);
                            playerQueues.Remove(p2.PlayerID);
                            
                            // Randomly select actual game mode for the match
                            var actualMode = GetRandomGameMode();
                            
                            matches.Add(new MatchPair
                            {
                                Player1ID = p1.PlayerID,
                                Player2ID = p2.PlayerID,
                                Mode = actualMode
                            });
                        }
                    }
                    else
                    {
                        // Normal mode-specific matching
                        while (queues[mode].Count >= 2)
                        {
                            var p1 = queues[mode][0];
                            var p2 = queues[mode][1];
                            
                            queues[mode].RemoveAt(0);
                            queues[mode].RemoveAt(0);
                            
                            playerQueues.Remove(p1.PlayerID);
                            playerQueues.Remove(p2.PlayerID);
                            
                            matches.Add(new MatchPair
                            {
                                Player1ID = p1.PlayerID,
                                Player2ID = p2.PlayerID,
                                Mode = mode
                            });
                        }
                    }
                }
                
                return matches;
            }
            
            private DuelMode GetRandomGameMode()
            {
                // Select random mode for Any queue matches — include Speargun when enabled
                var availableModes = new List<DuelMode>
                {
                    DuelMode.AK47,
                    DuelMode.SAR,
                    DuelMode.Bow,
                    DuelMode.Revolver
                };
                
                if (enableSpeargun) availableModes.Add(DuelMode.Speargun);
                
                return availableModes[UnityEngine.Random.Range(0, availableModes.Count)];
            }
        }
        
        public class QueueEntry
        {
            public ulong PlayerID;
            public string PlayerName;
            public DuelMode Mode;
        }
        
        public class MatchPair
        {
            public ulong Player1ID;
            public ulong Player2ID;
            public DuelMode Mode;
        }
        
        public class ArenaManager
        {
            private List<Arena> arenas = new List<Arena>();
            private int maxInstancesPerArena;
            private int lastArenaIndex = -1; // Track last used arena for round-robin distribution
            
            public ArenaManager(List<ArenaConfig> configs, int maxInstances = 5)
            {
                maxInstancesPerArena = maxInstances;
                foreach (var config in configs)
                {
                    arenas.Add(new Arena
                    {
                        Name = config.Name,
                        Spawn1 = config.Spawn1,
                        Spawn2 = config.Spawn2,
                        Radius = config.Radius,
                        InUse = false,
                        MaxInstances = maxInstances,
                        LobbyPosition = config.LobbyPosition,
                        LobbyRadius = config.LobbyRadius,
                        LobbyPositionSet = config.LobbyPositionSet
                    });
                }
            }
            
            public Arena GetAvailableArena()
            {
                // Round-robin arena selection for even distribution across all arenas
                if (arenas.Count == 0) return null;
                
                // Start from the next arena after the last used one
                int startIndex = (lastArenaIndex + 1) % arenas.Count;
                
                // Try to find an available arena starting from the next one
                for (int i = 0; i < arenas.Count; i++)
                {
                    int currentIndex = (startIndex + i) % arenas.Count;
                    if (arenas[currentIndex].HasAvailableInstance())
                    {
                        lastArenaIndex = currentIndex;
                        return arenas[currentIndex];
                    }
                }
                
                // No available arena found (all arenas at max capacity)
                return null;
            }
            
            public void ReleaseArena(Arena arena)
            {
                // Deprecated - kept for backward compatibility
                arena.InUse = false;
                arena.ActiveInstances.Clear();
            }
            
            public void ReleaseArenaInstance(Arena arena, int instanceId)
            {
                arena.ReleaseInstance(instanceId);
            }
            
            public void AddArena(ArenaConfig config)
            {
                arenas.Add(new Arena
                {
                    Name = config.Name,
                    Spawn1 = config.Spawn1,
                    Spawn2 = config.Spawn2,
                    Radius = config.Radius,
                    InUse = false,
                    MaxInstances = maxInstancesPerArena,
                    LobbyPosition = config.LobbyPosition,
                    LobbyRadius = config.LobbyRadius,
                    LobbyPositionSet = config.LobbyPositionSet
                });
            }
            
            public void RemoveArena(string name)
            {
                arenas.RemoveAll(a => a.Name == name);
            }
            
            public bool ArenaExists(string name)
            {
                return arenas.Any(a => a.Name == name);
            }
            
            public bool IsArenaInUse(string name)
            {
                var arena = arenas.FirstOrDefault(a => a.Name == name);
                return arena != null && arena.ActiveInstances.Count > 0;
            }
            
            public Arena GetArenaByName(string name)
            {
                return arenas.FirstOrDefault(a => a.Name == name);
            }
            
            public List<Arena> GetAllArenas()
            {
                return arenas;
            }
            
            // Lobby helper methods
            public Vector3 GetLobbyPosition()
            {
                // Get lobby from first arena (or default if none)
                return arenas.Count > 0 && arenas[0].LobbyPositionSet 
                    ? arenas[0].LobbyPosition 
                    : Vector3.zero;
            }
            
            public float GetLobbyRadius()
            {
                // Get lobby radius from first arena (or default)
                return arenas.Count > 0 ? arenas[0].LobbyRadius : 10f;
            }
            
            public bool IsLobbySet()
            {
                return arenas.Count > 0 && arenas[0].LobbyPositionSet;
            }
            
            public void SetLobby(Vector3 position, float radius)
            {
                // Set lobby for first arena (ensure at least one arena exists)
                if (arenas.Count == 0) return;
                
                arenas[0].LobbyPosition = position;
                arenas[0].LobbyRadius = radius;
                arenas[0].LobbyPositionSet = true;
            }
        }
        
        public class Arena
        {
            public string Name;
            public Vector3 Spawn1;
            public Vector3 Spawn2;
            public float Radius = 30f; // Zone radius for enforcement
            public bool InUse; // Deprecated - kept for backward compatibility
            public int MaxInstances;
            public HashSet<int> ActiveInstances = new HashSet<int>();
            
            // Lobby configuration (per arena)
            public Vector3 LobbyPosition;
            public float LobbyRadius = 10f;
            public bool LobbyPositionSet;
            
            public Arena()
            {
                MaxInstances = 5;
            }
            
            public bool HasAvailableInstance()
            {
                return ActiveInstances.Count < MaxInstances;
            }
            
            public int GetNextInstanceId()
            {
                for (int i = 0; i < MaxInstances; i++)
                {
                    if (!ActiveInstances.Contains(i))
                    {
                        return i;
                    }
                }
                return -1;
            }
            
            public void OccupyInstance(int instanceId)
            {
                ActiveInstances.Add(instanceId);
                InUse = ActiveInstances.Count > 0;
            }
            
            public void ReleaseInstance(int instanceId)
            {
                ActiveInstances.Remove(instanceId);
                InUse = ActiveInstances.Count > 0;
            }
        }
        
        public class LoadoutManager
        {
            private Dictionary<DuelMode, Loadout> loadouts = new Dictionary<DuelMode, Loadout>();
            
            public LoadoutManager(Configuration config)
            {
                // Load loadouts from config
                LoadLoadoutsFromConfig(config);
            }
            
            private void LoadLoadoutsFromConfig(Configuration config)
            {
                // Map string mode names to DuelMode enum
                var modeMap = new Dictionary<string, DuelMode>
                {
                    ["AK47"] = DuelMode.AK47,
                    ["SAR"] = DuelMode.SAR,
                    ["Speargun"] = DuelMode.Speargun,
                    ["Bow"] = DuelMode.Bow,
                    ["Revolver"] = DuelMode.Revolver
                };
                
                foreach (var kvp in config.Loadouts)
                {
                    if (modeMap.TryGetValue(kvp.Key, out var mode))
                    {
                        var loadout = new Loadout
                        {
                            Items = kvp.Value.Items.Select(item => new LoadoutItem
                            {
                                ShortName = item.ShortName,
                                Amount = item.Amount,
                                SkinID = item.SkinID
                            }).ToList()
                        };
                        
                        loadouts[mode] = loadout;
                    }
                }
            }
            
            public Loadout GetLoadout(DuelMode mode)
            {
                return loadouts.ContainsKey(mode) ? loadouts[mode] : new Loadout();
            }
        }
        
        public class Loadout
        {
            public List<LoadoutItem> Items = new List<LoadoutItem>();
        }
        
        public class LoadoutItem
        {
            public string ShortName;
            public int Amount;
            public ulong SkinID = 0;
        }
        
        public class ActiveMatch
        {
            public ulong Player1ID;
            public ulong Player2ID;
            public DuelMode Mode;
            public Arena Arena;
            public int InstanceID;
            public int BestOfRounds;
            public int CurrentRound = 1;
            public int Player1Score = 0;
            public int Player2Score = 0;
            public bool RoundInProgress = false;
            
            public ActiveMatch(ulong p1, ulong p2, DuelMode mode, Arena arena, int instanceId, int bestOf)
            {
                Player1ID = p1;
                Player2ID = p2;
                Mode = mode;
                Arena = arena;
                InstanceID = instanceId;
                BestOfRounds = bestOf;
                arena.OccupyInstance(instanceId);
            }
            
            public void StartRound()
            {
                RoundInProgress = true;
            }
            
            public void OnPlayerDeath(ulong playerID)
            {
                if (!RoundInProgress) return;
                
                RoundInProgress = false;
                
                if (playerID == Player1ID)
                {
                    Player2Score++;
                }
                else
                {
                    Player1Score++;
                }
                
                CurrentRound++;
            }
            
            public bool IsRoundFinished()
            {
                return !RoundInProgress && !IsFinished();
            }
            
            public bool IsFinished()
            {
                int requiredWins = (BestOfRounds / 2) + 1;
                return Player1Score >= requiredWins || Player2Score >= requiredWins;
            }
            
            public ulong GetWinner()
            {
                if (Player1Score > Player2Score) return Player1ID;
                if (Player2Score > Player1Score) return Player2ID;
                return 0;
            }
        }
        
        // Stat event for rolling leaderboard - individual kills/deaths with timestamps
        public class StatEvent
        {
            [JsonProperty("Type")]
            public string Type;  // "Kill", "Death", "Win", "Loss"
            
            [JsonProperty("Timestamp")]
            public DateTime Timestamp;
        }
        
        public class PlayerData
        {
            public int TotalMatches = 0;
            public int Wins = 0;
            public int Losses = 0;
            public int Kills = 0;  // Legacy field - kept for compatibility
            public int Deaths = 0;  // Legacy field - kept for compatibility
            public int RoundsWon = 0;
            public int RoundsLost = 0;
            public float WinRate = 0f;
            public DateTime LastMatchTime = DateTime.MinValue;
            
            // Event-based stats for rolling leaderboard
            public List<StatEvent> StatEvents = new List<StatEvent>();
        }
        
        public class AimTrainManager
        {
            private Dictionary<ulong, AimTrainSession> sessions = new Dictionary<ulong, AimTrainSession>();
            
            public void StartSession(ulong playerID)
            {
                sessions[playerID] = new AimTrainSession { PlayerID = playerID };
            }
            
            public void EndSession(ulong playerID)
            {
                sessions.Remove(playerID);
            }
        }
        
        public class AimTrainSession
        {
            public ulong PlayerID;
            public int TargetsHit = 0;
            public int TargetsMissed = 0;
        }
        
        public class ArenaBuilder
        {
            public string Name;
            public Vector3 Spawn1 = Vector3.zero;
            public Vector3 Spawn2 = Vector3.zero;
        }
        
        // Queue types for lobby browser
        public enum QueueType
        {
            Public,           // Any mode, random
            PublicAK,         // AK47 only
            PublicSAR,        // SAR only
            PublicBow,        // Bow only
            PublicRevolver,   // Revolver only
            PublicSpeargun    // Speargun only
        }
        
        // Private room data structure
        public class PrivateRoom
        {
            public string RoomID;
            public string RoomName;
            public ulong OwnerID;
            public string OwnerName;
            public List<ulong> PlayerIDs = new List<ulong>();
            public int MaxPlayers = 2;
            public DuelMode Mode;
            public DateTime Created;
            public bool IsOpen = true;
            
            public PrivateRoom()
            {
                RoomID = Guid.NewGuid().ToString();
                Created = DateTime.Now;
            }
            
            public bool IsFull()
            {
                return PlayerIDs.Count >= MaxPlayers;
            }
            
            public bool HasPlayer(ulong playerID)
            {
                return PlayerIDs.Contains(playerID);
            }
        }
        
        #endregion
    }
}
