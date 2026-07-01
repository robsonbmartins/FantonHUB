using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using NAudio.Midi;
using System.Linq;

namespace CmcMidiRouter;

public class DeviceSelectionForm : Form
{
    private MidiRouter _midiRouter;
    private CheckedListBox chkInputs;
    private CheckedListBox chkOutputs;
    private Button btnSave;

    private class DeviceItem
    {
        public string RealName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public override string ToString() => DisplayName;
    }

    public DeviceSelectionForm(MidiRouter midiRouter)
    {
        _midiRouter = midiRouter;

        this.Text = "Selecionar Dispositivos Físicos MIDI";
        this.Width = 600;
        this.Height = 500;
        this.StartPosition = FormStartPosition.CenterScreen;

        var lblInfo = new Label
        {
            Text = "Marque os dispositivos físicos que você deseja disponibilizar na Matriz de Roteamento.",
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            AutoSize = true
        };

        var panelLists = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        panelLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        panelLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        panelLists.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        panelLists.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var lblIn = new Label { Text = "Entradas MIDI", Font = new Font(this.Font, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
        var lblOut = new Label { Text = "Saídas MIDI", Font = new Font(this.Font, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };

        chkInputs = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        chkOutputs = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };

        panelLists.Controls.Add(lblIn, 0, 0);
        panelLists.Controls.Add(lblOut, 1, 0);
        panelLists.Controls.Add(chkInputs, 0, 1);
        panelLists.Controls.Add(chkOutputs, 1, 1);

        var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
        btnSave = new Button { Text = "Salvar e Aplicar", Dock = DockStyle.Right, Width = 150 };
        btnSave.Click += BtnSave_Click;
        panelBottom.Controls.Add(btnSave);

        this.Controls.Add(panelLists);
        this.Controls.Add(lblInfo);
        this.Controls.Add(panelBottom);

        LoadDevices();
    }

    private bool IsCmcDevice(string name)
    {
        return name.Contains("Steinberg CMC", StringComparison.OrdinalIgnoreCase) || 
               name.Contains("CMC-FD", StringComparison.OrdinalIgnoreCase) || 
               name.Contains("CMC-PD", StringComparison.OrdinalIgnoreCase);
    }

    private void LoadDevices()
    {
        var enabledInputs = DatabaseManager.GetEnabledPhysicalPorts(true);
        var enabledOutputs = DatabaseManager.GetEnabledPhysicalPorts(false);
        var virtualPorts = DatabaseManager.GetVirtualPorts();

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            try
            {
                string name = MidiIn.DeviceInfo(i).ProductName;
                if (virtualPorts.Contains(name)) continue;

                var item = new DeviceItem { RealName = name, DisplayName = name };
                if (IsCmcDevice(name)) item.DisplayName += " (Compatível / CMC)";
                
                bool isChecked = enabledInputs.Contains(name);
                chkInputs.Items.Add(item, isChecked);
            }
            catch { }
        }

        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            try
            {
                string name = MidiOut.DeviceInfo(i).ProductName;
                if (virtualPorts.Contains(name)) continue;

                var item = new DeviceItem { RealName = name, DisplayName = name };
                if (IsCmcDevice(name)) item.DisplayName += " (Compatível / CMC)";
                
                bool isChecked = enabledOutputs.Contains(name);
                chkOutputs.Items.Add(item, isChecked);
            }
            catch { }
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var selectedInputs = new List<string>();
        foreach (var item in chkInputs.CheckedItems)
        {
            if (item is DeviceItem di) selectedInputs.Add(di.RealName);
        }

        var selectedOutputs = new List<string>();
        foreach (var item in chkOutputs.CheckedItems)
        {
            if (item is DeviceItem di) selectedOutputs.Add(di.RealName);
        }

        DatabaseManager.SetEnabledPhysicalPorts(selectedInputs, true);
        DatabaseManager.SetEnabledPhysicalPorts(selectedOutputs, false);

        _midiRouter.RefreshDevices();

        MessageBox.Show("Dispositivos atualizados com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        this.Close();
    }
}
