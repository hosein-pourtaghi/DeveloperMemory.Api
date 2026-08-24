using DeveloperMemory.Api.Services;
using Xunit;

namespace DeveloperMemory.Api.Tests.Services;

public class StableIdHelperTests
{
    [Fact]
    public void GenerateFromFilePath_SamePathSameId()
    {
        var id1 = StableIdHelper.GenerateFromFilePath("/home/user/Knowledge/test.md");
        var id2 = StableIdHelper.GenerateFromFilePath("/home/user/Knowledge/test.md");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateFromFilePath_DifferentPathDifferentId()
    {
        var id1 = StableIdHelper.GenerateFromFilePath("/home/user/Knowledge/test1.md");
        var id2 = StableIdHelper.GenerateFromFilePath("/home/user/Knowledge/test2.md");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateFromFilePath_ReturnsNonEmptyGuid()
    {
        var id = StableIdHelper.GenerateFromFilePath("/some/path/file.md");
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void GenerateFromFilePath_CaseInsensitive()
    {
        // Path normalization should make case differences produce the same ID
        var id1 = StableIdHelper.GenerateFromFilePath("/Home/User/Knowledge/Test.md");
        var id2 = StableIdHelper.GenerateFromFilePath("/home/user/knowledge/test.md");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateFromFilePath_HandlesBackslashes()
    {
        // Backslashes should be normalized to forward slashes
        var id1 = StableIdHelper.GenerateFromFilePath("/home/user/Knowledge/test.md");
        var id2 = StableIdHelper.GenerateFromFilePath("\\home\\user\\Knowledge\\test.md");
        Assert.Equal(id1, id2);
    }
}
