using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Trakt.Configuration;

namespace Trakt;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
        : base(appPaths, xmlSerializer)
    {
        Instance = this;
        PollingTasks = new Dictionary<string, Task<bool>>();
    }

    /// <inheritdoc />
    public override string Name => "Trakt";

    /// <inheritdoc />
    public override Guid Id => new Guid("4fe3201e-d6ae-4f2e-8917-e12bda571281");

    /// <inheritdoc />
    public override string Description
        => "Watch, rate, and discover media using Trakt. The HTPC just got more social.";

    public static Plugin Instance { get; private set; }

    public PluginConfiguration PluginConfiguration => Configuration;

    public Dictionary<string, Task<bool>> PollingTasks { get; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "trakt",
                EmbeddedResourcePath = GetType().Namespace + ".Web.trakt.html",
            },
            new PluginPageInfo
            {
                Name = "traktjs",
                EmbeddedResourcePath = GetType().Namespace + ".Web.trakt.js"
            }
        };
    }
}
