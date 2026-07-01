using System;
using System.Windows.Forms;
using Serilog;

namespace CmcMidiRouter;

public class HiddenMessageWindow : Form
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const int DBT_DEVNODES_CHANGED = 0x0007;
    
    public event EventHandler? DeviceChanged;

    public HiddenMessageWindow()
    {
        this.Text = "CmcMidiRouter Hidden Window";
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Visible = false;
        this.Opacity = 0;
        
        // Crucial: forçar a criação da janela na API do Windows para podermos receber o WndProc
        var _ = this.Handle;
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false); // Garante que nunca ficará visível
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_DEVICECHANGE)
        {
            int wParam = m.WParam.ToInt32();
            if (wParam == DBT_DEVICEARRIVAL || wParam == DBT_DEVICEREMOVECOMPLETE || wParam == DBT_DEVNODES_CHANGED)
            {
                Log.Debug($"WM_DEVICECHANGE recebido (wParam: {wParam:X4}). Notificando alteração de dispositivo...");
                DeviceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
