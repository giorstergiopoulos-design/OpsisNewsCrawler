using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Test switches: pictures of the welcome guide, the Opsis family window and the shrink animation, without anybody clicking.</summary>
public static class SuiteSnapshots
{
    /// <summary><c>--snapshot-welcome &lt;folder&gt;</c>: one PNG per page.</summary>
    public static void Welcome(string folder)
    {
        Directory.CreateDirectory(folder);
        using var form = new WelcomeForm(MinimiseTarget.Tray) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        form.Show();
        Application.DoEvents();
        for (var i = 0; i < WelcomeForm.PageCount; i++)
        {
            Thread.Sleep(120);
            Application.DoEvents();
            if (form.PageOverflows(i, out var detail)) File.AppendAllText(Path.Combine(folder, "overflow.txt"), detail + Environment.NewLine);
            using var bmp = form.Snapshot(i);
            bmp.Save(Path.Combine(folder, $"welcome{i + 1}.png"));
        }
        form.CloseNow();
    }

    /// <summary><c>--snapshot-suite &lt;png&gt; [dataDir]</c>: the family window after the installed state was read (no network except the catalogue/latest checks).</summary>
    public static void Suite(string png, string dataDir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(png))!);
        using var form = new SuiteForm(dataDir, MinimiseTarget.Tray) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        form.Show();
        var task = form.RefreshAsync();
        var until = DateTime.UtcNow.AddSeconds(12);
        while (!task.IsCompleted && DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(30);
        }
        for (var i = 0; i < 12; i++) { Application.DoEvents(); Thread.Sleep(40); }
        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        bmp.Save(png);
        form.CloseNow();
    }

    /// <summary><c>--snapshot-minimise &lt;png&gt;</c>: the shrink animation drawn at 0, 25, 50, 75 % over a grey desktop, from the real window picture.</summary>
    public static void Minimise(string png)
    {
        using var form = new WelcomeForm(MinimiseTarget.Tray) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        form.Show();
        Application.DoEvents();
        Thread.Sleep(150);
        Application.DoEvents();
        using var picture = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
        form.CloseNow();

        const int w = 1400, h = 640;
        using var sheet = new Bitmap(w, h);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(52, 60, 78));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            var from = new Rectangle(310, 60, picture.Width, picture.Height);
            var target = new Rectangle(1240, 560, 24, 24);
            using var line = new Pen(Color.FromArgb(120, 255, 255, 255), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawRectangle(line, target);
            foreach (var t in new[] { 0.0, 0.25, 0.5, 0.75 })
            {
                var (bounds, opacity) = MinimiseAnimation.At(from, target, t, simple: false);
                using var attrs = new System.Drawing.Imaging.ImageAttributes();
                attrs.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)opacity });
                var r = Rectangle.Round(bounds);
                g.DrawImage(picture, r, 0, 0, picture.Width, picture.Height, GraphicsUnit.Pixel, attrs);
            }
        }
        sheet.Save(png);
    }
}

/// <summary><c>--snapshot-live-icon &lt;png&gt;</c>: the wide icon with the weather and alert symbols in several live states.</summary>
public static class LiveIconSnapshot
{
    public static void Save(string png)
    {
        var states = new[] { (1, 0), (0, 1), (5, 2), (7, 3), (12, 0), (3, 2) };
        using var sheet = new Bitmap(1060, states.Length * 150);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(40, 44, 54));
            for (var i = 0; i < states.Length; i++)
            {
                LiveIconState.Set(states[i].Item1, states[i].Item2, "");
                using var icon = AppIconArt.RenderWide(1024, 128);
                g.DrawImage(icon, 18, i * 150 + 10);
            }
        }
        sheet.Save(png);
        LiveIconState.Set(1, 0, "");
    }
}
