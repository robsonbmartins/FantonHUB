using System;
using System.Drawing;
using System.Windows.Forms;
using Serilog;

namespace CmcMidiRouter;

public class TrayApplicationContext : ApplicationContext
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip contextMenu;
    private MidiRouter midiRouter;
    private HiddenMessageWindow messageWindow;
    private System.Windows.Forms.Timer debounceTimer;
    private System.Windows.Forms.Timer pollTimer;
    private System.Collections.Generic.List<string> lastConnectedDevices = new System.Collections.Generic.List<string>();

    public TrayApplicationContext()
    {
        Log.Information("Iniciando a interface Tray Icon...");

        midiRouter = new MidiRouter();
        midiRouter.StartRouting();

        var initialDevices = DeviceDiscovery.EnumerateCmcDevices();
        foreach (var dev in initialDevices)
            lastConnectedDevices.Add(dev.InstanceId);

        messageWindow = new HiddenMessageWindow();
        messageWindow.DeviceChanged += OnDeviceChanged;

        debounceTimer = new System.Windows.Forms.Timer();
        debounceTimer.Interval = 1500;
        debounceTimer.Tick += DebounceTimer_Tick;

        pollTimer = new System.Windows.Forms.Timer();
        pollTimer.Interval = 2000;
        pollTimer.Tick += PollTimer_Tick;
        pollTimer.Start();

        // Initialize Context Menu
        contextMenu = new ContextMenuStrip();
        
        var mnuAutoStart = new ToolStripMenuItem("Iniciar com o Windows");
        mnuAutoStart.CheckOnClick = true;
        mnuAutoStart.Checked = CheckAutoStart();
        mnuAutoStart.CheckedChanged += AutoStart_CheckedChanged;

        contextMenu.Items.Add(mnuAutoStart);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Configurações", null, ShowConfig);
        contextMenu.Items.Add("Monitor MIDI", null, ShowMonitor);
        contextMenu.Items.Add("Ver Logs", null, ShowLogs);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Sair", null, Exit);

        // Initialize Tray Icon
        Icon appIcon = SystemIcons.Application;
        try 
        { 
            string iconPath = System.IO.Path.Combine(Application.StartupPath, "app.ico");
            if (System.IO.File.Exists(iconPath))
                appIcon = new Icon(iconPath); 
        } 
        catch { }

        trayIcon = new NotifyIcon()
        {
            Icon = appIcon,
            ContextMenuStrip = contextMenu,
            Visible = true,
            Text = "FantonHUB - MIDI Router"
        };
    }

    private bool CheckAutoStart()
    {
        using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false)!)
        {
            return key?.GetValue("CmcMidiRouter") != null;
        }
    }

    private void AutoStart_CheckedChanged(object? sender, EventArgs e)
    {
        var item = sender as ToolStripMenuItem;
        if (item == null) return;

        using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!)
        {
            if (item.Checked)
                key?.SetValue("CmcMidiRouter", Application.ExecutablePath);
            else
                key?.DeleteValue("CmcMidiRouter", false);
        }
    }

    private void ShowConfig(object? sender, EventArgs e)
    {
        new ConfigurationForm(midiRouter).Show();
    }

    private void ShowMonitor(object? sender, EventArgs e)
    {
        new MidiMonitorForm(midiRouter).Show();
    }

    private void ShowLogs(object? sender, EventArgs e)
    {
        new LogViewerForm().Show();
    }

    private void OnDeviceChanged(object? sender, EventArgs e)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        debounceTimer.Stop();
        Log.Information("Disparando re-verificação de portas MIDI devido a mensagem do sistema...");
        midiRouter.RefreshDevices();
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        try 
        {
            var currentDevices = DeviceDiscovery.EnumerateCmcDevices();
            var currentIds = new System.Collections.Generic.List<string>();
            foreach (var d in currentDevices) currentIds.Add(d.InstanceId);

            bool changed = (currentIds.Count != lastConnectedDevices.Count);
            if (!changed)
            {
                foreach (var id in currentIds)
                {
                    if (!lastConnectedDevices.Contains(id))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                Log.Information("Alteração de hardware USB detectada! Atualizando rotas MIDI...");
                lastConnectedDevices = currentIds;
                midiRouter.RefreshDevices();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro no loop de verificação de hardware.");
        }
    }

    private void Exit(object? sender, EventArgs e)
    {
        Log.Information("Encerrando a aplicação...");
        pollTimer?.Dispose();
        midiRouter.Dispose();
        debounceTimer.Dispose();
        messageWindow.Dispose();
        trayIcon.Visible = false;
        Application.Exit();
    }
}
