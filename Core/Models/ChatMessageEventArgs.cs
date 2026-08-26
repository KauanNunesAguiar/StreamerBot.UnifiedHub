namespace StreamerBot.UnifiedHub.Core.Models
{
    public class ChatMessageEventArgs(string BotLabel, string message) : EventArgs
    {
        public string BotLabel { get; } = BotLabel;
        public string Message { get; } = message;
    }
}