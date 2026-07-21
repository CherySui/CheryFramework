namespace CheryFramework.Preview;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using NativeDependencyLoader nativeDependencies = NativeDependencyLoader.Load();
        ApplicationConfiguration.Initialize();
        Application.Run(new PreviewForm());
    }
}
