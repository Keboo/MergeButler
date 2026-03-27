using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MergeButler.Mcp;
using ModelContextProtocol.Server;

namespace MergeButler.Commands;

public static class McpCommand
{
    public static Command Create()
    {
        Command command = new("mcp", "Start the MergeButler MCP server for local development (stdio transport).");

        command.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            // Route all logs to stderr so they don't interfere with the stdio MCP transport
            builder.Logging.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new()
                    {
                        Name = "MergeButler",
                        Version = "0.0.1"
                    };
                })
                .WithStdioServerTransport()
                .WithTools<PullRequestTools>()
                .WithTools<ConfigTools>();

            await builder.Build().RunAsync(cancellationToken);
        });

        return command;
    }
}
