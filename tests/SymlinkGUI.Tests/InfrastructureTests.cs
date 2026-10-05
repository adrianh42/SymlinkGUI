using Microsoft.Win32;
using SymlinkGUI.Core;

namespace SymlinkGUI.Tests;

public class PickStoreTests
{
    [Fact]
    public void PicksWithinWindowAreMerged()
    {
        using var tmp = new TempDir();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var store = new PickStore(tmp.Combine("picked.json"), clock, TimeSpan.FromSeconds(2));

        store.Pick([tmp.Combine("a")]);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        store.Pick([tmp.Combine("b")]);
        store.Pick([tmp.Combine("a")]); // duplicate ignored

        Assert.Equal([tmp.Combine("a"), tmp.Combine("b")], store.GetPicked());
    }

    [Fact]
    public void PickAfterWindowStartsNewSelection()
    {
        using var tmp = new TempDir();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var store = new PickStore(tmp.Combine("picked.json"), clock, TimeSpan.FromSeconds(2));

        store.Pick([tmp.Combine("a")]);
        clock.Advance(TimeSpan.FromSeconds(10));
        store.Pick([tmp.Combine("b")]);

        Assert.Equal([tmp.Combine("b")], store.GetPicked());
    }

    [Fact]
    public void ConcurrentPicksAreAllKept()
    {
        using var tmp = new TempDir();
        var file = tmp.Combine("picked.json");
        var paths = Enumerable.Range(0, 20).Select(i => tmp.Combine($"item{i}")).ToArray();

        Parallel.ForEach(paths, p => new PickStore(file).Pick([p]));

        Assert.Equal(paths.Order(), new PickStore(file).GetPicked().Order());
    }

    [Fact]
    public void EmptyAndCorruptStoresReturnNothing()
    {
        using var tmp = new TempDir();
        var file = tmp.Combine("picked.json");
        Assert.Empty(new PickStore(file).GetPicked());

        File.WriteAllText(file, "{ not json");
        Assert.Empty(new PickStore(file).GetPicked());
    }
}

public class CommandLineTests
{
    [Theory]
    [InlineData(@"C:\Users\me", @"C:\Users\me")]
    [InlineData(@"C:\Users\me\", @"C:\Users\me")]
    [InlineData("C:\"", @"C:\")]      // Explorer's "%V" for a drive root, as parsed by the CRT
    [InlineData("C:", @"C:\")]
    public void NormalizePathArgument(string raw, string expected) =>
        Assert.Equal(expected, CommandLine.NormalizePathArgument(raw));

    [Fact]
    public void Join_QuotesSpacesAndTrailingBackslashes()
    {
        Assert.Equal(@"--create-links symlink ""C:\My Folder"" D:\ ""E:\My Dir\\"" plain",
            CommandLine.Join(["--create-links", "symlink", @"C:\My Folder", @"D:\", @"E:\My Dir\", "plain"]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1314)]
    [InlineData(-(int)LinkError.AlreadyExists)]
    public void ExitCodeRoundTrips(int code)
    {
        var decoded = CommandRouter.DecodeExitCode(code, "s", "l");
        Assert.Equal(code, CommandRouter.EncodeExitCode(decoded));
    }
}

public sealed class ContextMenuRegistrarTests : IDisposable
{
    private readonly string _testRoot = $@"Software\SymlinkGUI.Tests\{Guid.NewGuid():N}";
    private string Classes => _testRoot + @"\Classes";
    private const string Exe = @"C:\Apps\SymlinkGUI\SymlinkGUI.exe";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_testRoot, throwOnMissingSubKey: false);

    private string? ReadCommand(string relative)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{Classes}\{relative}\command");
        return key?.GetValue(null) as string;
    }

    [Fact]
    public void InstallWithSingleTypeCreatesFlatVerbs()
    {
        var reg = new ContextMenuRegistrar(Exe, [LinkType.SymbolicLink], Registry.CurrentUser, Classes);
        reg.Install();

        Assert.True(reg.IsInstalled);
        Assert.False(reg.IsStale);
        Assert.Equal(Exe, reg.RegisteredExecutablePath);
        Assert.Equal($"\"{Exe}\" --pick \"%1\"", ReadCommand(@"*\shell\SymlinkGUI.Pick"));
        Assert.Equal($"\"{Exe}\" --pick \"%1\"", ReadCommand(@"Directory\shell\SymlinkGUI.Pick"));
        Assert.Equal($"\"{Exe}\" --open \"%1\"", ReadCommand(@"*\shell\SymlinkGUI.Open"));
        Assert.Equal($"\"{Exe}\" --drop symlink \"%V\"", ReadCommand(@"Directory\Background\shell\SymlinkGUI.Drop"));
        Assert.Equal($"\"{Exe}\" --drop symlink \"%1\"", ReadCommand(@"Directory\shell\SymlinkGUI.Drop"));
    }

    [Fact]
    public void InstallWithMultipleTypesCreatesSubmenu()
    {
        var reg = new ContextMenuRegistrar(Exe, [LinkType.SymbolicLink, LinkType.Junction], Registry.CurrentUser, Classes);
        reg.Install();

        Assert.Null(ReadCommand(@"Directory\Background\shell\SymlinkGUI.Drop"));
        Assert.Equal($"\"{Exe}\" --drop symlink \"%V\"", ReadCommand(@"Directory\Background\shell\SymlinkGUI.Drop\shell\01_symlink"));
        Assert.Equal($"\"{Exe}\" --drop junction \"%V\"", ReadCommand(@"Directory\Background\shell\SymlinkGUI.Drop\shell\02_junction"));
    }

    [Fact]
    public void UninstallRemovesOnlyOurVerbs()
    {
        using (var other = Registry.CurrentUser.CreateSubKey($@"{Classes}\*\shell\SomeoneElse"))
            other.SetValue(null, "keep me");

        var reg = new ContextMenuRegistrar(Exe, [LinkType.SymbolicLink], Registry.CurrentUser, Classes);
        reg.Install();
        reg.Uninstall();

        Assert.False(reg.IsInstalled);
        using var shell = Registry.CurrentUser.OpenSubKey($@"{Classes}\*\shell")!;
        Assert.Equal(["SomeoneElse"], shell.GetSubKeyNames());
    }

    [Fact]
    public void MovedExecutableIsDetectedAsStale()
    {
        new ContextMenuRegistrar(Exe, [LinkType.SymbolicLink], Registry.CurrentUser, Classes).Install();
        var moved = new ContextMenuRegistrar(@"D:\Elsewhere\SymlinkGUI.exe", [LinkType.SymbolicLink], Registry.CurrentUser, Classes);
        Assert.True(moved.IsStale);
    }
}
