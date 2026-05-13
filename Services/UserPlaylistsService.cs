using CollectionManager.Plugin.Configuration;
using CollectionManager.Plugin.ScheduledTasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollectionManager.Plugin.Services
{
    [Route("/CollectionManager/UserPlaylists/{UserId}", "GET", Summary = "Get a user's dynamic playlists configuration")]
    public class GetUserPlaylists : IReturn<UserDynamicPlaylistsConfig>
    {
        public string UserId { get; set; } = string.Empty;
    }

    [Route("/CollectionManager/UserPlaylists/{UserId}", "POST", Summary = "Save a user's dynamic playlists configuration")]
    public class SaveUserPlaylists : IReturnVoid
    {
        public string UserId { get; set; } = string.Empty;
        public bool EnableBingeMovieFranchises { get; set; } = true;
        public bool EnableBingeTvUniverses { get; set; } = true;
        public List<DynamicPlaylistDefinition> Playlists { get; set; } = new List<DynamicPlaylistDefinition>();
    }

    public class UserPlaylistsService : IService
    {
        public const string ConfigSubdir = "CollectionManager.Plugin";
        public const string UserPlaylistsSubdir = "userplaylists";

        private readonly ILogger _logger;
        private readonly IApplicationPaths _appPaths;
        private readonly IJsonSerializer _json;
        private readonly ITaskManager _taskManager;

        public UserPlaylistsService(ILogger logger, IApplicationPaths appPaths, IJsonSerializer json, ITaskManager taskManager)
        {
            _logger = logger;
            _appPaths = appPaths;
            _json = json;
            _taskManager = taskManager;
        }

        public object Get(GetUserPlaylists request)
        {
            var path = GetConfigPath(request.UserId);
            if (!File.Exists(path))
                return new UserDynamicPlaylistsConfig();

            try
            {
                using var stream = File.OpenRead(path);
                return _json.DeserializeFromStream<UserDynamicPlaylistsConfig>(stream) ?? new UserDynamicPlaylistsConfig();
            }
            catch (System.Exception ex)
            {
                _logger.Warn($"[CollectionManager/UserPlaylists] Read failed for user {request.UserId}: {ex.Message}");
                return new UserDynamicPlaylistsConfig();
            }
        }

        public void Post(SaveUserPlaylists request)
        {
            var path = GetConfigPath(request.UserId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var data = new UserDynamicPlaylistsConfig
            {
                EnableBingeMovieFranchises = request.EnableBingeMovieFranchises,
                EnableBingeTvUniverses     = request.EnableBingeTvUniverses,
                Playlists                  = request.Playlists ?? new List<DynamicPlaylistDefinition>()
            };
            using (var stream = File.Create(path))
            {
                _json.SerializeToStream(data, stream);
            }
            _logger.Info($"[CollectionManager/UserPlaylists] Saved {data.Playlists.Count} playlist(s) for user {request.UserId} → '{path}'");

            TriggerCollectionManagerTask();
        }

        private void TriggerCollectionManagerTask()
        {
            try
            {
                var worker = _taskManager.ScheduledTasks
                    .FirstOrDefault(t => t.ScheduledTask is CollectionManagerTask);
                if (worker == null)
                {
                    _logger.Warn("[CollectionManager/UserPlaylists] CollectionManagerTask worker not found — cannot trigger task");
                    return;
                }

                _taskManager.Execute(worker, new TaskOptions());
                _logger.Info("[CollectionManager/UserPlaylists] Triggered Collection Manager task after save");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[CollectionManager/UserPlaylists] Failed to trigger task: {ex.Message}");
            }
        }

        public static string GetConfigPath(IApplicationPaths appPaths, string userId)
        {
            return Path.Combine(appPaths.ConfigurationDirectoryPath, ConfigSubdir, UserPlaylistsSubdir, userId + ".json");
        }

        private string GetConfigPath(string userId) => GetConfigPath(_appPaths, userId);
    }
}
