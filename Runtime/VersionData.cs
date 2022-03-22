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
        /// デフォルトの生成位置であることを前提として、VersionDataを読み込む
        /// </summary>
        public static VersionData Default => Resources.Load<VersionData>(nameof(VersionData));

#if ODIN_INSPECTOR
        [ReadOnly]
#else
        [HideInInspector]
#endif
        public int major, minor, patch, iosBuildNumber, androidBundleVersionCode;

#if ODIN_INSPECTOR
        [ReadOnly]
#else
        [HideInInspector]
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
