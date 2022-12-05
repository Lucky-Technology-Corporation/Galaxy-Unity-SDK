using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Collections;
using UnityEditor.iOS.Xcode;
using System.IO;
 
 namespace GalaxySDK{
    public class SetContactsPlistValue {
    
        [PostProcessBuild]
        public static void SetInfoString(BuildTarget buildTarget, string pathToBuiltProject) {
    
            if (buildTarget == BuildTarget.iOS) {
        
                // Get plist
                string plistPath = pathToBuiltProject + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));
        
                // Get root
                PlistElementDict rootDict = plist.root;
        
                // Change value of CFBundleVersion in Xcode plist
                var buildKey = "NSContactsUsageDescription";
                rootDict.SetString(buildKey,"Allow access to your contacts to see & compete with friends");
        
                // Write to file
                File.WriteAllText(plistPath, plist.WriteToString());
            }
        }
    }  
 }