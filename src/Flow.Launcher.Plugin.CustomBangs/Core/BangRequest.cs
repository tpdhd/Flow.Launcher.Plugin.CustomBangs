using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Core;

public sealed record BangRequest(BangDefinition Bang, string QueryText);

