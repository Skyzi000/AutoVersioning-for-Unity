using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Skyzi000.AutoVersioning.Editor
{
    public class AutoVersionConfigurator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            VersioningSettings versionSettings = VersioningSettings.LoadSettings();
            if (versionSettings != null)
                versionSettings.LoadAndSaveAll();
        }
    }
}
