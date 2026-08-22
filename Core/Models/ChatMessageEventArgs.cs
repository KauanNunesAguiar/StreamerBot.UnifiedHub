namespace StreamerBot.UnifiedHub.Core.Models
{
    public class ChatMessageEventArgs(string botName, string message) : EventArgs
    {
        public string BotName { get; } = botName;
        public string Message { get; } = message;
    }
}