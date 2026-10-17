using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JellyDaily;

public sealed class SeriesRule
{
    public Guid UserId { get; set; }
    public Guid SeriesId { get; set; }
    public bool Paused { get; set; }
}
public sealed class Configuration : BasePluginConfiguration
{
    public string TimeZone { get; set; } = "Etc/UTC";
    public string ReleaseTime { get; set; } = "06:00";
    public double CompletionPercent { get; set; } = 90;
    public SeriesRule[] Rules { get; set; } = [];
}
public sealed class Plugin : BasePlugin<Configuration>, IHasWebPages
{
    public static Plugin Instance { get; private set; } = null!;
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer) { Instance = this; }
    public override string Name => "Daily Episodes";
    public override Guid Id => Guid.Parse("646aafae-e81c-4379-baf0-a66aa3a1b7ca");
    public override string Description => "Per-child watch-gated daily episode releases (experimental MVP).";
    public IEnumerable<PluginPageInfo> GetPages() => [new() { Name = "dailyepisodes", EmbeddedResourcePath = "JellyDaily.Web.config.html" }];
}
