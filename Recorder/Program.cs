using Velopack;

namespace Recorder;

static class Program
{
    [STAThread]
    static void Main()
    {
        VelopackApp.Build().Run();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
