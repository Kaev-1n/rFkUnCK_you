using Avalonia;

namespace Rfkunck.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = new Mutex(initiallyOwned: true, @"Local\rFkUnCK_you", out bool firstInstance);
        if (firstInstance)
            AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
