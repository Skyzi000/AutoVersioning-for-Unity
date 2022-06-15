#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

using UnityEngine;

#nullable enable
namespace Skyzi000.AutoVersioning.Runtime
{
    /// <summary>
    /// バージョン情報を持つ<see cref="ScriptableObject"/><br/>
    /// VersioningSettingsによって自動生成される
    /// </summary>
    public class VersionData : ScriptableObject
    {
        /// <summary>
        /// デフォルトの生成位置に保存されていることを前提としてVersionDataを読み込みキャッシュする
        /// </summary>
        /// <remarks>保存されていなければnull</remarks>
        public static VersionData Default => _default != null ? _default : _default = Resources.Load<VersionData>(nameof(VersionData));

        private static VersionData? _default = null;

#if ODIN_INSPECTOR
        [ReadOnly]
#endif
        public int major, minor, patch, iosBuildNumber, androidBundleVersionCode;

#if ODIN_INSPECTOR
        [ReadOnly]
#endif
        public string? hash;

#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        public string BundleVersion => $"{major.ToString()}.{minor.ToString()}.{patch.ToString()}";

#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        public string BundleVersionWithHash => $"{BundleVersion}{(string.IsNullOrEmpty(hash) ? string.Empty : $" ({hash})")}";
    }
}
