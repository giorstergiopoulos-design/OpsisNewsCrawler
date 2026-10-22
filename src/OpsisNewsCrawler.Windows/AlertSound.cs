using System.Media;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The sound of an urgent alert: a short newsroom-style stinger (whoosh, bell hits, ringing chord) synthesised in memory (no file to ship). It does not depend on the Windows
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

    /// <summary>
    /// A newsroom-style "breaking" stinger, 16-bit mono PCM at 44.1 kHz (about 2.3 s): a quick rising whoosh, three bell hits
    /// (E5, E5, B5) and a long G major-7 chord (G5, B5, D6, F#6) that rings out. Bell timbre = a few harmonics with an exponential decay.
    /// </summary>
    internal static byte[] Build()
    {
        const int rate = 44100;
        const double total = 2.3;
        var mix = new double[(int)(rate * total)];

        void Bell(double start, double hz, double length, double gain)
        {
            var from = (int)(start * rate);
            var n = Math.Min((int)(length * rate), mix.Length - from);
            for (var i = 0; i < n; i++)
            {
                var t = i / (double)rate;
                var attack = Math.Min(1.0, i / (rate * 0.004));
                var decay = Math.Exp(-t * 3.2 / length * 2);
                var v = Math.Sin(2 * Math.PI * hz * t)
                      + 0.45 * Math.Sin(2 * Math.PI * hz * 2 * t) * Math.Exp(-t * 6)
                      + 0.25 * Math.Sin(2 * Math.PI * hz * 3.01 * t) * Math.Exp(-t * 9)
                      + 0.12 * Math.Sin(2 * Math.PI * hz * 4.2 * t) * Math.Exp(-t * 14);
                mix[from + i] += v * attack * decay * gain;
            }
        }

        // Whoosh: a sine sweep 300 -> 1500 Hz in 0.35 s
        {
            var n = (int)(rate * 0.35);
            double phase = 0;
            for (var i = 0; i < n; i++)
            {
                var k = i / (double)n;
                var hz = 300 + 1200 * k * k;
                phase += 2 * Math.PI * hz / rate;
                mix[i] += Math.Sin(phase) * Math.Sin(Math.PI * k) * 0.35;
            }
        }
        Bell(0.40, 659.25, 0.45, 0.55);   // E5
        Bell(0.62, 659.25, 0.45, 0.55);   // E5
        Bell(0.84, 987.77, 0.60, 0.60);   // B5
        foreach (var hz in new[] { 783.99, 987.77, 1174.66, 1479.98 })
            Bell(1.15, hz, 1.15, 0.28);   // ringing chord

        var peak = mix.Max(Math.Abs);
        var scale = peak > 0 ? 0.8 * short.MaxValue / peak : 0;
        var samples = new short[mix.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            var fadeOut = Math.Min(1.0, (samples.Length - i) / (rate * 0.05));   // no click at the end
            samples[i] = (short)(mix[i] * scale * fadeOut);
        }
        var dataLength = samples.Length * 2;
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
