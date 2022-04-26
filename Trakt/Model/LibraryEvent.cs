using MediaBrowser.Controller.Entities;
using Trakt.Model.Enums;

namespace Trakt.Model;

internal class LibraryEvent
{
    public BaseItem Item { get; set; }

    public TraktUser TraktUser { get; set; }

    public EventType EventType { get; set; }
}
