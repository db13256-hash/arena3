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
        private HashSet<ulong> activeJoinRequestUIs = new HashSet<ulong>(); // Track players with pending-request overlay open
        private List<ArenaConfig> arenas = new List<ArenaConfig>(); // Arena storage (stored in data file, not config)
        // Lobby browser system
        private Dictionary<QueueType, List<ulong>> queuesByType = new Dictionary<QueueType, List<ulong>>();
        // Public queues for custom kits: kit name -> list of queued player IDs
        private Dictionary<string, List<ulong>> customQueuesByName = new Dictionary<string, List<ulong>>(StringComparer.OrdinalIgnoreCase);
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
            public int BestOfRounds = 1;
            
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
            public Dictionary<string, LoadoutConfig> Loadouts = GetPublicDefaultLoadouts();
            
            // Exposed so the /kit reset command can restore built-in defaults.
            public static Dictionary<string, LoadoutConfig> GetPublicDefaultLoadouts()
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
            
            // When non-empty, each match in this arena randomly picks one kit from this list.
            // Empty list means use the global kit for the chosen mode.
            public List<string> KitOverrides = new List<string>();
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
            queueManager = new QueueManager();
            arenaManager = new ArenaManager(arenas, config.MaxInstancesPerArena);
            loadoutManager = new LoadoutManager(config);
            aimTrainManager = new AimTrainManager();
            
            // Initialize lobby browser queues
            foreach (QueueType queueType in Enum.GetValues(typeof(QueueType)))
            {
                queuesByType[queueType] = new List<ulong>();
            }
            
            // Initialize public queues for custom kits
            foreach (var key in config.Loadouts.Keys)
            {
                if (!BuiltInKitNames.Contains(key))
                    customQueuesByName[key] = new List<ulong>();
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
            
            // Periodically refresh leaderboard for players in active matches so the
            // rolling time-window clears expired entries in real-time.
            timer.Repeat(30f, 0, () => RefreshAllLeaderboards());
            
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
                    var match = activeMatches[player.userID];
                    
                    // Cross-match damage protection: block all damage from players who are NOT
                    // this player's match opponent. This prevents physical interactions between
                    // concurrent matches sharing the same arena spawn points.
                    var attacker = info?.InitiatorPlayer;
                    if (attacker != null)
                    {
                        bool isMatchOpponent = attacker.userID == match.Player1ID || attacker.userID == match.Player2ID;
                        if (!isMatchOpponent)
                        {
                            info.damageTypes = new Rust.DamageTypeList();
                            info.DoHitEffects = false;
                            info.HitMaterial = 0;
                            return true; // Block damage from outside this match
                        }
                    }
                    
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
            DestroyLobbyBrowser(player);
            DestroyLeaderboardUI(player);
            DestroyJoinRequestUI(player);
            CuiHelper.DestroyUi(player, "RoomGunSelect");
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
            
            // Always clear inventory so players arrive in the lobby with nothing
            player.inventory.Strip();
            
            if (arenaManager.IsLobbySet())
            {
                player.Teleport(arenaManager.GetLobbyPosition());
                SendReply(player, "Welcome to the lobby!");
            }
            else
            {
                SendReply(player, "⚠ Lobby not configured. Admin: use /lobby setpos to set lobby location.");
            }
            
            // Force network re-evaluation for ALL connected players so CanNetworkTo is
            // applied in every direction, making lobby players invisible to each other.
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p != null && p.IsConnected)
                    p.SendNetworkUpdateImmediate();
            }
        }
        
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            // Determine which player "owns" the entity being networked.
            // We treat both BasePlayer entities and HeldEntity items with the same
            // visibility rules, so lobby players and their held items are all hidden.
            BasePlayer subjectPlayer = entity as BasePlayer;
            if (subjectPlayer == null && entity is HeldEntity heldEntity)
                subjectPlayer = heldEntity.GetOwnerPlayer();
            
            // Not a player-owned entity — leave default networking behaviour.
            if (subjectPlayer == null) return null;
            
            // If the subject player is in an active match, only their match opponent
            // (and themselves) should see them.
            if (activeMatches.ContainsKey(subjectPlayer.userID))
            {
                var match = activeMatches[subjectPlayer.userID];
                if (target.userID == match.Player1ID || target.userID == match.Player2ID)
                    return null; // Allow default behaviour (visible to match participants)
                return false;   // Hidden from everyone else
            }
            
            // If the VIEWER (target) is in an active match, only their match opponent
            // should be visible to them.
            if (activeMatches.ContainsKey(target.userID))
            {
                var match = activeMatches[target.userID];
                if (subjectPlayer.userID == match.Player1ID || subjectPlayer.userID == match.Player2ID)
                    return null; // Allow default behaviour
                return false;   // Lobby players (and their items) hidden from match players
            }
            
            // Neither the subject nor the viewer is in a match — both are in the lobby.
            // Always allow a player to see their own entity; hide everyone else.
            if (subjectPlayer.userID == target.userID) return null;
            return false;
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
            bool inNewQueue = GetPlayerQueueType(player.userID).HasValue || GetPlayerCustomQueue(player.userID) != null;
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
                // Check if player is in a private room waiting queue
                var roomID = GetPlayerRoom(player.userID);
                if (roomID != null)
                {
                    LeaveRoom(player, roomID);
                    TeleportToLobby(player);
                    ShowLobbyBrowser(player);
                }
                else
                {
                    SendReply(player, "You're not in queue.");
                }
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
            SendReply(player, "UI elements refreshed!");
            SendReply(player, "Lobby Browser: Right side");
            SendReply(player, "Leaderboard: visible during active matches only");
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
                
                // Restore leave button for players waiting in a Phase 3 queue
                if (GetPlayerQueueType(player.userID).HasValue || GetPlayerCustomQueue(player.userID) != null || queueManager.IsQueued(player.userID))
                {
                    ShowLeaveButton(player);
                }
                
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
        
        private int GetRecentWinsByQueue(PlayerData data, string queueKey, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return data.StatEvents.Count(e => e.Type == "Win" && e.QueueKey == queueKey && e.Timestamp >= cutoff);
        }
        
        private int GetRecentLossesByQueue(PlayerData data, string queueKey, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            return data.StatEvents.Count(e => e.Type == "Loss" && e.QueueKey == queueKey && e.Timestamp >= cutoff);
        }
        
        // Single-pass helper: returns win-rate percentage (0-100) for recent matches in queueKey.
        // Used in both sort lambdas and display to avoid iterating StatEvents twice per comparison.
        private float GetRecentWinRateByQueue(PlayerData data, string queueKey, int minutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-minutes);
            int wins = 0, losses = 0;
            foreach (var e in data.StatEvents)
            {
                if (e.QueueKey == queueKey && e.Timestamp >= cutoff)
                {
                    if (e.Type == "Win")       wins++;
                    else if (e.Type == "Loss") losses++;
                }
            }
            return (wins + losses > 0) ? (100f * wins / (wins + losses)) : 0f;
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
        
        // Maximum character length for custom kit names.
        private const int KitNameMaxLength = 20;
        
        // Minimum vertical anchor for the dynamically-sized gun select panel.
        private const float GunSelectPanelMinBottom = 0.05f;
        
        // The 5 built-in kit names that map directly to DuelMode enum values.
        private static readonly HashSet<string> BuiltInKitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "AK47", "SAR", "Bow", "Revolver", "Speargun" };
        
        // Valid name characters for new custom kit names created with /kit save.
        private static bool IsValidKitName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > KitNameMaxLength) return false;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') return false;
            return true;
        }
        
        [ChatCommand("kit")]
        private void KitCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, "hellisplugin.admin"))
            {
                SendReply(player, "You must be an admin to use /kit.");
                return;
            }
            
            if (args == null || args.Length == 0)
            {
                SendReply(player, "Kit Commands (Admin):");
                SendReply(player, "/kit list             - List all kit mode names");
                SendReply(player, "/kit show <mode>      - Show items in a kit");
                SendReply(player, "/kit save <mode>      - Save your inventory as the kit for <mode>");
                SendReply(player, "/kit reset <mode>     - Reset a built-in kit to defaults");
                SendReply(player, "/kit delete <mode>    - Delete a custom kit");
                SendReply(player, "New custom names: letters/digits/underscore/dash, max 20 chars.");
                return;
            }
            
            string sub = args[0].ToLower();
            
            switch (sub)
            {
                case "list":
                {
                    if (config.Loadouts.Count == 0)
                        SendReply(player, "No kits defined.");
                    else
                        SendReply(player, "Available kits: " + string.Join(", ", config.Loadouts.Keys));
                    break;
                }
                
                case "show":
                {
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /kit show <mode>  (e.g. /kit show AK47)");
                        return;
                    }
                    // Case-insensitive match against config keys
                    string modeName = config.Loadouts.Keys.FirstOrDefault(k =>
                        string.Equals(k, args[1], StringComparison.OrdinalIgnoreCase));
                    if (modeName == null)
                    {
                        SendReply(player, $"Unknown kit '{args[1]}'. Use /kit list to see available kits.");
                        return;
                    }
                    if (config.Loadouts[modeName].Items.Count == 0)
                    {
                        SendReply(player, $"Kit '{modeName}' is empty.");
                        return;
                    }
                    SendReply(player, $"=== Kit: {modeName} ===");
                    foreach (var item in config.Loadouts[modeName].Items)
                    {
                        string skinPart = item.SkinID != 0 ? $" (skin {item.SkinID})" : "";
                        SendReply(player, $"  {item.ShortName}  x{item.Amount}{skinPart}");
                    }
                    break;
                }
                
                case "save":
                {
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /kit save <mode>  (e.g. /kit save Shotgun)");
                        SendReply(player, "Equip yourself with the items you want, then run this command.");
                        return;
                    }
                    string inputName = args[1];
                    if (!IsValidKitName(inputName))
                    {
                        SendReply(player, "Invalid kit name. Use letters, digits, underscores or dashes (max 20 chars).");
                        return;
                    }
                    // Preserve existing casing if the kit already exists, otherwise use as typed
                    string modeName = config.Loadouts.Keys.FirstOrDefault(k =>
                        string.Equals(k, inputName, StringComparison.OrdinalIgnoreCase)) ?? inputName;
                    
                    // Build the new loadout from the admin's current inventory
                    // (main + belt + wear containers, deduplicated by shortname)
                    var newItems = new List<LoadoutItemConfig>();
                    var seen = new Dictionary<string, int>(); // shortname -> index in newItems
                    
                    var allContainers = new [] { player.inventory.containerMain, player.inventory.containerBelt, player.inventory.containerWear };
                    foreach (var container in allContainers)
                    {
                        foreach (Item item in container.itemList)
                        {
                            if (item == null) continue;
                            string sn = item.info.shortname;
                            if (seen.ContainsKey(sn))
                            {
                                newItems[seen[sn]].Amount += item.amount;
                            }
                            else
                            {
                                seen[sn] = newItems.Count;
                                newItems.Add(new LoadoutItemConfig
                                {
                                    ShortName = sn,
                                    Amount    = item.amount,
                                    SkinID    = item.skin
                                });
                            }
                        }
                    }
                    
                    if (newItems.Count == 0)
                    {
                        SendReply(player, "Your inventory is empty. Equip the items you want in the kit first.");
                        return;
                    }
                    
                    config.Loadouts[modeName] = new LoadoutConfig { Items = newItems };
                    SaveConfig();
                    loadoutManager.Reload(config);
                    
                    Puts($"{player.displayName} saved kit '{modeName}' ({newItems.Count} items)");
                    SendReply(player, $"✅ Kit '{modeName}' saved with {newItems.Count} item(s). It now appears in the private room mode selector.");
                    break;
                }
                
                case "reset":
                {
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /kit reset <mode>  (e.g. /kit reset AK47)");
                        SendReply(player, "Only the 5 built-in modes can be reset: AK47, SAR, Bow, Revolver, Speargun");
                        return;
                    }
                    var defaults = Configuration.GetPublicDefaultLoadouts();
                    string modeName = defaults.Keys.FirstOrDefault(k =>
                        string.Equals(k, args[1], StringComparison.OrdinalIgnoreCase));
                    if (modeName == null)
                    {
                        SendReply(player, $"No built-in default exists for '{args[1]}'. Built-in modes: AK47, SAR, Bow, Revolver, Speargun");
                        return;
                    }
                    
                    config.Loadouts[modeName] = defaults[modeName];
                    SaveConfig();
                    loadoutManager.Reload(config);
                    
                    Puts($"{player.displayName} reset kit '{modeName}' to defaults");
                    SendReply(player, $"✅ Kit '{modeName}' reset to built-in defaults.");
                    break;
                }
                
                case "delete":
                {
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /kit delete <mode>  (e.g. /kit delete Shotgun)");
                        SendReply(player, "The 5 built-in kits cannot be deleted, only custom ones.");
                        return;
                    }
                    if (BuiltInKitNames.Contains(args[1]))
                    {
                        SendReply(player, $"Cannot delete built-in kit '{args[1]}'. Use /kit reset to restore defaults.");
                        return;
                    }
                    string modeName = config.Loadouts.Keys.FirstOrDefault(k =>
                        string.Equals(k, args[1], StringComparison.OrdinalIgnoreCase));
                    if (modeName == null)
                    {
                        SendReply(player, $"Kit '{args[1]}' does not exist.");
                        return;
                    }
                    
                    config.Loadouts.Remove(modeName);
                    SaveConfig();
                    loadoutManager.Reload(config);
                    
                    Puts($"{player.displayName} deleted kit '{modeName}'");
                    SendReply(player, $"✅ Kit '{modeName}' deleted.");
                    break;
                }
                
                default:
                    SendReply(player, $"Unknown sub-command '{args[0]}'. Use /kit for help.");
                    break;
            }
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
                SendReply(player, "/arena setkit <name> <kit> - Set arena kit (replaces pool)");
                SendReply(player, "/arena addkit <name> <kit> - Add kit to random pool");
                SendReply(player, "/arena removekit <name> <kit> - Remove kit from pool");
                SendReply(player, "/arena clearkit <name> - Remove all kit overrides");
                SendReply(player, "/lobby setpos - Set lobby position");
                SendReply(player, "/lobby setradius <radius> - Set lobby zone radius");
                SendReply(player, "/clearleaderboard - Clear all leaderboard data (requires confirm)");
                SendReply(player, "/kit save <mode>      - Save your inventory as a kit (any name)");
                SendReply(player, "/kit show <mode>      - Show items in a kit");
                SendReply(player, "/kit list             - List all kit names");
                SendReply(player, "/kit reset <mode>     - Reset a built-in kit to defaults");
                SendReply(player, "/kit delete <mode>    - Delete a custom kit");
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
                    SaveLobbyData();
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
                        SaveLobbyData();
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
                                 "/arena edit <name> - Edit an existing arena (shows zone & spawns)\n" +
                                 "/arena setspawn1 - Set first spawn point\n" +
                                 "/arena setspawn2 - Set second spawn point\n" +
                                 "/arena setradius <radius> - Set zone radius during create/edit (default: 30m)\n" +
                                 "/arena setlobbyspawn - Set per-arena lobby spawn during create/edit\n" +
                                 "/arena setlobbyradius <radius> - Set lobby zone radius during create/edit (default: 10m)\n" +
                                 "/arena save - Save the arena\n" +
                                 "/arena cancel - Cancel arena creation/editing\n" +
                                 "/arena list - List all arenas\n" +
                                 "/arena delete <name> - Delete an arena\n" +
                                 "/arena setradius <name> <radius> - Change radius of a saved arena\n" +
                                 "/arena setkit <name> <kitName> - Set arena kit (replaces pool)\n" +
                                 "/arena addkit <name> <kitName> - Add kit to random pool\n" +
                                 "/arena removekit <name> <kitName> - Remove kit from pool\n" +
                                 "/arena clearkit <name> - Remove all kit overrides\n" +
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
                    
                case "edit":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /arena edit <name>");
                        return;
                    }
                    EditArena(player, string.Join(" ", args.Skip(1)));
                    break;
                    
                case "setspawn1":
                    SetSpawn1(player);
                    break;
                    
                case "setspawn2":
                    SetSpawn2(player);
                    break;
                    
                case "setlobbyspawn":
                    SetLobbySpawn(player);
                    break;
                    
                case "setlobbyradius":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /arena setlobbyradius <radius>");
                        return;
                    }
                    SetLobbyRadius(player, args[1]);
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
                    // During an active creation session: /arena setradius <radius>
                    if (args.Length == 2 && arenaBuilders.ContainsKey(player.userID))
                    {
                        float newRadius;
                        if (!float.TryParse(args[1], out newRadius) || newRadius <= 0)
                        {
                            SendReply(player, "Invalid radius. Please enter a positive number.");
                            return;
                        }
                        var builderForRadius = arenaBuilders[player.userID];
                        builderForRadius.Radius = newRadius;
                        SendReply(player, $"Arena radius set to {newRadius}m. Zone preview updated.");
                        // Restart visualization to immediately reflect the new radius
                        if (builderForRadius.Spawn1 != Vector3.zero)
                            StartBuilderVisualization(player, builderForRadius);
                        return;
                    }
                    
                    // Saved arena: /arena setradius <name> <radius>
                    if (args.Length < 3)
                    {
                        SendReply(player, "Usage: /arena setradius <radius>  (during creation)\n" +
                                         "       /arena setradius <name> <radius>  (saved arena)");
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
                    
                case "setkit":
                {
                    if (args.Length < 3)
                    {
                        SendReply(player, "Usage: /arena setkit <arenaName> <kitName>");
                        return;
                    }
                    // Last arg is the kit name; args[1] through args[Length-2] form the arena name.
                    string kitName = args[args.Length - 1];
                    string arenaNameForKit = string.Join(" ", args.Skip(1).Take(args.Length - 2));
                    
                    // Validate kit exists
                    string resolvedKit = config.Loadouts.Keys.FirstOrDefault(k =>
                        string.Equals(k, kitName, StringComparison.OrdinalIgnoreCase));
                    if (resolvedKit == null)
                    {
                        SendReply(player, $"Unknown kit '{kitName}'. Use /kit list to see available kits.");
                        return;
                    }
                    
                    // Update ArenaConfig (data file) and live Arena object
                    var arenaConfigForKit = arenas.FirstOrDefault(a =>
                        a.Name.Equals(arenaNameForKit, StringComparison.OrdinalIgnoreCase));
                    if (arenaConfigForKit == null)
                    {
                        SendReply(player, $"Arena '{arenaNameForKit}' not found.");
                        return;
                    }
                    arenaConfigForKit.KitOverrides = new List<string> { resolvedKit };
                    SaveArenas();
                    arenaManager.SetArenaKitOverrides(arenaNameForKit, arenaConfigForKit.KitOverrides);
                    SendReply(player, $"Arena '{arenaConfigForKit.Name}' kit pool set to: [{resolvedKit}]. Use /arena addkit to add more.");
                    break;
                }
                    
                case "addkit":
                {
                    if (args.Length < 3)
                    {
                        SendReply(player, "Usage: /arena addkit <arenaName> <kitName>");
                        return;
                    }
                    // Last arg is the kit name; args[1] through args[Length-2] form the arena name.
                    string kitName = args[args.Length - 1];
                    string arenaNameForAddKit = string.Join(" ", args.Skip(1).Take(args.Length - 2));
                    
                    string resolvedAddKit = config.Loadouts.Keys.FirstOrDefault(k =>
                        string.Equals(k, kitName, StringComparison.OrdinalIgnoreCase));
                    if (resolvedAddKit == null)
                    {
                        SendReply(player, $"Unknown kit '{kitName}'. Use /kit list to see available kits.");
                        return;
                    }
                    
                    var arenaConfigForAddKit = arenas.FirstOrDefault(a =>
                        a.Name.Equals(arenaNameForAddKit, StringComparison.OrdinalIgnoreCase));
                    if (arenaConfigForAddKit == null)
                    {
                        SendReply(player, $"Arena '{arenaNameForAddKit}' not found.");
                        return;
                    }
                    if (arenaConfigForAddKit.KitOverrides.Contains(resolvedAddKit))
                    {
                        SendReply(player, $"Kit '{resolvedAddKit}' is already in arena '{arenaConfigForAddKit.Name}' kit list.");
                        return;
                    }
                    arenaConfigForAddKit.KitOverrides.Add(resolvedAddKit);
                    SaveArenas();
                    arenaManager.SetArenaKitOverrides(arenaNameForAddKit, arenaConfigForAddKit.KitOverrides);
                    SendReply(player, $"Kit '{resolvedAddKit}' added to arena '{arenaConfigForAddKit.Name}'. " +
                                     $"Kit list: {string.Join(", ", arenaConfigForAddKit.KitOverrides)}");
                    break;
                }
                    
                case "removekit":
                {
                    if (args.Length < 3)
                    {
                        SendReply(player, "Usage: /arena removekit <arenaName> <kitName>");
                        return;
                    }
                    string kitName = args[args.Length - 1];
                    string arenaNameForRemoveKit = string.Join(" ", args.Skip(1).Take(args.Length - 2));
                    
                    var arenaConfigForRemoveKit = arenas.FirstOrDefault(a =>
                        a.Name.Equals(arenaNameForRemoveKit, StringComparison.OrdinalIgnoreCase));
                    if (arenaConfigForRemoveKit == null)
                    {
                        SendReply(player, $"Arena '{arenaNameForRemoveKit}' not found.");
                        return;
                    }
                    // Case-insensitive removal
                    string existing = arenaConfigForRemoveKit.KitOverrides?.FirstOrDefault(k =>
                        string.Equals(k, kitName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        SendReply(player, $"Kit '{kitName}' is not in arena '{arenaConfigForRemoveKit.Name}' kit list.");
                        return;
                    }
                    arenaConfigForRemoveKit.KitOverrides.Remove(existing);
                    SaveArenas();
                    arenaManager.SetArenaKitOverrides(arenaNameForRemoveKit, arenaConfigForRemoveKit.KitOverrides);
                    string remaining = arenaConfigForRemoveKit.KitOverrides.Count > 0
                        ? string.Join(", ", arenaConfigForRemoveKit.KitOverrides)
                        : "(none - uses mode-based kit)";
                    SendReply(player, $"Kit '{existing}' removed from arena '{arenaConfigForRemoveKit.Name}'. Remaining: {remaining}");
                    break;
                }
                    
                case "clearkit":
                {
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /arena clearkit <arenaName>");
                        return;
                    }
                    string arenaNameToClear = string.Join(" ", args.Skip(1));
                    var arenaConfigToClear = arenas.FirstOrDefault(a =>
                        a.Name.Equals(arenaNameToClear, StringComparison.OrdinalIgnoreCase));
                    if (arenaConfigToClear == null)
                    {
                        SendReply(player, $"Arena '{arenaNameToClear}' not found.");
                        return;
                    }
                    arenaConfigToClear.KitOverrides = new List<string>();
                    SaveArenas();
                    arenaManager.SetArenaKitOverrides(arenaNameToClear, new List<string>());
                    SendReply(player, $"Arena '{arenaConfigToClear.Name}' kit overrides cleared (uses mode-based kit).");
                    break;
                }
                    
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
        
        private void StartDuel(BasePlayer player1, BasePlayer player2, DuelMode mode, string roomID = null, QueueType? sourceQueueType = null, string customModeName = null)
        {
            var arena = arenaManager.GetAvailableArena();
            if (arena == null)
            {
                SendReply(player1, "No arenas available. Please wait.");
                SendReply(player2, "No arenas available. Please wait.");
                if (roomID != null && privateRooms.ContainsKey(roomID))
                {
                    // Re-add to room waiting queue on failure
                    privateRooms[roomID].WaitingQueue.Add(player1.userID);
                    privateRooms[roomID].WaitingQueue.Add(player2.userID);
                }
                else if (sourceQueueType.HasValue)
                {
                    JoinQueueByType(player1, sourceQueueType.Value);
                    JoinQueueByType(player2, sourceQueueType.Value);
                }
                else if (mode == DuelMode.Custom && customModeName != null && customQueuesByName.ContainsKey(customModeName))
                {
                    JoinCustomQueue(player1, customModeName);
                    JoinCustomQueue(player2, customModeName);
                }
                else
                {
                    queueManager.JoinQueue(player1.userID, player1.displayName, mode);
                    queueManager.JoinQueue(player2.userID, player2.displayName, mode);
                }
                return;
            }
            
            // Get instance ID for this match
            int instanceId = arena.GetNextInstanceId();
            if (instanceId == -1)
            {
                SendReply(player1, "No arena instances available. Please wait.");
                SendReply(player2, "No arena instances available. Please wait.");
                if (roomID != null && privateRooms.ContainsKey(roomID))
                {
                    privateRooms[roomID].WaitingQueue.Add(player1.userID);
                    privateRooms[roomID].WaitingQueue.Add(player2.userID);
                }
                else if (sourceQueueType.HasValue)
                {
                    JoinQueueByType(player1, sourceQueueType.Value);
                    JoinQueueByType(player2, sourceQueueType.Value);
                }
                else if (mode == DuelMode.Custom && customModeName != null && customQueuesByName.ContainsKey(customModeName))
                {
                    JoinCustomQueue(player1, customModeName);
                    JoinCustomQueue(player2, customModeName);
                }
                else
                {
                    queueManager.JoinQueue(player1.userID, player1.displayName, mode);
                    queueManager.JoinQueue(player2.userID, player2.displayName, mode);
                }
                return;
            }
            
            var match = new ActiveMatch(player1.userID, player2.userID, mode, arena, instanceId, config.BestOfRounds);
            match.RoomID = roomID;
            match.SourceQueueType = sourceQueueType;
            match.CustomModeName = customModeName;
            // Pick the kit once for the whole match so both players and all rounds share the same kit.
            match.ResolvedArenaKit = arena.PickRandomKitOverride();
            activeMatches[player1.userID] = match;
            activeMatches[player2.userID] = match;
            
            // Force network re-evaluation for the two newly matched players so CanNetworkTo
            // immediately hides them from lobby players and from players in other concurrent
            // matches sharing the same arena. Sending the update for each player causes the
            // server to re-check CanNetworkTo for every client, which drops visibility for
            // clients that are no longer permitted to see these players.
            player1.SendNetworkUpdateImmediate();
            player2.SendNetworkUpdateImmediate();
            
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
            GiveLoadout(player1, mode, customModeName, match.ResolvedArenaKit);
            GiveLoadout(player2, mode, customModeName, match.ResolvedArenaKit);
            
            // Hide lobby UI and join button during match; show leave button
            DestroyLobbyBrowser(player1);
            DestroyLobbyBrowser(player2);
            DestroyJoinButton(player1);
            DestroyJoinButton(player2);
            DestroyLeaveButton(player1);
            DestroyLeaveButton(player2);
            ShowLeaveButton(player1);
            ShowLeaveButton(player2);
            
            // Immediately show leaderboard scoped to this match's queue
            ShowLeaderboardUI(player1);
            ShowLeaderboardUI(player2);
            
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
                    ResetPlayerForNextRound(player1, match.Arena.Spawn1, match.Mode, match.CustomModeName, match.ResolvedArenaKit);
                }
                
                if (player2 != null)
                {
                    SendReply(player2, $"Round {match.CurrentRound}/{match.BestOfRounds} - Score: {match.Player1Score}-{match.Player2Score}");
                    ResetPlayerForNextRound(player2, match.Arena.Spawn2, match.Mode, match.CustomModeName, match.ResolvedArenaKit);
                }
                
                timer.Once(config.CountdownDuration, () => StartRound(match));
            }
        }
        
        private void ResetPlayerForNextRound(BasePlayer player, Vector3 spawnPos, DuelMode mode, string customModeName = null, string arenaKitOverride = null)
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
            GiveLoadout(player, mode, customModeName, arenaKitOverride);
            
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
            
            // Capture room context before removing from active matches
            string matchRoomID = match.RoomID;
            
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
                
                // Return to spawn
                ReturnPlayerToLobby(player1);
                
                // For room matches, re-add to room waiting queue; otherwise standard auto-requeue
                if (matchRoomID != null && privateRooms.ContainsKey(matchRoomID))
                {
                    var room = privateRooms[matchRoomID];
                    if (room.PlayerIDs.Contains(match.Player1ID))
                    {
                        room.WaitingQueue.Add(match.Player1ID);
                        SendReply(player1, $"Back in {room.RoomName}'s room queue. Waiting for next match...");
                    }
                }
                else if (config.AutoRequeue && !disconnect && !autoRequeueOptOut.Contains(player1.userID))
                {
                    if (IsCustomPublicQueueMatch(match))
                    {
                        // Custom public-queue match — re-add to custom queue
                        if (customQueuesByName.ContainsKey(match.CustomModeName) && !customQueuesByName[match.CustomModeName].Contains(player1.userID))
                            customQueuesByName[match.CustomModeName].Add(player1.userID);
                        SendReply(player1, $"Auto-requeued for {match.CustomModeName} match!");
                    }
                    else
                    {
                        var targetQueue = match.SourceQueueType ?? QueueType.Public;
                        if (!queuesByType.ContainsKey(targetQueue))
                            queuesByType[targetQueue] = new List<ulong>();
                        if (!queuesByType[targetQueue].Contains(player1.userID))
                            queuesByType[targetQueue].Add(player1.userID);
                        SendReply(player1, $"Auto-requeued for {GetQueueLabel(targetQueue)} match!");
                    }
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
                
                if (matchRoomID != null && privateRooms.ContainsKey(matchRoomID))
                {
                    var room = privateRooms[matchRoomID];
                    if (room.PlayerIDs.Contains(match.Player2ID))
                    {
                        room.WaitingQueue.Add(match.Player2ID);
                        SendReply(player2, $"Back in {room.RoomName}'s room queue. Waiting for next match...");
                    }
                }
                else if (config.AutoRequeue && !disconnect && !autoRequeueOptOut.Contains(player2.userID))
                {
                    if (IsCustomPublicQueueMatch(match))
                    {
                        if (customQueuesByName.ContainsKey(match.CustomModeName) && !customQueuesByName[match.CustomModeName].Contains(player2.userID))
                            customQueuesByName[match.CustomModeName].Add(player2.userID);
                        SendReply(player2, $"Auto-requeued for {match.CustomModeName} match!");
                    }
                    else
                    {
                        var targetQueue = match.SourceQueueType ?? QueueType.Public;
                        if (!queuesByType.ContainsKey(targetQueue))
                            queuesByType[targetQueue] = new List<ulong>();
                        if (!queuesByType[targetQueue].Contains(player2.userID))
                            queuesByType[targetQueue].Add(player2.userID);
                        SendReply(player2, $"Auto-requeued for {GetQueueLabel(targetQueue)} match!");
                    }
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
            
            // For room matches, try to start the next match from the waiting queue
            if (matchRoomID != null && privateRooms.ContainsKey(matchRoomID))
            {
                timer.Once(1.5f, () => TryRoomMatchmaking(matchRoomID));
            }
            
            // Refresh UI for both players — delay until after the 2 s WinLose overlay has dismissed
            timer.Once(2.5f, () =>
            {
                if (player1 != null && player1.IsConnected && !activeMatches.ContainsKey(player1.userID))
                {
                    DestroyLeaveButton(player1);
                    DestroyLeaderboardUI(player1); // Hide leaderboard when returning to lobby
                    ShowLobbyBrowser(player1);
                    if (GetPlayerQueueType(player1.userID).HasValue)
                        ShowLeaveButton(player1);
                    // Show pending join requests to room owner after match
                    var ownedRoomID = GetOwnedRoom(player1.userID);
                    if (ownedRoomID != null && privateRooms.ContainsKey(ownedRoomID) &&
                        privateRooms[ownedRoomID].PendingRequests.Count > 0)
                    {
                        ShowJoinRequestUI(player1, ownedRoomID);
                    }
                }
                if (player2 != null && player2.IsConnected && !activeMatches.ContainsKey(player2.userID))
                {
                    DestroyLeaveButton(player2);
                    DestroyLeaderboardUI(player2); // Hide leaderboard when returning to lobby
                    ShowLobbyBrowser(player2);
                    if (GetPlayerQueueType(player2.userID).HasValue)
                        ShowLeaveButton(player2);
                    var ownedRoomID = GetOwnedRoom(player2.userID);
                    if (ownedRoomID != null && privateRooms.ContainsKey(ownedRoomID) &&
                        privateRooms[ownedRoomID].PendingRequests.Count > 0)
                    {
                        ShowJoinRequestUI(player2, ownedRoomID);
                    }
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
            
            // Force network re-evaluation for ALL connected players so CanNetworkTo is
            // applied in every direction, making lobby players invisible to each other.
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p != null && p.IsConnected)
                    p.SendNetworkUpdateImmediate();
            }
        }
        
        private void GiveLoadout(BasePlayer player, DuelMode mode, string customModeName = null, string arenaKitOverride = null)
        {
            if (player == null) return;
            
            player.inventory.Strip();
            player.metabolism.calories.value = 500;
            player.metabolism.hydration.value = 250;
            player.health = 100;
            
            // Priority: arena kit override > custom mode name > mode-based loadout
            Loadout loadout;
            if (arenaKitOverride != null)
                loadout = loadoutManager.GetLoadoutByName(arenaKitOverride);
            else if (mode == DuelMode.Custom && customModeName != null)
                loadout = loadoutManager.GetLoadoutByName(customModeName);
            else
                loadout = loadoutManager.GetLoadout(mode);
            
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
        
        private void UpdatePlayerStats(ulong playerID, bool won, ActiveMatch match)
        {
            if (!playerData.ContainsKey(playerID))
            {
                playerData[playerID] = new PlayerData();
            }
            
            var data = playerData[playerID];
            data.TotalMatches++;
            data.LastMatchTime = DateTime.Now; // Update timestamp for leaderboard filtering
            
            // Determine queue key before recording stat events so it can be stored per-event
            string queueKey = GetMatchQueueKey(match);
            
            if (won)
            {
                data.Wins++;
                data.StatEvents.Add(new StatEvent { Type = "Win", Timestamp = DateTime.Now, QueueKey = queueKey });
            }
            else
            {
                data.Losses++;
                data.StatEvents.Add(new StatEvent { Type = "Loss", Timestamp = DateTime.Now, QueueKey = queueKey });
            }
            
            // Record per-queue win/loss (cumulative, kept for compatibility)
            var queueDict = won ? data.WinsByQueue : data.LossesByQueue;
            if (!queueDict.ContainsKey(queueKey)) queueDict[queueKey] = 0;
            queueDict[queueKey]++;
            
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
        
        // Returns the leaderboard key for the queue type a match was played in.
        // RoomID is checked first so private-room matches are never misclassified as public.
        // The SourceQueueType fallback to "Public" only applies to the legacy queueManager system,
        // which only ever ran public (random-mode) matches.
        private string GetMatchQueueKey(ActiveMatch match)
        {
            if (match.RoomID != null) return "Private";
            // Custom public-queue match (no SourceQueueType, but has a custom kit name)
            if (match.Mode == DuelMode.Custom && match.CustomModeName != null && !match.SourceQueueType.HasValue)
                return match.CustomModeName;
            if (!match.SourceQueueType.HasValue) return "Public"; // Legacy fallback
            switch (match.SourceQueueType.Value)
            {
                case QueueType.PublicAK:       return "AK";
                case QueueType.PublicBow:      return "Bow";
                case QueueType.PublicSpeargun: return "Speargun";
                default:                       return "Public";
            }
        }
        
        // Returns true when a match was started from a custom public-queue (not a private room,
        // not a built-in typed queue) and is therefore associated with customQueuesByName.
        private bool IsCustomPublicQueueMatch(ActiveMatch match) =>
            match.RoomID == null &&
            match.Mode == DuelMode.Custom &&
            match.CustomModeName != null &&
            !match.SourceQueueType.HasValue;
        
        // Returns a human-readable label for a QueueType (used in chat messages).
        private string GetQueueLabel(QueueType queueType)
        {
            switch (queueType)
            {
                case QueueType.PublicAK:       return "AK";
                case QueueType.PublicBow:      return "Bow";
                case QueueType.PublicSpeargun: return "Speargun";
                default:                       return "Public";
            }
        }
        
        // Returns win rate (0-100) for a specific queue key, or 0 when no games played.
        private float GetQueueWinRate(PlayerData data, string queueKey)
        {
            int w = data.WinsByQueue.ContainsKey(queueKey)  ? data.WinsByQueue[queueKey]  : 0;
            int l = data.LossesByQueue.ContainsKey(queueKey) ? data.LossesByQueue[queueKey] : 0;
            int total = w + l;
            return total > 0 ? (float)w / total * 100f : 0f;
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
            
            // Smaller button positioned to the right of center
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.8 0.3 0.2 0.9" }, // Orange/Red
                RectTransform = { AnchorMin = "0.65 0.072", AnchorMax = "0.78 0.108" },
                CursorEnabled = false  // Don't capture cursor
            }, "Hud", "LeaveButton");
            
            // Clickable button (fills panel, also carries the label)
            elements.Add(new CuiButton
            {
                Button = { Command = "leavebutton.click", Color = "0 0 0 0" }, // Transparent overlay
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "leave", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
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
            
            // Main panel - top right, dark gray background
            // CursorEnabled = false so the panel doesn't capture the cursor and lock camera rotation
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.17 0.17 0.17 0.95" }, // Dark gray #2B2B2B
                RectTransform = { AnchorMin = "0.70 0.30", AnchorMax = "0.98 0.99" },
                CursorEnabled = false
            }, "Hud", "LobbyBrowser");
            
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
            
            // Public Bow queue
            queueY -= 0.08f;
            int bowCount = queuesByType.ContainsKey(QueueType.PublicBow) ? queuesByType[QueueType.PublicBow].Count : 0;
            AddQueueEntry(elements, "LobbyBrowser", "Public Bow", $"({bowCount} Players)", queueY, "joinqueue.bow");
            
            // Public Speargun queue (shown only when enabled)
            if (config.EnableSpeargun)
            {
                queueY -= 0.08f;
                int spearCount = queuesByType.ContainsKey(QueueType.PublicSpeargun) ? queuesByType[QueueType.PublicSpeargun].Count : 0;
                AddQueueEntry(elements, "LobbyBrowser", "Speargun", $"({spearCount} Players)", queueY, "joinqueue.spear");
            }
            
            // Public custom-kit queues (one row per custom loadout in config)
            foreach (var kvp in customQueuesByName)
            {
                queueY -= 0.08f;
                int customCount = kvp.Value.Count;
                AddQueueEntry(elements, "LobbyBrowser", kvp.Key, $"({customCount} Players)", queueY, $"joinqueue.custom {kvp.Key}");
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
            
            // Private room listings — show up to 4 rooms
            float roomY = privateStartY - 0.08f;
            int roomCount = 0;
            foreach (var room in privateRooms.Values.Take(4))
            {
                AddRoomEntry(elements, "LobbyBrowser", room, roomY, player.userID);
                roomY -= 0.08f;
                roomCount++;
            }
            
            // If no rooms, show placeholder
            if (roomCount == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text = { Text = "No private rooms available", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
                    RectTransform = { AnchorMin = $"0.05 {roomY}", AnchorMax = $"0.95 {roomY + 0.06f}" }
                }, "LobbyBrowser");
            }
            
            // CREATE ROOM button — single click creates a room; mode is chosen afterwards via the GUNS button
            float createButtonY = 0.055f;
            
            bool ownsRoom = privateRooms.Values.Any(r => r.OwnerID == player.userID);
            if (ownsRoom)
            {
                // Player already has a room — show disabled state
                elements.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.3 0.3 0.3 0.6" },
                    RectTransform = { AnchorMin = $"0.10 {createButtonY}", AnchorMax = $"0.90 {createButtonY + 0.06f}" },
                    Text = { Text = "YOU HAVE A ROOM", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "LobbyBrowser");
            }
            else
            {
                // Single CREATE ROOM button — mode is chosen after creation via the GUNS button
                elements.Add(new CuiButton
                {
                    Button = { Command = "lobby.createroom", Color = "0 0.8 0.82 0.8" },
                    RectTransform = { AnchorMin = $"0.10 {createButtonY}", AnchorMax = $"0.90 {createButtonY + 0.06f}" },
                    Text = { Text = "CREATE ROOM", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "LobbyBrowser");
            }
            
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
        
        private void AddRoomEntry(CuiElementContainer elements, string parent, PrivateRoom room, float yPos, ulong viewerID)
        {
            // Background panel for room entry (taller to accommodate mode label + buttons)
            string entryName = $"{parent}.Room.{room.RoomID}";
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.12 0.12 0.8" },
                RectTransform = { AnchorMin = $"0.05 {yPos}", AnchorMax = $"0.95 {yPos + 0.07f}" }
            }, parent, entryName);
            
            // Room name: "OwnerName's Room (X)"
            string roomText = $"{room.OwnerName} ({room.PlayerIDs.Count})";
            elements.Add(new CuiLabel
            {
                Text = { Text = roomText, FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0.50", AnchorMax = "0.55 1" }
            }, entryName);
            
            // Mode label — show custom name when applicable
            string modeLabel = room.CustomModeName != null ? $"[{room.CustomModeName}]" : $"[{room.Mode}]";
            elements.Add(new CuiLabel
            {
                Text = { Text = modeLabel, FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0 0.8 0.82 1" },
                RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.45 0.50" }
            }, entryName);
            
            bool isOwner = viewerID == room.OwnerID;
            bool isMember = room.PlayerIDs.Contains(viewerID);
            bool hasPending = room.PendingRequests.ContainsKey(viewerID);
            
            if (isOwner)
            {
                // Owner: GUNS button + LEAVE button
                elements.Add(new CuiButton
                {
                    Button = { Command = "lobby.roomguns", Color = "0.25 0.35 0.75 0.9" },
                    RectTransform = { AnchorMin = "0.55 0.15", AnchorMax = "0.74 0.85" },
                    Text = { Text = "GUNS", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, entryName);
                
                elements.Add(new CuiButton
                {
                    Button = { Command = "lobby.leaveroom", Color = "0.7 0.2 0.1 0.9" },
                    RectTransform = { AnchorMin = "0.76 0.15", AnchorMax = "0.95 0.85" },
                    Text = { Text = "LEAVE", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, entryName);
                
                // Show pending request badge
                if (room.PendingRequests.Count > 0)
                {
                    elements.Add(new CuiLabel
                    {
                        Text = { Text = $"▲ {room.PendingRequests.Count} request(s)", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 0.7 0.1 1" },
                        RectTransform = { AnchorMin = "0.45 0.05", AnchorMax = "0.97 0.50" }
                    }, entryName);
                }
            }
            else if (isMember)
            {
                // Member: LEAVE button
                elements.Add(new CuiButton
                {
                    Button = { Command = "lobby.leaveroom", Color = "0.7 0.2 0.1 0.9" },
                    RectTransform = { AnchorMin = "0.76 0.15", AnchorMax = "0.95 0.85" },
                    Text = { Text = "LEAVE", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, entryName);
            }
            else if (hasPending)
            {
                // Has a pending request — show greyed out indicator
                elements.Add(new CuiLabel
                {
                    Text = { Text = "PENDING...", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.1 1" },
                    RectTransform = { AnchorMin = "0.65 0.15", AnchorMax = "0.95 0.85" }
                }, entryName);
            }
            else
            {
                // Non-member: REQUEST button
                elements.Add(new CuiButton
                {
                    Button = { Command = $"lobby.requestjoin {room.RoomID}", Color = "0 0.8 0.82 1" },
                    RectTransform = { AnchorMin = "0.68 0.15", AnchorMax = "0.95 0.85" },
                    Text = { Text = "REQUEST", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, entryName);
            }
        }
        
        private void DestroyLobbyBrowser(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "LobbyBrowser");
        }
        
        private void ShowJoinRequestUI(BasePlayer owner, string roomID)
        {
            if (owner == null || !owner.IsConnected || !privateRooms.ContainsKey(roomID)) return;
            
            // Don't interrupt an active match
            if (activeMatches.ContainsKey(owner.userID)) return;
            
            var room = privateRooms[roomID];
            
            DestroyJoinRequestUI(owner);
            
            if (room.PendingRequests.Count == 0) return;
            
            var elements = new CuiElementContainer();
            
            // Panel sits at top-center, height depends on number of requests (up to 3)
            int shown = Math.Min(room.PendingRequests.Count, 3);
            float panelH = 0.07f + shown * 0.09f;
            float panelBottom = 0.98f - panelH;
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.13 0.13 0.13 0.97" },
                RectTransform = { AnchorMin = $"0.30 {panelBottom:F4}", AnchorMax = "0.70 0.98" },
                CursorEnabled = false
            }, "Hud", "JoinRequestUI");
            
            elements.Add(new CuiLabel
            {
                Text = { Text = "JOIN REQUESTS", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0 0.8 0.82 1" },
                RectTransform = { AnchorMin = "0 0.86", AnchorMax = "1 1" }
            }, "JoinRequestUI");
            
            float rowY = 0.84f;
            int count = 0;
            foreach (var kvp in room.PendingRequests)
            {
                if (count >= 3) break;
                ulong requesterID = kvp.Key;
                string requesterName = kvp.Value;
                
                string rowName = $"JoinRequestUI.Row.{requesterID}";
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0.10 0.10 0.10 0.9" },
                    RectTransform = { AnchorMin = $"0.04 {rowY - 0.24f:F4}", AnchorMax = $"0.96 {rowY:F4}" }
                }, "JoinRequestUI", rowName);
                
                elements.Add(new CuiLabel
                {
                    Text = { Text = requesterName, FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.05 0", AnchorMax = "0.50 1" }
                }, rowName);
                
                elements.Add(new CuiButton
                {
                    Button = { Command = $"lobby.accept {requesterID}", Color = "0.1 0.65 0.1 0.9" },
                    RectTransform = { AnchorMin = "0.52 0.12", AnchorMax = "0.74 0.88" },
                    Text = { Text = "ACCEPT", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, rowName);
                
                elements.Add(new CuiButton
                {
                    Button = { Command = $"lobby.decline {requesterID}", Color = "0.65 0.1 0.1 0.9" },
                    RectTransform = { AnchorMin = "0.76 0.12", AnchorMax = "0.96 0.88" },
                    Text = { Text = "DECLINE", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, rowName);
                
                rowY -= 0.28f;
                count++;
            }
            
            CuiHelper.AddUi(owner, elements);
            activeJoinRequestUIs.Add(owner.userID);
        }
        
        private void DestroyJoinRequestUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "JoinRequestUI");
            activeJoinRequestUIs.Remove(player.userID);
        }
        
        private void ShowGunSelectUI(BasePlayer player, string roomID)
        {
            if (player == null || !privateRooms.ContainsKey(roomID)) return;
            var room = privateRooms[roomID];
            if (room.OwnerID != player.userID) return;
            
            CuiHelper.DestroyUi(player, "RoomGunSelect");
            
            // Build the mode list first so we can size the panel accordingly
            var modeList = new List<(string Label, string ModeArg)>();
            modeList.Add(("AK47", "AK47"));
            modeList.Add(("SAR", "SAR"));
            modeList.Add(("Bow", "Bow"));
            modeList.Add(("Revolver", "Revolver"));
            modeList.Add(("Random", "Any"));
            if (config.EnableSpeargun)
                modeList.Add(("Speargun", "Speargun"));
            // Custom modes: any config loadout key not in the built-in set
            foreach (var key in config.Loadouts.Keys)
            {
                if (!BuiltInKitNames.Contains(key))
                    modeList.Add((key, key));
            }
            
            // Dynamically size the panel: each button up to 0.11 + 0.01 gap, header 0.12, close button 0.10 + margins
            float gap       = 0.01f;
            float closeBtnH = 0.10f;
            // Scale button height down when there are many modes so everything fits within the panel.
            // Available relative height for buttons = btnStartY(0.84) - closeBtnH - 2*gap
            float btnH = modeList.Count > 0
                ? Math.Min(0.11f, (0.84f - closeBtnH - 2f * gap) / modeList.Count - gap)
                : 0.11f;
            int   btnFontSize  = btnH >= 0.095f ? 13 : (btnH >= 0.075f ? 11 : 10);
            float panelContentH = 0.12f + modeList.Count * (btnH + gap) + closeBtnH + 0.03f; // header + buttons + close
            float panelTop    = 0.95f;
            float panelBottom = Math.Max(GunSelectPanelMinBottom, panelTop - panelContentH);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.13 0.13 0.13 0.97" },
                RectTransform = { AnchorMin = $"0.38 {panelBottom:F4}", AnchorMax = $"0.62 {panelTop:F4}" },
                CursorEnabled = false
            }, "Hud", "RoomGunSelect");
            
            elements.Add(new CuiLabel
            {
                Text = { Text = "SELECT WEAPON MODE", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0 0.8 0.82 1" },
                RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 1" }
            }, "RoomGunSelect");
            
            // Determine currently-active mode label for the room
            string activeLabel = room.CustomModeName ?? (room.Mode == DuelMode.Any ? "Any" : room.Mode.ToString());
            
            float btnY = 0.84f;
            foreach (var (label, modeArg) in modeList)
            {
                bool selected = string.Equals(activeLabel, modeArg, StringComparison.OrdinalIgnoreCase)
                             || (modeArg == "Any" && activeLabel == "Random");
                string btnColor = selected ? "0.1 0.55 0.1 0.95" : "0.22 0.22 0.22 0.95";
                string checkmark = selected ? " ✓" : "";
                
                elements.Add(new CuiButton
                {
                    Button = { Command = $"lobby.roomsetmode {modeArg}", Color = btnColor },
                    RectTransform = { AnchorMin = $"0.08 {btnY - btnH:F4}", AnchorMax = $"0.92 {btnY:F4}" },
                    Text = { Text = $"{label}{checkmark}", FontSize = btnFontSize, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "RoomGunSelect");
                
                btnY -= btnH + gap;
            }
            
            // CLOSE button — position dynamically below last mode button so it never overlaps
            float closeBtnMax = btnY - gap;
            float closeBtnMin = closeBtnMax - closeBtnH;
            elements.Add(new CuiButton
            {
                Button = { Command = "lobby.closeguns", Color = "0.55 0.1 0.1 0.9" },
                RectTransform = { AnchorMin = $"0.08 {closeBtnMin:F4}", AnchorMax = $"0.92 {closeBtnMax:F4}" },
                Text = { Text = "CLOSE", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "RoomGunSelect");
            
            CuiHelper.AddUi(player, elements);
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
            
            // Check if player is in Phase 3 public queue
            if (GetPlayerQueueType(player.userID).HasValue)
            {
                LeaveQueueInternal(player, true); // handles DestroyLeaveButton internally
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
                // Also remove forfeiting player from their private room so they can freely queue
                var forfeiterRoomID = GetPlayerRoom(player.userID);
                if (forfeiterRoomID != null)
                {
                    LeaveRoom(player, forfeiterRoomID);
                }
                ShowLobbyBrowser(player); // Show lobby browser instead
                DestroyLeaderboardUI(player); // Hide leaderboard when returning to lobby
                
                if (opponent != null && opponent.IsConnected)
                {
                    DestroyLeaveButton(opponent);
                    TeleportToLobby(opponent);
                    ShowLobbyBrowser(opponent); // Show lobby browser instead
                    DestroyLeaderboardUI(opponent); // Hide leaderboard when returning to lobby
                }
                
                // For room matches: re-add the remaining player (opponent) to the waiting
                // queue so the next match can start when someone rejoins the room.
                string matchRoomID = match.RoomID;
                if (matchRoomID != null && privateRooms.ContainsKey(matchRoomID))
                {
                    var room = privateRooms[matchRoomID];
                    if (opponent != null && opponent.IsConnected
                        && room.PlayerIDs.Contains(opponentID)
                        && !room.WaitingQueue.Contains(opponentID))
                    {
                        room.WaitingQueue.Add(opponentID);
                    }
                    timer.Once(1.5f, () => TryRoomMatchmaking(matchRoomID));
                }
                else if (opponent != null && opponent.IsConnected)
                {
                    // Public match: auto-requeue the opponent (they didn't forfeit)
                    if (!autoRequeueOptOut.Contains(opponentID))
                    {
                        if (IsCustomPublicQueueMatch(match))
                            JoinCustomQueue(opponent, match.CustomModeName);
                        else
                            JoinQueueByType(opponent, match.SourceQueueType ?? QueueType.Public);
                    }
                    else
                    {
                        SendReply(opponent, "Auto-requeue disabled. Click JOIN QUEUE to play again.");
                    }
                }
                
                SavePlayerData();
                
                // Refresh leaderboard to show updated stats
                timer.Once(1f, () => RefreshAllLeaderboards());
                
                return;
            }
            
            // Not in queue or match - also leave any private room, then go to lobby
            var catchAllRoomID = GetPlayerRoom(player.userID);
            if (catchAllRoomID != null)
                LeaveRoom(player, catchAllRoomID);
            DestroyLeaveButton(player);
            TeleportToLobby(player);
        }
        
        [ConsoleCommand("joinqueue.click")]
        private void JoinQueueClickCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            // Block if player is inside a private room
            if (GetPlayerRoom(player.userID) != null)
            {
                SendReply(player, "You must leave your private room before joining a public queue!");
                return;
            }
            
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
        
        [ConsoleCommand("joinqueue.bow")]
        private void JoinQueueBowCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            JoinQueueByType(player, QueueType.PublicBow);
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
        
        [ConsoleCommand("joinqueue.custom")]
        private void JoinQueueCustomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(player, "Usage: joinqueue.custom <kitName>");
                return;
            }
            
            string kitName = arg.Args[0];
            if (!customQueuesByName.ContainsKey(kitName))
            {
                SendReply(player, $"Unknown custom kit: {kitName}");
                return;
            }
            
            JoinCustomQueue(player, kitName);
        }
        
        [ConsoleCommand("lobby.createroom")]
        private void CreateRoomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DuelMode mode = DuelMode.AK47;
            string customName = null;
            if (arg.Args != null && arg.Args.Length > 0)
            {
                switch (arg.Args[0].ToUpper())
                {
                    case "AK47":     mode = DuelMode.AK47;     break;
                    case "SAR":      mode = DuelMode.SAR;      break;
                    case "BOW":      mode = DuelMode.Bow;      break;
                    case "REVOLVER": mode = DuelMode.Revolver; break;
                    case "ANY":      mode = DuelMode.Any;      break;
                    case "SPEARGUN":
                        mode = config.EnableSpeargun ? DuelMode.Speargun : DuelMode.AK47;
                        break;
                    default:
                        // Check for a custom loadout name (case-insensitive)
                        var matched = config.Loadouts.Keys.FirstOrDefault(k =>
                            string.Equals(k, arg.Args[0], StringComparison.OrdinalIgnoreCase));
                        if (matched != null)
                        {
                            mode = DuelMode.Custom;
                            customName = matched;
                        }
                        break;
                }
            }
            CreateRoom(player, mode, customName);
        }
        
        // lobby.joinroom kept as alias → forwards to the request flow
        [ConsoleCommand("lobby.joinroom")]
        private void JoinRoomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (arg.Args == null || arg.Args.Length == 0) return;
            RequestJoinRoom(player, arg.Args[0]);
        }
        
        [ConsoleCommand("lobby.requestjoin")]
        private void RequestJoinRoomCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (arg.Args == null || arg.Args.Length == 0) return;
            RequestJoinRoom(player, arg.Args[0]);
        }
        
        [ConsoleCommand("lobby.accept")]
        private void AcceptJoinCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;
            if (!ulong.TryParse(arg.Args[0], out ulong requesterID)) return;
            var roomID = GetOwnedRoom(player.userID);
            if (roomID == null) return;
            AcceptJoinRequest(player, requesterID, roomID);
        }
        
        [ConsoleCommand("lobby.decline")]
        private void DeclineJoinCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;
            if (!ulong.TryParse(arg.Args[0], out ulong requesterID)) return;
            var roomID = GetOwnedRoom(player.userID);
            if (roomID == null) return;
            DeclineJoinRequest(player.userID, requesterID, roomID);
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
            
            var roomID = GetOwnedRoom(player.userID);
            if (roomID == null)
            {
                SendReply(player, "You need to own a room to start a match!");
                return;
            }
            
            TryRoomMatchmaking(roomID);
        }
        
        [ConsoleCommand("lobby.roomguns")]
        private void RoomGunsCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var roomID = GetOwnedRoom(player.userID);
            if (roomID == null)
            {
                SendReply(player, "You need to own a room to change its weapon mode!");
                return;
            }
            ShowGunSelectUI(player, roomID);
        }
        
        [ConsoleCommand("lobby.roomsetmode")]
        private void RoomSetModeCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;
            var roomID = GetOwnedRoom(player.userID);
            if (roomID == null) return;
            SetRoomMode(player, roomID, arg.Args[0]);
        }
        
        [ConsoleCommand("lobby.closeguns")]
        private void CloseGunsCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "RoomGunSelect");
        }
        
        #endregion
        
        #region Phase 3 - Queue Management
        
        private void JoinQueueByType(BasePlayer player, QueueType queueType)
        {
            if (player == null) return;
            
            // Block if player is inside a private room
            if (GetPlayerRoom(player.userID) != null)
            {
                SendReply(player, "You must leave your private room before joining a public queue!");
                return;
            }
            
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
            else if (GetPlayerCustomQueue(player.userID) != null)
            {
                // Was in a custom queue — leave it
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
                              queueType == QueueType.PublicBow ? "Bow" :
                              queueType == QueueType.PublicSpeargun ? "Speargun" : "Unknown";
            
            SendReply(player, $"Joined {queueName} queue! Waiting for opponent...");
            ShowLobbyBrowser(player);
            ShowLeaveButton(player); // Show leave button while waiting in queue
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
            
            // Also remove from custom kit queues
            foreach (var queue in customQueuesByName.Values)
            {
                if (queue.Remove(player.userID))
                {
                    wasInQueue = true;
                }
            }
            
            if (wasInQueue)
            {
                // Always destroy leave button when leaving a queue (regardless of updateUI)
                DestroyLeaveButton(player);
                
                if (updateUI)
                {
                    SendReply(player, "Left the queue.");
                    ShowLobbyBrowser(player);
                }
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
        
        private void ProcessAllQueues()
        {
            foreach (QueueType queueType in Enum.GetValues(typeof(QueueType)))
            {
                TryMatchPlayersInQueue(queueType);
            }
            
            // Process custom-kit public queues
            foreach (var kitName in customQueuesByName.Keys)
            {
                TryMatchCustomQueue(kitName);
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
                case QueueType.PublicBow:
                    mode = DuelMode.Bow;
                    break;
                case QueueType.PublicSpeargun:
                    mode = DuelMode.Speargun;
                    break;
                case QueueType.Public:
                default:
                    // Random mode for public queue — Speargun excluded (use dedicated Speargun queue)
                    var modeList = new List<DuelMode> { DuelMode.AK47, DuelMode.SAR, DuelMode.Bow, DuelMode.Revolver };
                    mode = modeList[UnityEngine.Random.Range(0, modeList.Count)];
                    break;
            }
            
            // Create the match using existing StartDuel method
            StartDuel(player1, player2, mode, null, queueType);
        }
        
        private void JoinCustomQueue(BasePlayer player, string kitName)
        {
            if (player == null) return;
            
            // Block if player is inside a private room
            if (GetPlayerRoom(player.userID) != null)
            {
                SendReply(player, "You must leave your private room before joining a public queue!");
                return;
            }
            
            // Check if player is already in a match
            if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
            {
                SendReply(player, "You're already in a match!");
                return;
            }
            
            // Check if already in this custom queue
            string currentCustom = GetPlayerCustomQueue(player.userID);
            if (currentCustom != null)
            {
                if (string.Equals(currentCustom, kitName, StringComparison.OrdinalIgnoreCase))
                {
                    SendReply(player, "You're already in this queue!");
                    return;
                }
                LeaveQueueInternal(player, false);
            }
            else if (GetPlayerQueueType(player.userID).HasValue)
            {
                // In a regular typed queue — leave it first
                LeaveQueueInternal(player, false);
            }
            
            customQueuesByName[kitName].Add(player.userID);
            SendReply(player, $"Joined {kitName} queue! Waiting for opponent...");
            ShowLobbyBrowser(player);
            ShowLeaveButton(player);
        }
        
        private string GetPlayerCustomQueue(ulong playerID)
        {
            foreach (var kvp in customQueuesByName)
            {
                if (kvp.Value.Contains(playerID))
                    return kvp.Key;
            }
            return null;
        }
        
        private void TryMatchCustomQueue(string kitName)
        {
            if (!customQueuesByName.ContainsKey(kitName)) return;
            var queue = customQueuesByName[kitName];
            if (queue.Count < 2) return;
            
            var player1ID = queue[0];
            var player2ID = queue[1];
            var player1 = BasePlayer.FindByID(player1ID);
            var player2 = BasePlayer.FindByID(player2ID);
            
            if (player1 == null || player2 == null)
            {
                if (player1 == null) queue.Remove(player1ID);
                if (player2 == null) queue.Remove(player2ID);
                return;
            }
            
            queue.RemoveAt(0);
            queue.RemoveAt(0);
            
            // Start the duel as a Custom-mode match with the kit name
            StartDuel(player1, player2, DuelMode.Custom, null, null, kitName);
        }
        
        #endregion
        
        #region Phase 4 - Private Rooms
        
        private void CreateRoom(BasePlayer player, DuelMode mode = DuelMode.AK47, string customModeName = null)
        {
            if (player == null) return;
            
            // Check if player is already in a match
            if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
            {
                SendReply(player, "You cannot create a room while in a match!");
                return;
            }
            
            // Prevent duplicate rooms
            if (privateRooms.Values.Any(r => r.OwnerID == player.userID))
            {
                SendReply(player, "You already own a room! Leave it first.");
                return;
            }
            
            // Prevent joining if already in another room
            var existingRoom = GetPlayerRoom(player.userID);
            if (existingRoom != null)
            {
                SendReply(player, "You're already in a room! Leave it first.");
                return;
            }
            
            // Generate unique short room ID (max 20 attempts before giving up)
            string roomID = GenerateRoomID();
            int idAttempts = 0;
            while (privateRooms.ContainsKey(roomID) && idAttempts < 20)
            {
                roomID = GenerateRoomID();
                idAttempts++;
            }
            if (privateRooms.ContainsKey(roomID))
            {
                SendReply(player, "Could not generate a unique room ID. Please try again.");
                return;
            }
            
            // Room is named after the creator's display name, mode set at creation time
            var room = new PrivateRoom
            {
                RoomID = roomID,
                RoomName = player.displayName,
                OwnerID = player.userID,
                OwnerName = player.displayName,
                PlayerIDs = new List<ulong> { player.userID },
                WaitingQueue = new List<ulong> { player.userID },
                Mode = mode,
                CustomModeName = customModeName,
                Created = DateTime.Now
            };
            
            privateRooms[roomID] = room;
            
            // Remove from any public queue
            LeaveQueueInternal(player, false);
            
            string modeStr = customModeName ?? (mode == DuelMode.Any ? "Random" : mode.ToString());
            SendReply(player, $"Room created ({modeStr} mode)! Others can request to join from the lobby.");
            
            UpdateRoomsList();
        }
        
        private void RequestJoinRoom(BasePlayer player, string roomID)
        {
            if (player == null || string.IsNullOrWhiteSpace(roomID)) return;
            
            if (!privateRooms.ContainsKey(roomID))
            {
                SendReply(player, "That room does not exist!");
                return;
            }
            
            var room = privateRooms[roomID];
            
            if (activeMatches.Values.Any(d => d.Player1ID == player.userID || d.Player2ID == player.userID))
            {
                SendReply(player, "You cannot request to join a room while in a match!");
                return;
            }
            
            if (room.HasPlayer(player.userID))
            {
                SendReply(player, "You're already in this room!");
                return;
            }
            
            if (room.HasPendingRequest(player.userID))
            {
                SendReply(player, "You've already sent a join request to this room!");
                return;
            }
            
            var existingRoom = GetPlayerRoom(player.userID);
            if (existingRoom != null)
            {
                SendReply(player, "You're already in another room! Leave it first.");
                return;
            }
            
            // Add to pending requests
            room.PendingRequests[player.userID] = player.displayName;
            
            SendReply(player, $"Join request sent to {room.OwnerName}'s room. Waiting for approval (15s)...");
            
            // Notify owner via overlay UI (only if not in a match)
            var owner = BasePlayer.FindByID(room.OwnerID);
            if (owner != null && owner.IsConnected)
            {
                ShowJoinRequestUI(owner, roomID);
            }
            
            // Update the lobby for all (shows PENDING state on the button for requester)
            UpdateRoomsList();
            
            // Auto-decline after 15 seconds if owner hasn't responded
            ulong requesterID = player.userID;
            timer.Once(15f, () =>
            {
                // Access through dictionary (not captured reference) to avoid stale-object issues
                if (privateRooms.ContainsKey(roomID) &&
                    privateRooms[roomID].PendingRequests.ContainsKey(requesterID))
                {
                    DeclineJoinRequest(privateRooms[roomID].OwnerID, requesterID, roomID);
                }
            });
        }
        
        private void AcceptJoinRequest(BasePlayer owner, ulong requesterID, string roomID)
        {
            if (owner == null || !privateRooms.ContainsKey(roomID)) return;
            var room = privateRooms[roomID];
            
            if (owner.userID != room.OwnerID)
            {
                SendReply(owner, "You are not the owner of this room!");
                return;
            }
            
            if (!room.PendingRequests.ContainsKey(requesterID))
            {
                // Request may have already timed out
                DestroyJoinRequestUI(owner);
                ShowJoinRequestUI(owner, roomID);
                return;
            }
            
            string requesterName = room.PendingRequests[requesterID];
            room.PendingRequests.Remove(requesterID);
            
            var requester = BasePlayer.FindByID(requesterID);
            if (requester == null || !requester.IsConnected)
            {
                SendReply(owner, $"{requesterName} is no longer online.");
                DestroyJoinRequestUI(owner);
                ShowJoinRequestUI(owner, roomID);
                return;
            }
            
            // Remove from any public queue
            LeaveQueueInternal(requester, false);
            
            // Add to room
            room.PlayerIDs.Add(requesterID);
            room.WaitingQueue.Add(requesterID);
            
            SendReply(owner, $"Accepted {requesterName} into your room!");
            SendReply(requester, $"Your join request was accepted! Welcome to {room.RoomName}'s room.");
            
            // Notify other room members
            foreach (var pid in room.PlayerIDs)
            {
                if (pid != owner.userID && pid != requesterID)
                {
                    var p = BasePlayer.FindByID(pid);
                    if (p != null && p.IsConnected)
                        SendReply(p, $"{requesterName} joined the room.");
                }
            }
            
            // Refresh request UI (show next pending request or hide if none left)
            DestroyJoinRequestUI(owner);
            ShowJoinRequestUI(owner, roomID);
            
            // Auto-start match when 2+ players are waiting
            TryRoomMatchmaking(roomID);
            
            UpdateRoomsList();
        }
        
        private void DeclineJoinRequest(ulong ownerID, ulong requesterID, string roomID)
        {
            if (!privateRooms.ContainsKey(roomID)) return;
            var room = privateRooms[roomID];
            
            if (!room.PendingRequests.ContainsKey(requesterID)) return;
            
            string requesterName = room.PendingRequests[requesterID];
            room.PendingRequests.Remove(requesterID);
            
            var requester = BasePlayer.FindByID(requesterID);
            if (requester != null && requester.IsConnected)
                SendReply(requester, $"Your join request to {room.RoomName}'s room was declined.");
            
            var owner = BasePlayer.FindByID(ownerID);
            if (owner != null && owner.IsConnected)
            {
                // Refresh request UI (show remaining requests)
                DestroyJoinRequestUI(owner);
                ShowJoinRequestUI(owner, roomID);
            }
            
            UpdateRoomsList();
        }
        
        private void TryRoomMatchmaking(string roomID, int depth = 0)
        {
            // Guard against infinite recursion when all waiting players are offline
            if (depth > 10) return;
            
            if (!privateRooms.ContainsKey(roomID)) return;
            var room = privateRooms[roomID];
            
            // Clean disconnected players from waiting queue upfront
            room.WaitingQueue.RemoveAll(id =>
            {
                var p = BasePlayer.FindByID(id);
                return p == null || !p.IsConnected;
            });
            
            if (room.WaitingQueue.Count < 2) return;
            
            var p1ID = room.WaitingQueue[0];
            var p2ID = room.WaitingQueue[1];
            room.WaitingQueue.RemoveAt(0);
            room.WaitingQueue.RemoveAt(0);
            
            var p1 = BasePlayer.FindByID(p1ID);
            var p2 = BasePlayer.FindByID(p2ID);
            
            // Both were already cleaned above; if somehow still null, retry with next pair
            if (p1 == null || !p1.IsConnected || p2 == null || !p2.IsConnected)
            {
                if (p1 != null && p1.IsConnected) room.WaitingQueue.Insert(0, p1ID);
                if (p2 != null && p2.IsConnected) room.WaitingQueue.Insert(0, p2ID);
                TryRoomMatchmaking(roomID, depth + 1);
                return;
            }
            
            // Start the duel using the room's chosen mode; resolve Any to a random concrete mode
            DuelMode duelMode = room.Mode;
            if (duelMode == DuelMode.Any)
            {
                var randomModes = new List<DuelMode> { DuelMode.AK47, DuelMode.SAR, DuelMode.Bow, DuelMode.Revolver };
                duelMode = randomModes[UnityEngine.Random.Range(0, randomModes.Count)];
            }
            StartDuel(p1, p2, duelMode, roomID, null, duelMode == DuelMode.Custom ? room.CustomModeName : null);
        }
        
        private void SetRoomMode(BasePlayer player, string roomID, string modeName)
        {
            if (!privateRooms.ContainsKey(roomID)) return;
            var room = privateRooms[roomID];
            
            if (room.OwnerID != player.userID)
            {
                SendReply(player, "Only the room owner can change the weapon mode!");
                return;
            }
            
            DuelMode mode;
            string customName = null;
            switch (modeName.ToUpper())
            {
                case "AK47":    mode = DuelMode.AK47;     break;
                case "SAR":     mode = DuelMode.SAR;      break;
                case "BOW":     mode = DuelMode.Bow;      break;
                case "REVOLVER":mode = DuelMode.Revolver; break;
                case "RANDOM":
                case "ANY":     mode = DuelMode.Any;      break;
                case "SPEARGUN":
                    if (!config.EnableSpeargun)
                    {
                        SendReply(player, "Speargun mode is disabled on this server!");
                        return;
                    }
                    mode = DuelMode.Speargun;
                    break;
                default:
                    // Check if it matches a custom loadout name (case-insensitive)
                    var match = config.Loadouts.Keys.FirstOrDefault(k =>
                        string.Equals(k, modeName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        mode = DuelMode.Custom;
                        customName = match;
                    }
                    else
                    {
                        SendReply(player, "Invalid mode! Available modes: " +
                            string.Join(", ", config.Loadouts.Keys));
                        return;
                    }
                    break;
            }
            
            room.Mode = mode;
            room.CustomModeName = customName;
            string displayName = customName ?? mode.ToString();
            SendReply(player, $"Room weapon mode set to {displayName}!");
            
            // Refresh gun select UI to show updated selection
            ShowGunSelectUI(player, roomID);
            UpdateRoomsList();
        }
        
        private void LeaveRoom(BasePlayer player, string roomID, bool updateUI = true)
        {
            if (player == null || string.IsNullOrWhiteSpace(roomID)) return;
            if (!privateRooms.ContainsKey(roomID)) return;
            
            var room = privateRooms[roomID];
            if (!room.PlayerIDs.Contains(player.userID)) return;
            
            room.PlayerIDs.Remove(player.userID);
            room.WaitingQueue.Remove(player.userID);
            
            // Cancel any pending requests this player sent to THIS room
            if (room.PendingRequests.ContainsKey(player.userID))
                room.PendingRequests.Remove(player.userID);
            
            SendReply(player, $"Left {room.RoomName}'s room.");
            
            // Close gun select UI if open
            CuiHelper.DestroyUi(player, "RoomGunSelect");
            DestroyJoinRequestUI(player);
            
            // Notify remaining members
            foreach (var pid in room.PlayerIDs)
            {
                var p = BasePlayer.FindByID(pid);
                if (p != null && p.IsConnected)
                    SendReply(p, $"{player.displayName} left the room.");
            }
            
            if (player.userID == room.OwnerID)
            {
                if (room.PlayerIDs.Count > 0)
                {
                    // Transfer ownership
                    room.OwnerID = room.PlayerIDs[0];
                    var newOwner = BasePlayer.FindByID(room.OwnerID);
                    if (newOwner != null)
                    {
                        room.OwnerName = newOwner.displayName;
                        SendReply(newOwner, "You are now the room owner.");
                        // Show any pending requests to new owner
                        if (room.PendingRequests.Count > 0)
                            ShowJoinRequestUI(newOwner, roomID);
                    }
                }
                else
                {
                    // No one left — decline all pending requests and close the room
                    foreach (var kvp in room.PendingRequests)
                    {
                        var requester = BasePlayer.FindByID(kvp.Key);
                        if (requester != null && requester.IsConnected)
                            SendReply(requester, $"{room.RoomName}'s room has been closed.");
                    }
                    privateRooms.Remove(roomID);
                }
            }
            else if (room.PlayerIDs.Count == 0)
            {
                privateRooms.Remove(roomID);
            }
            
            if (updateUI) UpdateRoomsList();
        }
        
        private string GetPlayerRoom(ulong playerID)
        {
            foreach (var room in privateRooms.Values)
            {
                if (room.PlayerIDs.Contains(playerID))
                    return room.RoomID;
            }
            return null;
        }
        
        private string GetOwnedRoom(ulong playerID)
        {
            foreach (var room in privateRooms.Values)
            {
                if (room.OwnerID == playerID)
                    return room.RoomID;
            }
            return null;
        }
        
        private void CleanupPlayerRooms(ulong playerID)
        {
            var roomsToRemove = new List<string>();
            
            // Cancel any pending requests this player sent to any room
            foreach (var room in privateRooms.Values)
            {
                if (room.PendingRequests.ContainsKey(playerID))
                {
                    room.PendingRequests.Remove(playerID);
                    // Refresh owner's request UI
                    var owner = BasePlayer.FindByID(room.OwnerID);
                    if (owner != null && owner.IsConnected)
                    {
                        DestroyJoinRequestUI(owner);
                        ShowJoinRequestUI(owner, room.RoomID);
                    }
                }
            }
            
            foreach (var room in privateRooms.Values)
            {
                if (!room.PlayerIDs.Contains(playerID)) continue;
                
                room.PlayerIDs.Remove(playerID);
                room.WaitingQueue.Remove(playerID);
                
                if (playerID == room.OwnerID && room.PlayerIDs.Count > 0)
                {
                    room.OwnerID = room.PlayerIDs[0];
                    var newOwner = BasePlayer.FindByID(room.OwnerID);
                    if (newOwner != null)
                    {
                        room.OwnerName = newOwner.displayName;
                        SendReply(newOwner, "You are now the room owner.");
                        if (room.PendingRequests.Count > 0)
                            ShowJoinRequestUI(newOwner, room.RoomID);
                    }
                }
                
                if (room.PlayerIDs.Count == 0)
                {
                    // Decline pending requests before closing
                    foreach (var kvp in room.PendingRequests)
                    {
                        var requester = BasePlayer.FindByID(kvp.Key);
                        if (requester != null && requester.IsConnected)
                            SendReply(requester, $"The room you requested to join has been closed.");
                    }
                    roomsToRemove.Add(room.RoomID);
                }
            }
            
            foreach (var rid in roomsToRemove)
                privateRooms.Remove(rid);
            
            if (roomsToRemove.Count > 0)
                UpdateRoomsList();
        }
        
        private void UpdateRoomsList()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected)
                    ShowLobbyBrowser(player);
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
            
            DestroyLeaderboardUI(player);
            
            // Determine context from the player's current match, defaulting to Public for lobby
            string queueKey = "Public";
            ulong otherPlayerID = 0;
            if (activeMatches.ContainsKey(player.userID))
            {
                var match = activeMatches[player.userID];
                queueKey = GetMatchQueueKey(match);
                otherPlayerID = match.Player1ID == player.userID ? match.Player2ID : match.Player1ID;
            }
            
            var elements = new CuiElementContainer();
            
            // Main panel - anchored to the true top-left corner, tall enough for 10 entries + footer.
            var mainPanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.17 0.17 0.17 0.95" },
                RectTransform = { AnchorMin = "0.01 0.60", AnchorMax = "0.20 1.0" },
                CursorEnabled = false
            }, "Hud", "LeaderboardUI");
            
            // Title background strip - named so labels can be parented to it,
            // ensuring they always render above the background panel.
            // Strip occupies top 16% of panel (~40 px at 812 p screen) so both labels
            // have enough height: font-size must be ≤ ~70% of the box height to render.
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0.8 0.82 0.25" },
                RectTransform = { AnchorMin = "0 0.84", AnchorMax = "1 1" }
            }, mainPanel, "LB.TitleBg");
            
            // Title label - queue name, upper 49% of strip (~20 px), font 13 = 65% of box ✓
            string titleText = queueKey == "Private"  ? "PRIVATE" :
                               queueKey == "AK"       ? "PUBLIC AK47" :
                               queueKey == "Bow"      ? "PUBLIC BOW" :
                               queueKey == "Speargun" ? "PUBLIC SPEARGUN" :
                                                        "PUBLIC";
            elements.Add(new CuiLabel
            {
                Text = { Text = titleText, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.03 0.48", AnchorMax = "0.97 0.97" }
            }, "LB.TitleBg");
            
            // Sub-label "Last N min" - lower 45% of strip (~18 px), font 9 = 50% of box ✓
            elements.Add(new CuiLabel
            {
                Text = { Text = $"Last {config.LeaderboardTimeWindowMinutes} min", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.03 0.03", AnchorMax = "0.97 0.48" }
            }, "LB.TitleBg");
            
            // Separator sits just below the strip bottom (strip AnchorMin y=0.84 in panel)
            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0.8 0.82 0.5" },
                RectTransform = { AnchorMin = "0.03 0.836", AnchorMax = "0.97 0.841" }
            }, mainPanel);
            
            // ---- Leaderboard entries ----
            var cutoffTime = DateTime.Now.AddMinutes(-config.LeaderboardTimeWindowMinutes);
            
            List<KeyValuePair<ulong, PlayerData>> topPlayers;
            if (queueKey == "Private" && otherPlayerID != 0)
            {
                // Private match: only show this match's two participants, sorted by recent wins
                topPlayers = playerData
                    .Where(p => p.Key == player.userID || p.Key == otherPlayerID)
                    .OrderByDescending(p => GetRecentWinsByQueue(p.Value, "Private", config.LeaderboardTimeWindowMinutes))
                    .ThenByDescending(p => GetRecentWinRateByQueue(p.Value, "Private", config.LeaderboardTimeWindowMinutes))
                    .ToList();
            }
            else
            {
                topPlayers = playerData
                    .Where(p => p.Value.StatEvents.Any(e => (e.Type == "Win" || e.Type == "Loss")
                                                         && e.QueueKey == queueKey
                                                         && e.Timestamp >= cutoffTime))
                    .OrderByDescending(p => GetRecentWinsByQueue(p.Value, queueKey, config.LeaderboardTimeWindowMinutes))
                    .ThenByDescending(p => GetRecentWinRateByQueue(p.Value, queueKey, config.LeaderboardTimeWindowMinutes))
                    .Take(10)
                    .ToList();
            }
            
            float startY      = 0.825f;
            float entryHeight = 0.075f;
            int   rank        = 1;
            
            if (topPlayers.Count == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text = { Text = $"No {queueKey} matches\nin the last {config.LeaderboardTimeWindowMinutes} min",
                             FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
                    RectTransform = { AnchorMin = "0.05 0.40", AnchorMax = "0.95 0.60" }
                }, mainPanel);
            }
            
            foreach (var entry in topPlayers)
            {
                var   playerName = covalence.Players.FindPlayerById(entry.Key.ToString())?.Name ?? "Unknown";
                if (playerName.Length > 12) playerName = playerName.Substring(0, 12);
                
                int   w    = GetRecentWinsByQueue(entry.Value, queueKey, config.LeaderboardTimeWindowMinutes);
                int   l    = GetRecentLossesByQueue(entry.Value, queueKey, config.LeaderboardTimeWindowMinutes);
                float wr   = GetRecentWinRateByQueue(entry.Value, queueKey, config.LeaderboardTimeWindowMinutes);
                string txt = $"{rank}. {playerName}  {w}W-{l}L ({wr:F0}%)";
                
                string textColor = entry.Key == player.userID ? "1 1 0 1" : "0.9 0.9 0.9 1";
                
                elements.Add(new CuiLabel
                {
                    Text = { Text = txt, FontSize = 11, Align = TextAnchor.UpperLeft, Color = textColor },
                    RectTransform = { AnchorMin = $"0.05 {startY - entryHeight:F3}", AnchorMax = $"0.95 {startY:F3}" }
                }, mainPanel);
                
                startY -= entryHeight;
                rank++;
                if (rank > 10) break;
            }
            
            // Footer: show viewer's own rank when outside top 10 (public queues only)
            if (queueKey != "Private" && playerData.ContainsKey(player.userID))
            {
                var yourData = playerData[player.userID];
                int yourW = GetRecentWinsByQueue(yourData, queueKey, config.LeaderboardTimeWindowMinutes);
                int yourL = GetRecentLossesByQueue(yourData, queueKey, config.LeaderboardTimeWindowMinutes);
                
                if (yourW > 0 || yourL > 0)
                {
                    int yourRank = playerData
                        .Where(p => p.Value.StatEvents.Any(e => (e.Type == "Win" || e.Type == "Loss")
                                                             && e.QueueKey == queueKey
                                                             && e.Timestamp >= cutoffTime))
                        .OrderByDescending(p => GetRecentWinsByQueue(p.Value, queueKey, config.LeaderboardTimeWindowMinutes))
                        .ThenByDescending(p => GetRecentWinRateByQueue(p.Value, queueKey, config.LeaderboardTimeWindowMinutes))
                        .ToList()
                        .FindIndex(p => p.Key == player.userID) + 1;
                    
                    if (yourRank > 10)
                    {
                        float yourWr = GetRecentWinRateByQueue(yourData, queueKey, config.LeaderboardTimeWindowMinutes);
                        elements.Add(new CuiLabel
                        {
                            Text = { Text = $"Your rank: #{yourRank}  {yourW}W-{yourL}L ({yourWr:F0}%)",
                                     FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 0 1" },
                            RectTransform = { AnchorMin = "0.03 0.01", AnchorMax = "0.97 0.07" }
                        }, mainPanel);
                    }
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
            
            // Auto-dismiss after 2 seconds
            timer.Once(2f, () => DestroyWinLoseUI(player));
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
                             "Step 3: (Optional) Use /arena setradius <radius> to adjust the zone size (default: 30m)\n" +
                             "Step 4: (Optional) Move to the lobby position and use /arena setlobbyspawn\n" +
                             "Step 5: (Optional) Use /arena setlobbyradius <radius> to adjust the lobby zone size (default: 10m)\n" +
                             "Step 6: Use /arena save to save the arena");
        }
        
        private void SetSpawn1(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not in a builder session. Use /arena create <name> or /arena edit <name> first.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            builder.Spawn1 = player.transform.position;
            SendReply(player, $"Spawn point 1 set at {builder.Spawn1}\n" +
                             "Now move to the second spawn point and use /arena setspawn2");
            StartBuilderVisualization(player, builder);
        }
        
        private void SetSpawn2(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not in a builder session. Use /arena create <name> or /arena edit <name> first.");
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
            // Restart visualization so it now also draws spawn2 and the full zone
            StartBuilderVisualization(player, builder);
        }
        
        private void SaveArena(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not in a builder session. Use /arena create <name> or /arena edit <name> first.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            
            if (builder.Spawn1 == Vector3.zero || builder.Spawn2 == Vector3.zero)
            {
                SendReply(player, "You need to set both spawn points first.\n" +
                                 "Use /arena setspawn1 and /arena setspawn2");
                return;
            }
            
            bool lobbySet = builder.LobbySpawn != Vector3.zero;
            
            if (builder.IsEditing)
            {
                // Update the existing ArenaConfig entry
                var existingConfig = arenas.FirstOrDefault(a =>
                    a.Name.Equals(builder.Name, StringComparison.OrdinalIgnoreCase));
                if (existingConfig == null)
                {
                    SendReply(player, $"Error: Arena '{builder.Name}' not found in data. Aborting.");
                    StopBuilderVisualization(builder);
                    arenaBuilders.Remove(player.userID);
                    return;
                }
                existingConfig.Spawn1 = builder.Spawn1;
                existingConfig.Spawn2 = builder.Spawn2;
                existingConfig.Radius = builder.Radius;
                existingConfig.LobbyPosition = builder.LobbySpawn;
                existingConfig.LobbyRadius = builder.LobbyRadius;
                existingConfig.LobbyPositionSet = lobbySet;
                SaveArenas();
                arenaManager.UpdateArena(existingConfig);
                
                string lobbyMsg = lobbySet ? $"\nLobby: {builder.LobbySpawn} r={builder.LobbyRadius}m" : "";
                SendReply(player, $"Arena '{builder.Name}' updated successfully!\n" +
                                 $"Spawn 1: {builder.Spawn1}\n" +
                                 $"Spawn 2: {builder.Spawn2}\n" +
                                 $"Zone radius: {builder.Radius}m" + lobbyMsg);
            }
            else
            {
                // Create a new ArenaConfig
                var arenaConfig = new ArenaConfig
                {
                    Name = builder.Name,
                    Spawn1 = builder.Spawn1,
                    Spawn2 = builder.Spawn2,
                    Radius = builder.Radius,
                    LobbyPosition = builder.LobbySpawn,
                    LobbyRadius = builder.LobbyRadius,
                    LobbyPositionSet = lobbySet
                };
                arenas.Add(arenaConfig);
                SaveArenas();
                arenaManager.AddArena(arenaConfig);
                
                string lobbyMsg = lobbySet ? $"\nLobby: {builder.LobbySpawn} r={builder.LobbyRadius}m" : "";
                SendReply(player, $"Arena '{builder.Name}' saved successfully!\n" +
                                 $"Spawn 1: {builder.Spawn1}\n" +
                                 $"Spawn 2: {builder.Spawn2}\n" +
                                 $"Zone radius: {builder.Radius}m" + lobbyMsg);
            }
            
            StopBuilderVisualization(builder);
            arenaBuilders.Remove(player.userID);
        }
        
        private void CancelArena(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not in a builder session.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            StopBuilderVisualization(builder);
            arenaBuilders.Remove(player.userID);
            string action = builder.IsEditing ? "Cancelled editing" : "Cancelled creation";
            SendReply(player, $"{action} of arena '{builder.Name}'");
        }
        
        private void EditArena(BasePlayer player, string name)
        {
            if (arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're already in a builder session. Use /arena cancel first.");
                return;
            }
            
            var arena = arenaManager.GetArenaByName(name);
            if (arena == null)
            {
                SendReply(player, $"Arena '{name}' not found.");
                return;
            }
            
            if (arenaManager.IsArenaInUse(name))
            {
                SendReply(player, $"Arena '{name}' is currently in use. Cannot edit.");
                return;
            }
            
            string lobbyInfo = arena.LobbyPositionSet
                ? $"\nLobby spawn: {arena.LobbyPosition} r={arena.LobbyRadius}m"
                : "\nNo lobby spawn set";
            
            arenaBuilders[player.userID] = new ArenaBuilder
            {
                Name = arena.Name,
                Spawn1 = arena.Spawn1,
                Spawn2 = arena.Spawn2,
                Radius = arena.Radius,
                LobbySpawn = arena.LobbyPositionSet ? arena.LobbyPosition : Vector3.zero,
                LobbyRadius = arena.LobbyRadius,
                IsEditing = true
            };
            
            StartBuilderVisualization(player, arenaBuilders[player.userID]);
            
            SendReply(player, $"Editing arena '{name}'.\n" +
                             $"Spawn 1: {arena.Spawn1}\n" +
                             $"Spawn 2: {arena.Spawn2}\n" +
                             $"Zone radius: {arena.Radius}m" + lobbyInfo + "\n" +
                             "Use /arena setspawn1, /arena setspawn2, /arena setradius, /arena setlobbyspawn, /arena setlobbyradius to adjust.\n" +
                             "Use /arena save to save or /arena cancel to discard changes.");
        }
        
        private void SetLobbySpawn(BasePlayer player)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not in a builder session. Use /arena create <name> or /arena edit <name> first.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            builder.LobbySpawn = player.transform.position;
            SendReply(player, $"Lobby spawn set at {builder.LobbySpawn} (radius: {builder.LobbyRadius}m)\n" +
                             "Use /arena setlobbyradius <radius> to adjust the lobby zone size.");
            StartBuilderVisualization(player, builder);
        }
        
        private void SetLobbyRadius(BasePlayer player, string radiusArg)
        {
            if (!arenaBuilders.ContainsKey(player.userID))
            {
                SendReply(player, "You're not in a builder session. Use /arena create <name> or /arena edit <name> first.");
                return;
            }
            
            float newRadius;
            if (!float.TryParse(radiusArg, out newRadius) || newRadius <= 0)
            {
                SendReply(player, "Invalid radius. Please enter a positive number.");
                return;
            }
            
            var builder = arenaBuilders[player.userID];
            builder.LobbyRadius = newRadius;
            SendReply(player, $"Lobby radius set to {newRadius}m.");
            if (builder.LobbySpawn != Vector3.zero)
                StartBuilderVisualization(player, builder);
        }
        
        // Draw ddraw sphere + text label markers for spawn points and the arena zone radius.
        // Duration is set to just over the refresh interval so visuals never flicker out.
        private const float VisualizationRefreshInterval = 3f;
        private const float VisualizationDuration = 3.5f;
        
        private void DrawArenaBuilderVisuals(BasePlayer player, ArenaBuilder builder)
        {
            if (player == null || !player.IsConnected) return;
            
            Color spawn1Color = new Color(0f, 1f, 0f);   // green
            Color spawn2Color = new Color(1f, 0.4f, 0f); // orange
            Color zoneColor   = new Color(0f, 0.6f, 1f); // cyan
            Color lobbyColor  = new Color(1f, 1f, 0f);   // yellow
            
            if (builder.Spawn1 != Vector3.zero)
            {
                // Small sphere at spawn 1 with an elevated text label
                player.SendConsoleCommand("ddraw.sphere",
                    VisualizationDuration, spawn1Color, builder.Spawn1, 0.5f);
                player.SendConsoleCommand("ddraw.text",
                    VisualizationDuration, spawn1Color,
                    builder.Spawn1 + Vector3.up * 2f,
                    $"<size=18>SPAWN 1\n{builder.Spawn1}</size>");
            }
            
            if (builder.Spawn2 != Vector3.zero)
            {
                // Small sphere at spawn 2 with an elevated text label
                player.SendConsoleCommand("ddraw.sphere",
                    VisualizationDuration, spawn2Color, builder.Spawn2, 0.5f);
                player.SendConsoleCommand("ddraw.text",
                    VisualizationDuration, spawn2Color,
                    builder.Spawn2 + Vector3.up * 2f,
                    $"<size=18>SPAWN 2\n{builder.Spawn2}</size>");
            }
            
            // Draw the lobby spawn if set
            if (builder.LobbySpawn != Vector3.zero)
            {
                player.SendConsoleCommand("ddraw.sphere",
                    VisualizationDuration, lobbyColor, builder.LobbySpawn, 0.5f);
                player.SendConsoleCommand("ddraw.sphere",
                    VisualizationDuration, lobbyColor, builder.LobbySpawn, builder.LobbyRadius);
                player.SendConsoleCommand("ddraw.text",
                    VisualizationDuration, lobbyColor,
                    builder.LobbySpawn + Vector3.up * (builder.LobbyRadius + 1f),
                    $"<size=18>LOBBY r={builder.LobbyRadius}m</size>");
            }
            
            // Draw the zone as a sphere centred at the midpoint between the two spawns
            // (or at spawn1 alone if spawn2 isn't set yet), using the builder's current radius.
            if (builder.Spawn1 != Vector3.zero)
            {
                Vector3 center = builder.Spawn2 != Vector3.zero
                    ? (builder.Spawn1 + builder.Spawn2) * 0.5f
                    : builder.Spawn1;
                float radius = builder.Radius;
                player.SendConsoleCommand("ddraw.sphere",
                    VisualizationDuration, zoneColor, center, radius);
                player.SendConsoleCommand("ddraw.text",
                    VisualizationDuration, zoneColor,
                    center + Vector3.up * (radius + 1f),
                    $"<size=18>{builder.Name}\nZONE r={radius}m</size>");
            }
        }
        
        private void StartBuilderVisualization(BasePlayer player, ArenaBuilder builder)
        {
            StopBuilderVisualization(builder);
            // Draw immediately and then on every interval
            DrawArenaBuilderVisuals(player, builder);
            ulong playerID = player.userID;
            builder.VisualizationTimer = timer.Every(VisualizationRefreshInterval, () =>
            {
                if (!arenaBuilders.ContainsKey(playerID))
                {
                    StopBuilderVisualization(builder);
                    return;
                }
                var livePlayer = BasePlayer.FindByID(playerID);
                if (livePlayer == null || !livePlayer.IsConnected)
                    return;
                DrawArenaBuilderVisuals(livePlayer, builder);
            });
        }
        
        private void StopBuilderVisualization(ArenaBuilder builder)
        {
            if (builder.VisualizationTimer != null)
            {
                builder.VisualizationTimer.Destroy();
                builder.VisualizationTimer = null;
            }
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
                if (arena.KitOverrides != null && arena.KitOverrides.Count > 0)
                {
                    string kitDisplay = arena.KitOverrides.Count == 1
                        ? $"Kit Override: {arena.KitOverrides[0]}"
                        : $"Kit Pool ({arena.KitOverrides.Count} random): {string.Join(", ", arena.KitOverrides)}";
                    message += $"  {kitDisplay}\n";
                }
                
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
            LoadLobbyData();
        }
        
        private void SaveData()
        {
            SavePlayerData();
            SaveLobbyData();
        }
        
        private void LoadLobbyData()
        {
            try
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<LobbyData>("HellisPlugin_Lobby");
                if (data != null && data.IsSet)
                {
                    arenaManager.SetLobby(data.Position, data.Radius);
                    Puts($"Loaded lobby position from data file");
                }
            }
            catch (Exception ex)
            {
                Puts($"Error loading lobby data: {ex.Message}");
            }
        }
        
        private void SaveLobbyData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject("HellisPlugin_Lobby", new LobbyData
                {
                    Position = arenaManager.GetLobbyPosition(),
                    Radius = arenaManager.GetLobbyRadius(),
                    IsSet = arenaManager.IsLobbySet()
                });
            }
            catch (Exception ex)
            {
                Puts($"Error saving lobby data: {ex.Message}");
            }
        }
        
        private class LobbyData
        {
            public Vector3 Position;
            public float Radius = 10f;
            public bool IsSet;
        }
        
        private void Unload()
        {
            // Stop all active arena-builder visualization timers
            foreach (var builder in arenaBuilders.Values)
                StopBuilderVisualization(builder);
            arenaBuilders.Clear();
            
            // Destroy all UI elements for every connected player before the plugin is unloaded.
            // Without this, Oxide leaves stale CUI panels on screen permanently.
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                DestroyLobbyBrowser(player);
                DestroyLeaderboardUI(player);
                DestroyJoinButton(player);
                DestroyLeaveButton(player);
                DestroyWinLoseUI(player);
                DestroyJoinRequestUI(player);
                CuiHelper.DestroyUi(player, "RoomGunSelect");
            }

            // Save all data on plugin unload
            SavePlayerData();
            SaveArenas();
            SaveLobbyData();
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
            Any,    // For random queue matchmaking
            Custom  // Admin-defined kit loaded by name from config
        }
        
        public class QueueManager
        {
            private Dictionary<DuelMode, List<QueueEntry>> queues = new Dictionary<DuelMode, List<QueueEntry>>();
            private Dictionary<ulong, DuelMode> playerQueues = new Dictionary<ulong, DuelMode>();
            
            public QueueManager()
            {
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
                // Select random mode for Any queue matches — Speargun excluded (use dedicated Speargun queue)
                var availableModes = new List<DuelMode>
                {
                    DuelMode.AK47,
                    DuelMode.SAR,
                    DuelMode.Bow,
                    DuelMode.Revolver
                };
                
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
            
            // Lobby state stored independently of arenas
            private Vector3 lobbyPosition;
            private float lobbyRadius = 10f;
            private bool lobbyPositionSet;
            
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
                        LobbyPositionSet = config.LobbyPositionSet,
                        KitOverrides = config.KitOverrides != null ? new List<string>(config.KitOverrides) : new List<string>()
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
                    LobbyPositionSet = config.LobbyPositionSet,
                    KitOverrides = config.KitOverrides != null ? new List<string>(config.KitOverrides) : new List<string>()
                });
            }
            
            public void RemoveArena(string name)
            {
                arenas.RemoveAll(a => a.Name == name);
            }
            
            // Update an existing live Arena object in place from an ArenaConfig (used by /arena edit + save).
            public void UpdateArena(ArenaConfig config)
            {
                var arena = arenas.FirstOrDefault(a =>
                    a.Name.Equals(config.Name, StringComparison.OrdinalIgnoreCase));
                if (arena == null) return;
                arena.Spawn1 = config.Spawn1;
                arena.Spawn2 = config.Spawn2;
                arena.Radius = config.Radius;
                arena.LobbyPosition = config.LobbyPosition;
                arena.LobbyRadius = config.LobbyRadius;
                arena.LobbyPositionSet = config.LobbyPositionSet;
                arena.KitOverrides = config.KitOverrides != null ? new List<string>(config.KitOverrides) : new List<string>();
            }
            
            // Update the kit override list on a live Arena object (called by /arena setkit, addkit, removekit, clearkit).
            public void SetArenaKitOverrides(string arenaName, List<string> kitOverrides)
            {
                var arena = arenas.FirstOrDefault(a =>
                    a.Name.Equals(arenaName, StringComparison.OrdinalIgnoreCase));
                if (arena != null)
                    arena.KitOverrides = kitOverrides != null ? new List<string>(kitOverrides) : new List<string>();
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
            public Vector3 GetLobbyPosition() => lobbyPosition;
            
            public float GetLobbyRadius() => lobbyRadius;
            
            public bool IsLobbySet() => lobbyPositionSet;
            
            public void SetLobby(Vector3 position, float radius)
            {
                lobbyPosition = position;
                lobbyRadius = radius;
                lobbyPositionSet = true;
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
            
            // When non-empty, each match in this arena randomly selects one kit from this list.
            // Empty list means use the mode-based global kit.
            public List<string> KitOverrides = new List<string>();
            
            // Picks a random kit from KitOverrides; returns null if the list is empty.
            public string PickRandomKitOverride()
            {
                if (KitOverrides == null || KitOverrides.Count == 0) return null;
                if (KitOverrides.Count == 1) return KitOverrides[0];
                return KitOverrides[UnityEngine.Random.Range(0, KitOverrides.Count)];
            }
            
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
            // All loadouts keyed by config name, including custom modes.
            private Dictionary<string, Loadout> namedLoadouts = new Dictionary<string, Loadout>();
            
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
                    var loadout = new Loadout
                    {
                        Items = kvp.Value.Items.Select(item => new LoadoutItem
                        {
                            ShortName = item.ShortName,
                            Amount = item.Amount,
                            SkinID = item.SkinID
                        }).ToList()
                    };
                    
                    if (modeMap.TryGetValue(kvp.Key, out var mode))
                    {
                        loadouts[mode] = loadout;
                    }
                    // All loadout names (including custom ones) are stored in namedLoadouts.
                    namedLoadouts[kvp.Key] = loadout;
                }
            }
            
            public Loadout GetLoadout(DuelMode mode)
            {
                return loadouts.ContainsKey(mode) ? loadouts[mode] : new Loadout();
            }
            
            // Look up a loadout by its config key (used for custom modes).
            public Loadout GetLoadoutByName(string name)
            {
                return namedLoadouts.ContainsKey(name) ? namedLoadouts[name] : new Loadout();
            }
            
            // Re-read loadouts from config after an in-game kit change.
            public void Reload(Configuration config)
            {
                loadouts.Clear();
                namedLoadouts.Clear();
                LoadLoadoutsFromConfig(config);
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
            public string RoomID = null; // Non-null if this match was started from a private room
            public QueueType? SourceQueueType = null; // Non-null for Phase 3 public queue matches
            public string CustomModeName = null; // Non-null when Mode == DuelMode.Custom
            // Kit picked randomly from Arena.KitOverrides at match start; null means use mode-based kit.
            // Fixed for the entire match so all rounds use the same kit.
            public string ResolvedArenaKit = null;
            
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
            
            [JsonProperty("QueueKey")]
            public string QueueKey = "";  // empty string for legacy events without queue context
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
            
            // Per-queue-type win/loss counts (keys: "Public", "AK", "Bow", "Speargun", "Private")
            public Dictionary<string, int> WinsByQueue = new Dictionary<string, int>();
            public Dictionary<string, int> LossesByQueue = new Dictionary<string, int>();
            
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
            public float Radius = 30f;
            public Vector3 LobbySpawn = Vector3.zero;
            public float LobbyRadius = 10f;
            // True when editing an existing arena rather than creating a new one.
            public bool IsEditing = false;
            // Repeating timer that refreshes ddraw visuals for this builder session.
            public Oxide.Plugins.Timer VisualizationTimer;
        }
        
        // Queue types for lobby browser
        public enum QueueType
        {
            Public,           // Any mode, random (AK47, SAR, Bow, Revolver)
            PublicAK,         // AK47 only
            PublicBow,        // Bow only
            PublicSpeargun    // Speargun only
        }
        
        // Private room data structure
        public class PrivateRoom
        {
            public string RoomID;
            public string RoomName;
            public ulong OwnerID;
            public string OwnerName;
            public List<ulong> PlayerIDs = new List<ulong>();         // All players currently in the room
            public List<ulong> WaitingQueue = new List<ulong>();      // Players waiting for a 1v1 match
            public Dictionary<ulong, string> PendingRequests = new Dictionary<ulong, string>(); // requesterID -> displayName
            public DuelMode Mode = DuelMode.AK47;
            public string CustomModeName = null; // Non-null when Mode == DuelMode.Custom
            public DateTime Created;
            public bool IsOpen = true;
            
            public PrivateRoom()
            {
                RoomID = Guid.NewGuid().ToString();
                Created = DateTime.Now;
            }
            
            public bool HasPlayer(ulong playerID) => PlayerIDs.Contains(playerID);
            public bool HasPendingRequest(ulong playerID) => PendingRequests.ContainsKey(playerID);
        }
        
        #endregion
    }
}
