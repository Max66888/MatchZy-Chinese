using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;

namespace MatchZy
{
    public partial class MatchZy
    {
        [ConsoleCommand("css_ready", "Marks the player ready")]
        public void OnPlayerReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!ready command] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($"{chatPrefix} 您已准备过啦！！(〃'▽'〃)");
                    } else {
                            playerReadyStatus[player.UserId.Value] = true;
                        player.PrintToChat($"{chatPrefix} 您已准备啦！(ﾉ´▽｀)ﾉ♪");
                    }
                    CheckLiveRequired();
                }
            }
        }

        [ConsoleCommand("css_unready", "Marks the player unready")]
        public void OnPlayerUnReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!unready command] {player.UserId}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (!playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($"{chatPrefix} 你还没有准备哦￣へ￣");
                    } else {
                            playerReadyStatus[player.UserId.Value] = false;
                        player.PrintToChat($"{chatPrefix} 你怎么取消准备了(；′⌒`)");
                    }
                }
            }
        }

        [ConsoleCommand("css_stay", "Stays after knife round")]
        public void OnTeamStay(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!stay command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{knifeWinnerName}{ChatColors.Default} 选择了 {ChatColors.Green}保持阵营{ChatColors.Default} ！！");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_switch", "Switch after knife round")]
        public void OnTeamSwitch(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!switch command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.ExecuteCommand("mp_swapteams;");
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{knifeWinnerName}{ChatColors.Default} 选择了 {ChatColors.Red}交换阵营{ChatColors.Default} ！！");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_tech", "Pause the match")]
        public void OnTechCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_pause", "Pause the match")]
        public void OnPauseCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_unpause", "Unpause the match")]
        public void OnUnpauseCommand(CCSPlayerController? player, CommandInfo? command) {
            if (isMatchLive && isPaused) {
                var pauseTeamName = unpauseData["pauseTeam"];
                var playerAdmin = IsPlayerAdmin(player);
                if ((string)pauseTeamName == "Admin" && !(bool)playerAdmin) {
                    player?.PrintToChat($"{chatPrefix} Match has been paused by an admin, hence it can be unpaused by an admin only.");
                    return;
                }
                if ((bool)playerAdmin) {
                    Server.PrintToChatAll($"{chatPrefix} 管理员取消了比赛暂停！游戏即将继续！");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                    if (!isPaused && pausedStateTimer != null) {
                        pausedStateTimer.Kill();
                        pausedStateTimer = null;
                    }
                    if (player == null) {
                        Server.PrintToConsole("[MatchZy] 管理员取消了比赛暂停！游戏即将继续！");
                    }
                    return;
                }
                string unpauseTeamName = "Admin";
                string remainingUnpauseTeam = "Admin";
                if (player?.TeamNum == 2) {
                    unpauseTeamName = T_TEAM_NAME;
                    remainingUnpauseTeam = CT_TEAM_NAME;
                    if (!(bool)unpauseData["t"]) {
                        unpauseData["t"] = true;
                    }
                    
                } else if (player?.TeamNum == 3) {
                    unpauseTeamName = CT_TEAM_NAME;
                    remainingUnpauseTeam = T_TEAM_NAME;
                    if (!(bool)unpauseData["ct"]) {
                        unpauseData["ct"] = true;
                    }
                } else {
                    return;
                }
                if ((bool)unpauseData["t"] && (bool)unpauseData["ct"]) {
                    Server.PrintToChatAll($"{chatPrefix} 双方队伍同意取消暂停！游戏即将继续！");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                } else if (unpauseTeamName == "Admin") {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} 取消了比赛暂停！游戏即将继续！");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                } else {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} 希望暂停比赛 {ChatColors.Green}{remainingUnpauseTeam}{ChatColors.Default}, 可打出 !unpause 以同意！");
                }
                if (!isPaused && pausedStateTimer != null) {
                    pausedStateTimer.Kill();
                    pausedStateTimer = null;
                }
            }
        }

        [ConsoleCommand("css_tac", "Starts a tactical timeout for the requested team")]
        public void OnTacCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            if (matchStarted && isMatchLive) {
                Log($"[.tac command sent via chat] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
                if (player.TeamNum == 2) {
                    Server.ExecuteCommand("timeout_terrorist_start");
                } else if (player.TeamNum == 3) {
                    Server.ExecuteCommand("timeout_ct_start");
                } 
            }
        }

        [ConsoleCommand("css_knife", "Toggles knife round for the match")]
        public void OnKifeCommand(CCSPlayerController? player, CommandInfo? command) {            
            if (IsPlayerAdmin(player)) {
                isKnifeRequired = !isKnifeRequired;
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Knife round is now {knifeStatus}!");
                } else {
                    player.PrintToChat($"{chatPrefix} 刀局选边已经 {ChatColors.Green}{knifeStatus}{ChatColors.Default}!");
                }
            }
        }

        [ConsoleCommand("css_readyrequired", "Sets number of ready players required to start the match")]
        public void OnReadyRequiredCommand(CCSPlayerController? player, CommandInfo command) {
            if (IsPlayerAdmin(player)) {
                if (command.ArgCount >= 2) {
                    string commandArg = command.ArgByIndex(1);
                    HandleReadyRequiredCommand(player, commandArg);
                }
                else {
                    string minimumReadyRequiredFormatted = (player == null) ? $"{minimumReadyRequired}" : $"{ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}";
                    ReplyToUserCommand(player, $"Current Ready Required: {minimumReadyRequiredFormatted} .Usage: !readyrequired <number_of_ready_players_required>");
                }                
            }
        }

        [ConsoleCommand("css_settings", "Shows the current match configuration/settings")]
        public void OnMatchSettingsCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;

            if (IsPlayerAdmin(player)) {
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                player.PrintToChat($"{chatPrefix} 目前设置: 刀局选边: {ChatColors.Green}{knifeStatus}{ChatColors.Default}, 最小人数: {ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}");
            }
        }

        [ConsoleCommand("css_restart", "Restarts the match")]
        public void OnRestartMatchCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player) && !isPractice) {
                  for(int i=0;i<10;i++){
                    player.PrintToChat($"{chatPrefix} 游戏将被管理员在10秒后{ChatColors.Red}重启{ChatColors.Default}！！！");
	            }
                SendRestartMessageTimer = AddTimer(10, SendRestartMessage);
            }
        }
        private void SendRestartMessage() {
            Server.PrintToChatAll($"{chatPrefix} 游戏已重启！");
            ResetMatch();
        }

        [ConsoleCommand("css_map", "Changes the map using changelevel")]
        public void OnChangeMapCommand(CCSPlayerController? player, CommandInfo command) {
            if (player == null) return;
            var mapName = command.ArgByIndex(1);
            HandleMapChangeCommand(player, mapName);
        }

        [ConsoleCommand("css_start", "Force starts the match")]
        public void OnStartCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            if (IsPlayerAdmin(player) && !isPractice) {
                if (matchStarted) {
                    player.PrintToChat($"{chatPrefix} 在游戏开始后您无法使用 {ChatColors.Green}.start{ChatColors.Default} ，如您想暂停可输入 {ChatColors.Green}.unpause{ChatColors.Default} ");
                } else {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}管理员{ChatColors.Default} 强制开始了比赛！！!");
                    HandleMatchStart();
                }
            }
        }

        [ConsoleCommand("css_asay", "Say as an admin")]
        public void OnAdminSay(CCSPlayerController? player, CommandInfo? command) {
            if (command == null) return;
            if (player == null) {
                Server.PrintToChatAll($"[{ChatColors.Red}管理员{ChatColors.Default}] {command.ArgString}");
                return;
            }
            if (!IsPlayerAdmin(player)) return;
            string message = "";
            for (int i = 1; i < command.ArgCount; i++) {
                message += command.ArgByIndex(i) + " ";
            }
            Server.PrintToChatAll($"[{ChatColors.Red}管理员{ChatColors.Default}] {message}");
        }

        [ConsoleCommand("reload_admins", "Reload admins of MatchZy")]
        public void OnReloadAdmins(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player)) {
                LoadAdmins();
                UpdatePlayersMap();
            }
        }

        [ConsoleCommand("css_match", "Starts match mode")]
        public void OnMatchCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player)) return;

            if (matchStarted) {
                ReplyToUserCommand(player, "MatchZy is already in match mode!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_exitprac", "Starts match mode")]
        public void OnExitPracCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player)) return;

            if (matchStarted) {
                ReplyToUserCommand(player, "MatchZy is already in match mode!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_rcon", "Triggers provided command on the server")]
        public void OnRconCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsPlayerAdmin(player)) return;
            Server.ExecuteCommand(command.ArgString);
            ReplyToUserCommand(player, "Command sent successfully!");
        }

    }
}
