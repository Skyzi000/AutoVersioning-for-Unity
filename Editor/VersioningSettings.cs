using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Skyzi000.AutoVersioning.Runtime;
using UnityEditor;
using UnityEngine;
using static Skyzi000.AutoVersioning.Editor.GitCommandExecutor;

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

        [SerializeField, LabelText("NumberingMethod"), Tooltip("Automated method of patch number")]
        public AutoVersioningMethod autoPatchNumberingMethod;

        [ShowInInspector, HorizontalGroup("BundleVersion", LabelWidth = 40, MaxWidth = 100, Title = "BundleVersion"),
         MinValue(0), OnInspectorInit(nameof(LoadBundleVersion))]
        private int _major, _minor;

        [ShowInInspector, HorizontalGroup("BundleVersion"), MinValue(0), DisableIf(nameof(AutoPatchNumberingEnabled))]
        private int _patch;

        /// <summary>
        /// ビルド番号の自動化が有効か
        /// </summary>
        public bool AutoIosBuildNumberingEnabled => autoIosBuildNumberingMethod != AutoVersioningMethod.None;

        [SerializeField, LabelText("NumberingMethod"), Tooltip("Automated method of Build number for iOS")]
        public AutoVersioningMethod autoIosBuildNumberingMethod = AutoVersioningMethod.CountGitCommits;

        [ShowInInspector, MinValue(0), ShowIf(nameof(AutoIosBuildNumberingEnabled)), ReadOnly]
        private int _iosBuildNumber;

        /// <summary>
        /// ビルド番号の自動化が有効か
        /// </summary>
        public bool AutoAndroidBuildNumberingEnabled => autoAndroidBuildNumberingMethod != AutoVersioningMethod.None;

        [SerializeField, LabelText("NumberingMethod"), Tooltip("Automated method of Bundle Version Code for Android")]
        public AutoVersioningMethod autoAndroidBuildNumberingMethod = AutoVersioningMethod.CountGitCommits;

        [ShowInInspector, MinValue(0), ShowIf(nameof(AutoAndroidBuildNumberingEnabled)), ReadOnly]
        private int _androidBundleVersionCode;

        [SerializeField, Tooltip("Save the current version data at runtime.")]
        private bool autoSaveVersionData = true;

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Tooltip("Path to automatic generation"), ShowIfGroup(nameof(autoSaveVersionData))]
        private string versionDataPath = $"{DirectoryPath}/Runtime/Resources/{nameof(VersionData)}.asset";

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Tooltip("Create a .gitignore to ignore the VersionData.")]
        private bool createGitIgnoreForVersionData = true;

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Tooltip("Save the commit hash value")]
        private bool saveCommitHash = true;

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Tooltip("How many characters to save the hash"),
         InfoBox("In Git, it's basically abbreviated to 7 characters."), ShowIf(nameof(saveCommitHash)), Range(1, 40, order = 1)]
        private int saveHashLength = 7;

        [SerializeField, Tooltip("Automatically save this setting when it is changed in the editor.")]
        private bool autoSaveVersioningSettings = true;

        private bool IsDirty => EditorUtility.IsDirty(this);

        private void OnEnable() => ApplyBuildNumbers();

        private void OnValidate()
        {
            ApplyBuildNumbers();
            if (autoSaveVersioningSettings)
                SaveVersioningSettings();
        }

        /// <summary>
        /// この設定のみを保存する
        /// </summary>
        [Button, EnableIf(nameof(IsDirty))]
        private void SaveVersioningSettings() => AssetDatabase.SaveAssetIfDirty(this);

        /// <summary>
        /// <see cref="PlayerSettings"/>をProjectSettings.assetに書き込み、保存する
        /// </summary>
        [Button]
        private void SavePlayerSettings()
        {
            PlayerSettings.bundleVersion = $"{_major}.{_minor}.{_patch}";
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
                _patch = GetBuildNumber(autoPatchNumberingMethod, _patch);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not interpret the version number from bundleVersion.");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// パッチ番号やビルド番号等を取得し、フィールドに適用する
        /// </summary>
        private void ApplyBuildNumbers()
        {
            _iosBuildNumber = GetBuildNumber(autoIosBuildNumberingMethod, int.Parse(PlayerSettings.iOS.buildNumber));
            _androidBundleVersionCode = GetBuildNumber(autoIosBuildNumberingMethod, PlayerSettings.Android.bundleVersionCode);
        }

        private VersionData SetData(VersionData data)
        {
            data.major = _major;
            data.minor = _minor;
            data.patch = _patch;
            data.iosBuildNumber = _iosBuildNumber;
            data.androidBundleVersionCode = _androidBundleVersionCode;
            data.hash = saveCommitHash ? GetCommitHash(saveHashLength) : null;
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
        public static int GetBuildNumber(AutoVersioningMethod numberingMethod, int defaultNumber) =>
            numberingMethod switch
            {
                AutoVersioningMethod.None => defaultNumber,
                AutoVersioningMethod.CountGitCommits => CountAllGitCommits(),
                AutoVersioningMethod.CountGitCommitsFromLastVersionTag => CountGitCommitsFromTag("v[0-9]*"),
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
