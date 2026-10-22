using Quizzer.Base;
using Quizzer.DataModels.Anzeige;
using System.Windows;
using System.Windows.Threading;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Der Countdown einer Frage (V5, 2026-10-22): der Spielleiter startet ihn im Fragefenster,
    /// der Beamer zeigt ihn groß. Ein Objekt für beide - so gibt es keine zweite Uhr, die anders
    /// geht.
    /// <para>
    /// Gerechnet wird in <see cref="Countdown"/> gegen die Uhr, nicht durch Abzählen der Ticks:
    /// ein Takt, der wegen eines vollen Oberflächen-Threads später kommt, verlängert die Zeit
    /// nicht.
    /// </para>
    /// </summary>
    public sealed class CountdownViewModel : UcViewModelBase
    {
        private readonly DispatcherTimer takt;
        private Countdown? laufend;
        private int letzteSekunde = -1;
        private bool zeitUm;
        private DateTime zeitUmBis;

        /// <summary>Wie lange „Zeit!" stehen bleibt.</summary>
        public static readonly TimeSpan ZeitUmAnzeige = TimeSpan.FromSeconds(3);

        public CountdownViewModel()
        {
            takt = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
            takt.Tick += (_, _) => Weiter(DateTime.UtcNow);
        }

        public void Starte(int sekunden)
        {
            laufend = new Countdown(TimeSpan.FromSeconds(sekunden), DateTime.UtcNow);
            letzteSekunde = -1;
            zeitUm = false;
            takt.Start();
            Weiter(DateTime.UtcNow);
        }

        public void Stoppe()
        {
            takt.Stop();
            laufend = null;
            zeitUm = false;
            Melde();
        }

        public bool Laeuft => laufend != null;

        private void Weiter(DateTime jetzt)
        {
            if (laufend is { } c)
            {
                var sekunde = c.Sekunden(jetzt);

                if (sekunde != letzteSekunde)
                {
                    if (c.Knapp(jetzt))
                        Klang.Tick();

                    letzteSekunde = sekunde;
                }

                if (c.Abgelaufen(jetzt))
                {
                    laufend = null;
                    zeitUm = true;
                    zeitUmBis = jetzt + ZeitUmAnzeige;
                    Klang.Ende();
                }
            }
            else if (zeitUm && jetzt >= zeitUmBis)
            {
                zeitUm = false;
                takt.Stop();
            }

            Melde();
        }

        private void Melde()
        {
            OnPropertyChanged(nameof(Visibility));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Anteil));
            OnPropertyChanged(nameof(Knapp));
            OnPropertyChanged(nameof(Laeuft));
            OnPropertyChanged(nameof(MasterText));
        }

        public Visibility Visibility => laufend != null || zeitUm ? Visibility.Visible : Visibility.Collapsed;

        public string Text => laufend?.Text(DateTime.UtcNow) ?? (zeitUm ? "Zeit!" : string.Empty);

        public double Anteil => laufend?.Anteil(DateTime.UtcNow) ?? 0;

        /// <summary>Die letzten Sekunden oder „Zeit!" - der Beamer färbt rot.</summary>
        public bool Knapp => zeitUm || (laufend?.Knapp(DateTime.UtcNow) ?? false);

        /// <summary>Die Zeile im Fragefenster des Spielleiters.</summary>
        public string MasterText => laufend != null ? $"Countdown: noch {Text}" : zeitUm ? "Countdown: Zeit ist um" : "Kein Countdown";
    }
}
