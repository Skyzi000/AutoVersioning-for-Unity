namespace Skyzi000.AutoVersioning.Editor
{
    /// <summary>
    /// バージョン番号の自動化手法
    /// </summary>
    public enum AutoVersioningMethod
    {
        /// <summary>
        /// 自動化しない
        /// </summary>
        None,

        /// <summary>
        /// Gitのコミット数の合計値
        /// </summary>
        CountGitCommits,

        /// <summary>
        /// 直近のバージョンタグ(例: v1.0)からのコミット数
        /// </summary>
        CountGitCommitsFromLastVersionTag,
    }
}
