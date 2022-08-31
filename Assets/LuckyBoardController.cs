using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

#if UNITY_2018_4_OR_NEWER
using UnityEngine.Networking;
#endif
using UnityEngine.UI;

//Make this a prefab!
public class LuckyBoardController : MonoBehaviour
{
    public Canvas canvas;
    WebViewObject webViewObject;
    private string Url;
    private int topMargin = 180;
    private string savedToken = "";

    private string backendUrlBase = "https://ishtar-nft.herokuapp.com/api/v1";
    // private string frontendUrlBase = "https://inanna.vercel.app";
    private string frontendUrlBase = "https://b984-2603-3024-1f24-100-e46e-86fd-beb8-588a.ngrok.io";

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Start LuckyBoard");
        canvas.GetComponent<Canvas>().enabled = false;
        savedToken = PlayerPrefs.GetString("token");
        if (true || savedToken == null || savedToken == "")
        { //MARK: Debug!
            StartCoroutine(SignInAnonymously());
        }
    }

    public void ShowLeaderboard()
    { //Shows Leaderboard UI over screen
        canvas.GetComponent<Canvas>().enabled = true;
        if (webViewObject == null)
        {
            StartCoroutine(LoadUp());
        }
        else
        {
            webViewObject.SetVisibility(true);
        }
    }

    public void HideLeaderboard()
    { //Hides Leaderboard UI
        webViewObject.SetVisibility(false);
        canvas.GetComponent<Canvas>().enabled = false;
    }

    public void GetLeaderboard(System.Action<Leaderboard> callback)
    { //Gets Leaderboard as JSON
        StartCoroutine(GetLeaderboardRequest("overview", callback));
    }

    public void ReportScore(int score)
    { //Reports a score for this user
        StartCoroutine(ReportScoreRequest(score));
    }


    private IEnumerator GetLeaderboardRequest(string type = "overview", System.Action<Leaderboard> callback = null)
    { //"friends" "tier" or "overview"
        UnityWebRequest www = UnityWebRequest.Get(backendUrlBase + "/leaderboards?type=" + type);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Get leaderboard error: " + www.error);
            callback(null);
        }
        else
        {
            var jsonString = www.downloadHandler.text;
            Leaderboard leaderboard = JsonUtility.FromJson<Leaderboard>(jsonString);
            callback(leaderboard);
        }
    }


    private IEnumerator ReportScoreRequest(int score)
    {

        var www = new UnityWebRequest(backendUrlBase + "/leaderboards/submit-score", "POST");

        string bodyJsonString = "{ \"score\": \"" + score + "\" }";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
        www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Authorization", savedToken);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Score report error: " + www.error);
        }
        else
        {
            Debug.Log("Successful score submission");
        }
    }

    private IEnumerator SignInAnonymously()
    {
        var bundle_id = Application.identifier;
        bundle_id = "9f3ba349-99af-4f9a-9bfd-98cce2c1d5b7"; //MARK: Debug!

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
            savedToken = www.downloadHandler.text;
            PlayerPrefs.SetString("token", savedToken);
            Debug.Log("Got token: " + savedToken);
        }
    }

    private IEnumerator LoadUp()
    {
        savedToken = PlayerPrefs.GetString("token");
        savedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhbm9ueW1vdXMiOnRydWUsIm5pY2tuYW1lIjoiYzNjY2FlMDEtZjI5Mi00NTNiLTk2YmItYWRhNzk3YzhmMWM2IiwiZ2FtZV9pZCI6IjlmM2JhMzQ5LTk5YWYtNGY5YS05YmZkLTk4Y2NlMmMxZDViNyIsImlzcyI6Im11bHRpcGxheUBnYWxheHkudXMiLCJzdWIiOiIzYWE2NmI2ZC1iODJjLTQ4YTctYTRjZC02OGZlMzQ0N2ZmZmIiLCJhdWQiOlsiOWYzYmEzNDktOTlhZi00ZjlhLTliZmQtOThjY2UyYzFkNWI3Il0sIm5iZiI6MTY2MDk0NDQ0NywiaWF0IjoxNjYwOTQ0NDQ3LCJqdGkiOiI0YjhkNThmMi03ZjQ1LTQzYTQtYTFlYi0yNDFlNmFkYWU0MGEifQ.8wAJNlki6fh2oRubbJmWVQlfjwHjOT5Q-Q5m-O9WEC0";
        Url = (frontendUrlBase + "?token=" + savedToken);

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
                  }
              }
          },
          transparent: false,
          zoom: false,
          enableWKWebView: true,
          wkContentMode: 1,  // 0: recommended, 1: mobile, 2: desktop
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
    { //public for testing
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
    }


}
