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
      // if(webViewObject != null){ break; }
      webViewObject.Init(
          cb: (msg) =>
          {
              Debug.Log(string.Format("CallFromJS[{0}]", msg));
              // status.text = msg;
              // status.GetComponent<Animation>().Play();
          },
          err: (msg) =>
          {
              Debug.Log(string.Format("CallOnError[{0}]", msg));
              // status.text = msg;
              // status.GetComponent<Animation>().Play();
          },
          httpErr: (msg) =>
          {
              Debug.Log(string.Format("CallOnHttpError[{0}]", msg));
              // status.text = msg;
              // status.GetComponent<Animation>().Play();
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
              Debug.Log(string.Format("CallOnLoaded[{0}]", msg));
#if UNITY_EDITOR_OSX || (!UNITY_ANDROID && !UNITY_WEBPLAYER && !UNITY_WEBGL)
              // NOTE: depending on the situation, you might prefer
              // the 'iframe' approach.
              // cf. https://github.com/gree/unity-webview/issues/189
#if true
              webViewObject.EvaluateJS(@"
                if (window && window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.unityControl) {
                  window.Unity = {
                    call: function(msg) {
                      window.webkit.messageHandlers.unityControl.postMessage(msg);
                    }
                  }
                } else {
                  window.Unity = {
                    call: function(msg) {
                      window.location = 'unity:' + msg;
                    }
                  }
                }
              ");
#else
              webViewObject.EvaluateJS(@"
                if (window && window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.unityControl) {
                  window.Unity = {
                    call: function(msg) {
                      window.webkit.messageHandlers.unityControl.postMessage(msg);
                    }
                  }
                } else {
                  window.Unity = {
                    call: function(msg) {
                      var iframe = document.createElement('IFRAME');
                      iframe.setAttribute('src', 'unity:' + msg);
                      document.documentElement.appendChild(iframe);
                      iframe.parentNode.removeChild(iframe);
                      iframe = null;
                    }
                  }
                }
              ");
#endif
#elif UNITY_WEBPLAYER || UNITY_WEBGL
              webViewObject.EvaluateJS(
                  "window.Unity = {" +
                  "   call:function(msg) {" +
                  "       parent.unityWebView.sendMessage('WebViewObject', msg)" +
                  "   }" +
                  "};");
#endif
              webViewObject.EvaluateJS(@"Unity.call('ua=' + navigator.userAgent)");
          },
          transparent: false,
          zoom: false,
          //ua: "custom user agent string",
          //// android
          //androidForceDarkMode: 0,  // 0: follow system setting, 1: force dark off, 2: force dark on
          //// ios
          enableWKWebView: true,
          wkContentMode: 1,  // 0: recommended, 1: mobile, 2: desktop
          wkAllowsLinkPreview: false
          //// editor
          //separated: false
          );
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
      webViewObject.bitmapRefreshCycle = 1;
#endif
      // cf. https://github.com/gree/unity-webview/pull/512
      // Added alertDialogEnabled flag to enable/disable alert/confirm/prompt dialogs. by KojiNakamaru · Pull Request #512 · gree/unity-webview
      //webViewObject.SetAlertDialogEnabled(false);

      // cf. https://github.com/gree/unity-webview/pull/728
      //webViewObject.SetCameraAccess(true);
      //webViewObject.SetMicrophoneAccess(true);

      // cf. https://github.com/gree/unity-webview/pull/550
      // introduced SetURLPattern(..., hookPattern). by KojiNakamaru · Pull Request #550 · gree/unity-webview
      //webViewObject.SetURLPattern("", "^https://.*youtube.com", "^https://.*google.com");

      // cf. https://github.com/gree/unity-webview/pull/570
      // Add BASIC authentication feature (Android and iOS with WKWebView only) by takeh1k0 · Pull Request #570 · gree/unity-webview
      //webViewObject.SetBasicAuthInfo("id", "password");

      //webViewObject.SetScrollbarsVisibility(true);

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
                  // NOTE: a more complete code that utilizes UnityWebRequest can be found in https://github.com/gree/unity-webview/commit/2a07e82f760a8495aa3a77a23453f384869caba7#diff-4379160fa4c2a287f414c07eb10ee36d
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
      // Destroy(webViewObject);
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

    public void GetContacts(){
      Contacts.LoadContactList( onDone, onLoadFailed );
    }
    
    void onLoadFailed( string reason )
    {
      Debug.Log(reason);
    }

    void onDone()
    {
      Debug.Log("Count: " + Contacts.ContactsList.Count);
      Contact c = Contacts.ContactsList[0];
      Debug.Log("First Contact First Number: " + c.Phones[0].Number);
    }


}
