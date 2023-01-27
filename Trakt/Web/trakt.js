const TraktConfigurationPage = {
    pluginUniqueId: '4fe3201e-d6ae-4f2e-8917-e12bda571281',
    loadConfiguration: function (userId, page) {
        ApiClient.getPluginConfiguration(TraktConfigurationPage.pluginUniqueId).then(function (config) {
            let currentUserConfig = config.TraktUsers.filter(function (curr) {
                return curr.LinkedMbUserId == userId;
                //return true;
            })[0];
            // User doesn't have a config, so create a default one.
            if (!currentUserConfig) {
                // You don't have to put every property in here, just the ones the UI is expecting (below)
                currentUserConfig = {
                    AccessToken: null,
                    SkipUnwatchedImportFromTrakt: true,
                    SkipWatchedImportFromTrakt: false,
                    PostWatchedHistory: true,
                    PostUnwatchedHistory: true,
                    PostSetWatched: true,
                    PostSetUnwatched: true,
                    ExtraLogging: false,
                    ExportMediaInfo: false,
                    SynchronizeCollections: true,
                    Scrobble: true,
                    DontRemoveItemFromTrakt: true
                };
            }
            // Default this to an empty array so the rendering code doesn't have to worry about it
            currentUserConfig.LocationsExcluded = currentUserConfig.LocationsExcluded || [];
            page.querySelector('#chkSkipUnwatchedImportFromTrakt').checked = currentUserConfig.SkipUnwatchedImportFromTrakt;
            page.querySelector('#chkSkipWatchedImportFromTrakt').checked = currentUserConfig.SkipWatchedImportFromTrakt;
            page.querySelector('#chkSkipPlaybackProgressImportFromTrakt').checked = currentUserConfig.SkipPlaybackProgressImportFromTrakt;
            page.querySelector('#chkPostWatchedHistory').checked = currentUserConfig.PostWatchedHistory;
            page.querySelector('#chkPostUnwatchedHistory').checked = currentUserConfig.PostUnwatchedHistory;
            page.querySelector('#chkPostSetWatched').checked = currentUserConfig.PostSetWatched;
            page.querySelector('#chkPostSetUnwatched').checked = currentUserConfig.PostSetUnwatched;
            page.querySelector('#chkExtraLogging').checked = currentUserConfig.ExtraLogging;
            page.querySelector('#chkExportMediaInfo').checked = currentUserConfig.ExportMediaInfo;
            page.querySelector('#chkSyncCollections').checked = currentUserConfig.SynchronizeCollections;
            page.querySelector('#chkScrobble').checked = currentUserConfig.Scrobble;
            page.querySelector('#chkDontRemoveItemFromTrakt').checked = currentUserConfig.DontRemoveItemFromTrakt;
            // List the folders the user can access
            ApiClient.getVirtualFolders(userId).then(function (result) {
                TraktConfigurationPage.loadFolders(currentUserConfig, result);
            });

            setAuthorizationElements(page, currentUserConfig.AccessToken != null);
            Dashboard.hideLoadingMsg();
        });
    },
    populateUsers: function (users) {
        let html = '';
        for (let i = 0, length = users.length; i < length; i++) {
            const user = users[i];
            html += '<option value="' + user.Id + '">' + user.Name + '</option>';
        }
        document.querySelector('#selectUser').innerHTML = html;
    },
    loadFolders: function (currentUserConfig, virtualFolders) {
        let html = '';
        html += '<div data-role="controlgroup">';
        for (let i = 0, length = virtualFolders.length; i < length; i++) {
            const virtualFolder = virtualFolders[i];
            html += TraktConfigurationPage.getFolderHtml(currentUserConfig, virtualFolder, i);
        }
        html += '</div>';
        const divTraktLocations = document.querySelector('#divTraktLocations');
        divTraktLocations.innerHTML = html;
        divTraktLocations.dispatchEvent(new Event('create'));
    },
    getFolderHtml: function (currentUserConfig, virtualFolder, index) {
        let html = '';
        for (let i = 0, length = virtualFolder.Locations.length; i < length; i++) {
            const id = 'chkFolder' + index + '_' + i;
            const location = virtualFolder.Locations[i];
            const isChecked = currentUserConfig.LocationsExcluded.filter(function (current) {
                return current.toLowerCase() == location.toLowerCase();
            }).length;
            const checkedAttribute = isChecked ? 'checked="checked"' : '';
            html += '<label><input is="emby-checkbox" class="chkTraktLocation" type="checkbox" data-mini="true" id="' + id + '" name="' + id + '" data-location="' + location + '" ' + checkedAttribute + ' /><span>' + location + '</span></label>';
        }
        return html;
    }
};

function setAuthorizationElements(page, isAuthorized) {
    let buttonText;
    if (isAuthorized) {
        page.querySelector('#activateWithCode').classList.add('hide');
        page.querySelector('#deauthorizeDevice').classList.remove('hide');
        page.querySelector('#authorizedDescription').classList.remove('hide');
        buttonText = 'Force re-authorization';
    } else {
        page.querySelector('#deauthorizeDevice').classList.add('hide');
        page.querySelector('#authorizedDescription').classList.add('hide');
        buttonText = 'Authorize device';
    }
    // Set the auth button
    page.querySelector('#authorizeDevice').textContent = buttonText;
    page.querySelector('#authorizeDevice').classList.remove('hide');
}

function save(page) {
    return new Promise((resolve) => {
        const currentUserId = page.querySelector('#selectUser').value;
        ApiClient.getPluginConfiguration(TraktConfigurationPage.pluginUniqueId).then(function (config) {
            let currentUserConfig = config.TraktUsers.filter(function (curr) {
                return curr.LinkedMbUserId == currentUserId;
            })[0];
            // User doesn't have a config, so create a default one.
            if (!currentUserConfig) {
                currentUserConfig = {};
                config.TraktUsers.push(currentUserConfig);
            }
            currentUserConfig.SkipUnwatchedImportFromTrakt = page.querySelector('#chkSkipUnwatchedImportFromTrakt').checked;
            currentUserConfig.SkipWatchedImportFromTrakt = page.querySelector('#chkSkipWatchedImportFromTrakt').checked;
            currentUserConfig.SkipPlaybackProgressImportFromTrakt = page.querySelector('#chkSkipPlaybackProgressImportFromTrakt').checked;
            currentUserConfig.PostWatchedHistory = page.querySelector('#chkPostWatchedHistory').checked;
            currentUserConfig.PostUnwatchedHistory = page.querySelector('#chkPostUnwatchedHistory').checked;
            currentUserConfig.PostSetWatched = page.querySelector('#chkPostSetWatched').checked;
            currentUserConfig.PostSetUnwatched = page.querySelector('#chkPostSetUnwatched').checked;
            currentUserConfig.ExtraLogging = page.querySelector('#chkExtraLogging').checked;
            currentUserConfig.ExportMediaInfo = page.querySelector('#chkExportMediaInfo').checked;
            currentUserConfig.SynchronizeCollections = page.querySelector('#chkSyncCollections').checked;
            currentUserConfig.Scrobble = page.querySelector('#chkScrobble').checked;
            currentUserConfig.DontRemoveItemFromTrakt = page.querySelector('#chkDontRemoveItemFromTrakt').checked;
            currentUserConfig.LinkedMbUserId = currentUserId;
            currentUserConfig.LocationsExcluded = Array.prototype.map.call(page.querySelectorAll('.chkTraktLocation:checked'), elem => {
                return elem.getAttribute('data-location');
            });
            if (currentUserConfig.UserName == '') {
                config.TraktUsers.remove(config.TraktUsers.indexOf(currentUserConfig));
            }
            ApiClient.updatePluginConfiguration(TraktConfigurationPage.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
                ApiClient.getUsers().then(function (users) {
                    const currentUserId = page.querySelector('#selectUser').value;
                    TraktConfigurationPage.populateUsers(users);
                    page.querySelector('#selectUser').value = currentUserId;
                    TraktConfigurationPage.loadConfiguration(currentUserId, page);
                    resolve();
                });
            });
        });
    });
}

export default function (view) {
    view.querySelector('#selectUser').addEventListener('change', function () {
        TraktConfigurationPage.loadConfiguration(this.value, view);
    });

    view.querySelector('#traktConfigurationForm').addEventListener('submit', function (e) {
        save(view);
        e.preventDefault();
        return false;
    });

    view.querySelector('#authorizeDevice').addEventListener('click', function (e) {
        const currentUserId = view.querySelector('#selectUser').value;
        const headers = {
            accept: 'application/json'
        };
        const request = {
            url: ApiClient.getUrl('Trakt/Users/' + currentUserId + '/Authorize'),
            dataType: 'json',
            type: 'POST',
            headers: headers
        };
        function handleError(result) {
            Dashboard.alert({
                message: 'An error occurred when trying to authorize device: ' + result.status + ' - ' + result.statusText
            });
        };
        ApiClient.fetch(request).then(function (result) {
            console.log('trakt.tv user code: ' + result.userCode);
            view.querySelector('#authorizedDescription').classList.add('hide');
            view.querySelector('#authorizeDevice').classList.add('hide');
            view.querySelector('#userCode').textContent = result.userCode;
            view.querySelector('#activateWithCode').classList.remove('hide');

            console.log('Polling for authorization.');
            request.url = ApiClient.getUrl('Trakt/Users/' + currentUserId + '/PollAuthorizationStatus');
            request.type = 'GET';
            ApiClient.fetch(request).then(function (result) {
                console.log('User is authorized: ' + result.isAuthorized);
                view.querySelector('#userCode').textContent = '';
                TraktConfigurationPage.loadConfiguration(currentUserId, view);
            }).catch(handleError);
        }).catch(handleError);
    });

    view.querySelector('#deauthorizeDevice').addEventListener('click', function (e) {
        const currentUserId = view.querySelector('#selectUser').value;
        const headers = {
            accept: 'application/json'
        };
        const request = {
            url: ApiClient.getUrl('Trakt/Users/' + currentUserId + '/Deauthorize'),
            dataType: 'json',
            type: 'POST',
            headers: headers
        };
        function handleError() {
            Dashboard.alert({
                message: 'An error occurred when trying to deauthorize device for user ' + currentUserId
            });
        };
        ApiClient.fetch(request).then(function () {
            view.querySelector('#authorizedDescription').classList.add('hide');
            view.querySelector('#authorizeDevice').classList.remove('hide');
            view.querySelector('#userCode').textContent = '';
            view.querySelector('#activateWithCode').classList.add('hide');
            TraktConfigurationPage.loadConfiguration(currentUserId, view);
        }).catch(handleError);
    });

    view.addEventListener('viewshow', function () {
        const page = this;
        ApiClient.getUsers().then(function (users) {
            TraktConfigurationPage.populateUsers(users);
            const currentUserId = page.querySelector('#selectUser').value;
            TraktConfigurationPage.loadConfiguration(currentUserId, page);
        });
    });
}
