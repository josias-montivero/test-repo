using BattleBitAPI.Common;
using BBRAPIModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BattleBitAPI.Server;
using System.Net.Http.Json;
using System.Linq.Expressions;

namespace BattleBitDiscordWebhooks;

[Module("Send some basic events to POST endpoint", "1.1.1")]
public class DiscordWebhooks : BattleBitModule
{
    private Queue<StringContent> discordMessageQueue = new();
    private Queue<StringContent> discordMessageQueue2 = new();
    private Queue<StringContent> discordMessageQueue3 = new();
    private HttpClient httpClient = new HttpClient();
    public WebhookConfiguration Configuration { get; set; } = null!;

    [ModuleReference]
    public dynamic? PlayerFinder { get; set; }

    [ModuleReference]
    public dynamic? GranularPermissions { get; set; }

    public override Task OnConnected()
    {
        var payload = new
        {
            startMatch = false,
            endMatch = false,
            startConnection = true
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        discordMessageQueue3.Enqueue(content);

        if (this.Server.RoundSettings.State == GameState.WaitingForPlayers)
        {
            this.Server.RoundSettings.PlayersToStart = 0;
            this.Server.RoundSettings.SecondsLeft = 60;
            this.Server.RoundSettings.TeamATickets = 10;
            this.Server.RoundSettings.TeamBTickets = 10;
            this.Server.RoundSettings.MaxTickets = 10;
            this.Server.ForceStartGame();
        }

        Task.Run(() => sendChatMessagesToDiscord());
        Task.Run(() => sendChatMessagesToDiscord2());
        Task.Run(() => sendChatMessagesToDiscord3());
        return Task.CompletedTask;
    }

    public override Task OnPlayerConnected(RunnerPlayer player)
    {
        if (this.GranularPermissions is not null && this.GranularPermissions.HasPermission(player.SteamID, "player-whitelist"))
            return Task.CompletedTask;

        this.Server.Kick(player.SteamID, "Not whitelisted");
        return Task.CompletedTask;
    }

    public override Task OnPlayerJoinedSquad(RunnerPlayer player, Squad<RunnerPlayer> squad)
    {
        var payload = new Dictionary<string, object>
        {
            ["steamID"] = player.SteamID,
            ["name"] = player.Name,
            ["joinSquad"] = true,
            ["team"] = squad.Team.ToString(),
            ["squad"] = squad.Name.ToString(),
            ["isSquadLeader"] = player.IsSquadLeader
        };

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        discordMessageQueue2.Enqueue(content);
        return Task.CompletedTask;
    }

    /*public override Task OnSquadLeaderChanged(Squad<RunnerPlayer> squad, RunnerPlayer leader)
    {
        var payload = new Dictionary<string, object>
        {
            ["steamID"] = leader.SteamID,
            ["name"] = leader.Name,
            ["newLeader"] = true,
            ["team"] = squad.Team.ToString(),
            ["squad"] = squad.Name.ToString(),
        };

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        discordMessageQueue2.Enqueue(content);
        return Task.CompletedTask;
    }*/

    public override Task OnGameStateChanged(GameState oldState, GameState newState)
    {
        switch (newState)
        {
            case GameState.WaitingForPlayers:
                this.Server.RoundSettings.PlayersToStart = 0;
                this.Server.ForceStartGame();
                break;
            case GameState.Playing:
                List<object> team__a = new();
                List<object> team__b = new();

                this.Server.RoundSettings.MaxTickets = 10;
                this.Server.RoundSettings.PlayersToStart = 0;
                this.Server.RoundSettings.SecondsLeft = 60;

                var payload = new Dictionary<string, object>
                {
                    ["startMatch"] = true,
                    ["endMatch"] = false
                };

                foreach (Squad<RunnerPlayer> squad in this.Server.TeamASquads)
                {
                    var teamTemp = new Dictionary<string, object>
                    {
                        ["name"] = squad.Name.ToString(),
                        ["points"] = squad.SquadPoints,
                        ["team"] = squad.Team.ToString()
                    };

                    if (squad.Members.Count() > 0)
                    {
                        var playersTemp = new Dictionary<string, object>();

                        foreach (RunnerPlayer member in squad.Members)
                        {
                            playersTemp.Add(member.SteamID.ToString(), new Dictionary<string, object>
                            {
                                ["steamId"] = member.SteamID,
                                ["name"] = member.Name,
                                ["isSquadLeader"] = member.IsSquadLeader
                            });
                        }

                        teamTemp.Add("players", playersTemp);
                    }

                    team__a.Add(teamTemp);
                }

                foreach (Squad<RunnerPlayer> squad in this.Server.TeamBSquads)
                {
                    var teamTemp = new Dictionary<string, object>
                    {
                        ["name"] = squad.Name.ToString(),
                        ["points"] = squad.SquadPoints,
                        ["team"] = squad.Team.ToString()
                    };

                    if (squad.Members.Count() > 0)
                    {
                        var playersTemp = new Dictionary<string, object>();

                        foreach (RunnerPlayer member in squad.Members)
                        {
                            playersTemp.Add(member.SteamID.ToString(), new Dictionary<string, object>
                            {
                                ["steamId"] = member.SteamID,
                                ["name"] = member.Name,
                                ["isSquadLeader"] = member.IsSquadLeader
                            });
                        }

                        teamTemp.Add("players", playersTemp);
                    }

                    team__b.Add(teamTemp);
                }

                payload.Add("team_a", team__a);
                payload.Add("team_b", team__b);

                var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                discordMessageQueue3.Enqueue(content);
                break;
            case GameState.EndingGame:
                List<object> team_a = new();
                List<object> team_b = new();

                var json = new Dictionary<string, object>
                {
                    ["startMatch"] = false,
                    ["endMatch"] = true
                };

                foreach (Squad<RunnerPlayer> squad in this.Server.TeamASquads)
                {
                    var teamTemp = new Dictionary<string, object>
                    {
                        ["name"] = squad.Name.ToString(),
                        ["points"] = squad.SquadPoints,
                        ["team"] = squad.Team.ToString()
                    };

                    if (squad.Members.Count() > 0)
                    {
                        var playersTemp = new Dictionary<string, object>();

                        foreach (RunnerPlayer member in squad.Members)
                        {
                            playersTemp.Add(member.SteamID.ToString(), new Dictionary<string, object>
                            {
                                ["steamId"] = member.SteamID,
                                ["name"] = member.Name,
                                ["isSquadLeader"] = member.IsSquadLeader
                            });
                        }

                        teamTemp.Add("players", playersTemp);
                    }

                    team_a.Add(teamTemp);
                }

                foreach (Squad<RunnerPlayer> squad in this.Server.TeamBSquads)
                {
                    var teamTemp = new Dictionary<string, object>
                    {
                        ["name"] = squad.Name.ToString(),
                        ["points"] = squad.SquadPoints,
                        ["team"] = squad.Team.ToString()
                    };

                    if(squad.Members.Count() > 0)
                    {
                        var playersTemp = new Dictionary<string, object>();

                        foreach (RunnerPlayer member in squad.Members)
                        {
                            playersTemp.Add(member.SteamID.ToString(), new Dictionary<string, object>
                            {
                                ["steamId"] = member.SteamID,
                                ["name"] = member.Name,
                                ["isSquadLeader"] = member.IsSquadLeader
                            });
                        }

                        teamTemp.Add("players", playersTemp);
                    }

                    team_b.Add(teamTemp);
                }

                json.Add("team_a", team_a);
                json.Add("team_b", team_b);

                payloadJson = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
                content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                discordMessageQueue3.Enqueue(content);
                break;
        }

        return Task.CompletedTask;
    }

    public override Task OnSavePlayerStats(ulong steamId, PlayerStats stats)
    {
        if(this.PlayerFinder == null)
            return Task.CompletedTask;

        RunnerPlayer? player = this.PlayerFinder.BySteamId(steamId);

        if (player is null)
        {
            return Task.CompletedTask;
        }

        var payload = new Dictionary<string, object>
        {
            ["steamID"] = steamId,
            ["name"] = player.Name,
            ["joinServer"] = false,
            ["leaveServer"] = true,
            ["team"] = player.Team.ToString(),
            ["squad"] = player.Squad.ToString(),
            ["stats"] = new Dictionary<string, object>
            {
                ["KillCount"] = stats.Progress.KillCount,
                ["DeathCount"] = stats.Progress.DeathCount,
                ["WinCount"] = stats.Progress.WinCount,
                ["LoseCount"] = stats.Progress.LoseCount,
                ["Assists"] = stats.Progress.Assists,
                ["Rank"] = stats.Progress.Rank,
                ["EXP"] = stats.Progress.EXP,
                ["ShotsFired"] = stats.Progress.ShotsFired,
                ["ShotsHit"] = stats.Progress.ShotsHit,
                ["Headshots"] = stats.Progress.Headshots,
                ["PlayTimeSeconds"] = stats.Progress.PlayTimeSeconds,
                ["TotalScore"] = stats.Progress.TotalScore
            }
        };

        // ON SQUAD JOINED FOR TRACKING SQUAD AND PLAYERS MEMBERS

        /*var payload = new
        {
            steamID = steamId,
            joinServer = false,
            leaveServer = true,
            team = player.Team.ToString(),
            squad = player.Squad.ToString(),
            stats = new
            {
                stats.Progress.KillCount,
                stats.Progress.LeaderKills,
                stats.Progress.AssaultKills,
                stats.Progress.MedicKills,
                stats.Progress.EngineerKills,
                stats.Progress.SupportKills,
                stats.Progress.ReconKills,
                stats.Progress.DeathCount,
                stats.Progress.WinCount,
                stats.Progress.LoseCount,
                stats.Progress.FriendlyShots,
                stats.Progress.FriendlyKills,
                stats.Progress.Revived,
                stats.Progress.RevivedTeamMates,
                stats.Progress.Assists,
                stats.Progress.Prestige,
                stats.Progress.Rank,
                stats.Progress.EXP,
                stats.Progress.ShotsFired,
                stats.Progress.ShotsHit,
                stats.Progress.Headshots,
                stats.Progress.ObjectivesComplated,
                stats.Progress.HealedHPs,
                stats.Progress.RoadKills,
                stats.Progress.Suicides,
                stats.Progress.VehiclesDestroyed,
                stats.Progress.VehicleHPRepaired,
                stats.Progress.LongestKill,
                stats.Progress.PlayTimeSeconds,
                stats.Progress.LeaderPlayTime,
                stats.Progress.AssaultPlayTime,
                stats.Progress.MedicPlayTime,
                stats.Progress.EngineerPlayTime,
                stats.Progress.SupportPlayTime,
                stats.Progress.ReconPlayTime,
                stats.Progress.LeaderScore,
                stats.Progress.AssaultScore,
                stats.Progress.MedicScore,
                stats.Progress.EngineerScore,
                stats.Progress.SupportScore,
                stats.Progress.ReconScore,
                stats.Progress.TotalScore
            }
        };*/

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        discordMessageQueue.Enqueue(content);

        return Task.CompletedTask;
    }


    public override Task OnPlayerJoiningToServer(ulong steamId, PlayerJoiningArguments args)
    {
        if (this.GranularPermissions is not null && !this.GranularPermissions.HasPermission(steamId, "player-whitelist")) {
            this.Server.Kick(steamId, "Not whitelisted");
            return Task.CompletedTask;
        }

        var payload = new Dictionary<string, object>
        {
            ["steamID"] = steamId,
            ["joinServer"] = true,
            ["leaveServer"] = false,
            ["team"] = args.Team.ToString(),
            ["squad"] = args.Squad.ToString(),
            ["stats"] = new Dictionary<string, object>
            {
                ["KillCount"] = args.Stats.Progress.KillCount,
                ["DeathCount"] = args.Stats.Progress.DeathCount,
                ["WinCount"] = args.Stats.Progress.WinCount,
                ["LoseCount"] = args.Stats.Progress.LoseCount,
                ["Assists"] = args.Stats.Progress.Assists,
                ["Rank"] = args.Stats.Progress.Rank,
                ["EXP"] = args.Stats.Progress.EXP,
                ["ShotsFired"] = args.Stats.Progress.ShotsFired,
                ["ShotsHit"] = args.Stats.Progress.ShotsHit,
                ["Headshots"] = args.Stats.Progress.Headshots,
                ["PlayTimeSeconds"] = args.Stats.Progress.PlayTimeSeconds,
                ["TotalScore"] = args.Stats.Progress.TotalScore
            }
        };

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        discordMessageQueue.Enqueue(content);

        return Task.CompletedTask;
    }

    private async Task sendChatMessagesToDiscord()
    {
        do
        {
            StringContent? message_ds = new StringContent("");
            do
            {
                try
                {
                    while (this.discordMessageQueue.TryDequeue(out StringContent? new_message))
                    {
                        if (new_message is null)
                        {
                            continue;
                        }

                        message_ds = new_message;
                    }

                    var stringConst = await message_ds.ReadAsStringAsync();
                    if (message_ds is not null && stringConst != "")
                    {
                        await sendWebhookMessage("https://srvaux01.progamerid.com/api/v2/rivals/battlebit/stats", message_ds);
                    }

                    message_ds = null;
                }
                catch (Exception ex)
                {
                    this.Logger.Error(ex);
                    await Task.Delay(500);
                }
            } while (message_ds is not null && (await message_ds.ReadAsStringAsync()) != "");

            await Task.Delay(500);
        } while (this.Server?.IsConnected == true);
    }

    private async Task sendChatMessagesToDiscord2()
    {
        do
        {
            StringContent? message_ds = new StringContent("");
            do
            {
                try
                {
                    while (this.discordMessageQueue2.TryDequeue(out StringContent? new_message))
                    {
                        if (new_message is null)
                        {
                            continue;
                        }

                        message_ds = new_message;
                    }

                    var stringConst = await message_ds.ReadAsStringAsync();
                    if (message_ds is not null && stringConst != "")
                    {
                        await sendWebhookMessage("https://srvaux01.progamerid.com/api/v2/rivals/battlebit/player", message_ds);
                    }

                    message_ds = null;
                }
                catch (Exception ex)
                {
                    this.Logger.Error(ex);
                    await Task.Delay(500);
                }
            } while (message_ds is not null && (await message_ds.ReadAsStringAsync()) != "");

            await Task.Delay(500);
        } while (this.Server?.IsConnected == true);
    }

    private async Task sendChatMessagesToDiscord3()
    {
        do
        {
            StringContent? message_ds = new StringContent("");
            do
            {
                try
                {
                    while (this.discordMessageQueue3.TryDequeue(out StringContent? new_message))
                    {
                        if (new_message is null)
                        {
                            continue;
                        }

                        message_ds = new_message;
                    }

                    var stringConst = await message_ds.ReadAsStringAsync();
                    if (message_ds is not null && stringConst != "")
                    {
                        await sendWebhookMessage("https://srvaux01.progamerid.com/api/v2/rivals/battlebit/map", message_ds);
                    }

                    message_ds = null;
                }
                catch (Exception ex)
                {
                    this.Logger.Error(ex);
                    await Task.Delay(500);
                }
            } while (message_ds is not null && (await message_ds.ReadAsStringAsync()) != "");

            await Task.Delay(500);
        } while (this.Server?.IsConnected == true);
    }

    private async Task sendWebhookMessage(string webhookUrl, StringContent message)
    {
        bool success = false;
        while (!success)
        {
            var response = await this.httpClient.PostAsync(webhookUrl, message);

            if (!response.IsSuccessStatusCode)
            {
                this.Logger.Error(response);
            }

            success = response.IsSuccessStatusCode;
        }
    }

}

public class WebhookConfiguration : ModuleConfiguration
{
    public string WebhookURL { get; set; } = string.Empty;
}