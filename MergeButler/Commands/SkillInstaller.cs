using System.Reflection;

namespace MergeButler.Commands;

/// <summary>
/// Installs the resolve-conflicts Copilot skill files into a target repository.
/// Skill files are embedded as assembly resources.
/// </summary>
public static class SkillInstaller
{
    private static readonly string[] SkillFiles =
    [
        "SKILL.md",
        "scripts/conflict-status.sh",
        "scripts/categorize-conflicts.sh",
        "scripts/conflict-status.ps1",
        "scripts/categorize-conflicts.ps1"
    ];

    /// <summary>
    /// Installs or updates the resolve-conflicts skill into the given repo root.
    /// Returns the list of files written.
    /// </summary>
    public static List<string> Install(string repoRoot)
    {
        string skillDir = Path.Combine(repoRoot, ".github", "skills", "resolve-conflicts");
        List<string> written = [];

        foreach (string relativePath in SkillFiles)
        {
            string resourceName = $"MergeButler.Skills.resolve-conflicts.{relativePath.Replace('/', '.')}";
            string targetPath = Path.Combine(skillDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

            string? targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir is not null && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
            using StreamReader reader = new(stream);
            string content = reader.ReadToEnd();

            File.WriteAllText(targetPath, content);
            written.Add(Path.GetRelativePath(repoRoot, targetPath));
        }

        return written;
    }
}
