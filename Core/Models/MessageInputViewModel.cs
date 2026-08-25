namespace StreamerBot.UnifiedHub.Core.Models
{
    public class MessageInputViewModel
    {
        public MessageDefinition Definition { get; set; } = new();
        public string Value { get; set; } = string.Empty;
    }
}