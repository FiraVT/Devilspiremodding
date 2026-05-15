using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
using System.Text;

namespace DevilSpireLauncher;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        MainAsync(args).GetAwaiter().GetResult();
    }

    static async Task MainAsync(string[] args)
    {
        File.AppendAllText("cli_debug.log", "Args: " + string.Join(" ", args) + "\n");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
