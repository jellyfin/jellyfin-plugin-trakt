Run the isolated playback event regressions with:

```sh
dotnet test
```

The tests raise real session-manager events before a start event, using fake
configuration and a strict HTTP-client factory. They do not send scrobbles or
contact trakt.tv.
