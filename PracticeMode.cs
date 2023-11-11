using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory;



namespace MatchZy
{
    public class Position
    {

        public Vector PlayerPosition { get; private set; }
        public QAngle PlayerAngle { get; private set; }
        public Position(Vector playerPosition, QAngle playerAngle)
        {
            // Create deep copies of the Vector and QAngle objects
            PlayerPosition = new Vector(playerPosition.X, playerPosition.Y, playerPosition.Z);
            PlayerAngle = new QAngle(playerAngle.X, playerAngle.Y, playerAngle.Z);
        }
    }

    public partial class MatchZy
    {
        public Dictionary<byte, List<Position>> spawnsData = new Dictionary<byte, List<Position>> {
            { (byte)CsTeam.CounterTerrorist, new List<Position>() },
            { (byte)CsTeam.Terrorist, new List<Position>() }
        };

        public const string practiceCfgPath = "MatchZy/prac.cfg";

        // This map stores the bots which are being used in prac (probably spawned using .bot). Key is the userid of the bot.
        public Dictionary<int, Dictionary<string, object>> pracUsedBots = new Dictionary<int, Dictionary<string, object>>();

        public void StartPracticeMode()
        {
            if (matchStarted) return;
            isPractice = true;
            isWarmup = false;
            readyAvailable = false;

            var absolutePath = Path.Join(Server.GameDirectory + "/csgo/cfg", practiceCfgPath);

            if (File.Exists(Path.Join(Server.GameDirectory + "/csgo/cfg", practiceCfgPath)))
            {
                Log($"[StartWarmup] Starting Practice Mode! Executing Practice CFG from {practiceCfgPath}");
                Server.ExecuteCommand($"exec {practiceCfgPath}");
            }
            else
            {
                Log($"[StartWarmup] Starting Practice Mode! Practice CFG not found in {absolutePath}, using default CFG!");
                Server.ExecuteCommand("""sv_cheats "true"; mp_force_pick_time "0"; bot_quota "0"; sv_showimpacts "1"; mp_limitteams "0"; sv_deadtalk "true"; sv_full_alltalk "true"; sv_ignoregrenaderadio "false"; mp_forcecamera "0"; sv_grenade_trajectory_prac_pipreview "true"; sv_grenade_trajectory_prac_trailtime "3"; sv_infinite_ammo "1"; weapon_auto_cleanup_time "15"; weapon_max_before_cleanup "30"; mp_buy_anywhere "1"; mp_maxmoney "9999999"; mp_startmoney "9999999";""");
                Server.ExecuteCommand("""mp_weapons_allow_typecount "-1"; mp_death_drop_breachcharge "false"; mp_death_drop_defuser "false"; mp_death_drop_taser "false"; mp_drop_knife_enable "true"; mp_death_drop_grenade "0"; ammo_grenade_limit_total "5"; mp_defuser_allocation "2"; mp_free_armor "2"; mp_ct_default_grenades "weapon_incgrenade weapon_hegrenade weapon_smokegrenade weapon_flashbang weapon_decoy"; mp_ct_default_primary "weapon_m4a1";""");
                Server.ExecuteCommand("""mp_t_default_grenades "weapon_molotov weapon_hegrenade weapon_smokegrenade weapon_flashbang weapon_decoy"; mp_t_default_primary "weapon_ak47"; mp_warmup_online_enabled "true"; mp_warmup_pausetimer "1"; mp_warmup_start; bot_quota_mode fill; mp_solid_teammates 2; mp_autoteambalance false; mp_teammates_are_enemies true;""");
            }
            GetSpawns();
            Server.PrintToChatAll($"{chatPrefix} 训练模式已启动！！！");
            Server.PrintToChatAll($"{chatPrefix} 可用命令: .spawn, .ctspawn, .tspawn, .bot, .nobots, .exitprac");
        }

        public void GetSpawns()
        {
            // Resetting spawn data to avoid any glitches
            spawnsData = new Dictionary<byte, List<Position>> {
                        { (byte)CsTeam.CounterTerrorist, new List<Position>() },
                        { (byte)CsTeam.Terrorist, new List<Position>() }
                    };

            var spawnsct = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_counterterrorist");

            foreach (var spawn in spawnsct)
            {
                if (spawn.IsValid)
                {
                    spawnsData[(byte)CsTeam.CounterTerrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin, spawn.CBodyComponent?.SceneNode?.AbsRotation));
                }
            }

            var spawnst = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_terrorist");
            foreach (var spawn in spawnst)
            {
                if (spawn.IsValid)
                {
                    spawnsData[(byte)CsTeam.Terrorist].Add(new Position(spawn.CBodyComponent?.SceneNode?.AbsOrigin, spawn.CBodyComponent?.SceneNode?.AbsRotation));
                }
            }
        }

        private void HandleSpawnCommand(CCSPlayerController? player, string commandArg, byte teamNum, string command)
        {
            if (!isPractice || player == null) return;
            if (teamNum != 2 && teamNum != 3) return;
            if (!string.IsNullOrWhiteSpace(commandArg))
            {
                if (int.TryParse(commandArg, out int spawnNumber) && spawnNumber >= 1)
                {
                    // Adjusting the spawnNumber according to the array index.
                    spawnNumber -= 1;
                    if (spawnsData.ContainsKey(teamNum) && spawnsData[teamNum].Count <= spawnNumber) return;
                    player.PlayerPawn.Value.Teleport(spawnsData[teamNum][spawnNumber].PlayerPosition, spawnsData[teamNum][spawnNumber].PlayerAngle, new Vector(0, 0, 0));

                }
                else
                {
                    ReplyToUserCommand(player, $"Invalid value for {command} command. Please specify a valid non-negative number. Usage: !{command} <number>");
                    return;
                }
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !{command} <number>");
            }
        }

        [ConsoleCommand("css_prac", "Starts practice mode")]
        public void OnPracCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player)) return;

            if (matchStarted)
            {
                ReplyToUserCommand(player, "Practice Mode cannot be started when a match has been started!");
                return;
            }

            StartPracticeMode();
        }

        [ConsoleCommand("css_spawn", "Teleport to provided spawn")]
        public void OnSpawnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();
            if (player == null || !player.PlayerPawn.IsValid) return;

            if (command.ArgCount >= 2)
            {
                string commandArg = command.ArgByIndex(1);
                HandleSpawnCommand(player, commandArg, player.TeamNum, "spawn");
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !spawn <round>");
            }
        }

        [ConsoleCommand("css_ctspawn", "Teleport to provided CT spawn")]
        public void OnCtSpawnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();
            if (player == null || !player.PlayerPawn.IsValid) return;

            if (command.ArgCount >= 2)
            {
                string commandArg = command.ArgByIndex(1);
                HandleSpawnCommand(player, commandArg, (byte)CsTeam.CounterTerrorist, "ctspawn");
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !ctspawn <round>");
            }
        }

        [ConsoleCommand("css_tspawn", "Teleport to provided T spawn")]
        public void OnTSpawnCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!isPractice) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();
            if (player == null || !player.PlayerPawn.IsValid) return;

            if (command.ArgCount >= 2)
            {
                string commandArg = command.ArgByIndex(1);
                HandleSpawnCommand(player, commandArg, (byte)CsTeam.Terrorist, "tspawn");
            }
            else
            {
                ReplyToUserCommand(player, $"Usage: !ctspawn <round>");
            }
        }

        [ConsoleCommand("css_bot", "Teleport to spawn")]
        public void OnBotCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null) return;
            // Checking if any of the Position List is empty
            if (spawnsData.Values.Any(list => list.Count == 0)) GetSpawns();

            // !bot/.bot command is made using a lot of workarounds, as there is no direct way to create a bot entity and spawn it in CSSharp
            // Hence there can be some issues with this approach. This will be revamped when we will be able to create entities and manipulate them.
            if (player.TeamNum == 2)
            {
                Server.ExecuteCommand("bot_join_team T");
                Server.ExecuteCommand("bot_add_t");
            }
            else if (player.TeamNum == 3)
            {
                Server.ExecuteCommand("bot_join_team CT");
                Server.ExecuteCommand("bot_add_ct");
            }
            
            // Adding a small timer so that bot can be added in the world
            // Once bot is added, we teleport it to the requested position
            AddTimer(0.1f, () => SpawnBot(player));
            Server.ExecuteCommand("bot_stop 1");
            Server.ExecuteCommand("bot_freeze 1");
            Server.ExecuteCommand("bot_zombie 1");
        }

        private void SpawnBot(CCSPlayerController botOwner)
        {
            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            bool unusedBotFound = false;
            foreach (var tempPlayer in playerEntities)
            {
                if (!tempPlayer.IsBot) continue;
                if (tempPlayer.UserId.HasValue)
                {
                    if (!pracUsedBots.ContainsKey(tempPlayer.UserId.Value) && unusedBotFound)
                    {
                        Log($"UNUSED BOT FOUND: {tempPlayer.UserId.Value} EXECUTING: kickid {tempPlayer.UserId.Value}");
                        // Kicking the unused bot. We have to do this because bot_add_t/bot_add_ct may add multiple bots but we need only 1, so we kick the remaining unused ones
                        Server.ExecuteCommand($"kickid {tempPlayer.UserId.Value}");
                        continue;
                    }
                    if (pracUsedBots.ContainsKey(tempPlayer.UserId.Value))
                    {
                        continue;
                    }
                    else
                    {
                        pracUsedBots[tempPlayer.UserId.Value] = new Dictionary<string, object>();
                    }

                    Position botOwnerPosition = new Position(botOwner.PlayerPawn.Value.CBodyComponent?.SceneNode?.AbsOrigin, botOwner.PlayerPawn.Value.CBodyComponent?.SceneNode?.AbsRotation);
                    // Add key-value pairs to the inner dictionary
                    pracUsedBots[tempPlayer.UserId.Value]["controller"] = tempPlayer;
                    pracUsedBots[tempPlayer.UserId.Value]["position"] = botOwnerPosition;
                    pracUsedBots[tempPlayer.UserId.Value]["owner"] = botOwner;

                    tempPlayer.PlayerPawn.Value.Teleport(botOwnerPosition.PlayerPosition, botOwnerPosition.PlayerAngle, new Vector(0, 0, 0));
                    unusedBotFound = true;
                }
            }
            if (!unusedBotFound) {
                Server.PrintToChatAll($"{chatPrefix} 数量已满，无法加入Bot了! 使用 .nobots 来删除Bot.");
            }
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;

            // Respawing a bot where it was actually spawned during practice session
            if (isPractice && player.IsValid && player.IsBot && player.UserId.HasValue)
            {
                if (pracUsedBots.ContainsKey(player.UserId.Value))
                {
                    if (pracUsedBots[player.UserId.Value]["position"] is Position botPosition)
                    {
                        player.PlayerPawn.Value.Teleport(botPosition.PlayerPosition, botPosition.PlayerAngle, new Vector(0, 0, 0));
                    }
                }
            }


            return HookResult.Continue;
        }

        [ConsoleCommand("css_nobots", "Removes bots from the practice session")]
        public void OnNoBotsCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!isPractice || player == null) return;
            Server.ExecuteCommand("bot_kick");
            pracUsedBots = new Dictionary<int, Dictionary<string, object>>();
        }

        public void ExecUnpracCommands() {
            Server.ExecuteCommand("sv_cheats false;sv_grenade_trajectory_prac_pipreview false;sv_grenade_trajectory_prac_trailtime 0; mp_ct_default_grenades \"\"; mp_ct_default_primary \"\"; mp_t_default_grenades\"\"; mp_t_default_primary\"\"; mp_teammates_are_enemies false;");
            Server.ExecuteCommand("mp_death_drop_breachcharge true; mp_death_drop_defuser true; mp_death_drop_taser true; mp_drop_knife_enable false; mp_death_drop_grenade 2; ammo_grenade_limit_total 4; mp_defuser_allocation 0; sv_infinite_ammo 0; mp_force_pick_time 15");
        }

    }
}
