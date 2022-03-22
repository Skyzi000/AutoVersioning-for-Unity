using Sirenix.OdinInspector;
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

        [ReadOnly]
        public int major, minor, patch, iosBuildNumber, androidBundleVersionCode;

        [ReadOnly]
        public string? hash;

        [ShowInInspector]
        public string BundleVersion => $"{major.ToString()}.{minor.ToString()}.{patch.ToString()}";

        [ShowInInspector]
        public string BundleVersionWithHash => $"{BundleVersion}{(string.IsNullOrEmpty(hash) ? string.Empty : $" ({hash})")}";
    }
}
