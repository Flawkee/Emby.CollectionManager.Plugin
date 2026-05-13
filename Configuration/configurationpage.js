define([
    'loading',
    'confirm',
    'emby-input',
    'emby-button',
    'emby-select',
    'emby-checkbox'
], function (loading, confirmDialog) {
    'use strict';

    var CONFIG_KEY = 'collectionmanager-userplaylists';

    function defaultPlaylist() {
        return {
            Enabled: true,
            Name: 'My Playlist',
            ContentType: 'Both',
            IncludedGenres: [],
            IncludedStudios: [],
            IncludedYears: [],
            IncludedOfficialRatings: [],
            IncludedTags: [],
            PlayState: 'Any',
            IsFavorite: 'Any',
            SeriesStatus: 'Any',
            MaxItems: 0
        };
    }

    function escAttr(s) {
        return (s == null ? '' : String(s))
            .replace(/&/g, '&amp;').replace(/"/g, '&quot;')
            .replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function renderCheckboxList(container, items, selectedValues, name) {
        var selected = (selectedValues || []).reduce(function (acc, v) { acc[v] = true; return acc; }, {});
        var html = items.map(function (item) {
            var checked = selected[item] ? ' checked="checked"' : '';
            return '<label class="emby-checkbox-label">'
                + '<input is="emby-checkbox" type="checkbox" name="' + escAttr(name) + '" value="' + escAttr(item) + '"' + checked + ' />'
                + '<span>' + escAttr(item) + '</span></label>';
        }).join('');
        container.innerHTML = html;
    }

    function readCheckboxList(container) {
        return Array.prototype.slice.call(container.querySelectorAll('input[type="checkbox"]'))
            .filter(function (cb) { return cb.checked; })
            .map(function (cb) { return cb.value; });
    }

    return function (view) {

        var _userId = null;
        var _config = { Playlists: [] };
        var _currentIdx = -1;
        var _libraryData = { genres: [], studios: [], years: [], ratings: [], tags: [] };

        var form          = view.querySelector('#cmPlaylistForm');
        var chkFranchises = view.querySelector('#chkEnableBingeFranchises');
        var chkUniverses  = view.querySelector('#chkEnableBingeUniverses');
        var playlistSel   = view.querySelector('#selectPlaylist');
        var btnAdd        = view.querySelector('#btnAddPlaylist');
        var btnDel        = view.querySelector('#btnDeletePlaylist');
        var editor        = view.querySelector('#playlistEditor');

        var divGenres   = view.querySelector('#divGenres');
        var divStudios  = view.querySelector('#divStudios');
        var divYears    = view.querySelector('#divYears');
        var divRatings  = view.querySelector('#divRatings');
        var divTags     = view.querySelector('#divTags');

        // ── Library data fetch ────────────────────────────────────────────────

        function loadLibraryData() {
            var common = { UserId: _userId, SortBy: 'SortName', SortOrder: 'Ascending', Recursive: true };

            var pGenres = ApiClient.getJSON(ApiClient.getUrl('Genres', common))
                .then(function (r) { _libraryData.genres = (r.Items || []).map(function (i) { return i.Name; }); })
                .catch(function () {});

            var pStudios = ApiClient.getJSON(ApiClient.getUrl('Studios', common))
                .then(function (r) { _libraryData.studios = (r.Items || []).map(function (i) { return i.Name; }); })
                .catch(function () {});

            var pTags = ApiClient.getJSON(ApiClient.getUrl('Tags', common))
                .then(function (r) { _libraryData.tags = (r.Items || []).map(function (i) { return i.Name; }); })
                .catch(function () {});

            var pYears = ApiClient.getJSON(ApiClient.getUrl('Items', {
                UserId: _userId, IncludeItemTypes: 'Movie,Series',
                Recursive: true, Fields: 'ProductionYear', Limit: 5000
            })).then(function (r) {
                var yrs = {};
                (r.Items || []).forEach(function (i) { if (i.ProductionYear) yrs[i.ProductionYear] = true; });
                _libraryData.years = Object.keys(yrs).sort(function (a, b) { return b - a; });
            }).catch(function () {});

            var pRatings = ApiClient.getJSON(ApiClient.getUrl('Items', {
                UserId: _userId, IncludeItemTypes: 'Movie,Series',
                Recursive: true, Fields: 'OfficialRating', Limit: 5000
            })).then(function (r) {
                var rts = {};
                (r.Items || []).forEach(function (i) { if (i.OfficialRating) rts[i.OfficialRating] = true; });
                _libraryData.ratings = Object.keys(rts).sort();
            }).catch(function () {});

            return Promise.all([pGenres, pStudios, pTags, pYears, pRatings]);
        }

        // ── Config I/O ────────────────────────────────────────────────────────

        function configUrl() {
            return ApiClient.getUrl('CollectionManager/UserPlaylists/' + _userId);
        }

        function loadConfig() {
            return ApiClient.getJSON(configUrl()).then(function (data) {
                if (data && Array.isArray(data.Playlists)) _config = data;
            }).catch(function () { /* no config yet */ });
        }

        function applyTopLevelToForm() {
            // Default to true when the field is missing (i.e. first run)
            chkFranchises.checked = (_config.EnableBingeMovieFranchises !== false);
            chkUniverses.checked  = (_config.EnableBingeTvUniverses     !== false);
        }

        function collectTopLevelFromForm() {
            _config.EnableBingeMovieFranchises = chkFranchises.checked;
            _config.EnableBingeTvUniverses     = chkUniverses.checked;
        }

        function saveConfig() {
            collectTopLevelFromForm();
            collectCurrentEdits();
            return ApiClient.ajax({
                type: 'POST',
                url: configUrl(),
                data: JSON.stringify(_config),
                contentType: 'application/json'
            });
        }

        // ── Rendering ─────────────────────────────────────────────────────────

        function populatePlaylistDropdown() {
            playlistSel.innerHTML = '';
            _config.Playlists.forEach(function (pl, i) {
                var opt = document.createElement('option');
                opt.value = String(i);
                opt.text = pl.Name || ('Playlist ' + (i + 1));
                playlistSel.add(opt);
            });
            if (_config.Playlists.length === 0) {
                editor.style.display = 'none';
                _currentIdx = -1;
            } else {
                _currentIdx = Math.min(_currentIdx >= 0 ? _currentIdx : 0, _config.Playlists.length - 1);
                playlistSel.value = String(_currentIdx);
                editor.style.display = '';
            }
        }

        function renderCurrentPlaylist() {
            if (_currentIdx < 0 || !_config.Playlists[_currentIdx]) {
                editor.style.display = 'none';
                return;
            }
            editor.style.display = '';

            var pl = _config.Playlists[_currentIdx];
            form.elements.txtPlaylistName.value     = pl.Name || '';
            form.elements.chkEnabled.checked        = !!pl.Enabled;
            form.elements.selectContentType.value   = pl.ContentType || 'Both';
            form.elements.selectPlayState.value     = pl.PlayState   || 'Any';
            form.elements.selectFavorite.value      = pl.IsFavorite  || 'Any';
            form.elements.selectSeriesStatus.value  = pl.SeriesStatus || 'Any';
            form.elements.txtMaxItems.value         = (pl.MaxItems != null ? pl.MaxItems : 0);

            renderCheckboxList(divGenres,  _libraryData.genres,  pl.IncludedGenres,          'genre');
            renderCheckboxList(divStudios, _libraryData.studios, pl.IncludedStudios,         'studio');
            renderCheckboxList(divYears,   _libraryData.years,   pl.IncludedYears,           'year');
            renderCheckboxList(divRatings, _libraryData.ratings, pl.IncludedOfficialRatings, 'rating');
            renderCheckboxList(divTags,    _libraryData.tags,    pl.IncludedTags,            'tag');
        }

        function collectCurrentEdits() {
            if (_currentIdx < 0 || !_config.Playlists[_currentIdx]) return;
            var pl = _config.Playlists[_currentIdx];
            pl.Name                    = form.elements.txtPlaylistName.value.trim() || ('Playlist ' + (_currentIdx + 1));
            pl.Enabled                 = form.elements.chkEnabled.checked;
            pl.ContentType             = form.elements.selectContentType.value;
            pl.PlayState               = form.elements.selectPlayState.value;
            pl.IsFavorite              = form.elements.selectFavorite.value;
            pl.SeriesStatus            = form.elements.selectSeriesStatus.value;
            pl.MaxItems                = parseInt(form.elements.txtMaxItems.value, 10) || 0;
            pl.IncludedGenres          = readCheckboxList(divGenres);
            pl.IncludedStudios         = readCheckboxList(divStudios);
            pl.IncludedYears           = readCheckboxList(divYears);
            pl.IncludedOfficialRatings = readCheckboxList(divRatings);
            pl.IncludedTags            = readCheckboxList(divTags);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        function onPlaylistChange() {
            collectCurrentEdits();
            _currentIdx = parseInt(playlistSel.value, 10);
            renderCurrentPlaylist();
        }

        function onAddClicked() {
            collectCurrentEdits();
            var pl = defaultPlaylist();
            pl.Name = 'Playlist ' + (_config.Playlists.length + 1);
            _config.Playlists.push(pl);
            _currentIdx = _config.Playlists.length - 1;
            populatePlaylistDropdown();
            renderCurrentPlaylist();
        }

        function onDeleteClicked() {
            if (_currentIdx < 0) return;
            collectCurrentEdits();
            var pl = _config.Playlists[_currentIdx];
            var name = pl.Name || 'this playlist';

            var text, title, confirmText, primary;
            if (pl.Enabled) {
                title = '"' + name + '" is currently enabled';
                text = 'If you delete it now, the playlist already created in your library will become unmanaged — '
                    + 'the scheduled task will no longer rebuild or remove it, and you will need to delete it manually.\n\n'
                    + 'To remove it cleanly, click Cancel, uncheck "Enabled", click Save, then delete the entry once the task has run.\n\n'
                    + 'Delete "' + name + '" anyway?';
                confirmText = 'Delete anyway';
                primary = 'cancel';
            } else {
                title = 'Delete playlist';
                text = 'Delete "' + name + '"?';
                confirmText = 'Delete';
                primary = 'submit';
            }

            confirmDialog({
                title: title,
                text: text,
                confirmText: confirmText,
                cancelText: 'Cancel',
                primary: primary
            }).then(function () {
                _config.Playlists.splice(_currentIdx, 1);
                _currentIdx = Math.min(_currentIdx, _config.Playlists.length - 1);
                populatePlaylistDropdown();
                renderCurrentPlaylist();
            }, function () { /* cancelled — no-op */ });
        }

        function onSubmit(ev) {
            ev.preventDefault();
            loading.show();
            saveConfig()
                .then(function () {
                    loading.hide();
                    Dashboard.alert('Settings saved.');
                })
                .catch(function () {
                    loading.hide();
                    Dashboard.alert({ title: 'Error', message: 'Failed to save settings.' });
                });
            return false;
        }

        // ── Wire-up ───────────────────────────────────────────────────────────

        form.addEventListener('submit', onSubmit);
        playlistSel.addEventListener('change', onPlaylistChange);
        btnAdd.addEventListener('click', onAddClicked);
        btnDel.addEventListener('click', onDeleteClicked);

        view.addEventListener('viewshow', function () {
            loading.show();
            _userId = ApiClient.getCurrentUserId();
            var pUser = _userId ? Promise.resolve() : ApiClient.getCurrentUser().then(function (u) { _userId = u.Id; });

            pUser
                .then(loadLibraryData)
                .then(loadConfig)
                .then(function () {
                    applyTopLevelToForm();
                    populatePlaylistDropdown();
                    renderCurrentPlaylist();
                    loading.hide();
                })
                .catch(function (err) {
                    loading.hide();
                    Dashboard.alert({ title: 'Error', message: 'Failed to load: ' + err });
                });
        });
    };
});
