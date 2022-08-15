using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

#nullable enable
namespace Skyzi000.AutoVersioning.Editor
{
    public class GitCommandExecutor
    {
        public const string DefaultGitPath = "git";

        /// <summary>
        /// Git実行ファイルへのパス
        /// </summary>
        public string GitPath { get; set; }

        public GitCommandExecutor(string gitPath = DefaultGitPath)
        {
            GitPath = gitPath;
        }

        /// <summary>
        /// Git実行ファイルへのパスが使えるか検証する
        /// </summary>
        /// <param name="gitPath">検証するパス</param>
        /// <returns>使えるならtrue"</returns>
        public static bool ValidateGitPath(string gitPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gitPath) || !GitExec(@"--version", gitPath).Contains("git version"))
                    return false;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// GitのHEADまでのコミット数を返す
        /// </summary>
        /// <remarks>
        /// v1.7までは<code>git log --oneline</code>の最終行を除いた改行文字数<br/><br/>
        /// v1.8からは<code>git rev-list --count HEAD</code>の出力
        /// </remarks>
        public int CountAllGitCommits()
        {
            var output = GitExec(@"rev-list --count HEAD");
            return int.TryParse(output, out var count) ? count : 0;
        }

        /// <summary>
        /// パターンに合致する直近のタグからのコミット数を返す
        /// </summary>
        /// <param name="tagRegex">タグのパターン(例: v[0-9].*)</param>
        public int CountGitCommitsFromTag(string tagRegex)
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
        public string GetCommitHash(int length = 7, string commit = "HEAD")
        {
            var hash = GitExec($"show \"{commit}\" --format=%H -s").Trim();
            if (0 <= length && length <= hash.Length)
                return length == hash.Length ? hash : hash.Substring(0, length);
            Debug.LogException(new ArgumentOutOfRangeException(nameof(length), $"The hash length(raw: {hash.Length}, arg: {length}) is wrong."));
            return hash;
        }

        /// <summary>
        /// Gitコマンドを実行して結果を返す
        /// </summary>
        /// <remarks>
        /// <see cref="GitPath"/>が<see cref="DefaultGitPath"/>と異なる状態で例外が発生した場合、自動的に<see cref="DefaultGitPath"/>でリトライする。
        /// それでも例外が発生した場合は、空文字列を返す。
        /// </remarks>
        /// <param name="commands">gitのサブコマンドやオプション</param>
        /// <returns>標準出力</returns>
        public string GitExec(string commands)
        {
            try
            {
                return GitExec(commands, GitPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (GitPath == DefaultGitPath)
                {
                    Debug.LogError(Application.systemLanguage == SystemLanguage.Japanese
                        ? "Gitの実行に失敗しました。Gitのパスが正しいこと、ローカルリポジトリに一つ以上のコミットが存在することを確認してください。"
                        : "Git execution failed. Please make sure that the Git path is correct and that one or more commits exist in the local repository.");
                    return "";
                }

                Debug.LogWarning(Application.systemLanguage == SystemLanguage.Japanese
                    ? $"Git({GitPath})の実行に失敗しました。デフォルトのGitパスでリトライします..."
                    : $"Git({GitPath}) execution failed. Retrying with default Git path...");
                try
                {
                    return GitExec(commands, DefaultGitPath);
                }
                catch (Exception e2)
                {
                    Debug.LogException(e2);
                    Debug.LogError(Application.systemLanguage == SystemLanguage.Japanese
                        ? "Gitの実行に失敗しました。Gitのパスが正しいこと、ローカルリポジトリに一つ以上のコミットが存在することを確認してください。"
                        : "Git execution failed. Please make sure that the Git path is correct and that one or more commits exist in the local repository.");
                    return "";
                }
            }
        }

        /// <inheritdoc cref="GitExec(string)"/>
        public static string GitExec(string commands, string gitPath)
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
