using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class LeaderboardObject{
    public string uid;
    public int rank;
    public int score_value;
    public string nickname;
    public string profile_image;
}

[Serializable]
public class Leaderboard{
    public List<LeaderboardObject> data;
}
