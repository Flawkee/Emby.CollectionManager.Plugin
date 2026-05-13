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

        function applyConfigToForm(cfg) {
            form.elements.chkEnableDynamic.checked        = !!cfg.EnableDynamicUserPlaylists;
            form.elements.chkEnableFranchises.checked     = !!cfg.EnableMovieSeriesPlaylists;
            form.elements.txtTmdbApiKey.value             = cfg.TmdbApiKey || '';
            form.elements.chkEnableUniverses.checked      = !!cfg.EnableTvUniversePlaylists;
            form.elements.chkUpdatePlaylistsImage.checked = !!cfg.UpdatePlaylistsLibraryImage;
            form.elements.chkEnableStreaming.checked      = !!cfg.EnableStreamingServiceCollections;
            form.elements.chkIncludeMovies.checked        = !!cfg.IncludeMovies;
            form.elements.chkIncludeTvShows.checked       = !!cfg.IncludeTvShows;
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
            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (cfg) {
                applyConfigToForm(cfg);
                loading.hide();
            }, function () {
                loading.hide();
                Dashboard.alert({ title: 'Error', message: 'Failed to load settings.' });
            });
        });
    };
});
