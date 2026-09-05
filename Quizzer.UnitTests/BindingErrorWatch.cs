using System.Diagnostics;
using System.Windows;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Sammelt die Bindungsfehler, die WPF beim Aufbau einer Ansicht meldet.
    /// <para>
    /// Eine Bindung auf eine Eigenschaft, die es nicht gibt, wirft nicht - sie bleibt still auf
    /// dem Standardwert stehen. Ein gruener Bau merkt davon nichts, und im Fenster fehlt einfach
    /// etwas oder es steht dauerhaft da. Genau das ist im Projekt schon zweimal passiert: eine
    /// Bindung auf ein <c>BuzzerState</c>, das es nie gab, und eine auf
    /// <c>ShowGameGridView</c> im Spielleiter-Fenster.
    /// </para>
    /// <para>
    /// WPF meldet solche Faelle ueber <see cref="PresentationTraceSources.DataBindingSource"/>.
    /// Diese Klasse haengt sich dort ein und macht daraus eine Liste, die ein Test nachsehen kann.
    /// </para>
    /// </summary>
    public sealed class BindingErrorWatch : IDisposable
    {
        private readonly SammelListener listener = new();
        private readonly SourceLevels vorherigesNiveau;

        public BindingErrorWatch()
        {
            // Ohne Refresh() bleibt das Bindungs-Protokoll stumm - gemessen am 2026-09-06: eine
            // absichtlich tote Bindung erzeugte keine einzige Meldung, bevor dieser Aufruf da war.
            PresentationTraceSources.Refresh();

            var quelle = PresentationTraceSources.DataBindingSource;

            vorherigesNiveau = quelle.Switch.Level;

            quelle.Switch.Level = SourceLevels.Warning;
            quelle.Listeners.Add(listener);
        }

        /// <summary>Alle bisher gemeldeten Bindungsfehler, in der Reihenfolge des Auftretens.</summary>
        public IReadOnlyList<string> Errors => listener.Errors;

        public void Dispose()
        {
            var quelle = PresentationTraceSources.DataBindingSource;

            quelle.Listeners.Remove(listener);
            quelle.Switch.Level = vorherigesNiveau;
        }

        private sealed class SammelListener : TraceListener
        {
            private readonly List<string> errors = new();
            private readonly System.Text.StringBuilder offen = new();

            public IReadOnlyList<string> Errors => errors;

            public override void Write(string? message) => offen.Append(message);

            public override void WriteLine(string? message)
            {
                offen.Append(message);

                var ganz = offen.ToString();
                offen.Clear();

                // Nur die Faelle, die auf eine fehlende Eigenschaft hindeuten. Andere Meldungen
                // (etwa zu Konvertern) sind fuer diese Pruefung Rauschen.
                // Nur die Faelle, die auf eine fehlende Eigenschaft hindeuten. Anderes
                // (etwa Meldungen zu Konvertern) ist fuer diese Pruefung Rauschen.
                if (ganz.Contains("path error") || ganz.Contains("cannot find governing"))
                    errors.Add(ganz.Trim());
            }
        }
    }
}
