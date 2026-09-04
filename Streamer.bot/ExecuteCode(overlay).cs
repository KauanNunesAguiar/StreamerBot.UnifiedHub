using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Integrations.Overlay.Hubs;

public class CPHInline
{
    private bool Report(StreamerBot.UnifiedHub.Core.Models.HubResult result)
    {
        if (!result.Success)
        {
            CPH.LogError($"[Overlay] {result.Message}");
        }

        return result.Success;
    }

    public bool OverlayInit()
    {
        CPH.TryGetArg("port", out int port);
        return Report(ChatOverlayHub.Start(port > 0 ? port : (int?)null));
    }

    public bool OverlayShutdown()
    {
        ChatOverlayHub.Stop();
        return true;
    }

    public bool OverlayTwitchChat()
    {
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("message", out string message);
        CPH.TryGetArg("color", out string color);
        CPH.TryGetArg("isModerator", out bool isModerator);
        CPH.TryGetArg("isVip", out bool isVip);
        CPH.TryGetArg("isSubscribed", out bool isSubscribed);

        CPH.TryGetArg("userName", out string userName);
        CPH.TryGetArg("broadcastUserName", out string broadcastUserName);
        CPH.TryGetArg("broadcastUserId", out string broadcastUserId);
        CPH.TryGetArg("badges", out string badges);

        foreach (var kv in args)
            CPH.LogInfo($"[OverlayDebug] {kv.Key} = {kv.Value}");

        bool isBroadcaster = !string.IsNullOrEmpty(userName) && string.Equals(userName, broadcastUserName, StringComparison.OrdinalIgnoreCase);

        string emotes = BuildEmotesString();

        return Report(ChatOverlayHub.PushTwitchMessageAsync(
            user ?? "", message ?? "",
            string.IsNullOrEmpty(color) ? null : color,
            string.IsNullOrEmpty(emotes) ? null : emotes,
            string.IsNullOrEmpty(badges) ? null : badges,
            string.IsNullOrEmpty(broadcastUserId) ? null : broadcastUserId,
            isBroadcaster, isModerator, isVip, isSubscribed).GetAwaiter().GetResult());
    }

    private string BuildEmotesString()
    {
        if (!CPH.TryGetArg("emotes", out object emotesObj) || emotesObj is not IEnumerable enumerable)
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var emote in enumerable)
        {
            if (emote == null) continue;
            var type = emote.GetType();

            string id = type.GetProperty("Id")?.GetValue(emote)?.ToString();
            var startVal = type.GetProperty("StartIndex")?.GetValue(emote);
            var endVal = type.GetProperty("EndIndex")?.GetValue(emote);

            if (string.IsNullOrEmpty(id) || startVal == null || endVal == null) continue;

            if (sb.Length > 0) sb.Append('/');
            sb.Append(id).Append(':').Append(startVal).Append('-').Append(endVal);
        }

        return sb.ToString();
    }

    public bool OverlayYoutubeChat()
    {
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("message", out string message);
        return Report(ChatOverlayHub.PushYoutubeMessage(user ?? "", message ?? ""));
    }

    public bool OverlayModeFadeOut() => Report(ChatOverlayHub.SetModeFadeOut());
    public bool OverlayModePermanent() => Report(ChatOverlayHub.SetModePermanent());

    public bool OverlayConfigPort()
    {
        CPH.TryGetArg("input0", out int port);
        return Report(ChatOverlayHub.SetPort(port));
    }

    public bool OverlayConfigEndpoint()
    {
        CPH.TryGetArg("input0", out string endpoint);
        return Report(ChatOverlayHub.SetEndpoint(endpoint ?? "/ws"));
    }

    public bool OverlayConfigMaxMessages()
    {
        CPH.TryGetArg("input0", out int max);
        return Report(ChatOverlayHub.SetMaxMessages(max));
    }

    public bool OverlayConfigFadeTime()
    {
        CPH.TryGetArg("input0", out int fadeMs);
        return Report(ChatOverlayHub.SetFadeTimeMs(fadeMs));
    }

    public bool OverlayConfigEmoteSize()
    {
        CPH.TryGetArg("input0", out int size);
        return Report(ChatOverlayHub.SetEmoteSize(size));
    }

    public bool OverlayConfigBadgeSize()
    {
        CPH.TryGetArg("input0", out int size);
        return Report(ChatOverlayHub.SetBadgeSize(size));
    }

    public bool OverlayConfigShowBadges()
    {
        CPH.TryGetArg("input0", out bool show);
        return Report(ChatOverlayHub.SetShowBadges(show));
    }

    private void DebugDumpEmoteProperties()
    {
        if (!CPH.TryGetArg("emotes", out object emotesObj) || emotesObj is not IEnumerable enumerable)
        {
            CPH.LogInfo("[OverlayDebug] Nenhum emote encontrado no argumento 'emotes'.");
            return;
        }

        bool any = false;
        foreach (var emote in enumerable)
        {
            if (emote == null) continue;
            any = true;
            var type = emote.GetType();
            CPH.LogInfo($"[OverlayDebug] Tipo do emote: {type.FullName}");

            foreach (var prop in type.GetProperties())
            {
                object value;
                try { value = prop.GetValue(emote); }
                catch (Exception ex) { value = $"<erro ao ler: {ex.Message}>"; }
                CPH.LogInfo($"[OverlayDebug]   {prop.Name} = {value}");
            }
            break; // só o primeiro emote já é suficiente
        }

        if (!any)
            CPH.LogInfo("[OverlayDebug] Lista de emotes estava vazia.");
    }

    public bool OverlayOpenSettings() => Report(ChatOverlayHub.OpenSettingsUiAsync().GetAwaiter().GetResult());
}