using System.Diagnostics;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string dllPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "CmcMidiRouter.dll");
        
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "dotnet";
        psi.Arguments = "\"" + dllPath + "\"";
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        
        Process.Start(psi);
    }
}
