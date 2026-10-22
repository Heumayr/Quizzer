namespace Quizzer.DataModels.Anzeige
{
    /// <summary>
    /// Der Countdown einer Frage (Verbesserung V5, 2026-10-22): der Spielleiter startet ihn, der
    /// Beamer zeigt ihn. Nur Rechnung, keine Uhr - die Uhrzeit kommt herein, damit sich das
    /// ohne Warten prüfen lässt.
    /// </summary>
    public sealed class Countdown
    {
        public Countdown(TimeSpan dauer, DateTime start)
        {
            if (dauer <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(dauer), "Ein Countdown braucht eine Dauer.");

            Dauer = dauer;
            Start = start;
        }

        public TimeSpan Dauer { get; }

        public DateTime Start { get; }

        public DateTime Ende => Start + Dauer;

        public TimeSpan Rest(DateTime jetzt)
        {
            var rest = Ende - jetzt;
            return rest < TimeSpan.Zero ? TimeSpan.Zero : rest > Dauer ? Dauer : rest;
        }

        /// <summary>
        /// Die angezeigte Sekunde - aufgerundet: bei 9,2 s Rest steht „10" da, und „0" erst, wenn
        /// die Zeit wirklich um ist. Abgerundet stünde die Null eine ganze Sekunde zu früh da.
        /// </summary>
        public int Sekunden(DateTime jetzt) => (int)Math.Ceiling(Rest(jetzt).TotalSeconds - 1e-9);

        /// <summary>Wie viel noch übrig ist, von 1 (gerade gestartet) bis 0 - für den Ring.</summary>
        public double Anteil(DateTime jetzt) => Rest(jetzt).TotalMilliseconds / Dauer.TotalMilliseconds;

        public bool Abgelaufen(DateTime jetzt) => jetzt >= Ende;

        /// <summary>Die letzten Sekunden, in denen der Beamer warnt (rot, Tick).</summary>
        public const int Warnung = 5;

        public bool Knapp(DateTime jetzt) => !Abgelaufen(jetzt) && Sekunden(jetzt) <= Warnung;

        /// <summary>„0:45" ab einer Minute, sonst nur die Sekunden.</summary>
        public string Text(DateTime jetzt)
        {
            var s = Sekunden(jetzt);
            return s >= 60 ? $"{s / 60}:{s % 60:00}" : s.ToString();
        }
    }
}
