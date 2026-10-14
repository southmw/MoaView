using System.IO;
using FluentAssertions;
using MoaView.Infrastructure.Configuration;
using MoaView.Infrastructure.Diagnostics;
using Xunit;

namespace MoaView.Infrastructure.Tests;

public class CrashReportWriterTests
{
    [Fact]
    public void Build_includes_environment_and_inner_exceptions()
    {
        var exception = new InvalidOperationException("outer", new IOException("inner"));

        var report = CrashReportWriter.Build(exception, "AppDomain", "0.1.0", "155.0.1");

        report.Should().Contain("crash report (AppDomain)")
            .And.Contain("App       : 0.1.0")
            .And.Contain("WebView2  : 155.0.1")
            .And.Contain("InvalidOperationException: outer")
            .And.Contain("--- Inner[1] ---")
            .And.Contain("IOException: inner");
    }

    [Fact]
    public void Write_creates_crash_file_in_logs_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "moaview-crash-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(root);
        try
        {
            var file = CrashReportWriter.Write(paths, new Exception("boom"), "Dispatcher", "0.1.0", null);

            file.Should().NotBeNull().And.StartWith(paths.LogsFolder);
            Path.GetFileName(file!).Should().StartWith("crash-").And.EndWith(".log");
            File.ReadAllText(file!).Should().Contain("boom").And.Contain("WebView2  : unknown");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
