using NUnit.Framework;
using Scalar.FunctionalTests.Properties;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Scalar.FunctionalTests.Tools
{
    public static class GitHelpers
    {
        /// <summary>
        /// This string must match the command name provided in the
        /// Scalar.FunctionalTests.LockHolder program.
        /// </summary>
        private const string LockHolderCommandName = @"Scalar.FunctionalTests.LockHolder";
        private const string LockHolderCommand = @"Scalar.FunctionalTests.LockHolder.dll";

        private const string WindowsPathSeparator = "\\";
        private const string GitPathSeparator = "/";

        private static string LockHolderCommandPath
        {
            get
            {
                // On OSX functional tests are run from inside Publish directory. Dependent
                // assemblies including LockHolder test are available at the same level in
                // the same directory.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return Path.Combine(
                        Settings.Default.CurrentDirectory,
                        LockHolderCommand);
                }
                else
                {
                    // On Windows, FT is run from the Output directory of Scalar.FunctionalTest project.
                    // LockHolder is a .netcore assembly and can be found inside netcoreapp2.1
                    // subdirectory of Scalar.FunctionalTest Output directory.
                    return Path.Combine(
                        Settings.Default.CurrentDirectory,
                        "netcoreapp2.1",
                        LockHolderCommand);
                }
            }
        }

        public static string ConvertPathToGitFormat(string relativePath)
        {
            return relativePath.Replace(WindowsPathSeparator, GitPathSeparator);
        }

        public static void CheckGitCommand(string virtualRepoRoot, string command, params string[] expectedLinesInResult)
        {
            ProcessResult result = GitProcess.InvokeProcess(virtualRepoRoot, command);
            result.Errors.ShouldBeEmpty();
            foreach (string line in expectedLinesInResult)
            {
                result.Output.ShouldContain(line);
            }
        }

        public static void CheckGitCommandAgainstScalarRepo(string virtualRepoRoot, string command, params string[] expectedLinesInResult)
        {
            ProcessResult result = InvokeGitAgainstScalarRepo(virtualRepoRoot, command);
            result.Errors.ShouldBeEmpty();
            foreach (string line in expectedLinesInResult)
            {
                result.Output.ShouldContain(line);
            }
        }

        public static ProcessResult InvokeGitAgainstScalarRepo(
            string scalarRepoRoot,
            string command,
            Dictionary<string, string> environmentVariables = null,
            bool removeWaitingMessages = true,
            bool removeUpgradeMessages = true,
            string input = null)
        {
            ProcessResult result = GitProcess.InvokeProcess(scalarRepoRoot, command, input, environmentVariables);
            string errors = result.Errors;

            if (!string.IsNullOrEmpty(errors) && (removeWaitingMessages || removeUpgradeMessages))
            {
                IEnumerable<string> errorLines = errors.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                IEnumerable<string> filteredErrorLines = errorLines.Where(line =>
                {
                    if (string.IsNullOrWhiteSpace(line) ||
                        (removeUpgradeMessages && line.StartsWith("A new version of Scalar is available.")) ||
                        (removeWaitingMessages && line.StartsWith("Waiting for ")))
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                });

                errors = filteredErrorLines.Any() ? string.Join(Environment.NewLine, filteredErrorLines) : string.Empty;
            }

            return new ProcessResult(
                result.Output,
                errors,
                result.ExitCode);
        }

        public static void ValidateGitCommand(
            ScalarFunctionalTestEnlistment enlistment,
            ControlGitRepo controlGitRepo,
            string command,
            params object[] args)
        {
            command = string.Format(command, args);
            string controlRepoRoot = controlGitRepo.RootPath;
            string scalarRepoRoot = enlistment.RepoRoot;

            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables["GIT_QUIET"] = "true";

            ProcessResult expectedResult = GitProcess.InvokeProcess(controlRepoRoot, command, environmentVariables);
            ProcessResult actualResult = GitHelpers.InvokeGitAgainstScalarRepo(scalarRepoRoot, command, environmentVariables);

            ErrorsShouldMatch(command, expectedResult, actualResult);
            actualResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ShouldMatchInOrder(expectedResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), LinesAreEqual, command + " Output Lines");

            if (command != "status")
            {
                ValidateGitCommand(enlistment, controlGitRepo, "status");
            }
        }

        /// <summary>
        /// Acquire the ScalarLock. This method will return once the ScalarLock has been acquired.
        /// </summary>
        /// <param name="processId">The ID of the process that acquired the lock.</param>
        /// <returns><see cref="ManualResetEvent"/> that can be signaled to exit the lock acquisition program.</returns>
        public static ManualResetEventSlim AcquireScalarLock(
            ScalarFunctionalTestEnlistment enlistment,
            out int processId,
            int resetTimeout = Timeout.Infinite,
            bool skipReleaseLock = false)
        {
            string args = LockHolderCommandPath;
            if (skipReleaseLock)
            {
                args += " --skip-release-lock";
            }

            return RunCommandWithWaitAndStdIn(
                enlistment,
                resetTimeout,
                "dotnet",
                args,
                GitHelpers.LockHolderCommandName,
                "done",
                out processId);
        }

        /// <summary>
        /// Run the specified Git command. This method will return once the ScalarLock has been acquired.
        /// </summary>
        /// <param name="processId">The ID of the process that acquired the lock.</param>
        /// <returns><see cref="ManualResetEvent"/> that can be signaled to exit the lock acquisition program.</returns>
        public static ManualResetEventSlim RunGitCommandWithWaitAndStdIn(
            ScalarFunctionalTestEnlistment enlistment,
            int resetTimeout,
            string command,
            string stdinToQuit,
            out int processId)
        {
            return
                RunCommandWithWaitAndStdIn(
                    enlistment,
                    resetTimeout,
                    Properties.Settings.Default.PathToGit,
                    command,
                    "git " + command,
                    stdinToQuit,
                    out processId);
        }

        public static void ErrorsShouldMatch(string command, ProcessResult expectedResult, ProcessResult actualResult)
        {
            actualResult.Errors.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ShouldMatchInOrder(expectedResult.Errors.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), LinesAreEqual, command + " Errors Lines");
        }

        /// <summary>
        /// Run the specified command as an external program. This method will return once the ScalarLock has been acquired.
        /// </summary>
        /// <param name="processId">The ID of the process that acquired the lock.</param>
        /// <returns><see cref="ManualResetEvent"/> that can be signaled to exit the lock acquisition program.</returns>
        private static ManualResetEventSlim RunCommandWithWaitAndStdIn(
            ScalarFunctionalTestEnlistment enlistment,
            int resetTimeout,
            string pathToCommand,
            string args,
            string lockingProcessCommandName,
            string stdinToQuit,
            out int processId)
        {
            ManualResetEventSlim resetEvent = new ManualResetEventSlim(initialState: false);

            ProcessStartInfo processInfo = new ProcessStartInfo(pathToCommand);
            processInfo.WorkingDirectory = enlistment.RepoRoot;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardInput = true;
            processInfo.Arguments = args;

            Process holdingProcess = Process.Start(processInfo);
            StreamWriter stdin = holdingProcess.StandardInput;
            processId = holdingProcess.Id;

            enlistment.WaitForLock(lockingProcessCommandName);

            Task.Run(
                () =>
                {
                    resetEvent.Wait(resetTimeout);

                    try
                    {
                        // Make sure to let the holding process end.
                        if (stdin != null)
                        {
                            stdin.WriteLine(stdinToQuit);
                            stdin.Close();
                        }

                        if (holdingProcess != null)
                        {
                            bool holdingProcessHasExited = holdingProcess.WaitForExit(10000);

                            if (!holdingProcess.HasExited)
                            {
                                holdingProcess.Kill();
                            }

                            holdingProcess.Dispose();

                            holdingProcessHasExited.ShouldBeTrue("Locking process did not exit in time.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"{nameof(RunCommandWithWaitAndStdIn)} exception closing stdin {ex.ToString()}");
                    }
                    finally
                    {
                        resetEvent.Set();
                    }
                });

            return resetEvent;
        }

        private static bool LinesAreEqual(string actualLine, string expectedLine)
        {
            return actualLine.Equals(expectedLine);
        }
    }
}
