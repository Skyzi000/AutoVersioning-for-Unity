using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Skyzi000.AutoVersioning.Editor
{
    public static class GitCommandExecutor
    {
        private const string DefaultGitPath = "git";

        /// <summary>
        /// 全コミット数を数えて返す
        /// </summary>
        public static int CountAllGitCommits()
        {
            var output = GitExec(@"log --oneline").Trim('\n', ' ');
            var count = 0;
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var c in output)
                if (c == '\n')
                    count++;
            return count;
        }

        /// <summary>
        /// パターンに合致する直近のタグからのコミット数を返す
        /// </summary>
        /// <param name="tagRegex">タグのパターン(例: v[0-9].*)</param>
        public static int CountGitCommitsFromTag(string tagRegex)
        {
            var matchedTags = GitExec($"tag --list \"{tagRegex}\"");
            if (string.IsNullOrEmpty(matchedTags))
                return CountAllGitCommits();
            var matchOption = string.IsNullOrEmpty(tagRegex) ? string.Empty : $" --match \"{tagRegex}\"";
            var output = GitExec($"describe --tags --long{matchOption}");
            return int.Parse(Regex.Match(output, @"-\d+-g").Value.Trim('-', 'g'));
        }

        /// <summary>
        /// コミットのハッシュ値を返す
        /// </summary>
        /// <param name="length">ハッシュ値の長さ。0~40が有効、7でgitの`--format=%h`指定に相当</param>
        /// <param name="commit">指定するコミット。デフォルトは"HEAD"</param>
        /// <exception cref="ArgumentOutOfRangeException">Gitから帰ってきたハッシュ値か、引数<see cref="length"/>の値がおかしい場合</exception>
        public static string GetCommitHash(int length = 7, string commit = "HEAD")
        {
            var hash = GitExec($"show \"{commit}\" --format=%H -s").Trim();
            if (0 > length || length > hash.Length)
                throw new ArgumentOutOfRangeException(nameof(length), $"The hash length(raw: {hash.Length}, arg: {length}) is wrong.");
            return length == hash.Length ? hash : hash.Substring(0, length);
        }

        /// <summary>
        /// Gitコマンドを実行して結果を返す
        /// </summary>
        /// <param name="commands">gitのサブコマンドやオプション</param>
        /// <param name="gitPath">Gitの実行ファイルへのパス、Pathが通っているなら"git"のみで大丈夫</param>
        /// <returns>標準出力</returns>
        /// <exception cref="InvalidOperationException">実行できなかった場合や、Gitが正常終了しなかった場合</exception>
        public static string GitExec(string commands, string gitPath = DefaultGitPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = gitPath,
                Arguments = commands,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{standardError}\nExitCode: {process.ExitCode}");
            return standardOutput;
        }
    }
}
