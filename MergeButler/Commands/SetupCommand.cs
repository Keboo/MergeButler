using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MergeButler.Commands;

public static class SetupCommand
{
    public static Command Create()
    {
        Option<bool> yesOption = new("--yes", ["-y"])
        {
            Description = "Skip all prompts and perform every setup step automatically."
        };

        Command command = new("setup",
            "Set up mergiraf, configure Git for structural merging, and install the resolve-conflicts Copilot skill.")
        {
            yesOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            bool yes = parseResult.CommandResult.GetValue(yesOption);
            TextWriter output = parseResult.InvocationConfiguration.Output;

            await ExecuteAsync(yes, output, cancellationToken);
        });

        return command;
    }

    internal static async Task ExecuteAsync(bool autoApprove, TextWriter output, CancellationToken cancellationToken)
    {
        string repoRoot = FindRepoRoot()
            ?? throw new InvalidOperationException("Not in a git repository. Run this command from within a git repo.");

        output.WriteLine("MergeButler Setup");
        output.WriteLine(new string('═', 40));
        output.WriteLine();

        // Step 1: Install mergiraf
        await SetupMergiraf(autoApprove, output, cancellationToken);

        // Step 2: Configure merge.conflictStyle = diff3
        await ConfigureGitSetting(
            "merge.conflictStyle", "diff3",
            "Configure diff3 conflict style? This shows the common ancestor in conflict markers, giving more context for resolution.",
            autoApprove, output, cancellationToken);

        // Step 3: Configure rerere.enabled = true
        await ConfigureGitSetting(
            "rerere.enabled", "true",
            "Enable rerere (reuse recorded resolution)? Git will remember how you resolve conflicts and replay those resolutions automatically.",
            autoApprove, output, cancellationToken);

        // Step 4: Register mergiraf merge driver
        await SetupMergeDriver(autoApprove, output, cancellationToken);

        // Step 5: Configure global git attributes
        await SetupGitAttributes(autoApprove, output, cancellationToken);

        // Step 6: Install the resolve-conflicts skill
        await InstallSkill(repoRoot, autoApprove, output);

        output.WriteLine();
        output.WriteLine("Setup complete!");
    }

    private static async Task SetupMergiraf(bool autoApprove, TextWriter output, CancellationToken cancellationToken)
    {
        output.WriteLine("Step 1: Mergiraf");
        output.WriteLine("─────────────────");

        bool isInstalled = await IsCommandAvailable("mergiraf", cancellationToken);
        if (isInstalled)
        {
            output.WriteLine("  ✓ mergiraf is already installed.");
            output.WriteLine();
            return;
        }

        (string managerName, string installCommand)? installer = DetectPackageManager();
        if (installer is null)
        {
            output.WriteLine("  ✗ No supported package manager found (brew, scoop, or cargo).");
            output.WriteLine("    Install mergiraf manually: https://mergiraf.org/installation.html");
            output.WriteLine();
            return;
        }

        string prompt = $"Install mergiraf via {installer.Value.managerName}? ({installer.Value.installCommand})";
        if (!autoApprove && !Confirm(prompt, output))
        {
            output.WriteLine("  Skipped.");
            output.WriteLine();
            return;
        }

        output.WriteLine($"  Running: {installer.Value.installCommand}");
        (bool success, string cmdOutput) = await RunCommand(installer.Value.installCommand, cancellationToken);
        if (success)
        {
            output.WriteLine("  ✓ mergiraf installed successfully.");
        }
        else
        {
            output.WriteLine($"  ✗ Installation failed. Install manually: https://mergiraf.org/installation.html");
            output.WriteLine($"    Output: {cmdOutput}");
        }

        output.WriteLine();
    }

    private static async Task ConfigureGitSetting(
        string key, string value, string description,
        bool autoApprove, TextWriter output, CancellationToken cancellationToken)
    {
        string stepName = key switch
        {
            "merge.conflictStyle" => "Step 2: diff3 conflict style",
            "rerere.enabled" => "Step 3: rerere (reuse recorded resolution)",
            _ => $"Configure {key}"
        };

        output.WriteLine(stepName);
        output.WriteLine(new string('─', stepName.Length));

        string? current = await GetGitConfig(key, cancellationToken);
        if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine($"  ✓ {key} is already set to {value}.");
            output.WriteLine();
            return;
        }

        if (!autoApprove && !Confirm(description, output))
        {
            output.WriteLine("  Skipped.");
            output.WriteLine();
            return;
        }

        string command = $"git config --global {key} {value}";
        (bool success, _) = await RunCommand(command, cancellationToken);
        if (success)
        {
            output.WriteLine($"  ✓ Set {key} = {value}");
        }
        else
        {
            output.WriteLine($"  ✗ Failed to set {key}.");
        }

        output.WriteLine();
    }

    private static async Task SetupMergeDriver(bool autoApprove, TextWriter output, CancellationToken cancellationToken)
    {
        output.WriteLine("Step 4: Mergiraf merge driver");
        output.WriteLine("─────────────────────────────");

        string? driverName = await GetGitConfig("merge.mergiraf.name", cancellationToken);
        if (driverName == "mergiraf")
        {
            output.WriteLine("  ✓ Mergiraf merge driver is already registered.");
            output.WriteLine();
            return;
        }

        string prompt = "Register mergiraf as a git merge driver? This lets Git use mergiraf for structural merging automatically.";
        if (!autoApprove && !Confirm(prompt, output))
        {
            output.WriteLine("  Skipped.");
            output.WriteLine();
            return;
        }

        await RunCommand("git config --global merge.mergiraf.name mergiraf", cancellationToken);
        (bool success, _) = await RunCommand(
            "git config --global merge.mergiraf.driver \"mergiraf merge --git %O %A %B -s %S -x %X -y %Y -p %P -l %L\"",
            cancellationToken);

        if (success)
        {
            output.WriteLine("  ✓ Mergiraf merge driver registered.");
        }
        else
        {
            output.WriteLine("  ✗ Failed to register merge driver.");
        }

        output.WriteLine();
    }

    private static async Task SetupGitAttributes(bool autoApprove, TextWriter output, CancellationToken cancellationToken)
    {
        output.WriteLine("Step 5: Global git attributes");
        output.WriteLine("──────────────────────────────");

        string attributesPath = GetGlobalAttributesPath();
        bool hasAttribute = false;

        if (File.Exists(attributesPath))
        {
            string content = await File.ReadAllTextAsync(attributesPath, cancellationToken);
            hasAttribute = content.Contains("merge=mergiraf");
        }

        if (hasAttribute)
        {
            output.WriteLine($"  ✓ Global attributes already configured ({attributesPath}).");
            output.WriteLine();
            return;
        }

        string prompt = $"Add '* merge=mergiraf' to global git attributes? This routes all files through mergiraf (unsupported types fall back to default).";
        if (!autoApprove && !Confirm(prompt, output))
        {
            output.WriteLine("  Skipped.");
            output.WriteLine();
            return;
        }

        string? dir = Path.GetDirectoryName(attributesPath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.AppendAllTextAsync(attributesPath, "* merge=mergiraf\n", cancellationToken);
        output.WriteLine($"  ✓ Added to {attributesPath}");
        output.WriteLine();
    }

    private static Task InstallSkill(string repoRoot, bool autoApprove, TextWriter output)
    {
        output.WriteLine("Step 6: Resolve-conflicts Copilot skill");
        output.WriteLine("────────────────────────────────────────");

        string prompt = "Install the resolve-conflicts Copilot skill into this repository's .github/skills/ directory?";
        if (!autoApprove && !Confirm(prompt, output))
        {
            output.WriteLine("  Skipped.");
            output.WriteLine();
            return Task.CompletedTask;
        }

        List<string> files = SkillInstaller.Install(repoRoot);
        foreach (string file in files)
        {
            output.WriteLine($"  ✓ {file}");
        }

        output.WriteLine();
        return Task.CompletedTask;
    }

    // --- Helpers ---

    private static (string Name, string Command)? DetectPackageManager()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (IsCommandAvailableSync("brew")) return ("Homebrew", "brew install mergiraf");
            if (IsCommandAvailableSync("cargo")) return ("Cargo", "cargo install --locked mergiraf");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (IsCommandAvailableSync("scoop")) return ("Scoop", "scoop install mergiraf");
            if (IsCommandAvailableSync("winget")) return ("winget", "winget install mergiraf");
            if (IsCommandAvailableSync("cargo")) return ("Cargo", "cargo install --locked mergiraf");
        }
        else // Linux
        {
            if (IsCommandAvailableSync("brew")) return ("Homebrew", "brew install mergiraf");
            if (IsCommandAvailableSync("cargo")) return ("Cargo", "cargo install --locked mergiraf");
        }

        return null;
    }

    private static bool IsCommandAvailableSync(string command)
    {
        try
        {
            string whichCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            ProcessStartInfo psi = new(whichCmd, command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process? proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsCommandAvailable(string command, CancellationToken cancellationToken)
    {
        try
        {
            string whichCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            ProcessStartInfo psi = new(whichCmd, command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process? proc = Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> GetGitConfig(string key, CancellationToken cancellationToken)
    {
        (bool success, string output) = await RunCommand($"git config --global --get {key}", cancellationToken);
        return success ? output.Trim() : null;
    }

    private static async Task<(bool Success, string Output)> RunCommand(string command, CancellationToken cancellationToken)
    {
        try
        {
            string shell;
            string args;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shell = "cmd.exe";
                args = $"/c {command}";
            }
            else
            {
                shell = "/bin/sh";
                args = $"-c \"{command.Replace("\"", "\\\"")}\"";
            }

            ProcessStartInfo psi = new(shell, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? proc = Process.Start(psi);
            if (proc is null) return (false, "Failed to start process");

            string stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            string stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            return (proc.ExitCode == 0, (stdout + stderr).Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static bool Confirm(string prompt, TextWriter output)
    {
        output.Write($"  {prompt} [Y/n] ");
        string? response = Console.ReadLine();
        return string.IsNullOrWhiteSpace(response) ||
               response.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               response.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGlobalAttributesPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "git", "attributes");
    }

    private static string? FindRepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
