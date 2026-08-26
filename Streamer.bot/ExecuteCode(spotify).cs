using System.Collections.Generic;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Spotify.Hubs;

public class CPHInline
{
	private class SbBridge : IStreamerBotBridge
	{
		private readonly CPHInline _outer;
		public SbBridge(CPHInline outer) => _outer = outer;
		public void SendMessage(string message, bool bot = true) => _outer.CPH.SendMessage(message, bot);
		public string? GetGlobalVar(string name, bool persisted = true) => _outer.CPH.GetGlobalVar<string>(name, persisted);
		public void SetGlobalVar(string name, object value, bool persisted = true) => _outer.CPH.SetGlobalVar(name, value, persisted);
	}

	private string GetUser()
	{
		CPH.TryGetArg("user", out string user);
		return user ?? "";
	}

	private bool Report(HubResult result)
	{
		if (!result.Success)
		{
			CPH.LogError($"[Spotify] {result.Message}");
			CPH.SendMessage(result.Message);
		}

		return result.Success;
	}

	// ---------- Ciclo de vida ----------
	public bool InitSpotify()
	{
		StreamerBotContext.Connect(new SbBridge(this));
		SpotifyHub.SetCommandProvider(() => new List<HubCommandInfo> { new("play", new[] { "!play" }, true), new("pause", new[] { "!pause" }, true), new("prev", new[] { "!prev" }, true), new("vol", new[] { "!vol" }, true), new("add", new[] { "!sr" }, true), new("undo", new[] { "!undo" }, true), new("atual", new[] { "!atual" }, true), new("fila", new[] { "!fila" }, true), new("skip", new[] { "!skip" }, true), new("voteskip", new[] { "!voteskip" }, true), });
		return Report(SpotifyHub.InitializeAsync(pollingIntervalMs: 1000).GetAwaiter().GetResult());
	}

	public bool Reconfigure() => Report(SpotifyHub.ReconfigureAsync().GetAwaiter().GetResult());
	public bool OpenConfigWindow() => Report(SpotifyHub.OpenSettingsUiAsync().GetAwaiter().GetResult());
	public bool Shutdown()
	{
		SpotifyHub.Shutdown();
		return true;
	}

	// ---------- Player ----------
	public bool Play() => Report(SpotifyHub.ResumeAsync(GetUser()).GetAwaiter().GetResult());
	public bool Pause() => Report(SpotifyHub.PauseAsync(GetUser()).GetAwaiter().GetResult());
	public bool Previous() => Report(SpotifyHub.PreviousAsync(GetUser()).GetAwaiter().GetResult());
	public bool Volume()
	{
		CPH.TryGetArg("input0", out int volume);
		return Report(SpotifyHub.SetVolumeAsync(volume, GetUser()).GetAwaiter().GetResult());
	}

	public bool Current() => Report(SpotifyHub.GetCurrentTrackAsync().GetAwaiter().GetResult());
	// ---------- Fila ----------
	public bool AddToQueue()
	{
		CPH.TryGetArg("input0", out string musica);
		CPH.TryGetArg("userId", out string userId);
		return Report(SpotifyHub.AddToQueueAsync(musica ?? "", userId ?? "", GetUser()).GetAwaiter().GetResult());
	}

	public bool Undo()
	{
		CPH.TryGetArg("userId", out string userId);
		CPH.TryGetArg("isModerator", out bool isMod);
		return Report(SpotifyHub.RemoveLastAddedFromQueueAsync(userId ?? "", isMod).GetAwaiter().GetResult());
	}

	public bool Queue() => Report(SpotifyHub.GetQueueAsync().GetAwaiter().GetResult());
	// ---------- Playlist / Skip ----------
	public bool PlaylistInfo() => Report(SpotifyHub.ShowPlaylistInfoAsync().GetAwaiter().GetResult());
	public bool AddToPlaylist() => Report(SpotifyHub.AddCurrentTrackToPlaylistAsync(GetUser()).GetAwaiter().GetResult());
	public bool ForceSkip() => Report(SpotifyHub.ForceSkipAsync(GetUser()).GetAwaiter().GetResult());
	public bool VoteSkip()
	{
		CPH.TryGetArg("userId", out string userId);
		return Report(SpotifyHub.VoteSkipAsync(userId ?? "").GetAwaiter().GetResult());
	}

	public bool SongHelp() => Report(SpotifyHub.ShowSongHelpAsync(GetUser()).GetAwaiter().GetResult());
	public bool NoPermission() => Report(SpotifyHub.NotifyNoPermissionAsync(GetUser()).GetAwaiter().GetResult());
	// ---------- Configurações Rápidas ----------
	public bool ConfigVoteSkip()
	{
		CPH.TryGetArg("input0", out int threshold);
		return Report(SpotifyHub.SetVoteSkipThreshold(threshold));
	}

	public bool ConfigQueueSize()
	{
		CPH.TryGetArg("input0", out int size);
		return Report(SpotifyHub.SetQueueSize(size));
	}

	public bool ConfigPollingInterval()
	{
		CPH.TryGetArg("input0", out int intervalMs);
		return Report(SpotifyHub.SetPollingIntervalMs(intervalMs));
	}

	public bool ConfigBotName()
	{
		CPH.TryGetArg("input0", out string botName);
		return Report(SpotifyHub.SetBotName(botName ?? ""));
	}

	public bool ConfigMessageToggle()
	{
		CPH.TryGetArg("input0", out string key);
		CPH.TryGetArg("input1", out bool enabled);
		return Report(SpotifyHub.SetMessageEnabled(key ?? "", enabled));
	}

	public bool ConfigMessageText()
	{
		CPH.TryGetArg("input0", out string key);
		CPH.TryGetArg("rawInput", out string template);
		return Report(SpotifyHub.SetMessageTemplate(key ?? "", template ?? ""));
	}
}