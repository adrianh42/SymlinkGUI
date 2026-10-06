using SymlinkGUI.Core;

namespace SymlinkGUI.Tests;

public class LinkServiceTests
{
    private static readonly LinkService Service = LinkService.Default;

    [Theory]
    [InlineData("My Link")]
    [InlineData("file.txt")]
    [InlineData("v1.2")]
    public void ValidateName_AcceptsValidNames(string name) => Assert.Null(LinkService.ValidateName(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("what?")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    public void ValidateName_RejectsInvalidNames(string name) => Assert.NotNull(LinkService.ValidateName(name));

    [Fact]
    public void GetUniquePath_ReturnsOriginalWhenFree()
    {
        using var tmp = new TempDir();
        Assert.Equal(tmp.Combine("a.txt"), LinkService.GetUniquePath(tmp.Path, "a.txt", isDirectory: false));
    }

    [Fact]
    public void GetUniquePath_FilesPutCounterBeforeExtension()
    {
        using var tmp = new TempDir();
        tmp.CreateFile("a.txt");
        tmp.CreateFile("a (2).txt");
        Assert.Equal(tmp.Combine("a (3).txt"), LinkService.GetUniquePath(tmp.Path, "a.txt", isDirectory: false));
    }

    [Fact]
    public void GetUniquePath_DirectoriesKeepFullName()
    {
        using var tmp = new TempDir();
        tmp.CreateDir("v1.2");
        Assert.Equal(tmp.Combine("v1.2 (2)"), LinkService.GetUniquePath(tmp.Path, "v1.2", isDirectory: true));
    }

    [Fact]
    public void CreateLink_FailsWhenSourceMissing()
    {
        using var tmp = new TempDir();
        var r = Service.CreateLink(LinkType.SymbolicLink, tmp.Combine("missing"), tmp.Combine("link"));
        Assert.Equal(LinkError.SourceNotFound, r.Error);
    }

    [Fact]
    public void CreateLink_FailsWhenDestinationFolderMissing()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateFile("src.txt");
        var r = Service.CreateLink(LinkType.SymbolicLink, src, tmp.Combine("nope", "link.txt"));
        Assert.Equal(LinkError.DestinationNotFound, r.Error);
    }

    [Fact]
    public void CreateLink_FailsWhenLinkPathExists()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateFile("src.txt");
        var existing = tmp.CreateFile("taken.txt");
        var r = Service.CreateLink(LinkType.SymbolicLink, src, existing);
        Assert.Equal(LinkError.AlreadyExists, r.Error);
        Assert.Equal("hello", File.ReadAllText(existing)); // never overwritten
    }

    [Fact]
    public void CreateLink_UnsupportedTypeIsRejected()
    {
        var customService = new LinkService([new SymbolicLinkCreator()]);
        using var tmp = new TempDir();
        var src = tmp.CreateFile("src.txt");
        var r = customService.CreateLink(LinkType.Junction, src, tmp.Combine("j"));
        Assert.False(r.Success);
        Assert.Equal(LinkError.NotSupportedForSource, r.Error);
    }

    [Fact]
    public void CreateLink_CreatesDirectoryJunction()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateDir("srcdir");
        File.WriteAllText(Path.Combine(src, "inner.txt"), "hello junction");
        var link = tmp.Combine("junc");

        var r = Service.CreateLink(LinkType.Junction, src, link);

        Assert.True(r.Success, r.Message);
        Assert.True(Directory.Exists(link));
        Assert.True(File.Exists(Path.Combine(link, "inner.txt")));
        Assert.Equal("hello junction", File.ReadAllText(Path.Combine(link, "inner.txt")));
    }

    [Fact]
    public void CreateLink_JunctionFailsOnSourceFile()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateFile("file.txt");
        var r = Service.CreateLink(LinkType.Junction, src, tmp.Combine("junc"));
        Assert.False(r.Success);
        Assert.Equal(LinkError.NotSupportedForSource, r.Error);
    }

    [Fact]
    public void CreateLink_CreatesHardLink()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateFile("source.txt", "initial");
        var link = tmp.Combine("hardlink.txt");

        var r = Service.CreateLink(LinkType.HardLink, src, link);

        Assert.True(r.Success, r.Message);
        Assert.True(File.Exists(link));
        Assert.Equal("initial", File.ReadAllText(link));

        // Hard links share the same file data:
        File.WriteAllText(src, "updated");
        Assert.Equal("updated", File.ReadAllText(link));
    }

    [Fact]
    public void CreateLink_HardLinkFailsOnDirectory()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateDir("srcdir");
        var r = Service.CreateLink(LinkType.HardLink, src, tmp.Combine("link.txt"));
        Assert.False(r.Success);
        Assert.Equal(LinkError.NotSupportedForSource, r.Error);
    }

    [Fact]
    public void HardLinkCreator_FailsAcrossDrives()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateFile("source.txt");
        var creator = new HardLinkCreator();
        var r = creator.Create(src, @"Z:\dummy\link.txt");
        Assert.Equal(LinkError.NotSameDrive, r.Error);
    }

    [Fact]
    public void DropLinks_Junctions_RenamesOnCollision()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateDir("myfolder");
        var dest = tmp.CreateDir("dest");
        tmp.CreateDir(Path.Combine("dest", "myfolder"));

        var results = Service.DropLinks(LinkType.Junction, [src], dest);
        var r = Assert.Single(results);
        Assert.True(r.Success, r.Message);
        Assert.Equal(Path.Combine(dest, "myfolder (2)"), r.LinkPath);
        Assert.True(Directory.Exists(r.LinkPath));
    }

    [Fact]
    public void DropLinks_HardLinks_RenamesOnCollision()
    {
        using var tmp = new TempDir();
        var src = tmp.CreateFile("doc.txt", "content");
        var dest = tmp.CreateDir("dest");
        tmp.CreateFile(Path.Combine("dest", "doc.txt"), "already there");

        var results = Service.DropLinks(LinkType.HardLink, [src], dest);
        var r = Assert.Single(results);
        Assert.True(r.Success, r.Message);
        Assert.Equal(Path.Combine(dest, "doc (2).txt"), r.LinkPath);
        Assert.True(File.Exists(r.LinkPath));
        Assert.Equal("content", File.ReadAllText(r.LinkPath));
    }

    [SkippableFact]
    public void CreateLink_CreatesFileSymlink()
    {
        Skip.IfNot(Elevation.CanCreateSymlinksUnprivileged, "Needs admin or Developer Mode.");
        using var tmp = new TempDir();
        var src = tmp.CreateFile("src.txt", "content");
        var link = tmp.Combine("link.txt");

        var r = Service.CreateLink(LinkType.SymbolicLink, src, link);

        Assert.True(r.Success, r.Message);
        var info = new FileInfo(link);
        Assert.Equal(src, info.LinkTarget);
        Assert.Equal("content", File.ReadAllText(link));
    }

    [SkippableFact]
    public void CreateLink_CreatesDirectorySymlink()
    {
        Skip.IfNot(Elevation.CanCreateSymlinksUnprivileged, "Needs admin or Developer Mode.");
        using var tmp = new TempDir();
        var src = tmp.CreateDir("srcdir");
        File.WriteAllText(Path.Combine(src, "inner.txt"), "x");
        var link = tmp.Combine("linkdir");

        var r = Service.CreateLink(LinkType.SymbolicLink, src, link);

        Assert.True(r.Success, r.Message);
        Assert.Equal(src, new DirectoryInfo(link).LinkTarget);
        Assert.True(File.Exists(Path.Combine(link, "inner.txt")));
    }

    [SkippableFact]
    public void DropLinks_RenamesOnCollision()
    {
        Skip.IfNot(Elevation.CanCreateSymlinksUnprivileged, "Needs admin or Developer Mode.");
        using var tmp = new TempDir();
        var src = tmp.CreateFile("a.txt");
        var dest = tmp.CreateDir("dest");
        File.WriteAllText(Path.Combine(dest, "a.txt"), "occupied");

        var results = Service.DropLinks(LinkType.SymbolicLink, [src], dest);

        var r = Assert.Single(results);
        Assert.True(r.Success, r.Message);
        Assert.Equal(Path.Combine(dest, "a (2).txt"), r.LinkPath);
    }

    [Fact]
    public void DropLinks_ReportsMissingSources()
    {
        using var tmp = new TempDir();
        var results = Service.DropLinks(LinkType.SymbolicLink, [tmp.Combine("gone.txt")], tmp.Path);
        Assert.Equal(LinkError.SourceNotFound, Assert.Single(results).Error);
    }
}
