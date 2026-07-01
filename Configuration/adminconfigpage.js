define([
    'loading',
    'emby-input',
    'emby-button',
    'emby-checkbox',
    'emby-select'
], function (loading) {
    'use strict';

    var pluginUniqueId = '80FDA42F-C32A-4BAE-8757-4DD49EF331A0';
    var dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

    return function (view) {

        var form = view.querySelector('#cmAdminForm');
        var divScheduledEditor = view.querySelector('#divScheduledCollectionsEditor');
        var selScheduledPreset = view.querySelector('#selScheduledPreset');
        var btnAddScheduledCollection = view.querySelector('#btnAddScheduledCollection');
        var btnApplyScheduledJson = view.querySelector('#btnApplyScheduledJson');

        function escAttr(s) {
            return (s == null ? '' : String(s))
                .replace(/&/g, '&amp;').replace(/"/g, '&quot;')
                .replace(/</g, '&lt;').replace(/>/g, '&gt;');
        }

        function splitCsv(value) {
            return (value || '').split(',').map(function (v) { return v.trim(); }).filter(function (v) { return v.length > 0; });
        }

        function joinCsv(values) {
            return (values || []).join(', ');
        }

        function presetDefinition(kind) {
            switch (kind) {
                case 'halloween':
                    return { Enabled: true, Name: 'Halloween Movies', ContentType: 'Movies', IncludedGenres: ['Horror'], ActiveStart: '10-01', ActiveEnd: '10-31', RemoveWhenInactive: true };
                case 'holiday':
                    return { Enabled: true, Name: 'Holiday Movies', ContentType: 'Movies', IncludedGenres: ['Christmas', 'Holiday'], ActiveStart: '12-01', ActiveEnd: '01-05', RemoveWhenInactive: true };
                case 'friday-action':
                    return { Enabled: true, Name: 'Friday Action Night', ContentType: 'Movies', IncludedGenres: ['Action'], ActiveDaysOfWeek: ['Friday'], MaxItems: 50, RemoveWhenInactive: true };
                default:
                    return { Enabled: true, Name: 'New Scheduled Collection', ContentType: 'Both', RemoveWhenInactive: true };
            }
        }

        function scheduleKind(def) {
            if (def.ActiveDaysOfWeek && def.ActiveDaysOfWeek.length) return 'days';
            if (def.ActiveStart || def.ActiveEnd) return 'dates';
            return 'always';
        }

        function scheduleControls(def) {
            var kind = scheduleKind(def);
            var days = def.ActiveDaysOfWeek || [];
            var dayHtml = dayNames.map(function (day) {
                var checked = days.indexOf(day) >= 0 ? ' checked="checked"' : '';
                return '<label style="display:inline-block;margin-right:.75em;margin-top:.35em;">'
                    + '<input is="emby-checkbox" type="checkbox" class="cmSchedDay" value="' + day + '"' + checked + ' />'
                    + '<span>' + day.slice(0, 3) + '</span></label>';
            }).join('');

            return '<div style="margin-top:.75em;">'
                + '<label>Schedule<br /><select class="cmSchedKind">'
                + '<option value="always"' + (kind === 'always' ? ' selected' : '') + '>Always active</option>'
                + '<option value="dates"' + (kind === 'dates' ? ' selected' : '') + '>Season/date range</option>'
                + '<option value="days"' + (kind === 'days' ? ' selected' : '') + '>Days of week</option>'
                + '</select></label>'
                + '<div class="cmSchedDates" style="margin-top:.5em;' + (kind === 'dates' ? '' : 'display:none;') + '">'
                + '<label>Start MM-DD<br /><input class="cmSchedStart" type="text" value="' + escAttr(def.ActiveStart || '') + '" placeholder="10-01" /></label> '
                + '<label>End MM-DD<br /><input class="cmSchedEnd" type="text" value="' + escAttr(def.ActiveEnd || '') + '" placeholder="10-31" /></label>'
                + '</div>'
                + '<div class="cmSchedDays" style="margin-top:.5em;' + (kind === 'days' ? '' : 'display:none;') + '">' + dayHtml + '</div>'
                + '</div>';
        }

        function collectionCardHtml(def, index) {
            def = def || presetDefinition('custom');
            return '<div class="cmScheduledCard" data-index="' + index + '" style="border:1px solid #444;border-radius:8px;padding:1em;margin:0 0 1em 0;background:rgba(255,255,255,.04);">'
                + '<div style="display:flex;justify-content:space-between;gap:1em;align-items:center;flex-wrap:wrap;">'
                + '<label><input is="emby-checkbox" type="checkbox" class="cmSchedEnabled"' + (def.Enabled !== false ? ' checked="checked"' : '') + ' /><span>Enabled</span></label>'
                + '<button is="emby-button" type="button" class="cmRemoveScheduled"><span>Remove</span></button>'
                + '</div>'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:.75em;margin-top:.75em;">'
                + '<label>Collection name<br /><input class="cmSchedName" type="text" value="' + escAttr(def.Name || '') + '" placeholder="Halloween Movies" /></label>'
                + '<label>Type<br /><select class="cmSchedContentType">'
                + '<option value="Both"' + ((def.ContentType || 'Both') === 'Both' ? ' selected' : '') + '>Movies + TV</option>'
                + '<option value="Movies"' + (def.ContentType === 'Movies' ? ' selected' : '') + '>Movies only</option>'
                + '<option value="TvShows"' + (def.ContentType === 'TvShows' ? ' selected' : '') + '>TV shows only</option>'
                + '</select></label>'
                + '<label>Max items<br /><input class="cmSchedMaxItems" type="number" min="0" value="' + escAttr(def.MaxItems || 0) + '" /></label>'
                + '</div>'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:.75em;margin-top:.75em;">'
                + '<label>Genres<br /><input class="cmSchedGenres" type="text" value="' + escAttr(joinCsv(def.IncludedGenres)) + '" placeholder="Horror, Action" /></label>'
                + '<label>Studios<br /><input class="cmSchedStudios" type="text" value="' + escAttr(joinCsv(def.IncludedStudios)) + '" placeholder="Disney, Netflix" /></label>'
                + '<label>Tags<br /><input class="cmSchedTags" type="text" value="' + escAttr(joinCsv(def.IncludedTags)) + '" placeholder="Kids, 4K" /></label>'
                + '<label>Years<br /><input class="cmSchedYears" type="text" value="' + escAttr(joinCsv(def.IncludedYears)) + '" placeholder="2023, 2024" /></label>'
                + '</div>'
                + scheduleControls(def)
                + '<label style="display:block;margin-top:.75em;"><input is="emby-checkbox" type="checkbox" class="cmSchedRemoveInactive"' + (def.RemoveWhenInactive !== false ? ' checked="checked"' : '') + ' /><span>Remove this collection when inactive</span></label>'
                + '</div>';
        }

        function renderScheduledCollections(definitions) {
            var defs = definitions || [];
            divScheduledEditor.innerHTML = defs.length
                ? defs.map(collectionCardHtml).join('')
                : '<div class="fieldDescription" style="margin-bottom:1em;">No scheduled collections yet. Pick a preset and click <b>Add collection</b>.</div>';
            form.elements.txtScheduledCollections.value = JSON.stringify(defs, null, 2);
        }

        function readScheduledCollectionsFromEditor() {
            return Array.prototype.slice.call(divScheduledEditor.querySelectorAll('.cmScheduledCard')).map(function (card) {
                var kind = card.querySelector('.cmSchedKind').value;
                var def = {
                    Enabled: card.querySelector('.cmSchedEnabled').checked,
                    Name: card.querySelector('.cmSchedName').value.trim(),
                    ContentType: card.querySelector('.cmSchedContentType').value,
                    IncludedGenres: splitCsv(card.querySelector('.cmSchedGenres').value),
                    IncludedStudios: splitCsv(card.querySelector('.cmSchedStudios').value),
                    IncludedYears: splitCsv(card.querySelector('.cmSchedYears').value),
                    IncludedTags: splitCsv(card.querySelector('.cmSchedTags').value),
                    MaxItems: parseInt(card.querySelector('.cmSchedMaxItems').value || '0', 10) || 0,
                    RemoveWhenInactive: card.querySelector('.cmSchedRemoveInactive').checked
                };
                if (kind === 'dates') {
                    def.ActiveStart = card.querySelector('.cmSchedStart').value.trim();
                    def.ActiveEnd = card.querySelector('.cmSchedEnd').value.trim();
                    def.ActiveDaysOfWeek = [];
                } else if (kind === 'days') {
                    def.ActiveStart = '';
                    def.ActiveEnd = '';
                    def.ActiveDaysOfWeek = Array.prototype.slice.call(card.querySelectorAll('.cmSchedDay'))
                        .filter(function (cb) { return cb.checked; })
                        .map(function (cb) { return cb.value; });
                } else {
                    def.ActiveStart = '';
                    def.ActiveEnd = '';
                    def.ActiveDaysOfWeek = [];
                }
                return def;
            }).filter(function (def) { return def.Name.length > 0; });
        }

        function syncScheduledJson() {
            form.elements.txtScheduledCollections.value = JSON.stringify(readScheduledCollectionsFromEditor(), null, 2);
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
            form.elements.chkUpdateCollectionsImage.checked = !!cfg.UpdateCollectionsLibraryImage;
            form.elements.chkEnableScheduledCollections.checked = !!cfg.EnableScheduledCollections;
            renderScheduledCollections(cfg.ScheduledCollections || []);
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
            cfg.EnableScheduledCollections       = form.elements.chkEnableScheduledCollections.checked;
            cfg.ScheduledCollections             = readScheduledCollectionsFromEditor();
            cfg.EnableDebugLogging               = form.elements.chkDebugLogging.checked;
            return cfg;
        }

        function onSubmit(ev) {
            ev.preventDefault();
            loading.show();
            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (cfg) {
                try { readConfigFromForm(cfg); syncScheduledJson(); }
                catch (err) {
                    loading.hide();
                    Dashboard.alert({ title: 'Invalid scheduled collections', message: err.message || String(err) });
                    return;
                }
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

        divScheduledEditor.addEventListener('click', function (ev) {
            var removeButton = ev.target.closest ? ev.target.closest('.cmRemoveScheduled') : null;
            if (!removeButton) return;
            var card = removeButton.closest('.cmScheduledCard');
            if (card) card.parentNode.removeChild(card);
            syncScheduledJson();
        });
        divScheduledEditor.addEventListener('change', function (ev) {
            if (ev.target.classList && ev.target.classList.contains('cmSchedKind')) {
                var card = ev.target.closest('.cmScheduledCard');
                var kind = ev.target.value;
                card.querySelector('.cmSchedDates').style.display = kind === 'dates' ? '' : 'none';
                card.querySelector('.cmSchedDays').style.display = kind === 'days' ? '' : 'none';
            }
            syncScheduledJson();
        });
        divScheduledEditor.addEventListener('input', syncScheduledJson);
        btnAddScheduledCollection.addEventListener('click', function () {
            var defs = readScheduledCollectionsFromEditor();
            defs.push(presetDefinition(selScheduledPreset.value));
            renderScheduledCollections(defs);
            form.elements.chkEnableScheduledCollections.checked = true;
        });
        btnApplyScheduledJson.addEventListener('click', function () {
            try {
                var defs = JSON.parse(form.elements.txtScheduledCollections.value || '[]');
                if (!Array.isArray(defs)) throw new Error('Scheduled collections must be a JSON array.');
                renderScheduledCollections(defs);
            } catch (err) {
                Dashboard.alert({ title: 'Invalid JSON', message: err.message || String(err) });
            }
        });

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
