using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CmcMidiRouter;

public class LogViewerForm : Form
{
    private RichTextBox richTextBox;
    private System.Windows.Forms.Timer refreshTimer;
    private long lastFileSize = 0;

    public LogViewerForm()
    {
        this.Text = "Visualizador de Logs - CMC MIDI Router";
        this.Width = 900;
        this.Height = 600;
        this.StartPosition = FormStartPosition.CenterScreen;

        richTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
            ForeColor = System.Drawing.Color.LightGray,
            Font = new System.Drawing.Font("Consolas", 10F),
            HideSelection = false
        };
        this.Controls.Add(richTextBox);

        refreshTimer = new System.Windows.Forms.Timer();
        refreshTimer.Interval = 1000;
        refreshTimer.Tick += RefreshTimer_Tick;
        refreshTimer.Start();

        LoadLatestLog();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        LoadLatestLog();
    }

    private void LoadLatestLog()
    {
        try
        {
            string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CmcMidiRouter", "logs");
            if (!Directory.Exists(logsDir)) return;

            var latestFile = Directory.GetFiles(logsDir, "cmc-router*.txt")
                                      .OrderByDescending(f => File.GetLastWriteTime(f))
                                      .FirstOrDefault();
            
            if (latestFile != null)
            {
                var fileInfo = new FileInfo(latestFile);
                if (fileInfo.Length == lastFileSize) return; // Otimização: Não reler se o arquivo não cresceu

                lastFileSize = fileInfo.Length;

                using (var fs = new FileStream(latestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string content = sr.ReadToEnd();
                    richTextBox.Text = content;
                    
                    // Rolagem automática para o final
                    richTextBox.SelectionStart = richTextBox.Text.Length;
                    richTextBox.ScrollToCaret();
                }
            }
        }
        catch (Exception)
        {
            // Ignora falhas temporárias de leitura (arquivo possivelmente em lock transiente pelo Serilog)
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        refreshTimer.Stop();
        refreshTimer.Dispose();
        base.OnFormClosing(e);
    }
}
