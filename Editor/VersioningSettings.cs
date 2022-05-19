#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

using System;
using System.IO;
using System.Linq;
using Skyzi000.AutoVersioning.Runtime;
using UnityEditor;
using UnityEngine;

#nullable enable
namespace Skyzi000.AutoVersioning.Editor
{
    /// <summary>
    /// バージョニング方法を記録するための自動生成<see cref="ScriptableObject"/>
    /// </summary>
    public class VersioningSettings : ScriptableObject
    {
        /// <summary>
        /// この設定などを保存するパス
        /// </summary>
        private const string DirectoryPath = "Assets/AutoVersioning";

        /// <summary>
        /// この設定を保存するファイル名
        /// </summary>
        private static string SettingsFileName => $"{nameof(VersioningSettings)}.asset";

        /// <summary>
        /// パッチ番号の自動化が有効か
        /// </summary>
        public bool AutoPatchNumberingEnabled => autoPatchNumberingMethod != AutoVersioningMethod.None;

#if ODIN_INSPECTOR
        [LabelText("Patch Numbering"), SerializeField, Tooltip("Automated method of patch number")]
#else
        [SerializeField, Tooltip("Automated method of patch number")]
#endif
        public AutoVersioningMethod autoPatchNumberingMethod;

#if ODIN_INSPECTOR
        [ShowInInspector, HorizontalGroup("BundleVersion", LabelWidth = 40, MaxWidth = 100, Title = "BundleVersion"),
         MinValue(0), OnInspectorInit(nameof(LoadBundleVersion))]
#endif
        private int _major, _minor;

#if ODIN_INSPECTOR
        [ShowInInspector, HorizontalGroup("BundleVersion"), MinValue(0), DisableIf(nameof(AutoPatchNumberingEnabled))]
#endif
        private int _patch;

        /// <summary>
        /// ビルド番号の自動化が有効か
        /// </summary>
        public bool AutoIosBuildNumberingEnabled => autoIosBuildNumberingMethod != AutoVersioningMethod.None;

#if ODIN_INSPECTOR
        [LabelText("iOS Build Numbering"), SerializeField, Tooltip("Automated method of Build number for iOS")]
#else
        [SerializeField, Tooltip("Automated method of Build number for iOS")]
#endif
        public AutoVersioningMethod autoIosBuildNumberingMethod = AutoVersioningMethod.CountGitCommits;

#if ODIN_INSPECTOR
        [ShowInInspector, MinValue(0), ShowIf(nameof(AutoIosBuildNumberingEnabled)), ReadOnly]
#endif
        private int _iosBuildNumber;

        /// <summary>
        /// ビルド番号の自動化が有効か
        /// </summary>
        public bool AutoAndroidBuildNumberingEnabled => autoAndroidBuildNumberingMethod != AutoVersioningMethod.None;

#if ODIN_INSPECTOR
        [LabelText("Bundle Version Code Numbering"), SerializeField, Tooltip("Automated method of Bundle Version Code for Android")]
#else
        [SerializeField, Tooltip("Automated method of Bundle Version Code for Android")]
#endif
        public AutoVersioningMethod autoAndroidBuildNumberingMethod = AutoVersioningMethod.CountGitCommits;

#if ODIN_INSPECTOR
        [ShowInInspector, MinValue(0), ShowIf(nameof(AutoAndroidBuildNumberingEnabled)), ReadOnly]
#endif
        private int _androidBundleVersionCode;

        [SerializeField, Tooltip("Patterns of tags to be covered by the CountGitCommitsFromLastVersionTag method.")]
        private string gitTagPattern = "*[0-9].[0-9]*";

#if ODIN_INSPECTOR
        [SerializeField, Tooltip("The path to the git executable, just \"git\" if you have already added it to Path."),
         ValidateInput(nameof(ValidateGitPath))]
#else
        [SerializeField, Tooltip("The path to the git executable, just \"git\" if you have already added it to Path.")]
#endif
        private string gitPath = GitCommandExecutor.DefaultGitPath;

        [SerializeField, Tooltip("Save the current version data at runtime.")]
        private bool autoSaveVersionData = true;

#if ODIN_INSPECTOR
        [BoxGroup("autoSaveVersionData/VersionData"), ShowIfGroup(nameof(autoSaveVersionData)), SerializeField, Tooltip("Path to automatic generation")]
#else
        [SerializeField, Tooltip("Path to automatic generation")]
#endif
        private string versionDataPath = $"{DirectoryPath}/Runtime/Resources/{nameof(VersionData)}.asset";

#if ODIN_INSPECTOR
        [BoxGroup("autoSaveVersionData/VersionData"), LabelText("Create .gitignore"), SerializeField, Tooltip("Create a .gitignore to ignore the VersionData.")]
#else
        [SerializeField, Tooltip("Create a .gitignore to ignore the VersionData.")]
#endif
        private bool createGitIgnoreForVersionData = true;

#if ODIN_INSPECTOR
        [BoxGroup("autoSaveVersionData/VersionData"), SerializeField, Tooltip("Save the commit hash value")]
#else
        [SerializeField, Tooltip("Save the commit hash value")]
#endif
        private bool saveCommitHash = true;

#if ODIN_INSPECTOR
        [BoxGroup("autoSaveVersionData/VersionData"), InfoBox("In Git, it's basically abbreviated to 7 characters."), ShowIf(nameof(saveCommitHash)),
         SerializeField, Tooltip("How many characters to save the hash"), Range(1, 40, order = 1)]
#else
        [SerializeField, Tooltip("How many characters to save the hash"), Range(1, 40, order = 1)]
#endif
        private int saveHashLength = 7;

        [SerializeField, Tooltip("Automatically save this setting when it is changed in the editor.")]
        private bool autoSaveVersioningSettings = true;

#if ODIN_INSPECTOR
        [ShowInInspector, ReadOnly]
#endif
        private string GitVersion
        {
            get
            {
                try
                {
                    return _gitExecutor.GitExec("--version").Replace("git version ", "");
                }
                catch (Exception)
                {
                    Debug.LogError("Git not found. Please install Git and set the path.");
                    return "Git not found.";
                }
            }
        }


        // ReSharper disable once UnusedMember.Local
        private bool IsDirty => EditorUtility.IsDirty(this);

        private GitCommandExecutor GitExecutor
        {
            get
            {
                ValidateAndApplyGitPath();
                return _gitExecutor;
            }
        }

        private readonly GitCommandExecutor _gitExecutor = new GitCommandExecutor();

#if !ODIN_INSPECTOR
        [SerializeField]
#else
        [SerializeField, HideInInspector]
#endif
#pragma warning disable CS0414
        // ReSharper disable once NotAccessedField.Local
        private bool warnIfOdinInspectorMissing = true;
#pragma warning restore CS0414

        private void OnEnable() => ApplyBuildNumbers();

        private void OnValidate()
        {
#if !ODIN_INSPECTOR
            if (warnIfOdinInspectorMissing)
                Debug.LogWarning("OdinInspector is not found in this project.\n" +
                                 "To use AutoVersioning conveniently, OdinInspector must be installed.\n" +
                                 "Without OdinInspector, the UI for VersioningSettings and VersionData in the Inspector will be inconvenient, " +
                                 "but the main functionality will not be affected.");
#endif
            ApplyBuildNumbers();
            if (autoSaveVersioningSettings)
                SaveVersioningSettings();
        }

        /// <summary>
        /// <see cref="gitPath"/>を検証し、使えるなら適用する
        /// </summary>
        /// <returns>使えるか、既に適用済みならtrue</returns>
        private bool ValidateAndApplyGitPath()
        {
            if (_gitExecutor.GitPath == gitPath)
                return true;
            if (!ValidateGitPath())
                return false;
            _gitExecutor.GitPath = gitPath;
            return true;
        }

        /// <summary>
        /// <inheritdoc cref="GitCommandExecutor.ValidateGitPath"/>
        /// </summary>
        private bool ValidateGitPath()
        {
            if (string.IsNullOrWhiteSpace(gitPath))
                gitPath = GitCommandExecutor.DefaultGitPath;
            return GitCommandExecutor.ValidateGitPath(gitPath);
        }

        /// <summary>
        /// この設定のみを保存する
        /// </summary>
#if ODIN_INSPECTOR
        [Button, EnableIf(nameof(IsDirty))]
#endif
        private void SaveVersioningSettings() => AssetDatabase.SaveAssetIfDirty(this);

        /// <summary>
        /// <see cref="PlayerSettings"/>をProjectSettings.assetに書き込み、保存する
        /// </summary>
#if ODIN_INSPECTOR
        [Button]
#endif
        private void SavePlayerSettings()
        {
            PlayerSettings.bundleVersion = $"{_major.ToString()}.{_minor.ToString()}.{_patch.ToString()}";
            if (AutoIosBuildNumberingEnabled)
                PlayerSettings.iOS.buildNumber = _iosBuildNumber.ToString();
            if (AutoAndroidBuildNumberingEnabled)
                PlayerSettings.Android.bundleVersionCode = _androidBundleVersionCode;
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// <see cref="VersionData"/>の自動保存が有効な場合、それを保存する
        /// </summary>
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod]
#endif
        public static void LoadAndSaveVersionDataIfEnabled()
        {
            VersioningSettings settings = LoadOrCreateSettings();
            if (settings.autoSaveVersionData == false)
                return;
            settings.LoadBundleVersion();
            settings.ApplyBuildNumbers();
            settings.SaveVersionData();
        }

        /// <summary>
        /// 読み込んで保存
        /// </summary>
        public void LoadAndSaveAll()
        {
            LoadBundleVersion();
            ApplyBuildNumbers();
            SavePlayerSettings();
            if (autoSaveVersionData)
                SaveVersionData();
        }

        /// <summary>
        /// <see cref="PlayerSettings.bundleVersion"/>を読み取る
        /// </summary>
        private static (int major, int minor, int patch) ParseBundleVersion()
        {
            var v = PlayerSettings.bundleVersion.Split('.').Select(int.Parse).ToList();
            return (v[0], v.Count > 1 ? v[1] : 0, v.Count > 2 ? v[2] : 0);
        }

        /// <summary>
        /// <see cref="PlayerSettings.bundleVersion"/>をフィールドに読み込む
        /// </summary>
        private void LoadBundleVersion()
        {
            try
            {
                (_major, _minor, _patch) = ParseBundleVersion();
            }
            catch (Exception e)
            {
                Debug.LogError("Could not interpret the version number from bundleVersion.");
                Debug.LogException(e);
                return;
            }
            try
            {
                _patch = GetBuildNumber(autoPatchNumberingMethod, _patch, gitTagPattern);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get patch number.\nTry creating a git repository or changing {nameof(autoPatchNumberingMethod)} to None.");
                Debug.LogException(e);
                return;
            }
        }

        /// <summary>
        /// パッチ番号やビルド番号等を取得し、フィールドに適用する
        /// </summary>
        private void ApplyBuildNumbers()
        {
            try
            {
                _iosBuildNumber = GetBuildNumber(autoIosBuildNumberingMethod, int.Parse(PlayerSettings.iOS.buildNumber), gitTagPattern);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get ios build number.\nTry creating a git repository or changing {nameof(autoIosBuildNumberingMethod)} to None.");
                Debug.LogException(e);
                return;
            }
            try
            {
                _androidBundleVersionCode = GetBuildNumber(autoAndroidBuildNumberingMethod, PlayerSettings.Android.bundleVersionCode, gitTagPattern);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get android bundle version code.\nTry creating a git repository or changing {nameof(autoAndroidBuildNumberingMethod)} to None.");
                Debug.LogException(e);
                return;
            }
        }

        private VersionData SetData(VersionData data)
        {
            data.major = _major;
            data.minor = _minor;
            data.patch = _patch;
            data.iosBuildNumber = _iosBuildNumber;
            data.androidBundleVersionCode = _androidBundleVersionCode;
            data.hash = saveCommitHash ? GitExecutor.GetCommitHash(saveHashLength) : null;
            return data;
        }

        /// <summary>
        /// 実行時に簡単に読み出せるようにバージョン情報を保存する
        /// </summary>
        private void SaveVersionData()
        {
            if (File.Exists(versionDataPath))
            {
                VersionData data = SetData(AssetDatabase.LoadAssetAtPath<VersionData>(versionDataPath));
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            else
            {
                CreateVersionData();
                if (createGitIgnoreForVersionData)
                    CreateGitIgnoreFile(versionDataPath);
                Debug.Log($"Create a new {nameof(VersionData)} since it did not exist: '{versionDataPath}'");
            }
        }

        private void CreateVersionData()
        {
            VersionData data = SetData(CreateInstance<VersionData>());
            Directory.CreateDirectory(versionDataPath);
            AssetDatabase.CreateAsset(data, versionDataPath);
        }

        /// <summary>
        /// 指定のファイルを無視する.gitignoreファイルを生成する<br/>
        /// そのファイルがResources以下の場合は、Resourcesに.gitignoreが入らないようResourcesと同階層に生成する
        /// </summary>
        private static async void CreateGitIgnoreFile(string ignorePath)
        {
            var resourcesPathIndex = ignorePath.IndexOf("Resources", StringComparison.Ordinal);
            if (resourcesPathIndex != -1)
            {
                var gitIgnore = new FileInfo($"{ignorePath.Substring(0, resourcesPathIndex)}.gitignore");
                using StreamWriter ignoreWriter = gitIgnore.CreateText();
                await ignoreWriter.WriteLineAsync(ignorePath.Substring(resourcesPathIndex)).ConfigureAwait(false);
                await ignoreWriter.WriteLineAsync($"{ignorePath.Substring(resourcesPathIndex)}.meta").ConfigureAwait(false);
            }
            else
            {
                var gitIgnore = new FileInfo(
                    Path.Combine(Path.GetDirectoryName(ignorePath) ?? throw new ArgumentNullException(nameof(ignorePath)), ".gitignore"));
                using StreamWriter ignoreWriter = gitIgnore.CreateText();
                await ignoreWriter.WriteLineAsync(Path.GetFileName(ignorePath)).ConfigureAwait(false);
                await ignoreWriter.WriteLineAsync($"{Path.GetFileName(ignorePath)}.meta").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// バージョン番号の自動化方法に基づいてビルド番号を取得する
        /// </summary>
        public int GetBuildNumber(AutoVersioningMethod numberingMethod, int defaultNumber, string tagPattern) =>
            numberingMethod switch
            {
                AutoVersioningMethod.None => defaultNumber,
                AutoVersioningMethod.CountGitCommits => GitExecutor.CountAllGitCommits(),
                AutoVersioningMethod.CountGitCommitsFromLastVersionTag => GitExecutor.CountGitCommitsFromTag(tagPattern),
                _ => throw new ArgumentOutOfRangeException(nameof(numberingMethod))
            };

        /// <summary>
        /// アセット保存先パスを返す
        /// </summary>
        private static string GetPath(string? directoryPath = DirectoryPath) => Path.Combine(directoryPath ?? DirectoryPath, SettingsFileName);

        /// <summary>
        /// この設定をファイルからロードして返す。見つからない場合はnull
        /// </summary>
        public static VersioningSettings? LoadSettings(string? directoryPath = DirectoryPath) =>
            AssetDatabase.LoadAssetAtPath<VersioningSettings>(GetPath(directoryPath));

        /// <summary>
        /// この設定をファイルからロードして返す。見つからない場合は新規作成する
        /// </summary>
        public static VersioningSettings LoadOrCreateSettings(string? directoryPath = DirectoryPath) =>
            LoadSettings(directoryPath) ?? CreateSettings(directoryPath);

        /// <summary>
        /// この設定を新規作成する
        /// </summary>
        public static VersioningSettings CreateSettings(string? directoryPath = DirectoryPath)
        {
            var path = GetPath(directoryPath);
            Directory.CreateDirectory(path);
            AssetDatabase.CreateAsset(CreateInstance<VersioningSettings>(), path);
            Debug.Log($"Create a new {nameof(VersioningSettings)}: '{path}'");
            return LoadSettings(directoryPath) ?? throw new InvalidOperationException($"Failed to create {nameof(VersioningSettings)}.");
        }

        /// <summary>
        /// この設定を選択する
        /// </summary>
        [MenuItem("Tools/AutoVersioning/Settings...")]
        private static void SelectSettings()
        {
            VersioningSettings settings = LoadOrCreateSettings();
            Selection.activeObject = settings;
        }
    }
}
