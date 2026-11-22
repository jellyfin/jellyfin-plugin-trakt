using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using Trakt.Configuration;
using Trakt.Model;
using Xunit;

namespace Trakt.Tests;

public sealed class PlaybackOrderingTests : IDisposable
{
    private readonly DirectoryInfo _temporary = Directory.CreateTempSubdirectory();
    private readonly Plugin? _previousInstance = Plugin.Instance;
    private readonly CaptureLogger _logger = new();
    private readonly Mock<ISessionManager> _session = new();
    private readonly Mock<IHttpClientFactory> _http = new(MockBehavior.Strict);
    private readonly User _user = new("viewer", "test-authentication", "test-password-reset");
    private readonly Plugin _plugin;
    private readonly ServerMediator _mediator;

    public PlaybackOrderingTests()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.PluginConfigurationsPath).Returns(_temporary.FullName);
        paths.SetupGet(p => p.PluginsPath).Returns(_temporary.FullName);
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
            .Returns(new PluginConfiguration());
        _plugin = new Plugin(paths.Object, serializer.Object);
        _plugin.Configuration.TraktUsers =
        [
            new TraktUser { LinkedMbUserId = _user.Id, AccessToken = "test-token", Scrobble = true }
        ];
        var factory = new Mock<ILoggerFactory>();
        factory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_logger);
        _mediator = new ServerMediator(
            _session.Object,
            Mock.Of<IUserDataManager>(),
            Mock.Of<ILibraryManager>(),
            factory.Object,
            _http.Object,
            Mock.Of<IServerApplicationHost>(),
            Mock.Of<IUserManager>());
    }

    [Fact]
    public async Task ProgressBeforeStart_InitializesStateWithoutAnActionableWarning()
    {
        await _mediator.StartAsync(TestContext.Current.CancellationToken);
        var progress = CreateProgress();

        _session.Raise(s => s.PlaybackProgress += null, progress);
        progress.PlaybackPositionTicks += TimeSpan.TicksPerSecond;
        _session.Raise(s => s.PlaybackProgress += null, progress);

        var initialization = Assert.Single(_logger.Entries, e => e.Message.Contains("initial state", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, initialization.Level);
        Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Warning);
        _http.VerifyNoOtherCalls();
        await _mediator.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DisabledScrobbling_DoesNotInitializePlaybackOrContactTrakt()
    {
        _plugin.Configuration.TraktUsers[0].Scrobble = false;
        await _mediator.StartAsync(TestContext.Current.CancellationToken);

        _session.Raise(s => s.PlaybackProgress += null, CreateProgress());

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("initial state", StringComparison.Ordinal));
        _http.VerifyNoOtherCalls();
        await _mediator.StopAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _mediator.Dispose();
        typeof(Plugin).GetProperty(nameof(Plugin.Instance))!.SetValue(null, _previousInstance);
        _temporary.Delete(recursive: true);
    }

    private PlaybackProgressEventArgs CreateProgress()
    {
        var movie = new PhysicalMovie { Id = Guid.NewGuid(), Path = "/media/movie.mkv", RunTimeTicks = TimeSpan.FromMinutes(90).Ticks };
        movie.SetProviderId(MetadataProvider.Imdb, "tt1234567");
        return new PlaybackProgressEventArgs
        {
            Item = movie,
            Users = [_user],
            PlaybackPositionTicks = TimeSpan.FromMinutes(1).Ticks,
            IsPaused = false
        };
    }

    private sealed class PhysicalMovie : Movie
    {
        public override LocationType LocationType => LocationType.FileSystem;
    }

    private sealed class CaptureLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
