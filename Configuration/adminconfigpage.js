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
    var monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

    return function (view) {

        var form = view.querySelector('#cmAdminForm');
        var divStreamingLibraries = view.querySelector('#divStreamingLibraries');
        var divScheduledEditor = view.querySelector('#divScheduledCollectionsEditor');
        var divScheduledOverview = view.querySelector('#divScheduledCollectionsOverview');
        var divScheduledSetupChecklist = view.querySelector('#divScheduledSetupChecklist');
        var selScheduledPreset = view.querySelector('#selScheduledPreset');
        var btnAddScheduledCollection = view.querySelector('#btnAddScheduledCollection');
        var btnRunAllScheduledCollections = view.querySelector('#btnRunAllScheduledCollections');
        var btnTestMdblistApiKey = view.querySelector('#btnTestMdblistApiKey');
        var divMdblistApiKeyStatus = view.querySelector('#divMdblistApiKeyStatus');
        var divFeaturedExamples = view.querySelector('#divFeaturedScheduledExamples');
        var divFeaturedStatus = view.querySelector('#divFeaturedScheduledStatus');
        var txtQuickScheduledName = view.querySelector('#txtQuickScheduledName');
        var txtQuickScheduledSource = view.querySelector('#txtQuickScheduledSource');
        var btnQuickAddScheduledCollection = view.querySelector('#btnQuickAddScheduledCollection');
        var divQuickScheduledHint = view.querySelector('#divQuickScheduledHint');
        var _libraries = [];
        var _metadata = { Libraries: [], Genres: [], Studios: [], Tags: [], Years: [], Ratings: [], ImdbProviderIdCount: 0, HasImdbProviderIds: false };

        function escAttr(s) {
            return (s == null ? '' : String(s))
                .replace(/&/g, '&amp;').replace(/"/g, '&quot;')
                .replace(/</g, '&lt;').replace(/>/g, '&gt;');
        }

        function escText(s) {
            return (s == null ? '' : String(s))
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        }

        function two(n) { n = parseInt(n, 10) || 0; return n < 10 ? '0' + n : String(n); }
        function datePart(value, index, fallback) {
            var parts = String(value || '').split('-');
            return parseInt(parts[index], 10) || fallback;
        }
        function makeDate(month, day) { return two(month) + '-' + two(day); }

        function extractImdbIdsFromText(text) {
            var matches = String(text || '').match(/tt\d{7,10}/gi) || [];
            var seen = {};
            return matches.map(function (id) { return id.toLowerCase(); }).filter(function (id) {
                if (seen[id]) return false;
                seen[id] = true;
                return true;
            });
        }

        function simpleCollectionDefinition(name, source) {
            var ids = extractImdbIdsFromText(source);
            return {
                Enabled: true,
                Name: (name || '').trim() || 'IMDb List Collection',
                ContentType: 'Both',
                IncludedImdbIds: ids,
                MdblistListPath: ids.length ? '' : (source || '').trim(),
                RemoveWhenInactive: false,
                MatchMode: 'All'
            };
        }

        function simpleCollectionHint(source) {
            var ids = extractImdbIdsFromText(source);
            if (!String(source || '').trim()) return '';
            if (ids.length) return 'Detected ' + ids.length + ' IMDb title ID' + (ids.length === 1 ? '' : 's') + '. No MDBList API key needed for direct title IDs.';
            return 'Detected a list source. If this is an IMDb watchlist/list, import it into MDBList first, add your MDBList API key above, then Preview.';
        }

        function mdblistPathKey(source) {
            var value = String(source || '').trim().replace(/^https?:\/\/[^/]+\/lists\//i, '').replace(/^lists\//i, '').replace(/^official:/i, 'official/').replace(/\/+$/g, '');
            return value.toLowerCase();
        }

        function friendlyMdblistSource(source) {
            var key = mdblistPathKey(source);
            if (key === 'official/movies/moviemeter') return 'IMDb MovieMeter Top Movies via MDBList';
            if (key === 'official/movies/popular') return 'Popular Movies via MDBList';
            if (key === 'official/movies/streaming-charts') return 'Streaming Chart Movies via MDBList';
            if (key === 'official/shows/moviemeter') return 'IMDb MovieMeter Top TV Shows via MDBList';
            return source ? 'Custom MDBList source' : '';
        }

        function friendlySourceLabel(def) {
            def = def || {};
            if (def.IncludedImdbIds && def.IncludedImdbIds.length) return def.IncludedImdbIds.length + ' direct IMDb title ID' + (def.IncludedImdbIds.length === 1 ? '' : 's');
            if (def.MdblistListPath) return friendlyMdblistSource(def.MdblistListPath);
            if (def.SortBy === 'CommunityRatingDescending') return 'Local Emby ratings';
            return 'Library filters';
        }

        function needsMdblistApiKey(def) {
            return !!(def && def.MdblistListPath && def.MdblistListPath.trim());
        }

        function hasLocalOrExternalSource(def) {
            def = def || {};
            return needsMdblistApiKey(def)
                || (def.IncludedImdbIds && def.IncludedImdbIds.length)
                || (def.IncludedGenres && def.IncludedGenres.length)
                || (def.IncludedStudios && def.IncludedStudios.length)
                || (def.IncludedTags && def.IncludedTags.length)
                || (def.IncludedYears && def.IncludedYears.length)
                || (def.IncludedOfficialRatings && def.IncludedOfficialRatings.length)
                || (def.PlayState && def.PlayState !== 'Any')
                || (def.IsFavorite && def.IsFavorite !== 'Any')
                || (def.SeriesStatus && def.SeriesStatus !== 'Any')
                || !!def.SortBy
                || def.MaxRuntimeMinutes > 0
                || def.ContentType === 'Movies'
                || def.ContentType === 'TvShows';
        }

        function renderSetupChecklist(defs) {
            if (!divScheduledSetupChecklist) return;
            defs = defs || [];
            var enabledDefs = defs.filter(function (d) { return d && d.Enabled !== false && d.Name; });
            var needsKey = enabledDefs.some(needsMdblistApiKey);
            var hasKey = !!((form.elements.txtMdblistApiKey && form.elements.txtMdblistApiKey.value) || '').trim();
            var hasSource = enabledDefs.some(hasLocalOrExternalSource);
            var hasImdbMetadata = !!(_metadata.HasImdbProviderIds || _metadata.ImdbProviderIdCount > 0);
            var items = [
                { ok: form.elements.chkEnableScheduledCollections.checked, label: 'Custom collections', detail: form.elements.chkEnableScheduledCollections.checked ? 'enabled' : 'turn this on before running presets' },
                { ok: enabledDefs.length > 0, label: 'Collection', detail: enabledDefs.length ? enabledDefs.length + ' enabled collection' + (enabledDefs.length === 1 ? '' : 's') : 'add a featured example or preset' },
                { ok: !needsKey || hasKey, label: 'MDBList API key', detail: needsKey ? (hasKey ? 'configured' : 'needed for MDBList/IMDb presets') : 'not needed for direct IMDb IDs/local filters' },
                { ok: !needsKey || hasImdbMetadata, label: 'Emby IMDb metadata', detail: hasImdbMetadata ? (_metadata.ImdbProviderIdCount + ' item(s) with IMDb IDs detected') : (needsKey ? 'refresh metadata if previews return zero matches' : 'not required for local filters') },
                { ok: enabledDefs.length > 0 && hasSource, label: 'Ready', detail: hasSource ? 'click Preview First to check matches' : 'add IMDb IDs, an MDBList source, or local filters' }
            ];
            divScheduledSetupChecklist.innerHTML = '<div style="border:1px solid #444;border-radius:10px;padding:.85em;background:rgba(255,255,255,.035);">'
                + '<b>Setup checklist</b>'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:.5em;margin-top:.65em;">'
                + items.map(function (item) {
                    return '<div><span style="color:' + (item.ok ? '#7bd88f' : '#ffcc66') + ';font-weight:700;">' + (item.ok ? '✓' : '⚠') + '</span> '
                        + '<b>' + escText(item.label) + '</b><br /><span class="fieldDescription">' + escText(item.detail) + '</span></div>';
                }).join('') + '</div></div>';
        }

        function renderSourceHelper(card) {
            var def = readCard(card);
            var helper = card.querySelector('.cmSourceHelper');
            if (helper) helper.innerHTML = escText(friendlySourceLabel(def));
        }

        function emptyPreviewHelp(def) {
            var tips = ['No items matched.'];
            if (needsMdblistApiKey(def)) {
                tips.push('Test/save the MDBList API key above, then preview again.');
                tips.push('Make sure Emby metadata has IMDb provider IDs; refresh metadata if needed.');
            } else if (def.IncludedImdbIds && def.IncludedImdbIds.length) {
                tips.push('These direct IMDb IDs only match Emby items that already have IMDb provider IDs.');
            } else {
                tips.push('Try fewer filters, choose a different library, or use Studio/Tags instead of Genre.');
            }
            return '<div style="margin-top:.5em;color:#ffcc66;"><b>No matches yet</b><ul style="margin:.35em 0 0 1.2em;">'
                + tips.map(function (t) { return '<li>' + escText(t) + '</li>'; }).join('') + '</ul></div>';
        }

        function renderPreviewResponse(card, data) {
            var items = data.Items || [];
            var rows = items.map(function (i) {
                return '<tr><td>' + escText(i.Name || '') + '</td><td>' + escText(i.Year || '') + '</td><td>' + escText(i.Type || '') + '</td></tr>';
            }).join('');
            var table = rows ? '<div style="overflow:auto;margin-top:.45em;"><table class="detailTable" style="width:100%;"><thead><tr><th>Title</th><th>Year</th><th>Type</th></tr></thead><tbody>' + rows + '</tbody></table></div>' : '';
            var warnings = (data.Warnings || []).map(function (w) {
                return '<li>' + escText(w) + '</li>';
            }).join('');
            var warningHtml = warnings ? '<div style="margin-top:.5em;color:#ffcc66;"><b>Warnings</b><ul style="margin:.35em 0 0 1.2em;">' + warnings + '</ul></div>' : '';
            var def = readCard(card);
            var emptyHtml = (data.Count || 0) === 0 ? emptyPreviewHelp(def) : '';
            setPreview(card, '<b>' + (data.Count || 0) + ' item(s)</b> matched' + table + emptyHtml + warningHtml);
            updateOverviewPreview(parseInt(card.getAttribute('data-index') || '0', 10), (data.Count || 0) + ' item(s)', !!warnings || (data.Count || 0) === 0);
        }

        function addPreset(kind, previewAfterAdd) {
            var defs = readScheduledCollectionsFromEditor();
            defs.push(presetDefinition(kind));
            renderScheduledCollections(defs);
            form.elements.chkEnableScheduledCollections.checked = true;
            renderSetupChecklist(readScheduledCollectionsFromEditor());
            if (previewAfterAdd) {
                var card = divScheduledEditor.querySelector('.cmScheduledCard[data-index="' + (defs.length - 1) + '"]');
                if (card) previewCard(card);
            }
        }

        function renderCheckboxList(container, items, selectedValues, name) {
            var selected = (selectedValues || []).reduce(function (acc, v) { acc[v] = true; return acc; }, {});
            container.innerHTML = items.map(function (item) {
                var checked = selected[item.Id] ? ' checked="checked"' : '';
                return '<label class="emby-checkbox-label">'
                    + '<input is="emby-checkbox" type="checkbox" name="' + escAttr(name) + '" value="' + escAttr(item.Id) + '"' + checked + ' />'
                    + '<span>' + escText(item.Name) + '</span></label>';
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

        function loadBuilderMetadata() {
            return ApiClient.getJSON(ApiClient.getUrl('CollectionManager/ScheduledCollections/Metadata')).then(function (data) {
                _metadata = data || _metadata;
                if (_metadata.Libraries && _metadata.Libraries.length) {
                    _libraries = _metadata.Libraries.map(function (i) { return { Id: i.Id, Name: i.Name }; });
                }
            }).catch(function () { /* keep library fallback */ });
        }

        function apiPost(path, data) {
            return ApiClient.ajax({
                type: 'POST',
                url: ApiClient.getUrl(path),
                data: JSON.stringify(data || {}),
                contentType: 'application/json'
            });
        }

        function presetDefinition(kind) {
            switch (kind) {
                case 'top-100-movies':
                    return { Enabled: true, Name: 'Top Rated Movies', ContentType: 'Movies', SortBy: 'CommunityRatingDescending', MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'top-rated-tv':
                    return { Enabled: true, Name: 'Top Rated TV', ContentType: 'TvShows', SortBy: 'CommunityRatingDescending', MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'popular-movies':
                    return { Enabled: true, Name: 'Popular Movies', ContentType: 'Movies', MdblistListPath: 'official:movies/popular', MaxItems: 50, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'streaming-chart-movies':
                    return { Enabled: true, Name: 'Streaming Chart Movies', ContentType: 'Movies', MdblistListPath: 'official:movies/streaming-charts', MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'top-100-tv':
                    return { Enabled: true, Name: 'Top 100 TV Shows', ContentType: 'TvShows', MdblistListPath: 'official:shows/moviemeter', MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'halloween':
                    return { Enabled: true, Name: 'Halloween Movies', ContentType: 'Movies', IncludedGenres: ['Horror'], ActiveStart: '10-01', ActiveEnd: '10-31', RemoveWhenInactive: true, MatchMode: 'All' };
                case 'kids-halloween':
                    return { Enabled: true, Name: 'Kids Halloween', ContentType: 'Movies', IncludedGenres: ['Horror'], IncludedOfficialRatings: ['G', 'PG', 'TV-Y7'], ActiveStart: '10-01', ActiveEnd: '10-31', RemoveWhenInactive: true, MatchMode: 'All' };
                case 'holiday':
                    return { Enabled: true, Name: 'Holiday Movies', ContentType: 'Movies', IncludedGenres: ['Christmas', 'Holiday'], ActiveStart: '12-01', ActiveEnd: '01-05', RemoveWhenInactive: true, MatchMode: 'Any' };
                case 'holiday-family':
                    return { Enabled: true, Name: 'Holiday Family Movies', ContentType: 'Movies', IncludedGenres: ['Christmas', 'Holiday'], IncludedOfficialRatings: ['G', 'PG'], ActiveStart: '12-01', ActiveEnd: '01-05', RemoveWhenInactive: true, MatchMode: 'Any' };
                case 'friday-action':
                    return { Enabled: true, Name: 'Friday Action Night', ContentType: 'Movies', IncludedGenres: ['Action'], ActiveDaysOfWeek: ['Friday'], MaxItems: 50, RemoveWhenInactive: true, MatchMode: 'All' };
                case 'weekend-movie-night':
                    return { Enabled: true, Name: 'Weekend Movie Night', ContentType: 'Movies', IncludedGenres: ['Action', 'Adventure', 'Comedy'], ActiveDaysOfWeek: ['Friday', 'Saturday'], MaxItems: 50, RemoveWhenInactive: true, MatchMode: 'Any' };
                case 'short-movies':
                    return { Enabled: true, Name: 'Short Movies Under 90 Minutes', ContentType: 'Movies', MaxRuntimeMinutes: 90, MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'action-movies':
                    return { Enabled: true, Name: 'Action Movies', ContentType: 'Movies', IncludedGenres: ['Action'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'comedy-movies':
                    return { Enabled: true, Name: 'Comedy Movies', ContentType: 'Movies', IncludedGenres: ['Comedy'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'horror-movies':
                    return { Enabled: true, Name: 'Horror Movies', ContentType: 'Movies', IncludedGenres: ['Horror'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'scifi-movies':
                    return { Enabled: true, Name: 'Sci-Fi Movies', ContentType: 'Movies', IncludedGenres: ['Science Fiction', 'Sci-Fi'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'Any' };
                case 'animated-movies':
                    return { Enabled: true, Name: 'Animated Movies', ContentType: 'Movies', IncludedGenres: ['Animation'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'documentaries':
                    return { Enabled: true, Name: 'Documentaries', ContentType: 'Movies', IncludedGenres: ['Documentary'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'drama-movies':
                    return { Enabled: true, Name: 'Drama Movies', ContentType: 'Movies', IncludedGenres: ['Drama'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'kids':
                    return { Enabled: true, Name: 'Kids Collection', ContentType: 'Both', IncludedOfficialRatings: ['G', 'PG', 'TV-Y', 'TV-Y7'], RemoveWhenInactive: false, MatchMode: 'Any' };
                case 'new-releases':
                    return { Enabled: true, Name: 'New Releases', ContentType: 'Movies', IncludedYears: [String(new Date().getFullYear()), String(new Date().getFullYear() - 1)], MaxItems: 75, RemoveWhenInactive: false, MatchMode: 'Any' };
                case 'recent-movies':
                    return { Enabled: true, Name: 'Recently Added Movies', ContentType: 'Movies', SortBy: 'DateCreatedDescending', MaxItems: 75, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'recent-tv':
                    return { Enabled: true, Name: 'Recently Added TV', ContentType: 'TvShows', SortBy: 'DateCreatedDescending', MaxItems: 75, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'unwatched':
                    return { Enabled: true, Name: 'Unwatched Movies', ContentType: 'Movies', PlayState: 'Unplayed', MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'unwatched-family':
                    return { Enabled: true, Name: 'Unwatched Family Movies', ContentType: 'Movies', PlayState: 'Unplayed', IncludedOfficialRatings: ['G', 'PG'], MaxItems: 100, RemoveWhenInactive: false, MatchMode: 'All' };
                case 'favorites':
                    return { Enabled: true, Name: 'Favorites', ContentType: 'Both', IsFavorite: 'Yes', RemoveWhenInactive: false, MatchMode: 'All' };
                case 'awards':
                    return { Enabled: true, Name: 'Award Winners', ContentType: 'Movies', IncludedTags: ['Oscar', 'Award Winner'], RemoveWhenInactive: false, MatchMode: 'Any' };
                case 'mdblist-imdb':
                    return { Enabled: true, Name: 'IMDb Watchlist', ContentType: 'Both', MdblistListPath: '', IncludedImdbIds: [], RemoveWhenInactive: false, MatchMode: 'All' };
                case '4k':
                    return { Enabled: true, Name: '4K Movies', ContentType: 'Movies', IncludedTags: ['4K'], RemoveWhenInactive: false, MatchMode: 'All' };
                case '4k-hdr':
                    return { Enabled: true, Name: '4K HDR Movies', ContentType: 'Movies', IncludedTags: ['4K', 'HDR'], RemoveWhenInactive: false, MatchMode: 'All' };
                default:
                    return { Enabled: true, Name: 'New Custom Collection', ContentType: 'Both', RemoveWhenInactive: true, MatchMode: 'All' };
            }
        }

        function migrateDefinition(def) {
            def = def || {};
            var mdblist = mdblistPathKey(def.MdblistListPath || '');
            if ((def.Name === 'Top 100 Movies' || def.Name === 'IMDb MovieMeter Top Movies') && mdblist === 'official/movies/moviemeter') {
                def.Name = 'Top Rated Movies';
                def.MdblistListPath = '';
                def.SortBy = 'CommunityRatingDescending';
                def.ContentType = 'Movies';
                def.MaxItems = def.MaxItems || 100;
            }
            return def;
        }

        function scheduleKind(def) {
            if (def.ActiveDaysOfWeek && def.ActiveDaysOfWeek.length) return 'days';
            if (def.ActiveStart || def.ActiveEnd) return 'dates';
            return 'always';
        }

        function optionList(values, query) {
            var q = (query || '').toLowerCase();
            return (values || [])
                .filter(function (v) { return !q || String(v).toLowerCase().indexOf(q) >= 0; })
                .slice(0, 25)
                .map(function (v) { return '<option value="' + escAttr(v) + '"></option>'; }).join('');
        }

        function tokenField(field, label, values, suggestions, placeholder, index) {
            var id = 'cmList' + field + '_' + index;
            var chips = (values || []).map(function (v) {
                return '<span class="cmTokenChip" data-field="' + field + '" data-value="' + escAttr(v) + '" style="display:inline-flex;align-items:center;gap:.35em;margin:.2em;padding:.25em .55em;border-radius:999px;background:rgba(255,255,255,.12);">'
                    + escText(v) + '<button type="button" class="cmButton cmRemoveToken" title="Remove" style="min-height:0;min-width:0;padding:.1em .45em !important;">×</button></span>';
            }).join('');
            return '<div class="cmTokenField" data-field="' + field + '">'
                + '<div class="fieldDescription" style="font-weight:600;margin-bottom:.25em;">' + escText(label) + '</div>'
                + '<div class="cmTokenChips">' + chips + '</div>'
                + '<div style="display:flex;gap:.35em;align-items:center;">'
                + '<input class="cmTokenInput" data-field="' + field + '" list="' + id + '" type="text" placeholder="' + escAttr(placeholder || 'Add value') + '" />'
                + '<datalist id="' + id + '">' + optionList(suggestions, '') + '</datalist>'
                + '<button type="button" class="cmButton cmAddToken" data-field="' + field + '">Add</button>'
                + '</div><div class="fieldDescription">Suggestions search as you type and are limited to 25.</div></div>';
        }

        function tokenSuggestions(field) {
            if (field === 'Genres') return _metadata.Genres || [];
            if (field === 'Studios') return _metadata.Studios || [];
            if (field === 'Tags') return _metadata.Tags || [];
            if (field === 'Years') return _metadata.Years || [];
            if (field === 'Ratings') return _metadata.Ratings || [];
            if (field === 'ImdbIds') return [];
            return [];
        }

        function updateTokenSuggestions(input) {
            var field = input.getAttribute('data-field');
            var list = view.querySelector('#' + input.getAttribute('list'));
            if (list) list.innerHTML = optionList(tokenSuggestions(field), input.value || '');
        }

        function monthOptions(selected) {
            return monthNames.map(function (m, i) {
                var n = i + 1;
                return '<option value="' + n + '"' + (n === selected ? ' selected' : '') + '>' + m + '</option>';
            }).join('');
        }

        function dayOptions(selected) {
            var html = '';
            for (var d = 1; d <= 31; d++) html += '<option value="' + d + '"' + (d === selected ? ' selected' : '') + '>' + d + '</option>';
            return html;
        }

        function scheduleControls(def) {
            var kind = scheduleKind(def);
            var days = def.ActiveDaysOfWeek || [];
            var startMonth = datePart(def.ActiveStart, 0, 10);
            var startDay = datePart(def.ActiveStart, 1, 1);
            var endMonth = datePart(def.ActiveEnd, 0, 10);
            var endDay = datePart(def.ActiveEnd, 1, 31);
            var dayHtml = dayNames.map(function (day) {
                var checked = days.indexOf(day) >= 0 ? ' checked="checked"' : '';
                return '<label style="display:inline-block;margin-right:.75em;margin-top:.35em;">'
                    + '<input is="emby-checkbox" type="checkbox" class="cmSchedDay" value="' + day + '"' + checked + ' />'
                    + '<span>' + day.slice(0, 3) + '</span></label>';
            }).join('');

            return '<div style="margin-top:1em;">'
                + '<h4 style="margin:.25em 0;">Schedule</h4>'
                + '<label>When should it appear?<br /><select class="cmSchedKind">'
                + '<option value="always"' + (kind === 'always' ? ' selected' : '') + '>Always</option>'
                + '<option value="dates"' + (kind === 'dates' ? ' selected' : '') + '>Season / date range</option>'
                + '<option value="days"' + (kind === 'days' ? ' selected' : '') + '>Days of week</option>'
                + '</select></label>'
                + '<div class="cmSchedDates" style="margin-top:.65em;' + (kind === 'dates' ? '' : 'display:none;') + '">'
                + '<label>Start<br /><select class="cmSchedStartMonth">' + monthOptions(startMonth) + '</select></label> '
                + '<label><span style="visibility:hidden;">Day</span><br /><select class="cmSchedStartDay">' + dayOptions(startDay) + '</select></label> '
                + '<label>End<br /><select class="cmSchedEndMonth">' + monthOptions(endMonth) + '</select></label> '
                + '<label><span style="visibility:hidden;">Day</span><br /><select class="cmSchedEndDay">' + dayOptions(endDay) + '</select></label>'
                + '</div>'
                + '<div class="cmSchedDays" style="margin-top:.65em;' + (kind === 'days' ? '' : 'display:none;') + '">' + dayHtml + '</div>'
                + '</div>';
        }

        function renderLibraryCheckboxes(def) {
            var selected = (def.SourceLibraryIds || []).reduce(function (acc, v) { acc[v] = true; return acc; }, {});
            return (_libraries || []).map(function (lib) {
                var checked = selected[lib.Id] ? ' checked="checked"' : '';
                return '<label class="emby-checkbox-label" style="display:inline-block;margin-right:.75em;">'
                    + '<input is="emby-checkbox" type="checkbox" class="cmSchedLibrary" value="' + escAttr(lib.Id) + '"' + checked + ' />'
                    + '<span>' + escText(lib.Name) + '</span></label>';
            }).join('') || '<div class="fieldDescription">No libraries found. The collection will scan all libraries.</div>';
        }

        function collectionCardHtml(def, index) {
            def = def || presetDefinition('custom');
            var noLimit = !def.MaxItems || def.MaxItems <= 0;
            return '<div class="cmScheduledCard" data-index="' + index + '" style="border:1px solid #444;border-radius:10px;padding:1em;margin:0 0 1em 0;background:rgba(255,255,255,.04);">'
                + '<div style="display:flex;justify-content:space-between;gap:1em;align-items:center;flex-wrap:wrap;">'
                + '<label><input is="emby-checkbox" type="checkbox" class="cmSchedEnabled"' + (def.Enabled !== false ? ' checked="checked"' : '') + ' /><span>Enabled</span></label>'
                + '<div style="display:flex;gap:.5em;flex-wrap:wrap;">'
                + '<button type="button" class="cmButton cmPreviewScheduled">Preview First</button>'
                + '<button type="button" class="cmButton cmButtonPrimary cmRunScheduled">Create Collection</button>'
                + '<button type="button" class="cmButton cmDuplicateScheduled">Duplicate</button>'
                + '<button type="button" class="cmButton cmRemoveScheduled">Remove</button>'
                + '</div></div>'
                + '<input type="hidden" class="cmSchedSortBy" value="' + escAttr(def.SortBy || '') + '" />'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:.75em;margin-top:.75em;">'
                + '<label>Collection name<br /><input class="cmSchedName" type="text" value="' + escAttr(def.Name || '') + '" placeholder="IMDb Watchlist" /></label>'
                + '<label>Include<br /><select class="cmSchedContentType">'
                + '<option value="Both"' + ((def.ContentType || 'Both') === 'Both' ? ' selected' : '') + '>Movies + TV Shows</option>'
                + '<option value="Movies"' + (def.ContentType === 'Movies' ? ' selected' : '') + '>Movies only</option>'
                + '<option value="TvShows"' + (def.ContentType === 'TvShows' ? ' selected' : '') + '>TV shows only</option>'
                + '</select></label>'
                + '<label><span>IMDb / MDBList source</span><br /><input class="cmSchedMdblistListPath" type="text" value="' + escAttr(def.MdblistListPath || '') + '" placeholder="MDBList link/ID, or use IMDb IDs below" /><div class="cmSourceHelper fieldDescription" style="margin-top:.3em;">' + escText(friendlySourceLabel(def)) + '</div></label>'
                + '</div>'
                + '<details class="cmCollectionSettings" style="margin-top:.9em;"><summary style="cursor:pointer;font-weight:600;">Collection settings</summary>'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:.75em;margin-top:.75em;">'
                + '<label>Match filters<br /><select class="cmSchedMatchMode">'
                + '<option value="All"' + ((def.MatchMode || 'All') === 'All' ? ' selected' : '') + '>All filters</option>'
                + '<option value="Any"' + (def.MatchMode === 'Any' ? ' selected' : '') + '>Any filter</option>'
                + '</select></label>'
                + '<label><span>Item limit</span><br /><input class="cmSchedMaxItems" type="number" min="1" value="' + escAttr(def.MaxItems || '') + '"' + (noLimit ? ' disabled="disabled"' : '') + ' placeholder="No limit" /></label>'
                + '<label style="align-self:end;"><input is="emby-checkbox" type="checkbox" class="cmSchedNoLimit"' + (noLimit ? ' checked="checked"' : '') + ' /><span>No limit</span></label>'
                + '<label><span>Max runtime minutes</span><br /><input class="cmSchedMaxRuntime" type="number" min="1" value="' + escAttr(def.MaxRuntimeMinutes || '') + '" placeholder="No runtime limit" /></label>'
                + '</div>'
                + '<div style="margin-top:.9em;"><h4 style="margin:.25em 0;">Libraries</h4>' + renderLibraryCheckboxes(def) + '</div>'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:.9em;margin-top:.9em;">'
                + tokenField('ImdbIds', 'IMDb title IDs / URLs', def.IncludedImdbIds, [], 'tt0111161 or imdb.com/title/tt0111161', index)
                + tokenField('Genres', 'Genres', def.IncludedGenres, _metadata.Genres, 'Horror', index)
                + tokenField('Studios', 'Studios / services', def.IncludedStudios, _metadata.Studios, 'Netflix', index)
                + tokenField('Tags', 'Tags', def.IncludedTags, _metadata.Tags, '4K', index)
                + tokenField('Years', 'Years', def.IncludedYears, _metadata.Years, String(new Date().getFullYear()), index)
                + tokenField('Ratings', 'Ratings', def.IncludedOfficialRatings, _metadata.Ratings, 'PG', index)
                + '</div>'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:.75em;margin-top:.9em;">'
                + '<label>Watched state<br /><select class="cmSchedPlayState"><option value="Any"' + ((def.PlayState || 'Any') === 'Any' ? ' selected' : '') + '>Any</option><option value="Played"' + (def.PlayState === 'Played' ? ' selected' : '') + '>Watched only</option><option value="Unplayed"' + (def.PlayState === 'Unplayed' ? ' selected' : '') + '>Unwatched only</option></select></label>'
                + '<label>Favorites<br /><select class="cmSchedFavorite"><option value="Any"' + ((def.IsFavorite || 'Any') === 'Any' ? ' selected' : '') + '>Any</option><option value="Yes"' + (def.IsFavorite === 'Yes' ? ' selected' : '') + '>Favorites only</option><option value="No"' + (def.IsFavorite === 'No' ? ' selected' : '') + '>Not favorites</option></select></label>'
                + '<label>Series status<br /><select class="cmSchedSeriesStatus"><option value="Any"' + ((def.SeriesStatus || 'Any') === 'Any' ? ' selected' : '') + '>Any</option><option value="Continuing"' + (def.SeriesStatus === 'Continuing' ? ' selected' : '') + '>Continuing</option><option value="Ended"' + (def.SeriesStatus === 'Ended' ? ' selected' : '') + '>Ended</option></select></label>'
                + '</div>'
                + scheduleControls(def)
                + '<label style="display:block;margin-top:.75em;"><input is="emby-checkbox" type="checkbox" class="cmSchedRemoveInactive"' + (def.RemoveWhenInactive !== false ? ' checked="checked"' : '') + ' /><span>Remove this collection when inactive</span></label>'
                + '</details>'
                + '<div class="cmPreviewResult fieldDescription" style="margin-top:.9em;"></div>'
                + '</div>';
        }

        function renderScheduledCollections(definitions) {
            var defs = definitions || [];
            renderSetupChecklist(defs);
            renderScheduledOverview(defs);
            divScheduledEditor.innerHTML = defs.length
                ? defs.map(collectionCardHtml).join('')
                : '<div class="fieldDescription" style="margin-bottom:1em;">No custom collections yet. Pick a preset and click <b>Add collection</b>.</div>';
        }

        function describeSchedule(def) {
            if (def.ActiveDaysOfWeek && def.ActiveDaysOfWeek.length) return def.ActiveDaysOfWeek.join(', ');
            if (def.ActiveStart || def.ActiveEnd) return (def.ActiveStart || '?') + ' – ' + (def.ActiveEnd || '?');
            return 'Always';
        }

        function describeFilters(def) {
            var parts = [];
            if (def.ContentType) parts.push(def.ContentType);
            if (def.SourceLibraryIds && def.SourceLibraryIds.length) parts.push(def.SourceLibraryIds.length + ' librar' + (def.SourceLibraryIds.length === 1 ? 'y' : 'ies'));
            if (def.IncludedGenres && def.IncludedGenres.length) parts.push('Genre: ' + def.IncludedGenres.slice(0, 2).join(', '));
            if (def.IncludedStudios && def.IncludedStudios.length) parts.push('Studio: ' + def.IncludedStudios.slice(0, 2).join(', '));
            if (def.IncludedTags && def.IncludedTags.length) parts.push('Tag: ' + def.IncludedTags.slice(0, 2).join(', '));
            if (def.IncludedYears && def.IncludedYears.length) parts.push('Year: ' + def.IncludedYears.slice(0, 2).join(', '));
            if (def.IncludedOfficialRatings && def.IncludedOfficialRatings.length) parts.push('Rating: ' + def.IncludedOfficialRatings.slice(0, 2).join(', '));
            if ((def.IncludedImdbIds && def.IncludedImdbIds.length) || def.MdblistListPath) parts.push(friendlySourceLabel(def));
            if (def.PlayState && def.PlayState !== 'Any') parts.push(def.PlayState);
            if (def.IsFavorite && def.IsFavorite !== 'Any') parts.push(def.IsFavorite === 'Yes' ? 'Favorites' : 'Not favorites');
            if (def.SortBy === 'DateCreatedDescending') parts.push('Recently added first');
            if (def.SortBy === 'CommunityRatingDescending') parts.push('Top rated first');
            if (def.MaxRuntimeMinutes && def.MaxRuntimeMinutes > 0) parts.push('≤ ' + def.MaxRuntimeMinutes + ' min');
            return parts.join(' • ') || 'No filters';
        }

        function renderScheduledOverview(defs) {
            if (!divScheduledOverview) return;
            if (!defs.length) { divScheduledOverview.innerHTML = ''; return; }
            var rows = defs.map(function (def, index) {
                return '<tr data-index="' + index + '">'
                    + '<td>' + (def.Enabled === false ? '—' : '✓') + '</td>'
                    + '<td><button type="button" class="cmButton cmOverviewEdit" data-index="' + index + '" style="min-height:0;min-width:0;padding:.2em .55em !important;">' + escText(def.Name || 'Unnamed') + '</button></td>'
                    + '<td>' + escText(describeSchedule(def)) + '</td>'
                    + '<td>' + escText(describeFilters(def)) + '</td>'
                    + '<td class="cmOverviewPreview" data-index="' + index + '">Preview not run</td>'
                    + '</tr>';
            }).join('');
            divScheduledOverview.innerHTML = '<h3 style="margin:.5em 0;">Custom collection overview</h3>'
                + '<div style="overflow:auto;"><table class="detailTable" style="width:100%;border-collapse:collapse;">'
                + '<thead><tr><th>Enabled</th><th>Name</th><th>Schedule</th><th>Filters</th><th>Last preview</th></tr></thead>'
                + '<tbody>' + rows + '</tbody></table></div>';
        }

        function updateOverviewPreview(index, text, hasWarning) {
            if (!divScheduledOverview) return;
            var cell = divScheduledOverview.querySelector('.cmOverviewPreview[data-index="' + index + '"]');
            if (cell) cell.innerHTML = (hasWarning ? '<span style="color:#ffcc66;">⚠ </span>' : '') + escText(text);
        }

        function tokenValues(card, field) {
            return Array.prototype.slice.call(card.querySelectorAll('.cmTokenChip[data-field="' + field + '"]'))
                .map(function (el) { return el.getAttribute('data-value'); })
                .filter(function (v) { return v; });
        }

        function selectedLibraries(card) {
            return Array.prototype.slice.call(card.querySelectorAll('.cmSchedLibrary'))
                .filter(function (cb) { return cb.checked; })
                .map(function (cb) { return cb.value; });
        }

        function readCard(card) {
            var kind = card.querySelector('.cmSchedKind').value;
            var noLimit = card.querySelector('.cmSchedNoLimit').checked;
            var def = {
                Enabled: card.querySelector('.cmSchedEnabled').checked,
                Name: card.querySelector('.cmSchedName').value.trim(),
                ContentType: card.querySelector('.cmSchedContentType').value,
                SourceLibraryIds: selectedLibraries(card),
                IncludedGenres: tokenValues(card, 'Genres'),
                IncludedStudios: tokenValues(card, 'Studios'),
                IncludedYears: tokenValues(card, 'Years'),
                IncludedOfficialRatings: tokenValues(card, 'Ratings'),
                IncludedTags: tokenValues(card, 'Tags'),
                IncludedImdbIds: tokenValues(card, 'ImdbIds'),
                MdblistListPath: card.querySelector('.cmSchedMdblistListPath').value.trim(),
                PlayState: card.querySelector('.cmSchedPlayState').value,
                IsFavorite: card.querySelector('.cmSchedFavorite').value,
                SeriesStatus: card.querySelector('.cmSchedSeriesStatus').value,
                MatchMode: card.querySelector('.cmSchedMatchMode').value,
                SortBy: card.querySelector('.cmSchedSortBy').value || '',
                MaxItems: noLimit ? 0 : (parseInt(card.querySelector('.cmSchedMaxItems').value || '0', 10) || 0),
                MaxRuntimeMinutes: parseInt(card.querySelector('.cmSchedMaxRuntime').value || '0', 10) || 0,
                RemoveWhenInactive: card.querySelector('.cmSchedRemoveInactive').checked
            };

            if (kind === 'dates') {
                def.ActiveStart = makeDate(card.querySelector('.cmSchedStartMonth').value, card.querySelector('.cmSchedStartDay').value);
                def.ActiveEnd = makeDate(card.querySelector('.cmSchedEndMonth').value, card.querySelector('.cmSchedEndDay').value);
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
        }

        function readScheduledCollectionsFromEditor() {
            return Array.prototype.slice.call(divScheduledEditor.querySelectorAll('.cmScheduledCard'))
                .map(readCard)
                .filter(function (def) { return def.Name.length > 0; });
        }

        function applyConfigToForm(cfg) {
            form.elements.chkEnableDynamic.checked        = !!cfg.EnableDynamicUserPlaylists;
            form.elements.chkEnableFranchises.checked     = !!cfg.EnableMovieSeriesPlaylists;
            form.elements.txtTmdbApiKey.value             = cfg.TmdbApiKey || '';
            form.elements.chkEnableUniverses.checked      = !!cfg.EnableTvUniversePlaylists;
            form.elements.chkUpdatePlaylistsImage.checked = !!cfg.UpdatePlaylistsLibraryImage;
            form.elements.chkEnableStreaming.checked      = !!cfg.EnableStreamingServiceCollections;
            form.elements.chkRepairManagedCollections.checked = (cfg.RepairManagedCollections !== false);
            form.elements.chkIncludeMovies.checked        = !!cfg.IncludeMovies;
            form.elements.chkIncludeTvShows.checked       = !!cfg.IncludeTvShows;
            renderCheckboxList(divStreamingLibraries, _libraries, cfg.StreamingLibraryIds || [], 'streamingLibrary');
            form.elements.chkUpdateCollectionsImage.checked = !!cfg.UpdateCollectionsLibraryImage;
            form.elements.chkEnableScheduledCollections.checked = !!cfg.EnableScheduledCollections;
            form.elements.txtMdblistApiKey.value = cfg.MdblistApiKey || '';
            renderScheduledCollections((cfg.ScheduledCollections || []).map(migrateDefinition));
            form.elements.chkDebugLogging.checked         = !!cfg.EnableDebugLogging;
        }

        function readConfigFromForm(cfg) {
            cfg.EnableDynamicUserPlaylists       = form.elements.chkEnableDynamic.checked;
            cfg.EnableMovieSeriesPlaylists       = form.elements.chkEnableFranchises.checked;
            cfg.TmdbApiKey                       = form.elements.txtTmdbApiKey.value;
            cfg.EnableTvUniversePlaylists        = form.elements.chkEnableUniverses.checked;
            cfg.UpdatePlaylistsLibraryImage      = form.elements.chkUpdatePlaylistsImage.checked;
            cfg.EnableStreamingServiceCollections = form.elements.chkEnableStreaming.checked;
            cfg.RepairManagedCollections          = form.elements.chkRepairManagedCollections.checked;
            cfg.IncludeMovies                    = form.elements.chkIncludeMovies.checked;
            cfg.IncludeTvShows                   = form.elements.chkIncludeTvShows.checked;
            cfg.StreamingLibraryIds              = readCheckboxList(divStreamingLibraries);
            cfg.UpdateCollectionsLibraryImage    = form.elements.chkUpdateCollectionsImage.checked;
            cfg.EnableScheduledCollections       = form.elements.chkEnableScheduledCollections.checked;
            cfg.MdblistApiKey                    = form.elements.txtMdblistApiKey.value;
            cfg.ScheduledCollections             = readScheduledCollectionsFromEditor();
            cfg.EnableDebugLogging               = form.elements.chkDebugLogging.checked;
            return cfg;
        }

        function saveConfig() {
            loading.show();
            return ApiClient.getPluginConfiguration(pluginUniqueId).then(function (cfg) {
                readConfigFromForm(cfg);
                return ApiClient.updatePluginConfiguration(pluginUniqueId, cfg).then(function (result) {
                    Dashboard.processPluginConfigurationUpdateResult(result);
                    loading.hide();
                    return cfg;
                }, function () {
                    loading.hide();
                    Dashboard.alert({ title: 'Error', message: 'Failed to save settings.' });
                    throw new Error('Failed to save settings.');
                });
            }, function () {
                loading.hide();
                Dashboard.alert({ title: 'Error', message: 'Failed to load current settings.' });
                throw new Error('Failed to load current settings.');
            });
        }

        function upsertScheduledDefinition(def) {
            def = migrateDefinition(def);
            var defs = readScheduledCollectionsFromEditor().map(migrateDefinition);
            var name = (def.Name || '').toLowerCase();
            var replaced = false;
            defs = defs.map(function (existing) {
                var existingName = (existing.Name || '').toLowerCase();
                if (existingName === name || (name === 'top rated movies' && existingName === 'top 100 movies')) {
                    replaced = true;
                    return def;
                }
                return existing;
            });
            if (!replaced) defs.push(def);
            renderScheduledCollections(defs);
            form.elements.chkEnableScheduledCollections.checked = true;
            renderSetupChecklist(readScheduledCollectionsFromEditor());
            return def;
        }

        function setStatus(el, message, good) {
            if (!el) return;
            el.innerHTML = '<span style="color:' + (good ? '#7bd88f' : '#ffcc66') + ';font-weight:700;">' + (good ? '✓' : '⚠') + '</span> ' + escText(message);
        }

        function createDefinitionNow(def, statusEl) {
            def = upsertScheduledDefinition(def);
            setStatus(statusEl || divFeaturedStatus, 'Saving and previewing ' + def.Name + '…', true);
            return saveConfig().then(function () {
                var previewRequest = JSON.parse(JSON.stringify(def));
                previewRequest.MdblistApiKey = (form.elements.txtMdblistApiKey.value || '').trim();
                return apiPost('CollectionManager/ScheduledCollections/Preview', previewRequest);
            }).then(function (r) {
                var preview = typeof r === 'string' ? JSON.parse(r || '{}') : r;
                if (!preview || !preview.Count) {
                    setStatus(statusEl || divFeaturedStatus, def.Name + ' saved, but preview found 0 matching items. Try another collection or check the library metadata.', false);
                    return preview;
                }
                setStatus(statusEl || divFeaturedStatus, 'Creating ' + def.Name + ' from ' + preview.Count + ' matched item(s)…', true);
                return apiPost('CollectionManager/ScheduledCollections/Run', def).then(function (runResponse) {
                    var run = typeof runResponse === 'string' ? JSON.parse(runResponse || '{}') : runResponse;
                    setStatus(statusEl || divFeaturedStatus, run.Message || ('Created ' + def.Name + '.'), !!(!run || run.Success !== false));
                    return run;
                });
            }, function () {
                setStatus(statusEl || divFeaturedStatus, 'Could not create this collection. Try another collection or check the library metadata.', false);
            });
        }

        function onSubmit(ev) {
            ev.preventDefault();
            saveConfig().then(function () {
                Dashboard.alert('Settings saved. Simple Collection buttons create collections automatically.');
            }).catch(function () {});
            return false;
        }

        function addToken(card, field) {
            var input = card.querySelector('.cmTokenInput[data-field="' + field + '"]');
            var value = (input.value || '').trim();
            if (!value) return;
            var existing = tokenValues(card, field).map(function (v) { return v.toLowerCase(); });
            if (existing.indexOf(value.toLowerCase()) >= 0) { input.value = ''; return; }
            var chips = card.querySelector('.cmTokenField[data-field="' + field + '"] .cmTokenChips');
            chips.insertAdjacentHTML('beforeend', '<span class="cmTokenChip" data-field="' + field + '" data-value="' + escAttr(value) + '" style="display:inline-flex;align-items:center;gap:.35em;margin:.2em;padding:.25em .55em;border-radius:999px;background:rgba(255,255,255,.12);">' + escText(value) + '<button type="button" class="cmButton cmRemoveToken" title="Remove" style="min-height:0;min-width:0;padding:.1em .45em !important;">×</button></span>');
            input.value = '';
        }

        function setPreview(card, html) {
            card.querySelector('.cmPreviewResult').innerHTML = html;
        }

        function previewCard(card) {
            var def = readCard(card);
            if (!def.Name) { setPreview(card, 'Name the collection first.'); return; }
            def.MdblistApiKey = (form.elements.txtMdblistApiKey.value || '').trim();
            setPreview(card, 'Previewing…');
            apiPost('CollectionManager/ScheduledCollections/Preview', def).then(function (r) {
                var data = typeof r === 'string' ? JSON.parse(r || '{}') : r;
                renderPreviewResponse(card, data);
            }, function () {
                setPreview(card, 'Preview failed. Check the setup checklist, save settings, and try again.');
            });
        }

        function runCard(card) {
            var def = readCard(card);
            saveConfig().then(function () {
                setPreview(card, 'Running…');
                return apiPost('CollectionManager/ScheduledCollections/Run', def);
            }).then(function (r) {
                var data = typeof r === 'string' ? JSON.parse(r || '{}') : r;
                setPreview(card, escText(data.Message || 'Collection run queued.'));
            }, function () {
                setPreview(card, 'Run failed.');
            });
        }

        if (divScheduledOverview) {
            divScheduledOverview.addEventListener('click', function (ev) {
                var btn = ev.target.closest ? ev.target.closest('.cmOverviewEdit') : null;
                if (!btn) return;
                var card = divScheduledEditor.querySelector('.cmScheduledCard[data-index="' + btn.getAttribute('data-index') + '"]');
                if (card) {
                    card.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    var name = card.querySelector('.cmSchedName');
                    if (name) name.focus();
                }
            });
        }

        divScheduledEditor.addEventListener('click', function (ev) {
            var card = ev.target.closest ? ev.target.closest('.cmScheduledCard') : null;
            if (ev.target.closest && ev.target.closest('.cmRemoveToken')) {
                var chip = ev.target.closest('.cmTokenChip');
                if (chip) chip.parentNode.removeChild(chip);
                if (card) renderSourceHelper(card);
                renderScheduledOverview(readScheduledCollectionsFromEditor());
                renderSetupChecklist(readScheduledCollectionsFromEditor());
                return;
            }
            if (ev.target.closest && ev.target.closest('.cmAddToken')) {
                var btn = ev.target.closest('.cmAddToken');
                addToken(card, btn.getAttribute('data-field'));
                if (card) renderSourceHelper(card);
                renderScheduledOverview(readScheduledCollectionsFromEditor());
                renderSetupChecklist(readScheduledCollectionsFromEditor());
                return;
            }
            if (!card) return;
            if (ev.target.closest('.cmRemoveScheduled')) {
                card.parentNode.removeChild(card);
                renderScheduledCollections(readScheduledCollectionsFromEditor());
            } else if (ev.target.closest('.cmDuplicateScheduled')) {
                var defs = readScheduledCollectionsFromEditor();
                var copy = JSON.parse(JSON.stringify(readCard(card)));
                copy.Name = (copy.Name || 'Custom Collection') + ' Copy';
                defs.push(copy);
                renderScheduledCollections(defs);
            } else if (ev.target.closest('.cmPreviewScheduled')) {
                previewCard(card);
            } else if (ev.target.closest('.cmRunScheduled')) {
                runCard(card);
            }
        });

        divScheduledEditor.addEventListener('change', function (ev) {
            if (ev.target.classList && ev.target.classList.contains('cmSchedKind')) {
                var card = ev.target.closest('.cmScheduledCard');
                var kind = ev.target.value;
                card.querySelector('.cmSchedDates').style.display = kind === 'dates' ? '' : 'none';
                card.querySelector('.cmSchedDays').style.display = kind === 'days' ? '' : 'none';
            }
            if (ev.target.classList && ev.target.classList.contains('cmSchedNoLimit')) {
                var max = ev.target.closest('.cmScheduledCard').querySelector('.cmSchedMaxItems');
                max.disabled = ev.target.checked;
                if (ev.target.checked) max.value = '';
            }
            var changedCard = ev.target.closest ? ev.target.closest('.cmScheduledCard') : null;
            if (changedCard) renderSourceHelper(changedCard);
            renderScheduledOverview(readScheduledCollectionsFromEditor());
            renderSetupChecklist(readScheduledCollectionsFromEditor());
        });

        divScheduledEditor.addEventListener('input', function (ev) {
            if (ev.target.classList && ev.target.classList.contains('cmTokenInput')) {
                updateTokenSuggestions(ev.target);
            }
            var inputCard = ev.target.closest ? ev.target.closest('.cmScheduledCard') : null;
            if (inputCard) renderSourceHelper(inputCard);
            renderScheduledOverview(readScheduledCollectionsFromEditor());
            renderSetupChecklist(readScheduledCollectionsFromEditor());
        });

        divScheduledEditor.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter' && ev.target.classList && ev.target.classList.contains('cmTokenInput')) {
                ev.preventDefault();
                var keyCard = ev.target.closest('.cmScheduledCard');
                addToken(keyCard, ev.target.getAttribute('data-field'));
                if (keyCard) renderSourceHelper(keyCard);
                renderSetupChecklist(readScheduledCollectionsFromEditor());
            }
        });

        btnAddScheduledCollection.addEventListener('click', function () {
            addPreset(selScheduledPreset.value, false);
        });

        if (txtQuickScheduledSource) {
            txtQuickScheduledSource.addEventListener('input', function () {
                divQuickScheduledHint.innerHTML = escText(simpleCollectionHint(txtQuickScheduledSource.value));
            });
        }

        if (btnQuickAddScheduledCollection) {
            btnQuickAddScheduledCollection.addEventListener('click', function () {
                var source = (txtQuickScheduledSource.value || '').trim();
                if (!source) {
                    Dashboard.alert({ title: 'Add IMDb / MDBList collection', message: 'Paste an IMDb title/list, IMDb IDs, or MDBList source first.' });
                    return;
                }
                var defs = readScheduledCollectionsFromEditor();
                var def = simpleCollectionDefinition(txtQuickScheduledName.value, source);
                defs.push(def);
                renderScheduledCollections(defs);
                form.elements.chkEnableScheduledCollections.checked = true;
                renderSetupChecklist(readScheduledCollectionsFromEditor());
                divQuickScheduledHint.innerHTML = escText(simpleCollectionHint(source) + ' Saving and previewing now…');
                txtQuickScheduledSource.value = '';
                createDefinitionNow(def, divQuickScheduledHint);
            });
        }

        if (divFeaturedExamples) {
            divFeaturedExamples.addEventListener('click', function (ev) {
                var btn = ev.target.closest ? ev.target.closest('.cmFeaturedPreset') : null;
                if (!btn) return;
                createDefinitionNow(presetDefinition(btn.getAttribute('data-preset') || 'custom'), divFeaturedStatus);
            });
        }

        if (btnTestMdblistApiKey) {
            btnTestMdblistApiKey.addEventListener('click', function () {
                var key = (form.elements.txtMdblistApiKey.value || '').trim();
                if (!key) {
                    divMdblistApiKeyStatus.innerHTML = '<span style="color:#ffcc66;">⚠ Enter an MDBList API key first.</span>';
                    return;
                }
                divMdblistApiKeyStatus.innerHTML = 'Testing MDBList…';
                apiPost('CollectionManager/ScheduledCollections/TestMdblistApiKey', { ApiKey: key, ListPath: 'official:movies/moviemeter' }).then(function (r) {
                    var data = typeof r === 'string' ? JSON.parse(r || '{}') : r;
                    divMdblistApiKeyStatus.innerHTML = '<span style="color:' + (data.Success ? '#7bd88f' : '#ffcc66') + ';font-weight:700;">' + (data.Success ? '✓' : '⚠') + '</span> ' + escText(data.Message || (data.Success ? 'Connected.' : 'Test failed.'));
                }, function () {
                    divMdblistApiKeyStatus.innerHTML = '<span style="color:#ffcc66;">⚠ MDBList test failed. Check the key and try again.</span>';
                });
            });
        }

        if (form.elements.txtMdblistApiKey) {
            form.elements.txtMdblistApiKey.addEventListener('input', function () {
                renderSetupChecklist(readScheduledCollectionsFromEditor());
            });
        }

        btnRunAllScheduledCollections.addEventListener('click', function () {
            saveConfig().then(function () {
                loading.show();
                return apiPost('CollectionManager/ScheduledCollections/RunTask', {});
            }).then(function (r) {
                loading.hide();
                var data = typeof r === 'string' ? JSON.parse(r || '{}') : r;
                Dashboard.alert(data.Message || 'Collection Manager task queued.');
            }, function () {
                loading.hide();
                Dashboard.alert({ title: 'Error', message: 'Failed to queue Collection Manager task.' });
            });
        });

        form.addEventListener('submit', onSubmit);
        form.elements.chkEnableScheduledCollections.addEventListener('change', function () {
            renderSetupChecklist(readScheduledCollectionsFromEditor());
        });

        view.addEventListener('viewshow', function () {
            loading.show();
            Promise.all([
                ApiClient.getPluginConfiguration(pluginUniqueId),
                loadLibraries().then(loadBuilderMetadata)
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
