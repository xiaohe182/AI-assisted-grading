using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

class Launcher
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool SetDllDirectory(string path);

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [STAThread]
    static int Main(string[] args)
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) {}
        bool first;
        string instanceName = @"Local\YueJuanTai.Activate.84a0ab5d";
        if (Array.IndexOf(args, "--smoke-test") >= 0)
            instanceName += ".test." + System.Diagnostics.Process.GetCurrentProcess().Id;
        using (var activate = new EventWaitHandle(false, EventResetMode.AutoReset,
            instanceName, out first))
        {
            if (!first)
            {
                activate.Set();
                return 0;
            }
            return Run(args, activate);
        }
    }

    static int Run(string[] args, EventWaitHandle activate)
    {
        string folder = Path.Combine(Path.GetTempPath(), "YueJuanTai-" + Guid.NewGuid().ToString("N"));
        bool test = Array.IndexOf(args, "--smoke-test") >= 0;
        try
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("workbench.zip"))
            using (var archive = new ZipArchive(stream))
                archive.ExtractToDirectory(folder);
            SetDllDirectory(folder);
            Application.EnableVisualStyles();
            var host = Assembly.LoadFrom(Path.Combine(folder, "Workbench.dll"));
            using (var window = (Form)Activator.CreateInstance(host.GetType("Workbench", true),
                Path.Combine(folder, "client"), test))
            {
                var handle = window.Handle;
                var listener = ThreadPool.RegisterWaitForSingleObject(activate, delegate {
                    try
                    {
                        window.BeginInvoke((Action)delegate {
                            if (window.WindowState == FormWindowState.Minimized)
                                window.WindowState = FormWindowState.Normal;
                            window.Show();
                            window.Activate();
                        });
                    }
                    catch (InvalidOperationException) { /* Window has closed. */ }
                }, null, Timeout.Infinite, false);
                try { Application.Run(window); }
                finally { listener.Unregister(null); }
            }
            return Environment.ExitCode;
        }
        catch (Exception error)
        {
            if (!test) MessageBox.Show(error.GetBaseException().Message, "阅卷台");
            return 1;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
    }
}
