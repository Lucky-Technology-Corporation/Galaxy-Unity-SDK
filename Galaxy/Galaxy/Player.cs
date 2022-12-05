using System;
using System.Collections;
using System.Collections.Generic;

namespace GalaxySDK{

    [Serializable]
    public class PlayerInfo
    {
        public bool is_anonymous;
        public string global_id;
        public string id;
        public string nickname;
        public string profile_image_url;
    }

    [Serializable]
    public class PlayerRecord
    {
        public bool is_anonymous;
        public string global_id;
        public string id;
        public int high_score;
        public int ranking;
        public int total_players;
        public string nickname;
        public string profile_image_url;
    }
}