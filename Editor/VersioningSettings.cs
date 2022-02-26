using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Skyzi000.AutoVersioning.Runtime;
using UnityEditor;
using UnityEngine;
using static Skyzi000.AutoVersioning.Editor.GitCommandExecutor;

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

        [SerializeField, LabelText("NumberingMethod"), Header("パッチ番号の自動化方法")]
        public AutoVersioningMethod autoPatchNumberingMethod;

        [ShowInInspector, HorizontalGroup("BundleVersion", LabelWidth = 40, MaxWidth = 100, Title = "BundleVersion"), MinValue(0), OnInspectorInit(nameof(LoadBundleVersion))]
        private int _major, _minor;

        [ShowInInspector, HorizontalGroup("BundleVersion"), MinValue(0), DisableIf(nameof(AutoPatchNumberingEnabled))]
        private int _patch;

        /// <summary>
        /// ビルド番号の自動化が有効か
        /// </summary>
        public bool AutoIosBuildNumberingEnabled => autoIosBuildNumberingMethod != AutoVersioningMethod.None;

        [SerializeField, LabelText("NumberingMethod"), Header("iOS用のBuild番号の自動化方法")]
        public AutoVersioningMethod autoIosBuildNumberingMethod = AutoVersioningMethod.CountGitCommits;

        [ShowInInspector, MinValue(0), ShowIf(nameof(AutoIosBuildNumberingEnabled)), ReadOnly]
        private int _iosBuildNumber;

        /// <summary>
        /// ビルド番号の自動化が有効か
        /// </summary>
        public bool AutoAndroidBuildNumberingEnabled => autoAndroidBuildNumberingMethod != AutoVersioningMethod.None;

        [SerializeField, LabelText("NumberingMethod"), Header("Android用のBundle Version Codeの自動化方法")]
        public AutoVersioningMethod autoAndroidBuildNumberingMethod = AutoVersioningMethod.CountGitCommits;

        [ShowInInspector, MinValue(0), ShowIf(nameof(AutoAndroidBuildNumberingEnabled)), ReadOnly]
        private int _androidBundleVersionCode;

        [SerializeField, Header("現在のバージョン情報を自動的に保存するか")]
        private bool autoSaveVersionData = true;

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Header("自動生成先パス"), ShowIfGroup(nameof(autoSaveVersionData))]
        private string versionDataPath = "Assets/AutoVersioning/Runtime/Resources/VersionData.asset";

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Header("コミットのハッシュ値を保存するか")]
        private bool saveCommitHash = true;

        [SerializeField, BoxGroup("autoSaveVersionData/VersionData"), Header("コミットのハッシュ値を何文字目まで保存するか"),
         InfoBox("Gitでは基本的に7文字に省略される"), ShowIf(nameof(saveCommitHash)), Range(1, 40, order = 1)]
        private int saveHashLength = 7;

        private bool IsDirty => EditorUtility.IsDirty(this);

        /// <summary>
        /// この設定のみを保存する
        /// </summary>
        [Button(ButtonHeight = 50), EnableIf(nameof(IsDirty))]
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
                Debug.LogError("バージョン番号を解釈できません");
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

        private void OnEnable() => ApplyBuildNumbers();

        private void OnValidate() => ApplyBuildNumbers();

        private VersionData SetData(VersionData data)
        {
            data.major = _major;
            data.minor = _minor;
            data.patch = _patch;
            data.iosBuildNumber = _iosBuildNumber;
            data.androidBundleVersionCode = _androidBundleVersionCode;
            data.hash = saveCommitHash ? GetCommitHash(saveHashLength) : string.Empty;
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
                Debug.Log($"{nameof(VersionData)}が存在しなかったため新規作成: '{versionDataPath}'");
            }
        }

        private void CreateVersionData()
        {
            VersionData data = SetData(CreateInstance<VersionData>());
            Directory.CreateDirectory(versionDataPath);
            AssetDatabase.CreateAsset(data, versionDataPath);
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
                _ => throw new NotImplementedException()
            };

        /// <summary>
        /// アセット保存先パスを返す
        /// </summary>
        private static string GetPath(string directoryPath = DirectoryPath) => Path.Combine(directoryPath ?? DirectoryPath, SettingsFileName);

        /// <summary>
        /// この設定をファイルからロードして返す。見つからない場合はnull
        /// </summary>
        public static VersioningSettings LoadSettings(string directoryPath = DirectoryPath) =>
            AssetDatabase.LoadAssetAtPath<VersioningSettings>(GetPath(directoryPath));

        /// <summary>
        /// この設定をファイルからロードして返す。見つからない場合は新規作成する
        /// </summary>
        public static VersioningSettings LoadOrCreateSettings(string directoryPath = DirectoryPath) =>
            LoadSettings(directoryPath) ?? CreateSettings(directoryPath);

        /// <summary>
        /// この設定を新規作成する
        /// </summary>
        public static VersioningSettings CreateSettings(string directoryPath = DirectoryPath)
        {
            var path = GetPath(directoryPath);
            Directory.CreateDirectory(path);
            AssetDatabase.CreateAsset(CreateInstance<VersioningSettings>(), path);
            Debug.Log($"{nameof(VersioningSettings)}を新規作成: '{path}'");
            return LoadSettings(directoryPath);
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
