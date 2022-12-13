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

namespace GalaxySDK{
    public class GalaxyController : MonoBehaviour
    {
        [Header("Publishable Key (optional)")]
        [Tooltip("Leave blank if you don't have one")]
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
        private string backendUrlBase = "https://api.galaxysdk.com/api/v1";
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

        void Start(){
            if(SDKKey == null || SDKKey == ""){
                // Debug.Log("No SDK Key provided so Galaxy will use the default account");
            }
            BeginReportingAnalytics();
        }

        void OnApplicationPause(bool isPaused)
        {
            if(isPaused){
                EndReportingAnalytics();
            }
            else{
                BeginReportingAnalytics();
            }
        }

        void OnApplicationQuit() 
        {
            EndReportingAnalytics();
        }

        private void BeginReportingAnalytics(){
            //Don't restart session if it's already running
            if(gameIsActive){ return; }
            gameIsActive = true;

            //Set start time
            System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            int currentTime = (int)Math.Round((DateTime.UtcNow - epochStart).TotalSeconds);
            PlayerPrefs.SetInt("sessionStart", currentTime);

            //Attempt to save any unsaved sessions
            attemptToSaveUnsavedSessions();
        }

        private void EndReportingAnalytics(){
            gameIsActive = false;

            //Get and delete saved start time
            int sessionStart = PlayerPrefs.GetInt("sessionStart");
            if(sessionStart == null || sessionStart == 0){ return; }
            PlayerPrefs.DeleteKey("sessionStart");

            //Calculate session body
            System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            int currentTime = (int)Math.Round((DateTime.UtcNow - epochStart).TotalSeconds);
            int sessionLength = currentTime - sessionStart;
            var sessionBody = "{\"session_length\": " + sessionLength + ", \"ended_at\":" + currentTime +  "}";

            //Save locally
            addToUnsavedSessionArray(sessionBody);

            //Save remotely if possible
            attemptToSaveUnsavedSessions();
        }

        private void addToUnsavedSessionArray(string session){
            var unsavedSessions = PlayerPrefs.GetString("unsavedSessions") ?? "";
            unsavedSessions += (session + ",");
            PlayerPrefs.SetString("unsavedSessions", unsavedSessions);
        }

        private void attemptToSaveUnsavedSessions(){
            //Ensure internet is connected and there is an unsaved session
            if(Application.internetReachability == NetworkReachability.NotReachable){ return; }
            var listOfSessions = PlayerPrefs.GetString("unsavedSessions");
            if(listOfSessions == null|| listOfSessions == ""){ return; }
            
            listOfSessions = listOfSessions.Substring(0, listOfSessions.Length - 1); //remove trailing comma
            var unsavedSessions = "{\"sessions\": [" + listOfSessions + "]}";
            SendRequest("/analytics/report_session", unsavedSessions, "POST", (response) => {
                if(response != null){
                    PlayerPrefs.SetString("unsavedSessions", "");
                }
            });
        }

        public void ReportRevenue(double amount, string product_id = ""){
            var body = "{\"amount\": " + amount;
            if(product_id == ""){
                body += "}";
            }
            else{
                body += ", \"product_id\": \"" + product_id + "\"}";
            } 
            
            SendRequest("/analytics/report_revenue", body, "POST", (response) => {
                if(response == null){
                    Debug.LogError("[Galaxy]: Error reporting revenue");
                }
            });
        }

        public void ReportAd(double amount = 1){
            var body = "{\"amount\": " + amount + "}";

            SendRequest("/analytics/report_ad", body, "POST", (response) => {
                if(response == null){
                    Debug.LogError("[Galaxy]: Error reporting ad");
                }
            });
        }
        public void ReportEvent(string name, double amount = 0.0){
            var body = "{\"key\": \"" + name;
            if(amount == 0.0){
                body += "\"}";
            }
            else{
                body += "\", \"value\": \"" + amount + "\"}";
            } 
            SendRequest("/analytics/report_event", body, "POST", (response) => {
                if(response == null){
                    Debug.LogError("[Galaxy]: Error reporting event. Have you created an event with the name " + name + " in the dashboard?");
                }
            });
        }

        public void GetValue(string name, System.Action<float?> callback){
            string savedValue = PlayerPrefs.GetString("_galaxy_dv_"+name);
            Debug.Log(savedValue);
            if(savedValue != null && savedValue != ""){
                callback(float.Parse(savedValue));
            }
            else{
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Debug.LogError("[Galaxy]: No internet connection and no cached value for " + name + ".");
                    callback(null);
                }
                else{
                    var body = "{\"name\": \"" + name + "\"}";
                    Debug.Log(body);
                    SendRequest("/analytics/get_value", body, "POST", (response) => {
                        if(response == null){
                            Debug.LogError("[Galaxy]: Error getting dynamic value. Have you created a dynamic value with the name " + name + " in the dashboard?");
                            callback(null);
                        }
                        else{
                            var info = JsonUtility.FromJson<DynamicValue>(response);
                            Debug.Log(JsonUtility.ToJson(info, true));
                            float floatResult = info.value;
                            PlayerPrefs.SetString("_galaxy_dv_"+name, floatResult.ToString());
                            callback(floatResult);
                        }
                    });
                }
            }
        }

        public void GetLeaderboardData(System.Action<Leaderboard> callback, string leaderboard_id = ""){
            if(leaderboard_id == ""){
                if(PlayerPrefs.GetString("galaxy_default_leaderboard_id") != null){
                    leaderboard_id = PlayerPrefs.GetString("galaxy_default_leaderboard_id");
                    MakeLeaderboardDataRequest(leaderboard_id, callback);
                }
                else{
                    var bundle_id = Application.identifier;
                    SendRequest("/lookup/bundle_id/" + bundle_id, "", "GET", (response) => {
                        if(response == null){
                            Debug.LogError("[Galaxy]: Error getting leaderboard data");
                        }
                        else{
                            var info = JsonUtility.FromJson<LeaderboardID>(response);
                            PlayerPrefs.SetString("galaxy_default_leaderboard_id", info.leaderboard_id);
                            MakeLeaderboardDataRequest(info.leaderboard_id, callback);
                        }
                    });
                }
            }
        }

        private void MakeLeaderboardDataRequest(string leaderboard_id, System.Action<Leaderboard> callback){
            SendRequest("/leaderboards/"+leaderboard_id+"/public", "", "GET", (response) => {
                if(response == null){
                    Debug.LogError("[Galaxy]: Error getting leaderboard data");
                    callback(null);
                }
                else{
                    var info = JsonUtility.FromJson<Leaderboard>(response);
                    callback(info);
                }
            });
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

        public void Show(){
            ShowLeaderboard();
        }
        public void ShowLeaderboard(string leaderboardId = "", int leftMargin = 0, int topMargin = 0, int rightMargin = 0, int bottomMargin = 0, bool hideCloseButton = false)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogError("[Galaxy]: No internet connection");
                return;
            }
            var UrlToRefresh = (frontendUrlBase + "/leaderboards/" + leaderboardId + "?token=" + savedToken);
            if(hideCloseButton == true){
                UrlToRefresh += "&hideCloseButton=true";
            }
            SetupWebview(UrlToRefresh, leftMargin, topMargin, rightMargin, bottomMargin);
        }

        public void ShowPayment(int leftMargin = 0, int topMargin = 0, int rightMargin = 0, int bottomMargin = 0)
        {
            if (didBuyCurrency == null) { Debug.LogError("didBuyCurrency delegate not set"); return; }
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogError("[Galaxy]: No internet connection");
                return;
            }
            var UrlToRefresh = (frontendUrlBase + "/points?token=" + savedToken);
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
                if(cancelButton) { cancelButton.interactable = true; }
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
                if(callback == null){
                    SendRequest("/leaderboards/submit-individual-score-async", body, "POST");
                }
                else {
                    SendRequest("/leaderboards/submit-individual-score", body, "POST", (response) =>
                    {
                        PlayerRecord playerRecord = JsonUtility.FromJson<PlayerRecord>(response);
                        if (callback != null) { callback(playerRecord); }
                    });
                }
            }
            else
            {
                body += ", \"leaderboard_id\": \"" + leaderboard_id + "\"}";
                if(callback == null){
                    SendRequest("/leaderboards/submit-individual-score-async", body, "POST");
                }
                else {
                    SendRequest("/leaderboards/submit-individual-score", body, "POST", (response) =>
                    {
                        PlayerRecord playerRecord = JsonUtility.FromJson<PlayerRecord>(response);
                        if (callback != null) { callback(playerRecord); }
                    });
                }
            }

            // ReportToPlatform(score, leaderboard_id);
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
    // #if UNITY_IOS
    //     Social.ReportScore ((long)score, leaderboard_id, success => {
    //         Debug.Log(success ? "Reported score to GameCenter successfully" : "Failed to report to GameCenter");
    //     });
    // #endif

    // #if UNITY_ANDROID
    //     Social.ReportScore((long)score, leaderboard_id, success =>
    //     {
    //     Debug.Log(success ? "Reported score to Google Play Services successfully" : "Failed to report to Google Play Services");
    //     });
    // #endif
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
            if(SDKKey == null || SDKKey == ""){
                SDKKey = "afd8434b-e433-420c-8914-14f77eb07a95"; //default key
            }
            using (UnityWebRequest www = new UnityWebRequest(backendUrlBase + urlRelativePath, method))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader(GetAuthorizationType(), savedToken);
                www.SetRequestHeader("Publishable-Key", SDKKey);
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Attempted to make a request to " + urlRelativePath + " but failed");
                    Debug.Log("Internet may be disconnected. A request failed: " + www.error);
                    if(callback != null) { callback(null); }
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
            if(bundle_id == null || bundle_id == ""){
                Debug.LogError("[Galaxy]: You need to set a bundle ID in File > Build Settings > Player Settings > Other Settings > Identification > Bundle Identifier");
            }
            else{
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
                        Debug.LogError("[Galaxy]: Error creating an anonymous account: " + www.error);
                    }
                    else
                    {
                        Debug.Log("[Galaxy]: Signed into anonymous account");
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
                Debug.Log("[Galaxy] LoadUp Error 1 " + msg);
                Hide();
            },
            httpErr: (msg) =>
            {
                Debug.Log("[Galaxy] LoadUp Error 2 " + msg);
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
                // webViewObject.EvaluateJS(@"document.body.style.background = 'black';");
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

                    if (msg.Contains("invite_friend"))
                    {
                        var phoneNumber = msg.Split("phone_number=")[1].Split("&")[0];
                        var name = msg.Split("name=")[1].Split("&")[0];
                        var iOSID = msg.Split("ios_id=")[1].Split("&")[0];
                        var androidID = msg.Split("android_id=")[1].Split("&")[0];
                        var gameName = msg.Split("game_name=")[1].Split("&")[0];

                        string iosLink = "https://apps.apple.com/app/" + iOSID;
                        string androidLink = "https://play.google.com/store/apps/details?id=" + androidID;

                        string message = "Hey - I'm playing a game called " + gameName + " and I think you'd like it. Download it here: ";
    #if UNITY_ANDROID
                        message += androidLink;
                        string URL = string.Format("sms:{0}?body={1}", phoneNumber, System.Uri.EscapeDataString(message));
                        Application.OpenURL(URL);
    #endif

    #if UNITY_IOS
                        message += iosLink;
                        string URL = string.Format("sms:{0}?&body={1}",phoneNumber,System.Uri.EscapeDataString(message));
                        Application.OpenURL(URL);
    #endif
                        Hide();
                    }

                    if (msg.Contains("convert_currency"))
                    {
                        var amount = msg.Split("amount=")[1].Split("&")[0];
                        var points = msg.Split("points=")[1].Split("&")[0];
                        var currencyName = msg.Split("currency=")[1].Split("&")[0];

                        didBuyCurrency(int.Parse(amount));
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
            Debug.LogError("[Galaxy]: Error getting contacts: " + reason);
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

    class DynamicValue{
        public float value;
        public DynamicValue(string name, float value){
            this.value = value;
        }
    }

    public class Leaderboard{
        public Record[] records;
    }
    public class Record{
        public string id;
        public int rank;
        public float score;
        public string nickname;
        public string avatar_url;
    }

    class LeaderboardID{
        public string leaderboard_id;
    }
}