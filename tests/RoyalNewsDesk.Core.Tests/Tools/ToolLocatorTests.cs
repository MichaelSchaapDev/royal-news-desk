using RoyalNewsDesk.Core.Tools;

namespace RoyalNewsDesk.Core.Tests.Tools;

public class ToolLocatorTests
{
    [Fact]
    public void ResolvesAllToolsUnderTheBaseDir()
    {
        var locator = new InstalledToolLocator(@"C:\base");

        Assert.Equal(@"C:\base\ffmpeg\ffmpeg.exe", locator.GetToolPath(ExternalTool.Ffmpeg));
        Assert.Equal(@"C:\base\ffmpeg\ffprobe.exe", locator.GetToolPath(ExternalTool.Ffprobe));
        Assert.Equal(@"C:\base\piper\piper.exe", locator.GetToolPath(ExternalTool.Piper));
        Assert.Equal(@"C:\base\rhubarb\rhubarb.exe", locator.GetToolPath(ExternalTool.Rhubarb));
    }
}
