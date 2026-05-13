using System.Collections.Generic;

namespace CollectionManager.Plugin.Configuration
{
    public class UserDynamicPlaylistsConfig
    {
        public bool EnableBingeMovieFranchises { get; set; } = true;
        public bool EnableBingeTvUniverses { get; set; } = true;
        public List<DynamicPlaylistDefinition> Playlists { get; set; } = new List<DynamicPlaylistDefinition>();
    }
}
