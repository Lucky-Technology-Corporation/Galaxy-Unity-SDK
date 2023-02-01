# Galaxy-Leaderboard
Free leaderboard for Unity. Comes with customizable avatars, contact syncing, prizes, leaderboard resets, cloud save, and more.

**[Read the docs here](https://docs.galaxy.us)**

## Get Started 

1. Sign up at [dashboard.galaxysdk.com](https://dashboard.galaxysdk.com) and get a publishable key (free!)
2. Drag `Galaxy.unitypackage` into your project
3. Report a score of 500 points:
```
GalaxyClientAPI.ReportScore(new GalaxySDK.ClientModels.ReportScoreRequest {
    Score = 500.0,
    LeaderboardId = "leaderboard_id",
}, result => { }, error => { });
```
4. Show the leaderboard: `FindObjectOfType<GalaxyController>().ShowLeaderboard();`

