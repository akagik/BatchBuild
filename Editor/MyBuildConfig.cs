namespace BatchBuild
{
    using UnityEditor;
    using UnityEngine;

    [CreateAssetMenu(menuName = "BatchBuild/BuildConfig")]
    public class MyBuildConfig : ScriptableObject
    {
        public BuildTarget targetPlatform = BuildTarget.StandaloneWindows;
        public string buildPath;
        [SerializeField] private string _buildNumber;
        public string bundleVersion;

        public bool isRelease = false;
        public string commitId;

        // 標準のビルドメソッドを上書きする.
        // 名前は "型名.メソッド名, アセンブリ名" または "型名.メソッド名" で指定する
        public string overrideBuildMethod = "";

        // Android key store settings
        [Header("Android")] public bool buildAppBundle;

        public bool il2cpp;

#if !UNITY_2019_1_OR_NEWER
        public AndroidMinification debugMinification;
        public AndroidMinification releaseMinification;
#endif

        public string keystorePath;
        public string keystorePass;
        public string keyaliasName;
        public string keyaliasPass;

        public string buildNumber
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_buildNumber))
                {
                    return "0";
                }

                return _buildNumber;
            }

            set => _buildNumber = value;
        }

        // Common Debugs
        [Header("Debug (isRealse が false のときのみ有効)")]
        public bool developmentBuild;

        public bool connectProfiler;
        public bool allowDebugging;
        public bool symlinkUnityLibraries;
    }
}