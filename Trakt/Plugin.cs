using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Trakt.Configuration;

namespace Trakt;

/// <summary>
/// Plugin class for the track.tv syncing.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        PollingTasks = new Dictionary<Guid, Task<bool>>();
    }

    /// <inheritdoc />
    public override string Name => "Trakt";

    /// <inheritdoc />
    public override Guid Id => new Guid("4fe3201e-d6ae-4f2e-8917-e12bda571281");

    /// <inheritdoc />
    public override string Description => "Sync your library to trakt.tv and scrobble your watch status.";

    /// <summary>
    /// Gets the instance of trakt.tv plugin.
    /// </summary>
    public static Plugin Instance { get; private set; }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public PluginConfiguration PluginConfiguration => Configuration;

    /// <summary>
    /// Gets the polling tasks.
    /// </summary>
    public Dictionary<Guid, Task<bool>> PollingTasks { get; }

    /// <summary>
    /// Return the plugin configuration page.
    /// </summary>
    /// <returns>PluginPageInfo.</returns>
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
