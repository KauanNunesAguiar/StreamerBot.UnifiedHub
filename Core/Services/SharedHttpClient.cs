namespace StreamerBot.UnifiedHub.Core.Services
{
    public static class SharedHttpClient
    {
        public static readonly HttpClient Instance = new();
    }
}