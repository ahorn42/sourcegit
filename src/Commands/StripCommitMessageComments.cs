using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    // Pipes a commit message through `git stripspace`, so cleanup matches what `git commit`
    // itself would do (comment stripping via `core.commentChar`, blank-line collapsing, etc.)
    // instead of reimplementing those rules.
    public static class StripCommitMessageComments
    {
        public static async Task<string> RunAsync(string repo, string message, bool stripComments)
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = stripComments ? "stripspace --strip-comments" : "stripspace";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardInput = true;
            starter.RedirectStandardOutput = true;
            starter.RedirectStandardError = true;
            starter.StandardInputEncoding = new UTF8Encoding(false);
            starter.StandardOutputEncoding = Encoding.UTF8;
            starter.StandardErrorEncoding = Encoding.UTF8;

            try
            {
                using var proc = Process.Start(starter)!;
                await proc.StandardInput.WriteAsync(message).ConfigureAwait(false);
                proc.StandardInput.Close();

                // Read both streams concurrently - reading one at a time can deadlock if
                // git writes enough to the other stream to fill its buffer.
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);

                if (proc.ExitCode != 0)
                {
                    Models.Notification.Send(repo, stderrTask.Result, true);
                    return string.Empty;
                }

                return stdoutTask.Result.Trim();
            }
            catch (Exception e)
            {
                Models.Notification.Send(repo, "Failed to run `git stripspace`: " + e.Message, true);
                return string.Empty;
            }
        }
    }
}
