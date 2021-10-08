namespace BatchBuild
{
    using UnityEditor;
    using UnityEngine;

    [CreateAssetMenu(menuName = "BatchBuild/BuildConfig")]
    public class MyBuildConfig : ScriptableObject
    {
        public BuildTarget targetPlatform;
        public string buildPath;
        public string buildNumber;
        public string version;
        
        public bool isRelease = false;
        public string commitId;

        // Android key store settings
        [Header("Android")]
        public bool buildAppBundle;
        public bool il2cpp;
        // public AndroidMinification debugMinification;
        // public AndroidMinification releaseMinification;
        
        public string keystorePath;
        public string keystorePass;
        public string keyaliasName;
        public string keyaliasPass;
        

        // Common Debugs
        [Header("Debug (isRealse が false のときのみ有効)")]
        public bool developmentBuild;

        public bool connectProfiler;
        public bool allowDebugging;
        public bool symlinkUnityLibraries;
    }
}