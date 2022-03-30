using System;
using System.Text;

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

            Debug.Log($"[ScriptLog] Start Build {buildConfig.targetPlatform}({buildConfig.buildNumber}) - {buildConfig.bundleVersion}");

            // 各種設定
            SetupCommonSettings(buildConfig);

            // プラットフォーム別の設定
            if (buildConfig.targetPlatform == BuildTarget.iOS)
            {
                SetiOSSettings(buildConfig);
            }
            else if (buildConfig.targetPlatform == BuildTarget.Android)
            {
                SetAndroidSettings(buildConfig);
            }
            
            // ビルド実行
            string[] scenes = GetBuildScenePaths();
            BuildOptions opt = BuildOptions.CompressWithLz4;
            string buildPath = GetBuildPathOrDefault(buildConfig);
            bool isSuccess = false;
            string buildErrorLog = "";

            // ビルドメソッドを上書きしない場合は標準の方法でビルドする.
            if (string.IsNullOrWhiteSpace(buildConfig.overrideBuildMethod))
            {
                BuildReport report = BuildPipeline.BuildPlayer(scenes, buildPath, buildConfig.targetPlatform, opt);
                
                // レポート表示
                if (report.summary.result == BuildResult.Succeeded)
                {
                    isSuccess = true;
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"=============== Error Message Begin ==========================" + System.Environment.NewLine);
                    for (int i = 0; i < report.steps.Length; i++)
                    {
                        var step = report.steps[i];
                        var msg  = step.messages;
                    
                        sb.Append($"STEP ({step.name})" + System.Environment.NewLine);

                        for (int j = 0; j < msg.Length; j++)
                        {
                            LogType type = msg[j].type;
                            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                            {
                                sb.Append(msg[j].content + System.Environment.NewLine);
                            }
                        }
                        sb.Append("----------------------------------------------");
                        sb.Append(System.Environment.NewLine);
                    }
                    sb.Append(System.Environment.NewLine);
                    sb.Append($"================= End ==================================");

                    buildErrorLog = sb.ToString();
                    isSuccess = false;
                }
            }
            // ビルドメソッドを上書きしてビルドする.
            else
            {
                if (OverrideBuildMethod.TryParse(buildConfig.overrideBuildMethod, out var method))
                {
                    if (method.TryGetType())
                    {
                        // static を前提でビルドメソッドをコールする.
                        // 引数は以下を渡す.
                        // 1. buildConfig
                        // 2. buildPath
                        isSuccess = (bool) method.methodInfo.Invoke(null, new object[] { buildConfig, buildPath });
                    }
                    else
                    {
                        Debug.Log("overrideBuildMethod で型名または MethodInfo の取得に失敗しました: " + buildConfig.overrideBuildMethod);
                        isSuccess = false;
                    }
                }
                else
                {
                    Debug.Log("overrideBuildMethod 名のパースに失敗しました.");
                    isSuccess = false;
                }
            }

            // ログの出力
            if (isSuccess)
            {
                Debug.Log($"[ScriptLog] Success Build {buildConfig.targetPlatform}");
            }
            else
            {
                Debug.Log($"[ScriptLog] Failed Build {buildConfig.targetPlatform}");
                Debug.Log(buildErrorLog);
            }

            // バッチモードの場合のみ、Editor を終了する
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(isSuccess ? 0 : 1);
            }
        }

        public static void SetiOSSettings(MyBuildConfig config)
        {
            if (config.isRelease)
            {
                EditorUserBuildSettings.iOSBuildConfigType = iOSBuildType.Release;
            }
            else
            {
                EditorUserBuildSettings.iOSBuildConfigType = iOSBuildType.Debug;
            }

            // ビルドナンバーなどの更新
            if (!string.IsNullOrWhiteSpace(config.buildNumber))
            {
                PlayerSettings.iOS.buildNumber = config.buildNumber;
            }
        }

        public static void SetAndroidSettings(MyBuildConfig config)
        {
            // 各種設定
            SetupAndroidUserBuildSettings(config);
            SetAndroidKeyStoreSetting(config);
            SetAndroidArchitechture(config);

            // ビルドナンバーなどの更新
            if (!string.IsNullOrWhiteSpace(config.buildNumber))
            {
                PlayerSettings.Android.bundleVersionCode = int.Parse(config.buildNumber);
            }
        }

        private static string GetBuildPathOrDefault(MyBuildConfig config)
        {
            string buildPath = config.buildPath;

            if (string.IsNullOrWhiteSpace(buildPath))
            {
                if (config.targetPlatform == BuildTarget.StandaloneWindows ||
                    config.targetPlatform == BuildTarget.StandaloneWindows64)
                {
                    buildPath = $"builds/{Application.productName}-{config.buildNumber}.exe";
                }
                else
                {
                    buildPath = $"builds/{Application.productName}-{config.buildNumber}";
                }
            }

            return buildPath;
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
            
            if (!string.IsNullOrWhiteSpace(config.bundleVersion))
            {
                PlayerSettings.bundleVersion = config.bundleVersion;
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

#if !UNITY_2019_1_OR_NEWER
            EditorUserBuildSettings.androidDebugMinification = config.debugMinification;
            EditorUserBuildSettings.androidReleaseMinification = config.releaseMinification;
#endif

            // EditorUserBuildSettings.androidDebugMinification = AndroidMinification.None;
            // EditorUserBuildSettings.androidReleaseMinification = AndroidMinification.None;
        }

        private static void SetAndroidKeyStoreSetting(MyBuildConfig config)
        {
            if (string.IsNullOrEmpty(config.keystorePath)) return;

            PlayerSettings.Android.keystoreName = config.keystorePath;
            PlayerSettings.Android.keystorePass = config.keystorePass;
            PlayerSettings.Android.keyaliasName = config.keyaliasName;
            PlayerSettings.Android.keyaliasPass = config.keyaliasPass;
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
                    case "--version":
                    case "--bundle-version":
                        config.bundleVersion = (args[i + 1]);
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
                    case "--override-build-method":
                        config.overrideBuildMethod = (args[i + 1]).ToString();
                        i += 1;
                        break;
#if !UNITY_2019_1_OR_NEWER
                    case "--minification":
                    
                        if (Enum.TryParse(args[i + 1], true, out AndroidMinification result))
                        {
                            config.debugMinification = result;
                            config.releaseMinification = result;
                        }
                        else
                        {
                            Debug.LogError("無効な引数をスキップ: " + args[i + 1]);
                        }
                        i += 1;
                        break;
#endif
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
