<h1 align="center">Trakt for Jellyfin Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org">Jellyfin Project</a></h3>

<p align="center">
<img alt="Plugin Banner" src="https://raw.githubusercontent.com/jellyfin/jellyfin-ux/master/plugins/SVG/jellyfin-plugin-trakt.svg?sanitize=true"/>
<br/>
<br/>
<a href="https://github.com/jellyfin/jellyfin-plugin-trakt/actions?query=workflow%3A%22Test+Build+Plugin%22">
<img alt="GitHub Workflow Status" src="https://img.shields.io/github/workflow/status/jellyfin/jellyfin-plugin-trakt/Test%20Build%20Plugin.svg">
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-trakt">
<img alt="MIT License" src="https://img.shields.io/github/license/jellyfin/jellyfin-plugin-trakt.svg"/>
</a>
<a href="https://github.com/jellyfin/jellyfin-plugin-trakt/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/jellyfin/jellyfin-plugin-trakt.svg"/>
</a>
</p>

## About

Available for install through the plugin catalog, Trakt for Jellyfin allows you to synchronize your watch states with ease.

## User self-service

Each Jellyfin user can link their own trakt.tv account and manage scrobble/sync preferences without admin access.

### Open the user page

Share or bookmark:

```text
/Trakt/SelfService
```

Example: `https://jellyfin.example.com/Trakt/SelfService`

The same link is shown (with a copy button) on **Dashboard → Plugins → Trakt**. Users must already be logged into Jellyfin in that browser (the page reads the session from `localStorage` and calls the `/Trakt/me*` APIs).

Do **not** use jellyfin-web `#/configurationpage` for this UI — that route is restricted to administrators and sends normal users home.

Stock jellyfin-web does not list plugin pages in the sidebar for non-admin users. Discovery is optional:

1. **Share the URL** (works for any logged-in user), or
2. **Optional sidebar entry** via jellyfin-web [`menuLinks`](https://jellyfin.org/docs/general/clients/web-config/) in the web `config.json`:

```json
"menuLinks": [
  {
    "name": "Trakt",
    "icon": "tv",
    "url": "/Trakt/SelfService"
  }
]
```

The plugin never modifies `config.json`. Admins can still configure any user from **Dashboard → Plugins → Trakt**.

### Self-service API

Authenticated as a Jellyfin user (except the HTML page, which is anonymous so the document can load):

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/Trakt/SelfService` | Standalone self-service HTML |
| `GET` | `/Trakt/me` | Link status, trakt.tv username (when linked), and editable preferences (no tokens) |
| `POST` | `/Trakt/me/Authorize` | Start device auth for the caller |
| `GET` | `/Trakt/me/PollAuthorizationStatus` | Wait for device auth completion |
| `POST` | `/Trakt/me/Deauthorize` | Unlink Trakt (keeps preferences) |
| `PUT` | `/Trakt/me/Settings` | Update the caller's preferences |
| `GET` | `/Trakt/me/Token` | Access token + expiry (see below) |

`PUT /Trakt/me/Settings` does **not** change admin-only fields: debug logging, library folder exclusions, or token export.

Routes under `/Trakt/Users/{userGuid}/...` allow the **same user** or an **administrator**. Deauthorize there also unlinks while keeping preferences (same as `/Trakt/me/Deauthorize`).

### Token export for other apps

`GET /Trakt/me/Token` returns `{ "accessToken": "...", "accessTokenExpiration": "..." }` only when:

1. The user has linked Trakt, and
2. An **administrator** enabled **Allow other apps to read this user's Trakt access token** for that user on **Dashboard → Plugins → Trakt**.

This is an admin-only policy (users cannot enable it via self-service). Disabled by default per user.
Admins can enable **Default: allow token export for new users** (server-wide) so newly created Trakt configs inherit `AllowExternalTokenAccess = true` (existing users are unchanged; override per user as needed).
The refresh token is never returned; apps should call this endpoint again when the access token is near expiry (Jellyfin refreshes it server-side).

## Installation

[See the official documentation for install instructions](https://jellyfin.org/docs/general/server/plugins/index.html#installing).

## Build

1. To build this plugin you will need [.Net 9.x](https://dotnet.microsoft.com/download/dotnet/9.0).

2. Build plugin with following command
  ```
  dotnet publish --configuration Release --output bin
  ```

3. Place the dll-file in the `plugins/trakt` folder (you might need to create the folders) of your JF install

## Releasing

To release the plugin we recommend [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) that will build and package the plugin.
For additional context and for how to add the packaged plugin zip to a plugin manifest see the [JPRM documentation](https://github.com/oddstr13/jellyfin-plugin-repository-manager) for more info.

## Contributing

We welcome all contributions and pull requests! If you have a larger feature in mind please open an issue so we can discuss the implementation before you start.
In general refer to our [contributing guidelines](https://github.com/jellyfin/.github/blob/master/CONTRIBUTING.md) for further information.

## Licence

This plugins code and packages are distributed under the MIT License. See [LICENSE](./LICENSE.md) for more information.
