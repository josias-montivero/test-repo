using BattleBitAPI.Common;
using BattleBitAPI.Server;
using BBRAPIModules;
using Commands;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;
using System.Threading;
using System.Net.Http;
using System.Text.Json;

namespace BattleBitBaseModules;

[RequireModule(typeof(CommandHandler))]
[Module("Basic moderator tools", "1.1.1")]
public class ModeratorTools : BattleBitModule
{
    [ModuleReference]
    public CommandHandler CommandHandler { get; set; } = null!;
    public string currentMapRotation = "";
    public bool validRestart = false;


    public override void OnModulesLoaded()
    {
        this.CommandHandler.Register(this);
    }

    public override Task OnConnected()
    {
        currentMapRotation = this.Server.Map;

        this.Server.GamemodeRotation.SetRotation("CONQ");
        this.Server.MapRotation.SetRotation(currentMapRotation);
        this.Server.ServerSettings.CanVoteDay = true;
        this.Server.ServerSettings.CanVoteNight = false;

        Task.Run(playerInspection);
        return Task.CompletedTask;
    }
        
    private async void playerInspection()
    {
        while (this.IsLoaded && this.Server.IsConnected)
        {
            foreach (KeyValuePair<RunnerPlayer, RunnerPlayer> inspection in this.inspectPlayers)
            {
                inspection.Key.Message(this.getPlayerInspectionText(inspection.Value));
            }

            await Task.Delay(250);
        }
    }

    private string getPlayerInspectionText(RunnerPlayer target)
    {
        StringBuilder playerInfo = new();
        playerInfo.AppendLine($"{target.Name} ({target.SteamID} - {target.Role}");
        playerInfo.AppendLine($"Net: {target.IP} - {target.PingMs}ms");
        playerInfo.AppendLine($"Game: {target.Team} - {target.SquadName} - {(target.IsConnected ? "Connected" : "Disconnected")}");
        playerInfo.AppendLine($"Health: {target.HP} - {(target.IsAlive ? "Alive" : "Dead")} - {(target.IsDown ? "Down" : "Up")} - {(target.IsBleeding ? "Bleeding" : "Not bleeding")}");
        playerInfo.AppendLine($"State: {target.StandingState} - {target.LeaningState} - {(target.InVehicle ? "In vehicle" : "Not in vehicle")}");
        playerInfo.AppendLine($"Position: {target.Position}");
        playerInfo.AppendLine($"Loadout: {target.CurrentLoadout.PrimaryWeapon.ToolName} - {target.CurrentLoadout.SecondaryWeapon.ToolName} - {target.CurrentLoadout.ThrowableName}");
        playerInfo.AppendLine($"Loadout: {target.CurrentLoadout.HeavyGadgetName} - {target.CurrentLoadout.LightGadgetName}");

        return playerInfo.ToString();
    }

    private void ForceEnd()
    {
        try
        {
            this.Server.ForceEndGame();
        }
        catch (ThreadAbortException)
        {
            // cleanup code, if needed...
        }
    }

    [CommandCallback("Restart", Description = "Restart round", Permissions = new[] { "ModeratorTools.Ban" })]
    public string Restart(Context context)
    {
        this.Logger.Info($"[Restart] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> Round restarted");

        this.Server.ServerSettings.CanVoteDay = true;
        this.Server.ServerSettings.CanVoteNight = true;

        this.Server.MapRotation.ClearRotation();

        var task = Task.Run(() => this.ForceEnd());

        if (task.Wait(TimeSpan.FromSeconds(1)))
            return $"Round Restarted";
        else
            throw new Exception("Timed out");
    }

    [CommandCallback("ChangePassword", Description = "Change password", Permissions = new[] { "ModeratorTools.Ban" })]
    public string ChangePassword(Context context, string newPassword)
    {
        this.Logger.Info($"[ChangePassword] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> Changed password to ({newPassword})");
        this.Server.ExecuteCommand($"setpass {newPassword}");

        return $"Password changed";
    }

    [CommandCallback("ChangeSquad", Description = "Change player Squad", Permissions = new[] { "ModeratorTools.Ban" })]
    public string ChangeSquad(Context context, RunnerPlayer target, string squadName)
    {
        this.Logger.Info($"[ChangeSquad] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name} ({target.SquadName}) to ({squadName})");
        int num = (int)GetSquadString(squadName);

        target.Kill();

        string text = $"Player {target.Name} changed squad from {target.SquadName} to {squadName}";
        this.Server.ExecuteCommand($"setsquad {target.SteamID} {num}");

        return text;
    }

    [CommandCallback("Say", Description = "Prints a message to all players", Permissions = new[] { "ModeratorTools.Say" }, ConsoleCommand = true)]
    public string Say(Context context, string message)
    {
        this.Logger.Info($"[Say] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)}: {message}");
        this.Server.SayToAllChat(message);

        return $"Message sent to all players";
    }

    [CommandCallback("SayToPlayer", Description = "Prints a message to all players", Permissions = new[] { "ModeratorTools.SayToPlayer" }, ConsoleCommand = true)]
    public string SayToPlayer(Context context, RunnerPlayer target, string message)
    {
        this.Logger.Info($"[SayToPlayer] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}: {message}");
        this.Server.SayToChat(message, target.SteamID);

        return $"Message sent to {target.Name}";
    }

    [CommandCallback("AnnounceShort", Description = "Prints a short announce to all players", Permissions = new[] { "ModeratorTools.AnnounceShort" }, ConsoleCommand = true)]
    public string AnnounceShort(Context context, string message)
    {
        this.Logger.Info($"[AnnounceShort] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)}: {message}");
        this.Server.AnnounceShort(message);

        return $"Announce sent";
    }

    [CommandCallback("AnnounceLong", Description = "Prints a long announce to all players", Permissions = new[] { "ModeratorTools.AnnounceLong" }, ConsoleCommand = true)]
    public string AnnounceLong(Context context, string message)
    {
        this.Logger.Info($"[AnnounceLong] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)}: {message}");
        this.Server.AnnounceLong(message);

        return $"Announce sent";
    }

    [CommandCallback("Message", Description = "Messages a specific player", Permissions = new[] { "ModeratorTools.Message" }, ConsoleCommand = true)]
    public string Message(Context context, RunnerPlayer target, string message, float? timeout = null)
    {
        this.Logger.Info($"[Message] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}: {message}");

        if (timeout.HasValue)
        {
            target.Message(message, timeout.Value);
        }
        else
        {
            target.Message(message);
        }

        return $"Message sent to {target.Name}";
    }

    [CommandCallback("Clear", Description = "Clears the chat", Permissions = new[] { "ModeratorTools.Clear" }, ConsoleCommand = true)]
    public string Clear(Context context)
    {
        this.Logger.Info($"[Clear] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)}");
        this.Server.SayToAllChat("".PadLeft(30, '\n') + "<size=0%>Chat cleared");

        return $"Chat cleared";
    }

    [CommandCallback("Kick", Description = "Kicks a player", Permissions = new[] { "ModeratorTools.Kick" }, ConsoleCommand = true)]
    public string Kick(Context context, RunnerPlayer target, string? reason = null)
    {
        this.Logger.Info($"[Kick] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}: {reason ?? string.Empty}");
        target.Kick(reason ?? string.Empty);

        return $"Player {target.Name} kicked";
    }

    [CommandCallback("Ban", Description = "Bans a player", Permissions = new[] { "ModeratorTools.Ban" }, ConsoleCommand = true)]
    public string Ban(Context context, RunnerPlayer target)
    {
        this.Logger.Info($"[Ban] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        this.Server.ExecuteCommand($"ban {target.SteamID}");
        target.Kick();

        return $"Player {target.Name} banned";
    }

    [CommandCallback("Kill", Description = "Kills a player", Permissions = new[] { "ModeratorTools.Kill" }, ConsoleCommand = true)]
    public string Kill(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Kill] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        target.Kill();

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} killed";
    }

    [CommandCallback("Gag", Description = "Gags a player", Permissions = new[] { "ModeratorTools.Gag" }, ConsoleCommand = true)]
    public string Gag(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Gag] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        if (this.gaggedPlayers.Contains(target.SteamID))
        {
            return $"Player {target.Name} is already gagged";
        }

        this.gaggedPlayers.Add(target.SteamID);

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} gagged";
    }

    [CommandCallback("Ungag", Description = "Ungags a player", Permissions = new[] { "ModeratorTools.Ungag" }, ConsoleCommand = true)]
    public string Ungag(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Ungag] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        if (!this.gaggedPlayers.Contains(target.SteamID))
        {
            return $"Player {target.Name} is not gagged";
        }

        this.gaggedPlayers.Remove(target.SteamID);

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} ungagged";
    }

    [CommandCallback("Mute", Description = "Mutes a player", Permissions = new[] { "ModeratorTools.Mute" }, ConsoleCommand = true)]
    public string Mute(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Mute] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        if (target.Modifications.IsVoiceChatMuted)
        {
            return $"Player {target.Name} is already muted";
        }

        target.Modifications.IsVoiceChatMuted = true;

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} muted";
    }

    [CommandCallback("Unmute", Description = "Unmutes a player", Permissions = new[] { "ModeratorTools.Unmute" }, ConsoleCommand = true)]
    public string Unmute(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Unmute] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        if (!target.Modifications.IsVoiceChatMuted)
        {
            return $"Player {target.Name} is not muted";
        }

        target.Modifications.IsVoiceChatMuted = false;

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} unmuted";
    }

    [CommandCallback("Silence", Description = "Mutes and gags a player", Permissions = new[] { "ModeratorTools.Silence" }, ConsoleCommand = true)]
    public string Silence(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Silence] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        Mute(context, target);
        Gag(context, target);

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} silenced";
    }

    [CommandCallback("Unsilence", Description = "Unmutes and ungags a player", Permissions = new[] { "ModeratorTools.Unsilence" }, ConsoleCommand = true)]
    public string Unsilence(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Unsilence] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        Unmute(context, target);
        Ungag(context, target);

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} unsilenced";
    }

    [CommandCallback("LockSpawn", Description = "Prevents a player or all players from spawning", Permissions = new[] { "ModeratorTools.LockSpawn" }, ConsoleCommand = true)]
    public string LockSpawn(Context context, RunnerPlayer? target = null, string? message = null)
    {
        if (target == null)
        {
            this.Logger.Info($"[LockSpawn] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> All");
            this.globalSpawnLock = true;
            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                player.Modifications.CanDeploy = false;
            }

            return "Spawn globally locked";
        }
        else
        {
            this.Logger.Info($"[LockSpawn] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
            if (this.lockedSpawns.Contains(target.SteamID))
            {
                return $"Spawn already locked for {target.Name}";
            }

            target.Modifications.CanDeploy = false;
            this.lockedSpawns.Add(target.SteamID);

            if (!string.IsNullOrEmpty(message))
            {
                target.Message(message);
            }

            return $"Spawn locked for {target.Name}";
        }
    }

    [CommandCallback("UnlockSpawn", Description = "Allows a player or all players to spawn", Permissions = new[] { "ModeratorTools.UnlockSpawn" }, ConsoleCommand = true)]
    public string UnlockSpawn(Context context, RunnerPlayer? target = null, string? message = null)
    {
        if (target == null)
        {
            this.Logger.Info($"[UnlockSpawn] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> All");
            this.globalSpawnLock = false;
            foreach (RunnerPlayer player in this.Server.AllPlayers)
            {
                player.Modifications.CanDeploy = true;
            }

            return "Spawn globally unlocked";
        }
        else
        {
            this.Logger.Info($"[UnlockSpawn] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
            if (!this.lockedSpawns.Contains(target.SteamID))
            {
                return $"Spawn already unlocked for {target.Name}";
            }

            target.Modifications.CanDeploy = true;
            this.lockedSpawns.Remove(target.SteamID);

            if (!string.IsNullOrEmpty(message))
            {
                target.Message(message);
            }

            return $"Spawn unlocked for {target.Name}";
        }
    }

    [CommandCallback("tp2me", Description = "Teleports a player to you", Permissions = new[] { "ModeratorTools.Teleport" })]
    public string TeleportPlayerToMe(Context context, RunnerPlayer target)
    {
        if (context.Source is not ChatSource chatSource)
        {
            return "Teleport player to me can only work from chat";
        }

        this.Logger.Info($"[TeleportPlayerToMe] {chatSource.Invoker.Name} -> {target.Name}");
        target.Teleport(new Vector3((int)chatSource.Invoker.Position.X, (int)chatSource.Invoker.Position.Y, (int)chatSource.Invoker.Position.Z));

        return $"Player {target.Name} teleported to you";
    }

    [CommandCallback("tpme2", Description = "Teleports you to a player", Permissions = new[] { "ModeratorTools.Teleport" })]
    public string TeleportMeToPlayer(Context context, RunnerPlayer target)
    {
        if (context.Source is not ChatSource chatSource)
        {
            return "Teleport me to player can only work from chat";
        }

        this.Logger.Info($"[TeleportMeToPlayer] {chatSource.Invoker.Name} -> {target.Name}");
        chatSource.Invoker.Teleport(new Vector3((int)target.Position.X, (int)target.Position.Y, (int)target.Position.Z));

        return $"You teleported to {target.Name}";
    }

    [CommandCallback("tp", Description = "Teleports a player to another player", Permissions = new[] { "ModeratorTools.Teleport" }, ConsoleCommand = true)]
    public string TeleportPlayerToPlayer(Context context, RunnerPlayer target, RunnerPlayer destination)
    {
        this.Logger.Info($"[TeleportPlayerToPlayer] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name} -> {destination.Name}");
        target.Teleport(new Vector3((int)destination.Position.X, (int)destination.Position.Y, (int)destination.Position.Z));

        return $"Player {target.Name} teleported to {destination.Name}";
    }

    [CommandCallback("tp2pos", Description = "Teleports a player to a position", Permissions = new[] { "ModeratorTools.Teleport" }, ConsoleCommand = true)]
    public string TeleportPlayerToPos(Context context, RunnerPlayer target, int x, int y, int z)
    {
        this.Logger.Info($"[TeleportPlayerToPos] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name} -> {x} {y} {z}");
        target.Teleport(new Vector3(x, y, z));

        return $"Player {target.Name} teleported to {x} {y} {z}";
    }

    [CommandCallback("tpme2pos", Description = "Teleports you to a position", Permissions = new[] { "ModeratorTools.Teleport" })]
    public string TeleportMeToPos(Context context, int x, int y, int z)
    {
        if (context.Source is not ChatSource chatSource)
        {
            return "Teleport me to position can only work from chat";
        }

        this.Logger.Info($"[TeleportMeToPos] {chatSource.Invoker.Name} -> {x} {y} {z}");
        chatSource.Invoker.Teleport(new Vector3(x, y, z));

        return $"You teleported to {x} {y} {z}";
    }

    [CommandCallback("freeze", Description = "Freezes a player", Permissions = new[] { "ModeratorTools.Freeze" }, ConsoleCommand = true)]
    public string Freeze(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Freeze] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        target.Modifications.Freeze = true;

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} frozen";
    }

    [CommandCallback("unfreeze", Description = "Unfreezes a player", Permissions = new[] { "ModeratorTools.Unfreeze" }, ConsoleCommand = true)]
    public string Unfreeze(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Unfreeze] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}");
        target.Modifications.Freeze = false;

        if (!string.IsNullOrEmpty(message))
        {
            target.Message(message);
        }

        return $"Player {target.Name} unfrozen";
    }

    private Dictionary<RunnerPlayer, RunnerPlayer> inspectPlayers = new();

    [CommandCallback("Inspect", Description = "Inspects a player or stops inspection", Permissions = new[] { "ModeratorTools.Inspect" }, ConsoleCommand = true)]
    public string? Inspect(Context context, RunnerPlayer? target = null)
    {
        if (context.Source is not ChatSource chatSource)
        {
            if (target is null)
            {
                return "A target player to inspect is required.";
            }

            return this.getPlayerInspectionText(target);
        }

        if (target is null)
        {
            this.inspectPlayers.Remove(chatSource.Invoker);
            return "Inspection stopped";
        }

        if (this.inspectPlayers.ContainsKey(chatSource.Invoker))
        {
            this.inspectPlayers[chatSource.Invoker] = target;
        }
        else
        {
            this.inspectPlayers.Add(chatSource.Invoker, target);
        }

        return null;
    }

    [CommandCallback("warn", Description = "Warns a player", Permissions = new[] { "ModeratorTools.Warn" }, ConsoleCommand = true)]
    public string Warn(Context context, RunnerPlayer target, string? message = null)
    {
        this.Logger.Info($"[Warn] {(context.Source is ChatSource chatSource ? chatSource.Invoker.Name : context.Source.GetType().Name)} -> {target.Name}: {message ?? "no reason"}");
        target.WarnPlayer(message ?? "no reason");
        target.Message($"You have been warned for\n{message ?? "no reason"}", 25);

        return $"Player {target.Name} warned";
    }

    public static Squads GetSquadString(string squadName)
    {
        var lowerName = squadName.ToLower();
        
        switch(lowerName)
        {
            case "nosquad":
                return Squads.NoSquad;
            case "alpha":
                return Squads.Alpha;
            case "bravo":
                return Squads.Bravo;
            case "charlie":
                return Squads.Charlie;
            case "delta":
                return Squads.Delta;
            case "echo":
                return Squads.Echo;
            case "foxtrot":
                return Squads.Foxtrot;
            case "golf":
                return Squads.Golf;
            case "hotel":
                return Squads.Hotel;
            case "india":
                return Squads.India;
            case "juliett":
                return Squads.Juliett;
            case "kilo":
                return Squads.Kilo;
            case "lima":
                return Squads.Lima;
            case "mike":
                return Squads.Mike;
            case "november":
                return Squads.November;
            case "oscar":
                return Squads.Oscar;
            case "papa":
                return Squads.Papa;
            case "quebec":
                return Squads.Quebec;
            case "romeo":
                return Squads.Romeo;
            case "sierra":
                return Squads.Sierra;
            case "tango":
                return Squads.Tango;
            case "uniform":
                return Squads.Uniform;
            case "whiskey":
                return Squads.Whiskey;
            case "xray":
                return Squads.Xray;
            case "yankee":
                return Squads.Yankee;
            case "zulu":
                return Squads.Zulu;
            case "ash":
                return Squads.Ash;
            case "baker":
                return Squads.Baker;
            case "cast":
                return Squads.Cast;
            case "diver":
                return Squads.Diver;
            case "eagle":
                return Squads.Eagle;
            case "fisher":
                return Squads.Fisher;
            case "george":
                return Squads.George;
            case "hanover":
                return Squads.Hanover;
            case "ice":
                return Squads.Ice;
            case "jake":
                return Squads.Jake;
            case "king":
                return Squads.King;
            case "lash":
                return Squads.Lash;
            case "mule":
                return Squads.Mule;
            case "neptune":
                return Squads.Neptune;
            case "ostend":
                return Squads.Ostend;
            case "page":
                return Squads.Page;
            case "quail":
                return Squads.Quail;
            case "raft":
                return Squads.Raft;
            case "scout":
                return Squads.Scout;
            case "tare":
                return Squads.Tare;
            case "unit":
                return Squads.Unit;
            case "william":
                return Squads.William;
            case "xaintrie":
                return Squads.Xaintrie;
            case "yoke":
                return Squads.Yoke;
            case "zebra":
                return Squads.Zebra;
            case "ace":
                return Squads.Ace;
            case "beer":
                return Squads.Beer;
            case "cast2":
                return Squads.Cast2;
            case "duff":
                return Squads.Duff;
            case "edward":
                return Squads.Edward;
            case "freddy":
                return Squads.Freddy;
            case "Gustav":
                return Squads.Gustav;
            case "Henry":
                return Squads.Henry;
            case "Ivar":
                return Squads.Ivar;
            case "Jazz":
                return Squads.Jazz;
            case "Key":
                return Squads.Key;
            case "Lincoln":
                return Squads.Lincoln;
            case "Mary":
                return Squads.Mary;
            case "Nora":
                return Squads.Nora;
        }

        return Squads.NoSquad;
    }

    private List<ulong> gaggedPlayers = new();
    private List<ulong> lockedSpawns = new();
    private bool globalSpawnLock = false;

    public override Task<bool> OnPlayerTypedMessage(RunnerPlayer player, ChatChannel channel, string msg)
    {
        if (this.gaggedPlayers.Contains(player.SteamID))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public override Task<OnPlayerSpawnArguments?> OnPlayerSpawning(RunnerPlayer player, OnPlayerSpawnArguments request)
    {
        if (this.globalSpawnLock || this.lockedSpawns.Contains(player.SteamID))
        {
            return Task.FromResult<OnPlayerSpawnArguments?>(null);
        }

        return Task.FromResult(request as OnPlayerSpawnArguments?);
    }
}
