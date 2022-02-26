using Sirenix.OdinInspector;
using UnityEngine;

namespace Skyzi000.AutoVersioning.Runtime
{
    /// <summary>
    /// バージョン情報を持つ<see cref="ScriptableObject"/><br/>
    /// VersioningSettingsによって自動生成される
    /// </summary>
    public class VersionData : ScriptableObject
    {
        [ReadOnly]
        public int major, minor, patch, iosBuildNumber, androidBundleVersionCode;

        [ReadOnly]
        public string hash;
    }
}
