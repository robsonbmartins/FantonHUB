using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace CmcMidiRouter;

public class MidiMonitorForm : Form
{
    private MidiRouter _midiRouter;
    private ListView listView;
    private ComboBox cmbFilter;
    private Button btnClear;
    private Button btnPause;
    
    private bool _isPaused = false;
    private const int MAX_ITEMS = 1000; // Limite de linhas para evitar estouro de memória

    public MidiMonitorForm(MidiRouter midiRouter)
    {
        _midiRouter = midiRouter;

        this.Text = "Monitor de Tráfego MIDI";
        this.Width = 800;
        this.Height = 500;
        this.StartPosition = FormStartPosition.CenterScreen;

        var panelTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
        
        var lblFilter = new Label { Text = "Filtrar por Porta:", AutoSize = true, Location = new Point(10, 15) };
        cmbFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, 12), Width = 250 };
        cmbFilter.Items.Add("Todas as Portas");
        cmbFilter.SelectedIndex = 0;
        
        btnClear = new Button { Text = "Limpar Tela", Location = new Point(380, 10), Width = 100 };
        btnClear.Click += (s, e) => listView.Items.Clear();

        btnPause = new Button { Text = "Pausar", Location = new Point(490, 10), Width = 100 };
        btnPause.Click += (s, e) => 
        {
            _isPaused = !_isPaused;
            btnPause.Text = _isPaused ? "Retomar" : "Pausar";
        };

        panelTop.Controls.Add(lblFilter);
        panelTop.Controls.Add(cmbFilter);
        panelTop.Controls.Add(btnClear);
        panelTop.Controls.Add(btnPause);

        listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9F)
        };
        
        // Double buffering hack for ListView
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                       ?.SetValue(listView, true, null);

        listView.Columns.Add("Hora", 100);
        listView.Columns.Add("Direção", 50);
        listView.Columns.Add("Porta", 200);
        listView.Columns.Add("Mensagem", 300);

        this.Controls.Add(listView);
        this.Controls.Add(panelTop);

        this.Load += MidiMonitorForm_Load;
        this.FormClosing += MidiMonitorForm_FormClosing;
    }

    private void MidiMonitorForm_Load(object? sender, EventArgs e)
    {
        // Populate filter combo
        var ports = new HashSet<string>();
        foreach (var p in _midiRouter.ConnectedInputNames) ports.Add(p);
        foreach (var p in _midiRouter.ConnectedOutputNames) ports.Add(p);
        foreach (var p in DatabaseManager.GetVirtualPorts()) ports.Add(p);
        
        foreach (var p in ports.OrderBy(p => p)) cmbFilter.Items.Add(p);

        // Ativa o monitoramento no roteador
        _midiRouter.IsMonitoring = true;
        _midiRouter.MessageRouted += OnMessageRouted;
    }

    private void OnMessageRouted(object? sender, MidiMessageEventArgs e)
    {
        if (_isPaused) return;

        string filter = "";
        if (cmbFilter.InvokeRequired)
        {
            cmbFilter.Invoke(new Action(() => filter = cmbFilter.SelectedItem?.ToString() ?? ""));
        }
        else
        {
            filter = cmbFilter.SelectedItem?.ToString() ?? "";
        }

        bool filterAll = filter == "Todas as Portas";
        
        // Regra de Filtro:
        // Se escolheu porta específica, só mostramos se a Origem ou o Destino for ela.
        if (!filterAll && e.SourcePort != filter && e.DestPort != filter) return;

        string direction = "";
        string portDisplay = "";

        if (filterAll)
        {
            portDisplay = $"{e.SourcePort} -> {e.DestPort}";
            direction = "->";
        }
        else
        {
            if (e.SourcePort == filter)
            {
                direction = "->";
                portDisplay = e.DestPort; // Está enviando para o destino
            }
            else if (e.DestPort == filter)
            {
                direction = "<-";
                portDisplay = e.SourcePort; // Está recebendo da origem
            }
        }

        string msgStr = MidiTranslator.Translate(e.Data);
        string timeStr = e.Timestamp.ToString("HH:mm:ss.fff");

        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action(() => AddLogLine(timeStr, direction, portDisplay, msgStr)));
        }
        else
        {
            AddLogLine(timeStr, direction, portDisplay, msgStr);
        }
    }

    private void AddLogLine(string time, string dir, string port, string msg)
    {
        var item = new ListViewItem(new[] { time, dir, port, msg });
        
        if (dir == "<-") item.ForeColor = Color.Cyan;
        else if (dir == "->") item.ForeColor = Color.Orange;

        listView.Items.Add(item);
        
        if (listView.Items.Count > MAX_ITEMS)
        {
            listView.Items.RemoveAt(0);
        }

        // Auto-scroll to bottom
        listView.EnsureVisible(listView.Items.Count - 1);
    }

    private void MidiMonitorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _midiRouter.MessageRouted -= OnMessageRouted;
        _midiRouter.IsMonitoring = false;
    }
}
