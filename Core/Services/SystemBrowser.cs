using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StreamerBot.UnifiedHub.Core.Abstractions;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public class SystemBrowser : IBrowserService
    {
        public bool OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Em produção ou futuramente, você pode direcionar para um ILogger
                Console.WriteLine($"[SystemBrowser] Erro ao abrir navegador para URL '{url}': {ex.Message}");
                return false;
            }
        }
    }
}