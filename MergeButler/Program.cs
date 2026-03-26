using System.CommandLine;
using MergeButler.Commands;

namespace MergeButler;

public sealed class Program
{
    private static Task<int> Main(string[] args)
    {
        RootCommand rootCommand = BuildCommandLine();
        return rootCommand.Parse(args).InvokeAsync();
    }

    public static RootCommand BuildCommandLine()
    {
        RootCommand rootCommand = new("MergeButler - Automated PR approval tool")
        {
            EvaluateCommand.Create()
        };

        return rootCommand;
    }
}