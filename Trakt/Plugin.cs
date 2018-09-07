using System;
using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Trakt.Configuration;
using System.IO;
using MediaBrowser.Model.Drawing;

namespace Trakt
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public SemaphoreSlim TraktResourcePool = new SemaphoreSlim(1, 1);

        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "Trakt";

        private Guid _id = new Guid("8abc6789-fde2-4705-8592-4028806fa343");
        public override Guid Id
        {
            get { return _id; }
        }

        public override string Description
            => "Watch, rate and discover media using Trakt. The htpc just got more social";

        public static Plugin Instance { get; private set; }

        public PluginConfiguration PluginConfiguration => Configuration;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Trakt",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }
    }
}
