namespace StreamerBot.UnifiedHub.Core.Models
{
    public class MessageDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Placeholders { get; set; } = [];
        public bool Enabled { get; set; } = true;
    }
}