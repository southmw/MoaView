using System.IO;
using FluentAssertions;
using MoaView.Infrastructure.Configuration;
using Xunit;

namespace MoaView.Infrastructure.Tests;

public class DataResetTests
{
    [Fact]
    public void Execute_without_marker_does_nothing()
    {
        var root = TempRoot();
        var paths = new AppPaths(root);
        try
        {
            paths.EnsureCreated();
            File.WriteAllText(paths.DatabaseFile, "db");

            var removed = DataReset.ExecuteIfScheduled(paths, out var errors);

            removed.Should().BeEmpty();
            errors.Should().BeEmpty();
            File.Exists(paths.DatabaseFile).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Execute_with_marker_removes_db_webview_icons_and_keeps_logs()
    {
        var root = TempRoot();
        var paths = new AppPaths(root);
        try
        {
            paths.EnsureCreated();
            File.WriteAllText(paths.DatabaseFile, "db");
            File.WriteAllText(paths.DatabaseFile + "-wal", "wal");
            File.WriteAllText(Path.Combine(paths.WebView2UserDataFolder, "cookie"), "x");
            File.WriteAllText(Path.Combine(paths.IconsFolder, "netflix.png"), "x");
            File.WriteAllText(Path.Combine(paths.LogsFolder, "app.log"), "log");
            DataReset.Schedule(paths);
            DataReset.IsScheduled(paths).Should().BeTrue();

            var removed = DataReset.ExecuteIfScheduled(paths, out var errors, attempts: 1, retryDelayMs: 0);

            errors.Should().BeEmpty();
            removed.Should().Contain(paths.DatabaseFile).And.Contain(paths.WebView2UserDataFolder).And.Contain(paths.IconsFolder);
            File.Exists(paths.DatabaseFile).Should().BeFalse();
            Directory.Exists(paths.WebView2UserDataFolder).Should().BeFalse();
            Directory.Exists(paths.IconsFolder).Should().BeFalse();
            File.Exists(Path.Combine(paths.LogsFolder, "app.log")).Should().BeTrue("로그는 남긴다");
            DataReset.IsScheduled(paths).Should().BeFalse("완료하면 표식을 지운다");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Cancel_removes_marker()
    {
        var root = TempRoot();
        var paths = new AppPaths(root);
        try
        {
            DataReset.Schedule(paths);
            DataReset.Cancel(paths);
            DataReset.IsScheduled(paths).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "moaview-reset-" + Guid.NewGuid().ToString("N"));
}
