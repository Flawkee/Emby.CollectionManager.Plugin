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
        var selScheduledPreset = view.querySelector('#selScheduledPreset');
        var btnAddScheduledCollection = view.querySelector('#btnAddScheduledCollection');
        var btnRunAllScheduledCollections = view.querySelector('#btnRunAllScheduledCollections');
        var txtQuickScheduledName = view.querySelector('#txtQuickScheduledName');
        var txtQuickScheduledSource = view.querySelector('#txtQuickScheduledSource');
        var btnQuickAddScheduledCollection = view.querySelector('#btnQuickAddScheduledCollection');
        var divQuickScheduledHint = view.querySelector('#divQuickScheduledHint');
        var _libraries = [];
        var _metadata = { Libraries: [], Genres: [], Studios: [], Tags: [], Years: [], Ratings: [] };

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
                    + escText(v) + '<button is="emby-button" type="button" class="cmRemoveToken" title="Remove" style="min-width:0;padding:.1em .35em;"><span>×</span></button></span>';
            }).join('');
            return '<div class="cmTokenField" data-field="' + field + '">'
                + '<div class="fieldDescription" style="font-weight:600;margin-bottom:.25em;">' + escText(label) + '</div>'
                + '<div class="cmTokenChips">' + chips + '</div>'
                + '<div style="display:flex;gap:.35em;align-items:center;">'
                + '<input class="cmTokenInput" data-field="' + field + '" list="' + id + '" type="text" placeholder="' + escAttr(placeholder || 'Add value') + '" />'
                + '<datalist id="' + id + '">' + optionList(suggestions, '') + '</datalist>'
                + '<button is="emby-button" type="button" class="cmAddToken" data-field="' + field + '"><span>Add</span></button>'
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
                + '<button is="emby-button" type="button" class="cmPreviewScheduled"><span>Preview</span></button>'
                + '<button is="emby-button" type="button" class="cmRunScheduled"><span>Save & Run This</span></button>'
                + '<button is="emby-button" type="button" class="cmDuplicateScheduled"><span>Duplicate</span></button>'
                + '<button is="emby-button" type="button" class="cmRemoveScheduled"><span>Remove</span></button>'
                + '</div></div>'
                + '<input type="hidden" class="cmSchedSortBy" value="' + escAttr(def.SortBy || '') + '" />'
                + '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:.75em;margin-top:.75em;">'
                + '<label>Collection name<br /><input class="cmSchedName" type="text" value="' + escAttr(def.Name || '') + '" placeholder="IMDb Watchlist" /></label>'
                + '<label>Include<br /><select class="cmSchedContentType">'
                + '<option value="Both"' + ((def.ContentType || 'Both') === 'Both' ? ' selected' : '') + '>Movies + TV Shows</option>'
                + '<option value="Movies"' + (def.ContentType === 'Movies' ? ' selected' : '') + '>Movies only</option>'
                + '<option value="TvShows"' + (def.ContentType === 'TvShows' ? ' selected' : '') + '>TV shows only</option>'
                + '</select></label>'
                + '<label><span>IMDb / MDBList source</span><br /><input class="cmSchedMdblistListPath" type="text" value="' + escAttr(def.MdblistListPath || '') + '" placeholder="MDBList link/ID, or use IMDb IDs below" /></label>'
                + '</div>'
                + '<details class="cmAdvancedOptions" style="margin-top:.9em;"><summary style="cursor:pointer;font-weight:600;">More options: filters, schedule, libraries</summary>'
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
            if (def.IncludedImdbIds && def.IncludedImdbIds.length) parts.push('IMDb IDs: ' + def.IncludedImdbIds.length);
            if (def.MdblistListPath) parts.push('MDBList: ' + def.MdblistListPath);
            if (def.PlayState && def.PlayState !== 'Any') parts.push(def.PlayState);
            if (def.IsFavorite && def.IsFavorite !== 'Any') parts.push(def.IsFavorite === 'Yes' ? 'Favorites' : 'Not favorites');
            if (def.SortBy === 'DateCreatedDescending') parts.push('Recently added first');
            if (def.MaxRuntimeMinutes && def.MaxRuntimeMinutes > 0) parts.push('≤ ' + def.MaxRuntimeMinutes + ' min');
            return parts.join(' • ') || 'No filters';
        }

        function renderScheduledOverview(defs) {
            if (!divScheduledOverview) return;
            if (!defs.length) { divScheduledOverview.innerHTML = ''; return; }
            var rows = defs.map(function (def, index) {
                return '<tr data-index="' + index + '">'
                    + '<td>' + (def.Enabled === false ? '—' : '✓') + '</td>'
                    + '<td><button is="emby-button" type="button" class="cmOverviewEdit" data-index="' + index + '" style="min-width:0;padding:.2em .35em;"><span>' + escText(def.Name || 'Unnamed') + '</span></button></td>'
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

        function onSubmit(ev) {
            ev.preventDefault();
            saveConfig().then(function () {
                Dashboard.alert('Settings saved. Use Preview, Save & Run This, or Run All Collections when you want to make collection changes.');
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
            chips.insertAdjacentHTML('beforeend', '<span class="cmTokenChip" data-field="' + field + '" data-value="' + escAttr(value) + '" style="display:inline-flex;align-items:center;gap:.35em;margin:.2em;padding:.25em .55em;border-radius:999px;background:rgba(255,255,255,.12);">' + escText(value) + '<button is="emby-button" type="button" class="cmRemoveToken" title="Remove" style="min-width:0;padding:.1em .35em;"><span>×</span></button></span>');
            input.value = '';
        }

        function setPreview(card, html) {
            card.querySelector('.cmPreviewResult').innerHTML = html;
        }

        function previewCard(card) {
            var def = readCard(card);
            if (!def.Name) { setPreview(card, 'Name the collection first.'); return; }
            setPreview(card, 'Previewing…');
            apiPost('CollectionManager/ScheduledCollections/Preview', def).then(function (r) {
                var data = typeof r === 'string' ? JSON.parse(r || '{}') : r;
                var items = data.Items || [];
                var rows = items.map(function (i) {
                    return '<tr><td>' + escText(i.Name || '') + '</td><td>' + escText(i.Year || '') + '</td><td>' + escText(i.Type || '') + '</td></tr>';
                }).join('');
                var table = rows ? '<div style="overflow:auto;margin-top:.45em;"><table class="detailTable" style="width:100%;"><thead><tr><th>Title</th><th>Year</th><th>Type</th></tr></thead><tbody>' + rows + '</tbody></table></div>' : '';
                var warnings = (data.Warnings || []).map(function (w) {
                    return '<li>' + escText(w) + '</li>';
                }).join('');
                var warningHtml = warnings ? '<div style="margin-top:.5em;color:#ffcc66;"><b>Warnings</b><ul style="margin:.35em 0 0 1.2em;">' + warnings + '</ul></div>' : '';
                setPreview(card, '<b>' + (data.Count || 0) + ' item(s)</b> matched' + table + warningHtml);
                updateOverviewPreview(parseInt(card.getAttribute('data-index') || '0', 10), (data.Count || 0) + ' item(s)', !!warnings);
            }, function () {
                setPreview(card, 'Preview failed. Save the settings and try again.');
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
                renderScheduledOverview(readScheduledCollectionsFromEditor());
                return;
            }
            if (ev.target.closest && ev.target.closest('.cmAddToken')) {
                var btn = ev.target.closest('.cmAddToken');
                addToken(card, btn.getAttribute('data-field'));
                renderScheduledOverview(readScheduledCollectionsFromEditor());
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
            renderScheduledOverview(readScheduledCollectionsFromEditor());
        });

        divScheduledEditor.addEventListener('input', function (ev) {
            if (ev.target.classList && ev.target.classList.contains('cmTokenInput')) {
                updateTokenSuggestions(ev.target);
            }
            renderScheduledOverview(readScheduledCollectionsFromEditor());
        });

        divScheduledEditor.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter' && ev.target.classList && ev.target.classList.contains('cmTokenInput')) {
                ev.preventDefault();
                addToken(ev.target.closest('.cmScheduledCard'), ev.target.getAttribute('data-field'));
            }
        });

        btnAddScheduledCollection.addEventListener('click', function () {
            var defs = readScheduledCollectionsFromEditor();
            defs.push(presetDefinition(selScheduledPreset.value));
            renderScheduledCollections(defs);
            form.elements.chkEnableScheduledCollections.checked = true;
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
                defs.push(simpleCollectionDefinition(txtQuickScheduledName.value, source));
                renderScheduledCollections(defs);
                form.elements.chkEnableScheduledCollections.checked = true;
                divQuickScheduledHint.innerHTML = escText(simpleCollectionHint(source) + ' Collection added below. Click Preview to check matches.');
                txtQuickScheduledSource.value = '';
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
