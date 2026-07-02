using System;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace CmcMidiRouter;

public class ConfigurationForm : Form
{
    private MidiRouter _midiRouter;
    
    // Tab 1: Virtual Ports
    private ListBox virtualPortsList;
    private TextBox txtNewPort;
    private Button btnAddPort;
    private Button btnRemovePort;

    // Tab 2: Routing
    private ListBox listInputs;
    private ListBox listOutputs;
    private Panel routingPanel;
    private Button btnConnect;
    private Button btnDisconnect;
    private Button btnSave;
    private Button btnExport;
    private Button btnImport;
    private Button btnSelectDevices;
    private System.Collections.Generic.List<Tuple<string, string>> _activeRoutes;

    public ConfigurationForm(MidiRouter midiRouter)
    {
        _midiRouter = midiRouter;
        _activeRoutes = DatabaseManager.GetRoutes();
        _midiRouter.DevicesRefreshed += OnDevicesRefreshed;

        this.Text = "FantonHUB - MIDI Router";
        this.Width = 800;
        this.Height = 500;
        this.StartPosition = FormStartPosition.CenterScreen;

        var tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) };
        
        var tabRouting = new TabPage("Matriz de Roteamento MIDI");
        var tabPorts = new TabPage("Portas MIDI Virtuais");

        BuildRoutingTab(tabRouting);
        BuildVirtualPortsTab(tabPorts);

        tabControl.TabPages.Add(tabRouting);
        tabControl.TabPages.Add(tabPorts);

        this.Controls.Add(tabControl);

        RefreshData();
    }

    private void BuildVirtualPortsTab(TabPage tab)
    {
        tab.BackColor = SystemColors.Control;
        
        var panelTop = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
        var lbl = new Label { Text = "Nome da nova porta:", AutoSize = true, Location = new Point(10, 20) };
        txtNewPort = new TextBox { Location = new Point(150, 17), Width = 200 };
        btnAddPort = new Button { Text = "+ Adicionar", Location = new Point(360, 15), Width = 100 };
        btnAddPort.Click += BtnAddPort_Click;

        panelTop.Controls.Add(lbl);
        panelTop.Controls.Add(txtNewPort);
        panelTop.Controls.Add(btnAddPort);

        var panelCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        virtualPortsList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        panelCenter.Controls.Add(virtualPortsList);

        var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
        btnRemovePort = new Button { Text = "- Remover Porta Selecionada", Dock = DockStyle.Left, Width = 250 };
        btnRemovePort.Click += BtnRemovePort_Click;
        panelBottom.Controls.Add(btnRemovePort);

        tab.Controls.Add(panelCenter);
        tab.Controls.Add(panelTop);
        tab.Controls.Add(panelBottom);
    }

    private void BuildRoutingTab(TabPage tab)
    {
        tab.BackColor = Color.FromArgb(120, 130, 150); // Cor de fundo estilo MIDI-OX

        routingPanel = new DoubleBufferedPanel { Dock = DockStyle.Fill };
        routingPanel.Paint += RoutingPanel_Paint;

        listInputs = new ListBox { Location = new Point(20, 50), Width = 250, Height = 320, IntegralHeight = false, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
        listOutputs = new ListBox { Location = new Point(500, 50), Width = 250, Height = 320, IntegralHeight = false, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };

        var lblIn = new Label { Text = "Input Ports", Location = new Point(20, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent };
        var lblOut = new Label { Text = "Output Ports", Location = new Point(500, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent };

        btnConnect = new Button { Text = "Conectar", Location = new Point(320, 150), Width = 120, BackColor = Color.LightGreen };
        btnDisconnect = new Button { Text = "Desconectar", Location = new Point(320, 190), Width = 120, BackColor = Color.LightCoral };
        btnSave = new Button { Text = "Salvar Roteamento", Location = new Point(320, 230), Width = 120, BackColor = Color.LightSkyBlue };
        btnExport = new Button { Text = "Exportar...", Location = new Point(320, 270), Width = 120, BackColor = Color.Gainsboro };
        btnImport = new Button { Text = "Importar...", Location = new Point(320, 310), Width = 120, BackColor = Color.Gainsboro };
        
        btnSelectDevices = new Button { Text = "Dispositivos Físicos...", Location = new Point(305, 50), Width = 150, BackColor = Color.WhiteSmoke };

        btnConnect.Click += BtnConnect_Click;
        btnDisconnect.Click += BtnDisconnect_Click;
        btnSave.Click += BtnSave_Click;
        btnExport.Click += BtnExport_Click;
        btnImport.Click += BtnImport_Click;
        btnSelectDevices.Click += BtnSelectDevices_Click;
        listInputs.SelectedIndexChanged += (s, e) => routingPanel.Invalidate();
        listOutputs.SelectedIndexChanged += (s, e) => routingPanel.Invalidate();

        routingPanel.Controls.Add(listInputs);
        routingPanel.Controls.Add(listOutputs);
        routingPanel.Controls.Add(lblIn);
        routingPanel.Controls.Add(lblOut);
        routingPanel.Controls.Add(btnConnect);
        routingPanel.Controls.Add(btnDisconnect);
        routingPanel.Controls.Add(btnSave);
        routingPanel.Controls.Add(btnExport);
        routingPanel.Controls.Add(btnImport);
        routingPanel.Controls.Add(btnSelectDevices);

        tab.Controls.Add(routingPanel);
    }

    private void BtnSelectDevices_Click(object? sender, EventArgs e)
    {
        var frm = new DeviceSelectionForm(_midiRouter);
        frm.ShowDialog(this);
    }

    private void BtnAddPort_Click(object? sender, EventArgs e)
    {
        string portName = txtNewPort.Text.Trim();
        if (!string.IsNullOrEmpty(portName))
        {
            _midiRouter.CreateVirtualPort(portName);
            txtNewPort.Clear();
            RefreshData();
        }
    }

    private void BtnRemovePort_Click(object? sender, EventArgs e)
    {
        if (virtualPortsList.SelectedItem is string portName)
        {
            _midiRouter.RemoveVirtualPort(portName);
            RefreshData();
        }
    }

    private void BtnConnect_Click(object? sender, EventArgs e)
    {
        if (listInputs.SelectedItem is string input && listOutputs.SelectedItem is string output)
        {
            var route = new Tuple<string, string>(input, output);
            if (!_activeRoutes.Any(r => r.Item1 == input && r.Item2 == output))
            {
                _activeRoutes.Add(route);
                routingPanel.Invalidate();
            }
        }
    }

    private void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        if (listInputs.SelectedItem is string input && listOutputs.SelectedItem is string output)
        {
            _activeRoutes.RemoveAll(r => r.Item1 == input && r.Item2 == output);
            routingPanel.Invalidate();
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        DatabaseManager.SaveAllRoutes(_activeRoutes);
        MessageBox.Show("Configuração de roteamento salva com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public class RoutingProfile
    {
        public System.Collections.Generic.List<string> VirtualPorts { get; set; } = new();
        public System.Collections.Generic.List<RouteItem> Routes { get; set; } = new();
    }

    public class RouteItem
    {
        public string Source { get; set; } = "";
        public string Dest { get; set; } = "";
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using (var sfd = new SaveFileDialog { Filter = "JSON File|*.json", Title = "Exportar Perfil de Roteamento", FileName = "PerfilMidi.json" })
        {
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var profile = new RoutingProfile { VirtualPorts = DatabaseManager.GetVirtualPorts() };
                foreach (var r in _activeRoutes) profile.Routes.Add(new RouteItem { Source = r.Item1, Dest = r.Item2 });
                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(sfd.FileName, json);
                MessageBox.Show("Perfil exportado com sucesso!", "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private void BtnImport_Click(object? sender, EventArgs e)
    {
        using (var ofd = new OpenFileDialog { Filter = "JSON File|*.json", Title = "Importar Perfil de Roteamento" })
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var profile = JsonSerializer.Deserialize<RoutingProfile>(json);
                    if (profile != null)
                    {
                        foreach (var vp in profile.VirtualPorts)
                        {
                            _midiRouter.CreateVirtualPort(vp);
                        }
                        
                        _activeRoutes.Clear();
                        foreach (var r in profile.Routes)
                        {
                            _activeRoutes.Add(new Tuple<string, string>(r.Source, r.Dest));
                        }
                        
                        DatabaseManager.SaveAllRoutes(_activeRoutes);
                        RefreshData();
                        MessageBox.Show("Perfil importado com sucesso!", "Importar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao importar perfil: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private void OnDevicesRefreshed(object? sender, EventArgs e)
    {
        if (this.InvokeRequired) this.Invoke(new Action(RefreshData));
        else RefreshData();
    }

    private void RefreshData()
    {
        // Refresh Virtual Ports List
        virtualPortsList.Items.Clear();
        foreach (var p in DatabaseManager.GetVirtualPorts()) virtualPortsList.Items.Add(p);

        // Refresh Routing Matrix
        var allVirtuals = DatabaseManager.GetVirtualPorts();
        
        string inSel = listInputs.SelectedItem as string ?? "";
        string outSel = listOutputs.SelectedItem as string ?? "";

        listInputs.Items.Clear();
        var allInputs = _midiRouter.ConnectedInputNames.Concat(allVirtuals).Distinct().ToList();
        foreach (var i in allInputs) listInputs.Items.Add(i);

        listOutputs.Items.Clear();
        var allOutputs = _midiRouter.ConnectedOutputNames.Concat(allVirtuals).Distinct().ToList();
        foreach (var o in allOutputs) listOutputs.Items.Add(o);

        if (listInputs.Items.Contains(inSel)) listInputs.SelectedItem = inSel;
        if (listOutputs.Items.Contains(outSel)) listOutputs.SelectedItem = outSel;

        if (routingPanel != null) routingPanel.Invalidate();
    }

    private void RoutingPanel_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var routes = _activeRoutes;

        string selIn = listInputs.SelectedItem as string ?? "";
        string selOut = listOutputs.SelectedItem as string ?? "";

        foreach (var route in routes)
        {
            int idxIn = listInputs.Items.IndexOf(route.Item1);
            int idxOut = listOutputs.Items.IndexOf(route.Item2);

            if (idxIn >= 0 && idxOut >= 0)
            {
                var rectIn = listInputs.GetItemRectangle(idxIn);
                var rectOut = listOutputs.GetItemRectangle(idxOut);

                Point ptIn = new Point(listInputs.Right, listInputs.Top + rectIn.Top + rectIn.Height / 2);
                Point ptOut = new Point(listOutputs.Left, listOutputs.Top + rectOut.Top + rectOut.Height / 2);

                bool isHighlighted = (route.Item1 == selIn || route.Item2 == selOut);
                
                using var pen = new Pen(isHighlighted ? Color.White : Color.LightGray, isHighlighted ? 3 : 1);
                e.Graphics.DrawLine(pen, ptIn, ptOut);
                
                // Draw nodes at connection points
                using var brush = new SolidBrush(Color.LightBlue);
                e.Graphics.FillEllipse(brush, ptIn.X - 4, ptIn.Y - 4, 8, 8);
                e.Graphics.FillEllipse(brush, ptOut.X - 4, ptOut.Y - 4, 8, 8);
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _midiRouter.DevicesRefreshed -= OnDevicesRefreshed;
        base.OnFormClosing(e);
    }
}

public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
}
