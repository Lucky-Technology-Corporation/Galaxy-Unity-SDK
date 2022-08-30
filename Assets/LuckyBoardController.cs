using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    
    // Start is called before the first frame update
    void Start(){
      Debug.Log("Start LuckyBoard");
      canvas.GetComponent<Canvas>().enabled = false;
    }

    private IEnumerator LoadUp()
    {
      print("LoadUp");
      string savedToken = "";
      Url = ("https://google.com?token=" + savedToken);

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
          if(msg.Contains("?sdk_action=request_contacts")){
            GetContacts();
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
      if (Url.StartsWith("http")) {
          webViewObject.LoadURL(Url.Replace(" ", "%20"));
      } else {
        var exts = new string[]{
            ".jpg",
            ".js",
            ".html"  // should be last
        };
        foreach (var ext in exts) {
            var url = Url.Replace(".html", ext);
            var src = System.IO.Path.Combine(Application.streamingAssetsPath, url);
            var dst = System.IO.Path.Combine(Application.persistentDataPath, url);
            byte[] result = null;
            if (src.Contains("://")) {  // for Android
              #if UNITY_2018_4_OR_NEWER
                var unityWebRequest = UnityWebRequest.Get(src);
                yield return unityWebRequest.SendWebRequest();
                result = unityWebRequest.downloadHandler.data;
              #else
                var www = new WWW(src);
                yield return www;
                result = www.bytes;
              #endif
              } else {
                  result = System.IO.File.ReadAllBytes(src);
              }
              System.IO.File.WriteAllBytes(dst, result);
              if (ext == ".html") {
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

    void Update()
    {
        
    }

    public void CloseView(){
      webViewObject.SetVisibility(false);
      canvas.GetComponent<Canvas>().enabled = false;
    }

    public void OpenView(){
      canvas.GetComponent<Canvas>().enabled = true;
      if(webViewObject == null){
        StartCoroutine(LoadUp());
      }
      else{
        webViewObject.SetVisibility(true);
      }
    }

    public void GetContacts(){ //public for testing
      Contacts.LoadContactList(onDone, onLoadFailed);
    }
    
    void onLoadFailed(string reason)
    {
      Debug.Log("Failed for reason: " + reason);
    }

    void onDone()
    {
      Debug.Log("Count: " + Contacts.ContactsList.Count);
      Contact c = Contacts.ContactsList[0];
      Debug.Log("First Contact First Number: " + c.Phones[0].Number);
      //Upload contacts here
    }


}
