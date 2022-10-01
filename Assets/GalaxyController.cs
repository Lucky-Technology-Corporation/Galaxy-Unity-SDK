using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using System.Linq;

#if UNITY_2018_4_OR_NEWER
using UnityEngine.Networking;
#endif
using UnityEngine.UI;

//Make this a prefab!
public class GalaxyController : MonoBehaviour
{
    
    public string SDKKey;

    public delegate void AvatarDidChange(Texture2D newAvatar);
    public AvatarDidChange avatarDidChange;

    public delegate void DidSignIn(string playerId);
    public DidSignIn didSignIn;

    public delegate void InfoDidChange(PlayerInfo info);
    public InfoDidChange infoDidChange;

    private string currentGalaxyLeaderboardID = "";
    private WebViewObject webViewObject;
    private string Url;
    private int topMargin = 180;
    private string savedToken = "";
    private string backendUrlBase = "https://ishtar-nft.herokuapp.com/api/v1";
    private string frontendUrlBase = "https://inanna.vercel.app";
    private string currentPlayerId = "";
    private Texture2D cachedProfileImage;

    // Start is called before the first frame update
    void Awake()
    {
        Debug.Log("Initializing LuckyBoard");
        savedToken = PlayerPrefs.GetString("token");
        currentPlayerId = PlayerPrefs.GetString("currentPlayerId");
        if (true || savedToken == null || savedToken == "")
        {
            Debug.Log("Signing in...");
            StartCoroutine(SignInAnonymously());
        }
    }


    public string GetPlayerID()
    {
        return currentPlayerId;
    }

    public void SetPlayerID(string newId) //not set up yet
    {
        currentPlayerId = newId;
        var body = "{\"id\":\"" + newId + "\"}";
        StartCoroutine(SendPostRequest("/player/update-id", body));
    }

    public void GetPlayerAvatarTexture(System.Action<Texture2D> callback, bool forceDownload = false)
    {
        if (!forceDownload && cachedProfileImage != null)
        {
            callback(cachedProfileImage);
        }
        else
        {
            var imageUrl = backendUrlBase + "/users/" + currentPlayerId + "/avatar.png";
            Debug.Log("Downloading image from " + imageUrl);
            StartCoroutine(DownloadImage(imageUrl, callback));
        }
    }

    private IEnumerator DownloadImage(string MediaUrl, System.Action<Texture2D> callback)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl);
        yield return request.SendWebRequest();
        if (request.isNetworkError || request.isHttpError)
        {
            Debug.Log(request.error);
            callback(null);
        }
        else
        {
            callback(((DownloadHandlerTexture)request.downloadHandler).texture);
        }
    }


    public void GetPlayerInfo(System.Action<PlayerInfo> callback)
    {
        StartCoroutine(PlayerInfoRequest(callback));
    }

    private IEnumerator PlayerInfoRequest(System.Action<PlayerInfo> callback = null)
    {
        UnityWebRequest www = UnityWebRequest.Get(backendUrlBase + "/users/profile");
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Get player info error: " + www.error);
            callback(null);
        }
        else
        {
            var jsonString = www.downloadHandler.text;
            PlayerInfo playerInfo = JsonUtility.FromJson<PlayerInfo>(jsonString);
            callback(playerInfo);
        }
    }

    public void GetPlayerFriends(System.Action<List<PlayerInfo>> callback)
    {
        StartCoroutine(PlayerFriendsRequest(callback));
    }

    private IEnumerator PlayerFriendsRequest(System.Action<List<PlayerInfo>> callback = null)
    {
        UnityWebRequest www = UnityWebRequest.Get(backendUrlBase + "/users/friends");
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Get player friends error: " + www.error);
            callback(null);
        }
        else
        {
            var jsonString = www.downloadHandler.text;
            PlayerInfo[] playerInfo = JsonHelper.FromJson<PlayerInfo>(jsonString);
            callback(playerInfo.ToList());
        }
    }

    public void GetPlayerRecord(string leaderboardId, System.Action<PlayerRecord> callback)
    {
        StartCoroutine(PlayerRecordRequest(leaderboardId, callback));
    }

    private IEnumerator PlayerRecordRequest(string leaderboardId, System.Action<PlayerRecord> callback = null)
    {
        UnityWebRequest www = UnityWebRequest.Get(backendUrlBase + "/users/profile/" + leaderboardId);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Get player record error: " + www.error);
            callback(null);
        }
        else
        {
            var jsonString = www.downloadHandler.text;
            PlayerRecord playerRecord = JsonUtility.FromJson<PlayerRecord>(jsonString);
            callback(playerRecord);
        }
    }

    public void GetLeaderboardURL(string leaderboardId)
    {
        return (frontendUrlBase + "?token=" + savedToken);
    }

    public void ShowLeaderboard(string leaderboardId = "", int leftMargin = 0, int topMargin = 180, int rightMargin = 0, int bottomMargin = 0)
    {   
        if (webViewObject == null)
        {
            StartCoroutine(LoadUp());
            webViewObject.SetMargins(leftMargin, topMargin, rightMargin, bottomMargin);
        }
        else
        {
            var UrlToRefresh = (frontendUrlBase + "?token=" + savedToken);
            webViewObject.EvaluateJS("window.location = '" + UrlToRefresh + "';");
            webViewObject.SetMargins(leftMargin, topMargin, rightMargin, bottomMargin);
            webViewObject.SetVisibility(true);
        }
    }

    public void HideLeaderboard()
    {
        webViewObject.SetVisibility(false);
    }

    public void ReportScore(double score, string leaderboard_id = "")
    { //Reports a score for this user
        var body = "{\"score\":" + score;
        if(leaderboard_id == ""){
            body += "}";
            StartCoroutine(SendPostRequest("/leaderboards/submit-individual-score", body));
        }
        else{
            body += ", \"leaderboard_id\": \"" + leaderboard_id + "\"}";
            StartCoroutine(SendPostRequest("/leaderboards/submit-individual-score", body));
        }

        ReportToPlatform(score, leaderboard_id);
    }

    public void UpdateSkill(string matchId, string[] placements)
    {
        var stringifiedPlacements = "[";
        for (var i = 0; i < placements.Length; i++)
        {
            var delimeter = "";
            if (i < placements.Length - 1) { delimeter = ", "; }
            stringifiedPlacements += ("\"" + placements[i] + "\"" + delimeter);
        }
        stringifiedPlacements += "]";


        var body = "{\"player_ids\":" + stringifiedPlacements + ", \"match_id\":" + matchId + "}";
        StartCoroutine(SendPostRequest("/leaderboards/submit-score", body));
    }


    private void ReportToPlatform(double score, string leaderboard_id)
    {
#if UNITY_IOS
    Social.ReportScore ((long)score, leaderboard_id, success => {
        Debug.Log(success ? "Reported score to GameCenter successfully" : "Failed to report ios score");
    });
#endif

#if UNITY_ANDROID
        Social.ReportScore((long)score, leaderboard_id, success =>
        {
            Debug.Log(success ? "Reported score to Google Play Services successfully" : "Failed to report android score");
        });
#endif
    }

    private IEnumerator SendPostRequest(string urlRelativePath, string body = "")
    {
        var www = new UnityWebRequest(backendUrlBase + urlRelativePath, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader(GetAuthorizationType(), savedToken);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Lucky post report error: " + www.error);
        }
        else
        {
            Debug.Log("Successful lucky post");
        }
    }

    private IEnumerator SignInAnonymously()
    {
        var bundle_id = Application.identifier;
        bundle_id = "94a4881a-3f6d-4365-8b21-ea7e7e55b908"; //MARK: Debug!

        var www = new UnityWebRequest(backendUrlBase + "/signup/anonymous", "POST");

        string bodyJsonString = "{ \"game_id\": \"" + bundle_id + "\", \"device_id\": \"" + SystemInfo.deviceUniqueIdentifier + "\" }";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.downloadHandler.text);
            Debug.Log("Anonymous signin error: " + www.error);
        }
        else
        {
            Debug.Log("Signed in!");
            savedToken = www.downloadHandler.text;
            PlayerPrefs.SetString("token", savedToken);

            currentPlayerId = getPlayerIdFromJWT(savedToken);
            PlayerPrefs.SetString("currentPlayerId", currentPlayerId);
        }
    }

    private string getPlayerIdFromJWT(string token)
    {
        var parts = token.Split('.');
        if (parts.Length > 2)
        {
            var decode = parts[1];
            var padLength = 4 - decode.Length % 4;
            if (padLength < 4)
            {
                decode += new string('=', padLength);
            }
            var bytes = System.Convert.FromBase64String(decode);
            var userInfo = System.Text.ASCIIEncoding.ASCII.GetString(bytes);

            if (userInfo.Contains("user_id"))
            {
                var playerId = userInfo.Split("\"user_id\":\"")[1].Split("\"")[0];
                return playerId;
            }

            //MARK: Debug!
            if (userInfo.Contains("nickname"))
            {
                var nickname = userInfo.Split("\"nickname\":\"")[1].Split("\"")[0];
                return nickname;
            }

        }
        return "";
    }

    private IEnumerator LoadUp()
    {
        savedToken = PlayerPrefs.GetString("token");
        Url = (frontendUrlBase + "?token=" + savedToken);
        // if(currentGalaxyLeaderboardID != ""){
        //   Url = Url + "&leaderboard_id=" + currentGalaxyLeaderboardID;
        // }

        webViewObject = (new GameObject("WebViewObject")).AddComponent<WebViewObject>();
        webViewObject.Init(
          cb: (msg) =>
          {
              Debug.Log(string.Format("CallFromJS[{0}]", msg));
          },
          err: (msg) =>
          {
              Debug.Log(string.Format("CallOnError[{0}]", msg));
          },
          httpErr: (msg) =>
          {
              Debug.Log(string.Format("CallOnHttpError[{0}]", msg));
          },
          started: (msg) =>
          {
              Debug.Log(string.Format("CallOnStarted[{0}]", msg));
          },
          hooked: (msg) =>
          {
              Debug.Log(string.Format("CallOnHooked[{0}]", msg));
          },
          ld: (msg) =>
          {
              Debug.Log("Loaded " + msg);
              if (msg.Contains("sdk_action=request_contacts"))
              {
                  GetContacts();
              }
              else if (msg.Contains("sdk_action=save_token"))
              {
                  string[] splitArray = msg.Split(new string[] { "token=" }, System.StringSplitOptions.None);
                  if (splitArray.Length > 1)
                  {
                      string tokenStart = splitArray[1];
                      string[] safeTokenArray = tokenStart.Split(new string[] { "&" }, System.StringSplitOptions.None);
                      string tokenTrimmed = safeTokenArray[0];

                      PlayerPrefs.SetString("token", tokenTrimmed);
                      savedToken = tokenTrimmed;

                      currentPlayerId = getPlayerIdFromJWT(savedToken);
                      PlayerPrefs.SetString("currentPlayerId", currentPlayerId);
                  }
              }
              else if(msg.Contains("sdk_action=signed_in")){
                    didSignIn(currentPlayerId);
              }
              else if(msg.Contains("sdk_action=avatar_edited")){
                    GetPlayerAvatarTexture((texture) => {
                        avatarDidChange(texture);
                    }, true);
              }
              else if(msg.Contains("sdk_action=info_changed")){ 
                    GetPlayerInfo((info) => {
                        infoDidChange(info);
                    });
              }
          },
          transparent: false,
          zoom: false,
          enableWKWebView: true,
          wkContentMode: 1,  // 0: recommended, 1: mobile, 2: desktop
          androidForceDarkMode: 1,  // 0: follow system setting, 1: force dark off, 2: force dark on
          wkAllowsLinkPreview: false
        );
        #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        webViewObject.bitmapRefreshCycle = 1;
        #endif
        webViewObject.SetMargins(0, topMargin, 0, 0);
        webViewObject.SetTextZoom(100);  // android only. cf. https://stackoverflow.com/questions/21647641/android-webview-set-font-size-system-default/47017410#47017410
        webViewObject.SetVisibility(true);

        #if !UNITY_WEBPLAYER && !UNITY_WEBGL
        if (Url.StartsWith("http"))
        {
            webViewObject.LoadURL(Url.Replace(" ", "%20"));
        }
        else
        {
            var exts = new string[]{
            ".jpg",
            ".js",
            ".html"  // should be last
        };
            foreach (var ext in exts)
            {
                var url = Url.Replace(".html", ext);
                var src = System.IO.Path.Combine(Application.streamingAssetsPath, url);
                var dst = System.IO.Path.Combine(Application.persistentDataPath, url);
                byte[] result = null;
                if (src.Contains("://"))
                {  // for Android
                    #if UNITY_2018_4_OR_NEWER
                    var unityWebRequest = UnityWebRequest.Get(src);
                    yield return unityWebRequest.SendWebRequest();
                    result = unityWebRequest.downloadHandler.data;
                    #else
                    var www = new WWW(src);
                    yield return www;
                    result = www.bytes;
                    #endif
                }
                else
                {
                    result = System.IO.File.ReadAllBytes(src);
                }
                System.IO.File.WriteAllBytes(dst, result);
                if (ext == ".html")
                {
                    webViewObject.LoadURL("file://" + dst.Replace(" ", "%20"));
                    break;
                }
            }
        }
        #else
        if (Url.StartsWith("http")) {
            webViewObject.LoadURL(Url.Replace(" ", "%20"));
        } else {
            webViewObject.LoadURL("StreamingAssets/" + Url.Replace(" ", "%20"));
        }
        #endif
        yield break;
    }

    private void GetContacts()
    { 
        Contacts.LoadContactList(onDone, onLoadFailed);
    }

    private void onLoadFailed(string reason)
    {
        Debug.Log("Failed for reason: " + reason);
    }

    private void onDone()
    {
        Debug.Log("Count: " + Contacts.ContactsList.Count);
        Contact c = Contacts.ContactsList[0];
        Debug.Log("First Contact First Number: " + c.Phones[0].Number);
        //Sanatize and upload contacts here
        var jsonToSend = "{\"contacts\": [";
        for(var i = 0; i < Contacts.ContactsList.Count; i++){
            var contact = Contacts.ContactsList[i];
            var name = contact.Name;
            var numberArray = contact.Phones.Select(x => x.Number).ToArray();
            
            for(var j = 0; j < numberArray.Length; j++){
                var number = numberArray[j];
                jsonToSend += "{\"name\": \"" + name + "\", \"phone_number\": \"" + number + "\"}";
                if(i != Contacts.ContactsList.Count - 1 || j != numberArray.Length - 1){
                    jsonToSend += ",";
                }
            }
        }
        jsonToSend += "]}";
        Debug.Log(jsonToSend);
        StartCoroutine(SendPostRequest("/users/update_contacts", jsonToSend));
    }

    private String GetAuthorizationType()
    {
        var parts = savedToken.Split('.');
        if (parts.Length > 2)
        {
            var decode = parts[1];
            var padLength = 4 - decode.Length % 4;
            if (padLength < 4)
            {
                decode += new string('=', padLength);
            }
            var bytes = System.Convert.FromBase64String(decode);
            var userInfo = System.Text.ASCIIEncoding.ASCII.GetString(bytes);

            if (userInfo.Contains("anonymous"))
            {
                var anonymous = userInfo.Split("\"anonymous\":\"")[1].Split("\"")[0];
                if (anonymous != "true")
                {
                    return "Super-Authorization";
                }
            }
        }
        return "Anonymous-Authorization";
    }

}

// //Marked private during development
// private void GetLeaderboard(System.Action<List<Player>> callback){ //Gets Leaderboard as JSON
//   StartCoroutine(GetLeaderboardRequest("overview", callback));
// }

// private IEnumerator GetLeaderboardRequest(string type = "overview", System.Action<List<Player>> callback = null){ //"friends" "tier" or "overview"
//     UnityWebRequest www = UnityWebRequest.Get(backendUrlBase + "/leaderboards");
//     yield return www.SendWebRequest();
//     if (www.result != UnityWebRequest.Result.Success)
//     {
//         Debug.Log("Get leaderboard error: " + www.error);
//         callback(null);
//     }
//     else
//     {
//       var jsonString = www.downloadHandler.text;
//       List<Player> leaderboard = JsonUtility.FromJson<List<Player>>(jsonString);
//       callback(leaderboard);
//     }
// }
