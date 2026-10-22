namespace Quizzer.Client
{
    public enum WiederverbindungsErgebnis
    {
        /// <summary>Server noch nicht da - später wieder versuchen.</summary>
        NichtErreichbar,

        /// <summary>Wieder verbunden; der Abgleich läuft.</summary>
        Verbunden,

        /// <summary>Server da, aber die gespeicherte Anmeldung gilt nicht (mehr) - ohne Kennwort geht es nicht.</summary>
        AnmeldungNoetig,
    }

    /// <summary>
    /// Versucht im Hintergrund, eine offline begonnene Sitzung wieder mit dem Server zu verbinden
    /// (Nutzerwunsch 2026-10-22: „live speichern des spielstandes ... wenn internet verbindung da
    /// ist"). Bis dahin blieb ein offline gestartetes Programm den ganzen Abend offline, auch wenn
    /// das WLAN nach zehn Minuten wieder da war.
    /// <para>
    /// Mit wachsendem Abstand (<see cref="Erster"/> bis <see cref="Hoechstens"/>), bis der
    /// Versuch etwas anderes als <see cref="WiederverbindungsErgebnis.NichtErreichbar"/> meldet -
    /// dann einmal <see cref="Beendet"/> und Schluss. Was der Versuch tut, steht beim Aufrufer.
    /// </para>
    /// </summary>
    public sealed class Wiederverbindung : IDisposable
    {
        private readonly Func<CancellationToken, Task<WiederverbindungsErgebnis>> versuch;
        private readonly CancellationTokenSource stop = new();
        private Task? lauf;

        public Wiederverbindung(Func<CancellationToken, Task<WiederverbindungsErgebnis>> versuch, TimeSpan? erster = null, TimeSpan? hoechstens = null)
        {
            this.versuch = versuch;
            Erster = erster ?? TimeSpan.FromSeconds(20);
            Hoechstens = hoechstens ?? TimeSpan.FromMinutes(2);
        }

        public TimeSpan Erster { get; }

        public TimeSpan Hoechstens { get; }

        /// <summary>Wird einmal ausgelöst, auf einem Hintergrund-Thread.</summary>
        public event EventHandler<WiederverbindungsErgebnis>? Beendet;

        public void Start() => lauf ??= Task.Run(LaufeAsync);

        /// <summary>Für Tests: wartet, bis der Wächter fertig ist.</summary>
        public Task Fertig => lauf ?? Task.CompletedTask;

        private async Task LaufeAsync()
        {
            var abstand = Erster;

            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(abstand, stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                WiederverbindungsErgebnis ergebnis;
                try
                {
                    ergebnis = await versuch(stop.Token);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    // Was immer schiefgeht - der Abend läuft lokal weiter, und später kommt der nächste Versuch.
                    ergebnis = WiederverbindungsErgebnis.NichtErreichbar;
                }

                if (ergebnis != WiederverbindungsErgebnis.NichtErreichbar)
                {
                    Beendet?.Invoke(this, ergebnis);
                    return;
                }

                abstand = TimeSpan.FromTicks(Math.Min(abstand.Ticks * 2, Hoechstens.Ticks));
            }
        }

        public void Dispose()
        {
            stop.Cancel();
            stop.Dispose();
        }
    }
}
