using OpsisNewsCrawler.Core;
namespace OpsisNewsCrawler.Windows;

/// <summary>Standalone settings window: just a frame around <see cref="SettingsPanel"/>.</summary>
public sealed class SettingsForm : Form
{
    private readonly SettingsPanel panel;
    private readonly TickerHost host;

    public SettingsForm(TickerHost host)
    {
        this.host = host;
        Opacity = Math.Clamp(host.Config.SettingsOpacityPercent, 60, 100) / 100.0;
        Icon = AppIcon.Get();
        Text = Loc.T("app.name") + " — " + Loc.T("window.settings");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(900, 620);
        FormBorderStyle = FormBorderStyle.Sizable;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;

        panel = new SettingsPanel(host, showCloseButton: true);
        panel.CloseRequested += Hide;
        panel.Applied += () => Opacity = Math.Clamp(this.host.Config.SettingsOpacityPercent, 60, 100) / 100.0;
        Controls.Add(panel);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (panel.ConfirmCloseIfNeeded()) Hide();
                e.Handled = true;
            }
        };

        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
        Theme.Changed += OnThemeChanged;
        OnThemeChanged();

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (panel.ConfirmCloseIfNeeded()) Hide();
            }
        };
    }

    private void OnThemeChanged()
    {
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Theme.ApplyTitleBar(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Theme.Changed -= OnThemeChanged;
        base.Dispose(disposing);
    }

    public void SelectTab(int index) => panel.SelectTab(index);

    /// <summary>Test helper (--snapshot): saves every tab as a PNG without needing the window to be visible on screen.</summary>
    public void SaveSnapshots(string folder)
    {
        Directory.CreateDirectory(folder);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-4000, 0);
        panel.LoadFromConfig();
        Show();
        for (var i = 0; i < panel.TabCount; i++)
        {
            panel.SelectTab(i);
            Application.DoEvents();
            Thread.Sleep(450);
            Application.DoEvents();
            using var bmp = new Bitmap(Width, Height);
            DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(Path.Combine(folder, $"tab{i}.png"));
        }
        Hide();
    }

    public void ShowSettings()
    {
        panel.LoadFromConfig();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        // The ticker never takes focus, so Windows may refuse to bring this window forward;
        // toggling TopMost forces it to the front without leaving it always-on-top.
        TopMost = true;
        Show();
        Activate();
        TopMost = false;
    }
}
