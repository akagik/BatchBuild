namespace BatchBuild
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.Build.Reporting;

    /// <summary>
    /// 自動ビルド用.
    /// </summary>
    public static class BatchBuilder
    {
        [MenuItem("File/BatchBuild/Build")]
        public static void Build()
        {
            //  ビルド設定を読み込む
            MyBuildConfig buildConfig = null;

            if (Application.isBatchMode)
            {
                buildConfig = ScriptableObject.CreateInstance<MyBuildConfig>();

                // バッチモードの場合、コマンドライン引数でオプション設定を上書きする
                OverrideWithCmdLineArgs(buildConfig);
            }
            else
            {
                buildConfig = LoadOnlyOneAsset<MyBuildConfig>("t:MyBuildConfig");
                if (buildConfig == null)
                {
                    Debug.LogWarning("Create a new build config because of missing a default config");
                    buildConfig = ScriptableObject.CreateInstance<MyBuildConfig>();
                }
            }

            Debug.Log($"[ScriptLog] Start Build {buildConfig.targetPlatform}");

            BuildReport report = null;

            if (buildConfig.targetPlatform == BuildTarget.iOS)
            {
                report = BuildiOS(buildConfig);
            }
            else
            {
                report = BuildAndroid(buildConfig);
            }

            // レポート表示
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ScriptLog] Success Build {buildConfig.targetPlatform}");
            }
            else
            {
                Debug.Log($"[ScriptLog] Failed Build {buildConfig.targetPlatform}");
                Debug.Log(System.Environment.NewLine + report + System.Environment.NewLine);
            }

            // バッチモードの場合のみ、Editor を終了する
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }

        public static BuildReport BuildiOS(MyBuildConfig config)
        {
            // 各種設定
            SetupCommonSettings(config);

            if (config.isRelease)
            {
                EditorUserBuildSettings.iOSBuildConfigType = iOSBuildType.Release;
            }
            else
            {
                EditorUserBuildSettings.iOSBuildConfigType = iOSBuildType.Debug;
            }

            // ビルドナンバーの更新
            PlayerSettings.iOS.buildNumber = config.buildNumber;

            // ビルド実行
            string[] scenes = GetBuildScenePaths();
            string buildPath = config.buildPath;
            BuildOptions opt = BuildOptions.CompressWithLz4;

            return BuildPipeline.BuildPlayer(scenes, buildPath, config.targetPlatform, opt);
        }

        public static BuildReport BuildAndroid(MyBuildConfig config)
        {
            // 各種設定
            SetupAndroidUserBuildSettings(config);
            SetAndroidKeyStoreSetting(config);
            SetupCommonSettings(config);
            SetAndroidArchitechture(config);

            // ビルドナンバーの更新
            PlayerSettings.Android.bundleVersionCode = int.Parse(config.buildNumber);

            // ビルド実行
            string[] scenes = GetBuildScenePaths();
            string buildPath = config.buildPath;
            BuildOptions opt = BuildOptions.CompressWithLz4;

            return BuildPipeline.BuildPlayer(scenes, buildPath, config.targetPlatform, opt);
        }

        private static void SetupCommonSettings(MyBuildConfig config)
        {
            if (config.isRelease)
            {
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.connectProfiler = false;
                EditorUserBuildSettings.allowDebugging = false;
                EditorUserBuildSettings.symlinkLibraries = false;
            }
            else
            {
                EditorUserBuildSettings.development = config.developmentBuild;
                EditorUserBuildSettings.connectProfiler = config.connectProfiler;
                EditorUserBuildSettings.allowDebugging = config.allowDebugging;
                EditorUserBuildSettings.symlinkLibraries = config.symlinkUnityLibraries;
            }
        }

        public static void SetupAndroidUserBuildSettings(MyBuildConfig config)
        {
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.Generic;
            EditorUserBuildSettings.androidETC2Fallback = AndroidETC2Fallback.Quality32Bit;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.buildAppBundle = config.isRelease && config.buildAppBundle;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;

            // NOTE: 一旦 proguard 外しておく
//        EditorUserBuildSettings.androidDebugMinification = AndroidMinification.Proguard;
//        EditorUserBuildSettings.androidReleaseMinification = AndroidMinification.Proguard;

            EditorUserBuildSettings.androidDebugMinification = AndroidMinification.None;
            EditorUserBuildSettings.androidReleaseMinification = AndroidMinification.None;

            EditorUserBuildSettings.androidUseLegacySdkTools = false;
        }

        private static void SetAndroidKeyStoreSetting(MyBuildConfig config)
        {
            if (string.IsNullOrEmpty(config.keystorePath)) return;

            PlayerSettings.Android.keystoreName = config.keystorePath;
            PlayerSettings.Android.keystorePass = config.keystorePass;
            PlayerSettings.Android.keyaliasName = config.keyaliasName;
            PlayerSettings.Android.keyaliasPass = config.keyaliasPass;
        }

        private static MyBuildConfig LoadDefaultBuildConfig()
        {
            var table = LoadOnlyOneAsset<MyBuildConfig>("t:MyBuildConfig");
            if (table == null)
            {
                Debug.LogWarning("Create a new build config because of missing a default config");
                return ScriptableObject.CreateInstance<MyBuildConfig>();
            }

            return table;
        }

        private static void SetAndroidArchitechture(MyBuildConfig config)
        {
            if (config.il2cpp)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            }
            else
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            }
        }

        /// <summary>
        /// コマンドライン引数を解釈して変数に格納する
        /// </summary>
        private static void OverrideWithCmdLineArgs(MyBuildConfig config)
        {
            string[] args = System.Environment.GetCommandLineArgs();

            for (int i = 0, max = args.Length; i < max; ++i)
            {
                switch (args[i].ToLower())
                {
                    case "--platform":
                        config.targetPlatform = (BuildTarget) System.Enum.Parse(typeof(BuildTarget), args[i + 1]);
                        i += 1;
                        break;
                    case "--build-path":
                        config.buildPath = (args[i + 1]).ToString();
                        i += 1;
                        break;
                    case "--build-number":
                        config.buildNumber = (args[i + 1]).ToString();
                        i += 1;
                        break;
                    case "--build-app-bundle":
                        config.buildAppBundle = true;
                        break;
                    case "--release":
                        config.isRelease = true;
                        config.developmentBuild = false;
                        config.allowDebugging = false;
                        config.connectProfiler = false;
                        config.symlinkUnityLibraries = false;
                        config.il2cpp = true;
                        break;
                    case "--development":
                        config.isRelease = false;
                        config.developmentBuild = true;
                        config.allowDebugging = true;
                        config.connectProfiler = true;
                        config.symlinkUnityLibraries = true;
                        config.il2cpp = false;
                        break;
                    case "--commit-id":
                        config.commitId = (args[i + 1]).ToString();
                        i += 1;
                        break;
                    case "--keystore-path":
                        config.keystorePath = (args[i + 1]).ToString();
                        i += 1;
                        break;
                    case "--keystore-pass":
                        config.keystorePass = (args[i + 1]).ToString();
                        i += 1;
                        break;
                    case "--keyalias-name":
                        config.keyaliasName = (args[i + 1]).ToString();
                        i += 1;
                        break;
                    case "--keyalias-pass":
                        config.keyaliasPass = (args[i + 1]).ToString();
                        i += 1;
                        break;
                }
            }
        }

        private static string[] GetBuildScenePaths()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            return scenes
                .Where((arg) => arg.enabled)
                .Select((arg) => arg.path)
                .ToArray();
        }

        private static T LoadOnlyOneAsset<T>(string filter) where T : UnityEngine.Object
        {
            string guid = FindGUIDOfOnlyOneAsset(filter);
            if (guid == null)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static string FindGUIDOfOnlyOneAsset(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter);

            if (guids.Length == 0)
            {
                Debug.LogErrorFormat("指定のフィルターのアセットが見つかりません", filter);
                return null;
            }

            if (guids.Length > 1)
            {
                Debug.LogErrorFormat("指定のフィルターのアセットが複数見つかりました", filter);
                return null;
            }

            return guids[0];
        }
    }
}