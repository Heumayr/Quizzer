using Quizzer.DataModels.Anzeige;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Die Punkteänderung nach einer Frage (Nutzerwunsch 2026-10-21): die Zahl zählt zum neuen
    /// Stand, darüber steht einige Sekunden „+200" bzw. „−100".
    /// <para>
    /// <b>Gemessen wird gegen den zuletzt <i>gezeigten</i> Stand</b>, nicht gegen einen
    /// gespeicherten Vorher-Wert: die Punkte ändern sich auf mehreren Wegen (Frage beenden,
    /// Ergebnis nachträglich ändern), und jeder soll auf dem Beamer nachvollziehbar sein.
    /// Beim ersten Aufbau einer Kachel gibt es keinen gezeigten Stand - dann wird nichts
    /// eingeblendet, sonst erschiene beim Öffnen eines Spiels der ganze Punktestand als „+".
    /// </para>
    /// </summary>
    public partial class PlayerStatsContext
    {
        private int? gezeigt;
        private DispatcherTimer? zaehler;
        private DispatcherTimer? ausblender;
        private DateTime zaehlStart;
        private int zaehlVon;
        private int zaehlBis;

        /// <summary>Was die Kachel als Punktzahl zeigt - beim Zählen der Zwischenstand.</summary>
        public int AnzeigePunkte
        {
            get => anzeigePunkte;
            private set
            {
                if (anzeigePunkte == value)
                    return;

                anzeigePunkte = value;
                OnPropertyChanged();
            }
        }

        private int anzeigePunkte;

        /// <summary>„+200" oder „−100" - leer, solange nichts eingeblendet ist.</summary>
        public string WechselText { get; private set; } = string.Empty;

        /// <summary>Die Änderung als Zahl, für die Übersicht.</summary>
        public int Wechsel { get; private set; }

        /// <summary>Ob gerade eine Änderung eingeblendet ist - daran hängen die Animationen.</summary>
        public bool WechselAktiv { get; private set; }

        /// <summary>Grün für Plus, Rot für Minus - und der Text trägt das Vorzeichen zusätzlich.</summary>
        public Brush WechselBrush => Wechsel >= 0 ? PlusBrush : MinusBrush;

        private static readonly Brush PlusBrush = Gefroren(Color.FromRgb(0x1E, 0x9E, 0x4A));
        private static readonly Brush MinusBrush = Gefroren(Color.FromRgb(0xC6, 0x28, 0x28));

        private static Brush Gefroren(Color farbe)
        {
            var brush = new SolidColorBrush(farbe);
            brush.Freeze();
            return brush;
        }

        private void ZeigeWechsel(int neu)
        {
            // Erster Aufbau oder Siegerehrung: einfach stehen lassen.
            if (gezeigt == null || IsGameFinished)
            {
                gezeigt = neu;
                AnzeigePunkte = neu;
                return;
            }

            var delta = neu - gezeigt.Value;

            if (delta == 0)
                return;

            // Laeuft noch eine Zaehlung, beginnt die neue beim aktuellen Zwischenstand - sonst
            // spraenge die Zahl zurueck.
            zaehlVon = AnzeigePunkte;
            zaehlBis = neu;
            zaehlStart = DateTime.UtcNow;
            gezeigt = neu;

            Wechsel = delta;
            WechselText = Punktewechsel.Text(delta);

            // Erst aus, dann an: so startet die Einblende-Animation auch dann neu, wenn die
            // vorige noch lief (DataTrigger reagiert nur auf den Wechsel des Werts).
            WechselAktiv = false;
            OnPropertyChanged(nameof(WechselAktiv));

            OnPropertyChanged(nameof(Wechsel));
            OnPropertyChanged(nameof(WechselText));
            OnPropertyChanged(nameof(WechselBrush));

            WechselAktiv = true;
            OnPropertyChanged(nameof(WechselAktiv));
            StatsContext?.AenderungenGeaendert();

            StarteZaehlen();
            StarteAusblenden();
        }

        private void StarteZaehlen()
        {
            // Animationen aus: gleich der neue Stand, die Plakette mit "+200" bleibt trotzdem -
            // sie ist Auskunft, keine Zier.
            if (!Quizzer.Base.Bewegung.Aktiv)
            {
                AnzeigePunkte = zaehlBis;
                return;
            }

            zaehler ??= new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Render, (_, _) => Zaehle(), Dispatcher.CurrentDispatcher);
            zaehler.Start();
            Zaehle();
        }

        private void Zaehle()
        {
            var anteil = (DateTime.UtcNow - zaehlStart).TotalMilliseconds / Punktewechsel.Zaehldauer.TotalMilliseconds;

            AnzeigePunkte = Punktewechsel.Zwischenstand(zaehlVon, zaehlBis, anteil);

            if (anteil >= 1)
                zaehler?.Stop();
        }

        private void StarteAusblenden()
        {
            ausblender ??= new DispatcherTimer(Punktewechsel.Anzeigedauer, DispatcherPriority.Normal, (_, _) => Blende(), Dispatcher.CurrentDispatcher);

            // Neu aufziehen: eine zweite Aenderung kurz nach der ersten steht wieder volle Zeit.
            ausblender.Stop();
            ausblender.Start();
        }

        private void Blende()
        {
            ausblender?.Stop();

            WechselAktiv = false;
            OnPropertyChanged(nameof(WechselAktiv));
            StatsContext?.AenderungenGeaendert();
        }
    }
}
