using System.Media;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The sound of an urgent alert: a short rising two-tone beep built in memory (no file to ship). It does not depend on the Windows
/// sound scheme, which may be set to "No sounds". Falls back to the system "exclamation" sound if it cannot be played.
/// </summary>
internal static class AlertSound
{
    private static byte[]? wav;
    private static SoundPlayer? player;

    public static void Play()
    {
        try
        {
            wav ??= Build();
            player?.Dispose();
            player = new SoundPlayer(new MemoryStream(wav));
            player.Play(); // asynchronous: the bar is not blocked
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            try { SystemSounds.Exclamation.Play(); }
            catch (Exception inner) when (inner is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
    }

    /// <summary>16-bit mono PCM, 22.05 kHz: three beeps (660, 880, 660 Hz), 140 ms each with a short fade, 40 ms apart.</summary>
    internal static byte[] Build()
    {
        const int rate = 22050;
        var tones = new[] { 660.0, 880.0, 660.0 };
        var samples = new List<short>();
        foreach (var hz in tones)
        {
            var n = rate * 140 / 1000;
            for (var i = 0; i < n; i++)
            {
                var envelope = Math.Min(1.0, Math.Min(i, n - i) / (rate * 0.01)); // 10 ms fade in/out
                samples.Add((short)(Math.Sin(2 * Math.PI * hz * i / rate) * envelope * 0.6 * short.MaxValue));
            }
            samples.AddRange(new short[rate * 40 / 1000]);
        }
        var dataLength = samples.Count * 2;
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLength);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);       // PCM
            w.Write((short)1);       // mono
            w.Write(rate);
            w.Write(rate * 2);       // byte rate
            w.Write((short)2);       // block align
            w.Write((short)16);      // bits
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLength);
            foreach (var s in samples) w.Write(s);
        }
        return ms.ToArray();
    }
}
