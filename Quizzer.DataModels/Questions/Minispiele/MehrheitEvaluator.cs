using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Was die Mehrheit ergeben hat.</summary>
    /// <param name="Stimmen">Je gewaehlter Taste die Zahl der Stimmen - nur Tasten, die jemand gewaehlt hat.</param>
    /// <param name="GewinnerTasten">Die Tasten der siegreichen Gruppe(n); leer, wenn niemand gewaehlt hat.</param>
    /// <param name="Abgaben">Je Spieler seine gueltige Wahl - wer fehlt, hat nicht (gueltig) gewaehlt.</param>
    public sealed record MehrheitErgebnis(
        IReadOnlyDictionary<string, int> Stimmen,
        IReadOnlySet<string> GewinnerTasten,
        IReadOnlyDictionary<Guid, string> Abgaben)
    {
        /// <summary>Ob der Spieler gueltig gewaehlt hat.</summary>
        public bool HatGewaehlt(Guid spieler) => Abgaben.ContainsKey(spieler);

        /// <summary>Ob der Spieler zur siegreichen Gruppe gehoert.</summary>
        public bool IstGewinner(Guid spieler)
            => Abgaben.TryGetValue(spieler, out var taste) && GewinnerTasten.Contains(taste);

        /// <summary>Wie viele mit dem Spieler dasselbe gewaehlt haben (er eingeschlossen).</summary>
        public int GruppeVon(Guid spieler)
            => Abgaben.TryGetValue(spieler, out var taste) ? Stimmen[taste] : 0;
    }

    /// <summary>
    /// Wertet die Mehrheit aus (M35 B2): die groesste Gruppe gewinnt, bei Gleichstand alle groessten.
    /// In der Spielweise <i>Minderheit</i> gewinnt die kleinste Gruppe, die jemand gewaehlt hat.
    /// <para>
    /// <b>Nur Vorschlaege, nie Minus</b> (E1): wer verliert oder nicht waehlt, bekommt nichts
    /// vorgeschlagen. Rein, ohne Oberflaeche - die Spielerkarte uebernimmt nur das Ergebnis.
    /// </para>
    /// </summary>
    public static class MehrheitEvaluator
    {
        /// <summary>Index der Spielweise „Minderheit gewinnt" in den Varianten des Profils.</summary>
        public const int MinorityVariant = 1;

        /// <summary>Ob die Frage in der Spielweise „Minderheit" laeuft.</summary>
        public static bool IsMinority(MinigameQuestion frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            return frage.Kind == MinigameKind.Majority && frage.Variant == MinorityVariant;
        }

        /// <summary>Wertet die Abgaben aus.</summary>
        /// <param name="abgaben">Je Spieler die gewaehlte Taste; <c>null</c> oder leer heisst: nichts gewaehlt.</param>
        /// <param name="gueltigeTasten">Die Tasten der Optionen. Eine andere zaehlt nicht - sie kommt
        /// aus dem WLAN. <c>null</c> laesst jede nicht leere Taste gelten.</param>
        /// <param name="minderheit">Ob die kleinste statt der groessten Gruppe gewinnt.</param>
        public static MehrheitErgebnis Werte(
            IReadOnlyDictionary<Guid, string?> abgaben, IEnumerable<string>? gueltigeTasten, bool minderheit)
        {
            ArgumentNullException.ThrowIfNull(abgaben);

            var erlaubt = gueltigeTasten?.ToHashSet(StringComparer.Ordinal);

            var gueltig = abgaben
                .Where(a => !string.IsNullOrEmpty(a.Value) && (erlaubt == null || erlaubt.Contains(a.Value)))
                .ToDictionary(a => a.Key, a => a.Value!);

            var stimmen = gueltig.Values
                .GroupBy(t => t, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            var gewinner = new HashSet<string>(StringComparer.Ordinal);

            if (stimmen.Count > 0)
            {
                // Leere Gruppen tauchen in "stimmen" gar nicht auf - die kleinste ist also nie eine,
                // die niemand gewaehlt hat.
                var ziel = minderheit ? stimmen.Values.Min() : stimmen.Values.Max();

                foreach (var (taste, zahl) in stimmen)
                {
                    if (zahl == ziel)
                        gewinner.Add(taste);
                }
            }

            return new MehrheitErgebnis(stimmen, gewinner, gueltig);
        }
    }
}
