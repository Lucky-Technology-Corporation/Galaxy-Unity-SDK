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
    [Header("Publishable Key")]
    [Tooltip("Get this from the developer dashboard")]
    public string SDKKey;


    public delegate void AvatarDidChange(Texture2D newAvatar);
    public AvatarDidChange avatarDidChange;

    public delegate void DidSignIn(string playerId);
    public DidSignIn didSignIn;

    public delegate void InfoDidChange(PlayerInfo info);
    public InfoDidChange infoDidChange;

    public delegate void UserDidClose();
    public UserDidClose userDidClose;

    private string currentGalaxyLeaderboardID = "";
    private WebViewObject webViewObject;
    private string Url;
    // private int topMargin = 180;
    private string savedToken = "";
    private string backendUrlBase = "https://api.galaxy.us/api/v1";
    private string frontendUrlBase = "https://app.galaxy.us";
    private string currentPlayerId = "";
    private Texture2D cachedProfileImage;

    private bool shouldCloseOnNextSignInNotification = false;

    // Start is called before the first frame update
    void Awake()
    {
        Debug.Log("Initializing LuckyBoard");
        savedToken = PlayerPrefs.GetString("token");
        currentPlayerId = PlayerPrefs.GetString("currentPlayerId");
        if (savedToken == null || savedToken == "")
        {
            Debug.Log("Signing in anonymously...");
            StartCoroutine(SignInAnonymously());
        }
    }


    public string GetPlayerID()
    {
        return currentPlayerId;
    }

    public void SetPlayerID(string newId)
    {
        var body = "{\"id\":\"" + newId + "\"}";
        SendRequest("/users/update_alias", body, "PATCH");
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
        SendRequest("/users/profile", "", "GET", (response) =>
        {
            var info = JsonUtility.FromJson<PlayerInfo>(response);
            callback(info);
        });
    }

    public void GetPlayerFriends(System.Action<List<PlayerInfo>> callback)
    {
        SendRequest("/users/friends", "", "GET", (response) =>
        {
            PlayerInfo[] playerInfo = JsonUtility.FromJson<PlayerInfo[]>(response);
            callback(playerInfo.ToList());
        });
    }

    public void GetPlayerRecord(string leaderboardId, System.Action<PlayerRecord> callback)
    {
        SendRequest("/users/profile/" + leaderboardId, "", "GET", (response) =>
        {
            PlayerRecord playerRecord = JsonUtility.FromJson<PlayerRecord>(response);
            callback(playerRecord);
        });
    }

    public string GetLeaderboardURL(string leaderboardId)
    {
        return (frontendUrlBase + "/leaderboards/"+leaderboardId + "?token=" + savedToken);
    }

    public void SignIn(bool shouldCloseOnCompletion){
        shouldCloseOnNextSignInNotification = shouldCloseOnCompletion;
        var urlToSignIn = frontendUrlBase + "/sign_in";
        SetupWebview(urlToSignIn, 0, 180, 0, 0);
    }

    public void ShowLeaderboard(string leaderboardId = "", int leftMargin = 0, int topMargin = 0, int rightMargin = 0, int bottomMargin = 0)
    {   
        var UrlToRefresh = (frontendUrlBase + "/leaderboards/"+leaderboardId+"?token=" + savedToken);
        SetupWebview(UrlToRefresh, leftMargin, topMargin, rightMargin, bottomMargin);
    }

    public void HideLeaderboard()
    {
        webViewObject.SetVisibility(false);
    }

    private void SetupWebview(string url = "", int leftMargin = 0, int topMargin = 0, int rightMargin = 0, int bottomMargin = 0){
        if (webViewObject == null)
        {
            StartCoroutine(LoadUp(url));
            webViewObject.SetMargins(leftMargin, topMargin, rightMargin, bottomMargin);
        }
        else
        {
            webViewObject.EvaluateJS("window.location = '" + url + "';");
            webViewObject.SetMargins(leftMargin, topMargin, rightMargin, bottomMargin);
            webViewObject.SetVisibility(true);
        }
    }

    public void ReportScore(double score, string leaderboard_id = "")
    { //Reports a score for this user
        var body = "{\"score\":" + score;
        if(leaderboard_id == ""){
            body += "}";
            SendRequest("/leaderboards/submit-individual-score", body);
        }
        else{
            body += ", \"leaderboard_id\": \"" + leaderboard_id + "\"}";
            SendRequest("/leaderboards/submit-individual-score", body);
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
        SendRequest("/leaderboards/submit-score", body);
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


    private void SendRequest(string urlRelativePath, string body = "", string method = "POST", System.Action<string> callback = null){
        StartCoroutine(MakeRequest(urlRelativePath, body, method, callback));
    }

    private IEnumerator MakeRequest(string urlRelativePath, string body = "", string method = "POST", System.Action<string> callback = null)
    {
        var www = new UnityWebRequest(backendUrlBase + urlRelativePath, method);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader(GetAuthorizationType(), savedToken);
        www.SetRequestHeader("Publishable-Key", SDKKey);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Failed to post to " + urlRelativePath + " becuase " + www.error);
        }
        Debug.Log("gonna call if " + callback + " is not null");
        if(callback != null){
            Debug.Log("calling...");
            callback(www.downloadHandler.text);
        }
    }

    private IEnumerator SignInAnonymously()
    {
        var bundle_id = Application.identifier;
        bundle_id = "cb44bec7-9bad-4c2f-a5d3-b9ff0517a70c";

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

            GetPlayerAvatarTexture((texture) => {
                Debug.Log("avatarDidChange");
                avatarDidChange(texture);
            }, true);
            
            GetPlayerInfo((playerInfo) => {
                Debug.Log("infoDidChange");
                Debug.Log(playerInfo);
                infoDidChange(playerInfo);
            });

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

        }
        return "";
    }

    private IEnumerator LoadUp(string Url)
    {
        // savedToken = PlayerPrefs.GetString("token");
        // Url = (frontendUrlBase + "?token=" + savedToken);
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
            //First check for a new token
            if (msg.Contains("save_token")){
                Debug.Log("save_token");
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
                    Debug.Log("Token was saved");
                }
            }

            //Then check for other SDK actions
            if(msg.Contains("sdk_action")){
                if (msg.Contains("request_contacts")){
                    Debug.Log("request_contacts");
                    GetContacts();
                }
                if(msg.Contains("signed_in")){
                    Debug.Log("signed_in");
                    if(shouldCloseOnNextSignInNotification){
                        shouldCloseOnNextSignInNotification = false;
                        HideLeaderboard();
                    }
                    didSignIn(currentPlayerId);
                }
                if(msg.Contains("avatar_edited")){
                    GetPlayerAvatarTexture((texture) => {
                        Debug.Log("avatarDidChange");
                        avatarDidChange(texture);
                    }, true);
                    
                    GetPlayerInfo((playerInfo) => {
                        Debug.Log("infoDidChange");
                        Debug.Log(playerInfo);
                        infoDidChange(playerInfo);
                    });
                }

                if(msg.Contains("close_window")){
                    HideLeaderboard();
                    userDidClose();
                }
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
        webViewObject.SetMargins(0, 0, 0, 0);
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
        SendRequest("/users/update_contacts", jsonToSend);
    }

    private String GetAuthorizationType()
    {
        var parts = savedToken.Split('.');
        Debug.Log(parts.Length);
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
            Debug.Log(userInfo);

            if (userInfo.Contains("anonymous"))
            {
                var anonymous = userInfo.Split("\"anonymous\":")[1].Split(",\"")[0];
                Debug.Log(anonymous);
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
