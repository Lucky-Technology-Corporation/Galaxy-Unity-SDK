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

    public delegate void DidBuyCurrency(int amount);
    public DidBuyCurrency didBuyCurrency;

    private string currentGalaxyLeaderboardID = "";
    private WebViewObject webViewObject;
    private string Url;
    private string savedToken = "";
    private string backendUrlBase = "https://api.galaxy.us/api/v1";
    private string frontendUrlBase = "https://app.galaxy.us";
    private string currentPlayerId = "";
    private Texture2D cachedProfileImage;
    private GameObject touchBlocker;
    private Button cancelButton;

    private bool shouldCloseOnNextSignInNotification = false;
    private bool gameIsActive = false;

    // Start is called before the first frame update
    void Awake()
    {
        savedToken = PlayerPrefs.GetString("token");
        currentPlayerId = PlayerPrefs.GetString("currentPlayerId");
        if (savedToken == null || savedToken == "")
        {
            StartCoroutine(SignInAnonymously());
        }
        else
        {
            GetPlayerAvatarTexture((texture) =>
            {
                if (avatarDidChange != null) { avatarDidChange(texture); }
            }, true);

            GetPlayerInfo((playerInfo) =>
            {
                if (infoDidChange != null) { infoDidChange(playerInfo); }
            });

            var url = (frontendUrlBase + "/leaderboards/?token=" + savedToken);
            StartCoroutine(LoadUp(url, true));
        }

        DontDestroyOnLoad(gameObject);
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
            StartCoroutine(DownloadImage(imageUrl, callback));
        }
    }

    private IEnumerator DownloadImage(string MediaUrl, System.Action<Texture2D> callback)
    {
        if (savedToken == null || savedToken == "")
        {
            yield return new WaitUntil(() => savedToken != null && savedToken != "");
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl))
        {
            yield return request.SendWebRequest();
            if ((request.result == UnityWebRequest.Result.ConnectionError) || (request.result == UnityWebRequest.Result.ProtocolError))
            {
                callback(null);
            }
            else
            {
                callback(((DownloadHandlerTexture)request.downloadHandler).texture);
            }
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
            if (response.Trim() == "[]") { callback(new List<PlayerInfo>()); return; }
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
        return (frontendUrlBase + "/leaderboards/" + leaderboardId + "?token=" + savedToken);
    }

    public void SignIn(bool shouldCloseOnCompletion)
    {
        shouldCloseOnNextSignInNotification = shouldCloseOnCompletion;
        var urlToSignIn = frontendUrlBase + "/sign_in";
        SetupWebview(urlToSignIn, 0, 70, 0, 0);
    }

    public void SaveState(string body = "")
    {
        SendRequest("/users/game_state", "{\"state\":" + body + "}");
    }

    public void GetState(System.Action<string> callback)
    {
        SendRequest("/users/game_state", "", "GET", (response) =>
        {
            callback(response);
        });
    }

    public void ShowLeaderboard(string leaderboardId = "", int leftMargin = 0, int topMargin = 0, int rightMargin = 0, int bottomMargin = 0)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogError("No internet connection");
            return;
        }
        var UrlToRefresh = (frontendUrlBase + "/leaderboards/" + leaderboardId + "?token=" + savedToken);
        SetupWebview(UrlToRefresh, leftMargin, topMargin, rightMargin, bottomMargin);
    }

    public void Hide()
    {
        Destroy(webViewObject);
        Destroy(touchBlocker);
        webViewObject = null;
        touchBlocker = null;
    }

    public void HideLeaderboard()
    {
        Hide();
    }

    private void SetupWebview(string url = "", int leftMargin = 0, int topMargin = 0, int rightMargin = 0, int bottomMargin = 0, bool skipTouchBlocker = false)
    {
        if (webViewObject == null)
        {
            StartCoroutine(LoadUp(url));
            webViewObject.SetMargins(leftMargin, topMargin, rightMargin, bottomMargin);
        }
        else
        {
            webViewObject.EvaluateJS("if(window.location != '" + url + "') { window.location = '" + url + "'; }");
            webViewObject.SetMargins(leftMargin, topMargin, rightMargin, bottomMargin);
            webViewObject.SetVisibility(true);
        }

        if (skipTouchBlocker) { return; }
        if (touchBlocker == null)
        {
            touchBlocker = new GameObject();
            touchBlocker.name = "TouchBlocker";
            var canvas = touchBlocker.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            touchBlocker.AddComponent<GraphicRaycaster>();
            var image = touchBlocker.AddComponent<Image>();
            var hMargin = leftMargin + rightMargin;
            var vMargin = topMargin + bottomMargin;
            image.transform.localScale = new Vector3((Screen.width - hMargin) / 100, (Screen.height - vMargin) / 100, 1);
            image.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            image.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0);
            image.GetComponent<RectTransform>().pivot = new Vector2(0, 0);
            image.GetComponent<RectTransform>().position = new Vector3(leftMargin, topMargin, 0);
            image.color = new Color(0, 0, 0, 0.5f);

            var loadingTextObject = new GameObject();
            var loadingText = loadingTextObject.AddComponent<Image>();
            loadingTextObject.name = "CancelButton";
            loadingTextObject.transform.SetParent(touchBlocker.transform);
            loadingTextObject.transform.localScale = new Vector3(1, 1, 1);
            loadingTextObject.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
            loadingTextObject.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
            loadingTextObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            loadingTextObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
            loadingTextObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 85);
            loadingText.color = new Color(1, 1, 1, 1);
            loadingText.sprite = Resources.Load<Sprite>("loading");
        }
        else
        {
            touchBlocker.SetActive(true);
            cancelButton.interactable = true;
        }
    }

    public void OnMouseDown()
    {
        return;
    }

    public void ReportScore(double score, string leaderboard_id = "", System.Action<PlayerRecord> callback = null)
    {
        //Save scores if offline
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("No internet connection, saving score to be reported on next score submission");
            string savedScoresList = PlayerPrefs.GetString("cachedScores") ?? "";
            savedScoresList += score.ToString() + "|" + leaderboard_id + ",";
            PlayerPrefs.SetString("cachedScores", savedScoresList);
            return;
        }

        //Get and report saved offline scores
        string savedScores = PlayerPrefs.GetString("cachedScores") ?? "";
        if (savedScores != "")
        {
            List<string> cachedList = StringToList(savedScores, ",");
            foreach (string cachedScore in cachedList)
            {
                var savedScore = cachedScore.Split('|')[0];
                var savedLeaderboard = cachedScore.Split('|')[1];
                MakeReport(double.Parse(savedScore), savedLeaderboard);
            }
        }
        PlayerPrefs.SetString("cachedScores", "");

        //Report this score
        MakeReport(score, leaderboard_id, callback);
    }

    private List<string> StringToList(string message, string seperator)
    {
        List<string> ExportList = new List<string>();
        string tok = "";
        foreach (char character in message)
        {
            tok = tok + character;
            if (tok.Contains(seperator))
            {
                tok = tok.Replace(seperator, "");
                ExportList.Add(tok);
                tok = "";
            }
        }
        return ExportList;
    }

    private void MakeReport(double score, string leaderboard_id = "", System.Action<PlayerRecord> callback = null)
    {
        var body = "{\"score\":" + score;
        if (leaderboard_id == "")
        {
            body += "}";
            SendRequest("/leaderboards/submit-individual-score", body, "POST", (response) =>
            {
                PlayerRecord playerRecord = JsonUtility.FromJson<PlayerRecord>(response);
                if (callback != null) { callback(playerRecord); }
            });
        }
        else
        {
            body += ", \"leaderboard_id\": \"" + leaderboard_id + "\"}";
            SendRequest("/leaderboards/submit-individual-score", body, "POST", (response) =>
            {
                PlayerRecord playerRecord = JsonUtility.FromJson<PlayerRecord>(response);
                if (callback != null) { callback(playerRecord); }
            });
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
        Debug.Log(success ? "Reported score to GameCenter successfully" : "Failed to report to GameCenter");
    });
#endif

#if UNITY_ANDROID
    Social.ReportScore((long)score, leaderboard_id, success =>
    {
      Debug.Log(success ? "Reported score to Google Play Services successfully" : "Failed to report to Google Play Services");
    });
#endif
    }


    private void SendRequest(string urlRelativePath, string body = "", string method = "POST", System.Action<string> callback = null)
    {
        StartCoroutine(MakeRequest(urlRelativePath, body, method, callback));
    }

    private IEnumerator MakeRequest(string urlRelativePath, string body = "", string method = "POST", System.Action<string> callback = null)
    {
        if (savedToken == null || savedToken == "")
        {
            yield return new WaitUntil(() => savedToken != null && savedToken != "");
        }

        using (UnityWebRequest www = new UnityWebRequest(backendUrlBase + urlRelativePath, method))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader(GetAuthorizationType(), savedToken);
            www.SetRequestHeader("Bundle-ID", Application.identifier);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Internet may be disconnected. A request failed: " + www.error);
                callback(null);
            }
            if (callback != null)
            {
                var callbackPayload = www.downloadHandler.text;
                if(callbackPayload == null){ callbackPayload = ""; }
                callback(callbackPayload);
            }
        }
    }

    private IEnumerator SignInAnonymously()
    {
        var bundle_id = Application.identifier;
        using (UnityWebRequest www = new UnityWebRequest(backendUrlBase + "/signup/anonymous", "POST"))
        {
            string bodyJsonString = "{ \"bundle_id\": \"" + bundle_id + "\", \"device_id\": \"" + SystemInfo.deviceUniqueIdentifier + "\" }";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error creating an anonymous account: " + www.error);
            }
            else
            {
                savedToken = www.downloadHandler.text;
                PlayerPrefs.SetString("token", savedToken);

                currentPlayerId = getPlayerIdFromJWT(savedToken);
                PlayerPrefs.SetString("currentPlayerId", currentPlayerId); 
                
                GetPlayerAvatarTexture((texture) =>
                {
                    if (avatarDidChange != null) { avatarDidChange(texture); }
                }, true);

                GetPlayerInfo((playerInfo) =>
                {
                    if (infoDidChange != null) { infoDidChange(playerInfo); }
                });
                var url = (frontendUrlBase + "/leaderboards/?token=" + savedToken);
                StartCoroutine(LoadUp(url, true));
            }
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

    private IEnumerator LoadUp(string Url, bool loadInvisibly = false)
    {
        webViewObject = FindObjectOfType<GalaxyController>().gameObject.AddComponent<WebViewObject>();
        webViewObject.Init(
          cb: (msg) =>
          {
          },
          err: (msg) =>
          {
              Hide();
          },
          httpErr: (msg) =>
          {
              Hide();
          },
          started: (msg) =>
          {
              if (msg.Contains("close_window"))
              {
                  webViewObject.SetVisibility(false);
                  touchBlocker.SetActive(false);
              }
          },
          hooked: (msg) =>
          {
          },
          ld: (msg) =>
          {
              if (cancelButton) { cancelButton.interactable = false; }

              if (!loadInvisibly)
              {
                  touchBlocker.GetComponent<Image>().color = new Color(0, 0, 0, 1);
              }
              //First check for a new token
              if (msg.Contains("save_token"))
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
              //Then check for other SDK actions
              if (msg.Contains("sdk_action"))
              {

                  if (msg.Contains("request_contacts"))
                  {
                      GetContacts();
                  }
                  if (msg.Contains("signed_in"))
                  {
                      if (shouldCloseOnNextSignInNotification)
                      {
                          shouldCloseOnNextSignInNotification = false;
                          Hide();
                      }
                      if (didSignIn != null) { didSignIn(currentPlayerId); }
                  }
                  if (msg.Contains("avatar_edited"))
                  {
                    GetPlayerAvatarTexture((texture) =>
                    {
                        if (avatarDidChange != null) { avatarDidChange(texture); }
                    }, true);

                    GetPlayerInfo((playerInfo) =>
                    {
                        if (infoDidChange != null) { infoDidChange(playerInfo); }
                    });
                  }

                  if (msg.Contains("close_window"))
                  {
                      Hide();
                      if (userDidClose != null) { userDidClose(); }
                  }

              }
          },
          transparent: true,
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
        if (loadInvisibly)
        {
            webViewObject.SetVisibility(false);
        }
        else
        {
            webViewObject.SetVisibility(true);
        }

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
                    using (UnityWebRequest unityWebRequest = UnityWebRequest.Get(src))
                    {
                        yield return unityWebRequest.SendWebRequest();
                        result = unityWebRequest.downloadHandler.data;
                    }
#else
                    using (var www = new WWW(src))
                    {
                        yield return www;
                        result = www.bytes;
                    }
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
        Debug.LogError("Error getting contacts: " + reason);
    }

    private void onDone()
    {
        Contact c = Contacts.ContactsList[0];
        //Sanatize and upload contacts here
        var jsonToSend = "{\"contacts\": [";
        for (var i = 0; i < Contacts.ContactsList.Count; i++)
        {
            var contact = Contacts.ContactsList[i];
            var name = contact.Name;
            var numberArray = contact.Phones.Select(x => x.Number).ToArray();

            for (var j = 0; j < numberArray.Length; j++)
            {
                var number = numberArray[j];
                jsonToSend += "{\"name\": \"" + name + "\", \"phone_number\": \"" + number + "\"}";
                if (i != Contacts.ContactsList.Count - 1 || j != numberArray.Length - 1)
                {
                    jsonToSend += ",";
                }
            }
        }
        jsonToSend += "]}";
        SendRequest("/users/update_contacts", jsonToSend);
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
                var anonymous = userInfo.Split("\"anonymous\":")[1].Split(",\"")[0];
                if (anonymous != "true")
                {
                    return "Super-Authorization";
                }
            }
        }
        return "Anonymous-Authorization";
    }

}