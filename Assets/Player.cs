using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class PlayerInfo
{
    public string id;
    public string nickname;
    public string profile_image_url;
}

[Serializable]
public class PlayerRecord
{
    public string id;
    public int high_score;
    public int rank;
    public int total_players;
    public string nickname;
    public string profile_image_url;
}
