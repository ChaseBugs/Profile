using SurvNetTool;

namespace SurvNetTool;

internal static class Program
{
    [STAThread]  // Required for OpenFileDialog / SaveFileDialog (COM-based)
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
