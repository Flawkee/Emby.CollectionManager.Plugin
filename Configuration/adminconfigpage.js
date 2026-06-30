define([
    'loading',
    'emby-input',
    'emby-button',
    'emby-checkbox'
], function (loading) {
    'use strict';

    var pluginUniqueId = '80FDA42F-C32A-4BAE-8757-4DD49EF331A0';

    return function (view) {

        var form = view.querySelector('#cmAdminForm');
        var divStreamingLibraries = view.querySelector('#divStreamingLibraries');
        var _libraries = [];

        function escAttr(s) {
            return (s == null ? '' : String(s))
                .replace(/&/g, '&amp;').replace(/"/g, '&quot;')
                .replace(/</g, '&lt;').replace(/>/g, '&gt;');
        }

        function renderCheckboxList(container, items, selectedValues, name) {
            var selected = (selectedValues || []).reduce(function (acc, v) { acc[v] = true; return acc; }, {});
            container.innerHTML = items.map(function (item) {
                var checked = selected[item.Id] ? ' checked="checked"' : '';
                return '<label class="emby-checkbox-label">'
                    + '<input is="emby-checkbox" type="checkbox" name="' + escAttr(name) + '" value="' + escAttr(item.Id) + '"' + checked + ' />'
                    + '<span>' + escAttr(item.Name) + '</span></label>';
            }).join('') || '<div class="fieldDescription">No libraries found.</div>';
        }

        function readCheckboxList(container) {
            return Array.prototype.slice.call(container.querySelectorAll('input[type="checkbox"]'))
                .filter(function (cb) { return cb.checked; })
                .map(function (cb) { return cb.value; });
        }

        function loadLibraries() {
            return ApiClient.getJSON(ApiClient.getUrl('Library/VirtualFolders')).then(function (items) {
                _libraries = (items || []).map(function (i) {
                    return { Id: i.ItemId || i.Id || i.Guid, Name: i.Name || 'Unnamed Library' };
                }).filter(function (i) { return i.Id; });
            }).catch(function () { _libraries = []; });
        }

        function applyConfigToForm(cfg) {
            form.elements.chkEnableDynamic.checked        = !!cfg.EnableDynamicUserPlaylists;
            form.elements.chkEnableFranchises.checked     = !!cfg.EnableMovieSeriesPlaylists;
            form.elements.txtTmdbApiKey.value             = cfg.TmdbApiKey || '';
            form.elements.chkEnableUniverses.checked      = !!cfg.EnableTvUniversePlaylists;
            form.elements.chkUpdatePlaylistsImage.checked = !!cfg.UpdatePlaylistsLibraryImage;
            form.elements.chkEnableStreaming.checked      = !!cfg.EnableStreamingServiceCollections;
            form.elements.chkIncludeMovies.checked        = !!cfg.IncludeMovies;
            form.elements.chkIncludeTvShows.checked       = !!cfg.IncludeTvShows;
            renderCheckboxList(divStreamingLibraries, _libraries, cfg.StreamingLibraryIds || [], 'streamingLibrary');
            form.elements.chkUpdateCollectionsImage.checked = !!cfg.UpdateCollectionsLibraryImage;
            form.elements.chkDebugLogging.checked         = !!cfg.EnableDebugLogging;
        }

        function readConfigFromForm(cfg) {
            cfg.EnableDynamicUserPlaylists       = form.elements.chkEnableDynamic.checked;
            cfg.EnableMovieSeriesPlaylists       = form.elements.chkEnableFranchises.checked;
            cfg.TmdbApiKey                       = form.elements.txtTmdbApiKey.value;
            cfg.EnableTvUniversePlaylists        = form.elements.chkEnableUniverses.checked;
            cfg.UpdatePlaylistsLibraryImage      = form.elements.chkUpdatePlaylistsImage.checked;
            cfg.EnableStreamingServiceCollections = form.elements.chkEnableStreaming.checked;
            cfg.IncludeMovies                    = form.elements.chkIncludeMovies.checked;
            cfg.IncludeTvShows                   = form.elements.chkIncludeTvShows.checked;
            cfg.StreamingLibraryIds              = readCheckboxList(divStreamingLibraries);
            cfg.UpdateCollectionsLibraryImage    = form.elements.chkUpdateCollectionsImage.checked;
            cfg.EnableDebugLogging               = form.elements.chkDebugLogging.checked;
            return cfg;
        }

        function onSubmit(ev) {
            ev.preventDefault();
            loading.show();

            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (cfg) {
                readConfigFromForm(cfg);
                ApiClient.updatePluginConfiguration(pluginUniqueId, cfg).then(function (result) {
                    Dashboard.processPluginConfigurationUpdateResult(result);
                    loading.hide();
                    Dashboard.alert('Settings saved. The Collection Manager task has been queued.');
                }, function () {
                    loading.hide();
                    Dashboard.alert({ title: 'Error', message: 'Failed to save settings.' });
                });
            }, function () {
                loading.hide();
                Dashboard.alert({ title: 'Error', message: 'Failed to load current settings.' });
            });

            return false;
        }

        form.addEventListener('submit', onSubmit);

        view.addEventListener('viewshow', function () {
            loading.show();
            Promise.all([
                ApiClient.getPluginConfiguration(pluginUniqueId),
                loadLibraries()
            ]).then(function (results) {
                applyConfigToForm(results[0]);
                loading.hide();
            }, function () {
                loading.hide();
                Dashboard.alert({ title: 'Error', message: 'Failed to load settings.' });
            });
        });
    };
});
