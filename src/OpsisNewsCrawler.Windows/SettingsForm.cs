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
        ClientSize = new Size(1080, 680);
        MinimumSize = new Size(700, 560);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;

        panel = new SettingsPanel(host, showCloseButton: true);
        panel.CloseRequested += Hide;
        panel.Applied += () => Opacity = Math.Clamp(this.host.Config.SettingsOpacityPercent, 60, 100) / 100.0;
        Controls.Add(panel);

        HandleCreated += (_, _) => Theme.ApplyTitleBar(this);
        Theme.Changed += OnThemeChanged;
        OnThemeChanged();

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
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
