using System.CommandLine;

namespace MergeButler.Tests;

public class ProgramTests
{
    [Fact]
    public async Task Invoke_WithHelpOption_DisplaysHelp()
    {
        using StringWriter stdOut = new();
        int exitCode = await Invoke("--help", stdOut);
        
        Assert.Equal(0, exitCode);
        Assert.Contains("--help", stdOut.ToString());
    }

    [Fact]
    public async Task Invoke_EvaluateHelp_DisplaysEvaluateOptions()
    {
        using StringWriter stdOut = new();
        int exitCode = await Invoke("evaluate --help", stdOut);

        Assert.Equal(0, exitCode);
        string output = stdOut.ToString();
        Assert.Contains("--config", output);
        Assert.Contains("--pr", output);
        Assert.Contains("--platform", output);
    }

    [Fact]
    public async Task Invoke_RootDescription_ContainsMergeButler()
    {
        using StringWriter stdOut = new();
        int exitCode = await Invoke("--help", stdOut);

        Assert.Equal(0, exitCode);
        Assert.Contains("MergeButler", stdOut.ToString());
    }

    private static Task<int> Invoke(string commandLine, StringWriter console)
    {
        RootCommand rootCommand = Program.BuildCommandLine();
        ParseResult parseResult = rootCommand.Parse(commandLine);
        parseResult.InvocationConfiguration.Output = console;
        return parseResult.InvokeAsync();
    }
}