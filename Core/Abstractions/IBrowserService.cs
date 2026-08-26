using System;
using System.Collections.Generic;
using System.Text;

namespace StreamerBot.UnifiedHub.Core.Abstractions
{
    public interface IBrowserService
    {
        bool OpenUrl(string url);
    }
}