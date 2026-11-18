using Quizzer.DataModels.Enumerations;
using System.Globalization;

namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Der Stand eines Spielers im Tauziehen.</summary>
    /// <param name="TeamId">Für welches Team er zieht - am Server beim Start der Runde festgehalten, nie aus der Nachricht.</param>
    /// <param name="Tipps">So oft hat er bisher getippt.</param>
    /// <param name="Ende">Der Endstand - danach nimmt der Server von ihm nichts mehr an.</param>
    public sealed record TauziehenZug(Guid TeamId, int Tipps, bool Ende);

    /// <summary>Ein Team am Seil.</summary>
    /// <param name="Mitglieder">Wie viele beim Start mitzogen - der Teiler des Durchschnitts, auch wer nie tippt.</param>
    /// <param name="Tipps">Die Tipps aller Mitglieder zusammen.</param>
    /// <param name="Wertung">Summe oder Durchschnitt, wie am Spiel eingestellt (F-M37-11).</param>
    public sealed record TauziehenTeam(Guid TeamId, int Mitglieder, int Tipps, TeamMinigameScoring Wertung)
    {
        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Wie stark das Team zieht: Summe oder Durchschnitt je Mitglied; eine unbekannte Wertung zieht nicht (fail-closed).</summary>
        public double Zug => Wertung switch
        {
            TeamMinigameScoring.Sum => Tipps,
            TeamMinigameScoring.Average when Mitglieder > 0 => Tipps / (double)Mitglieder,
            _ => 0,
        };

        /// <summary>„42 Tipps“ bzw. „Ø 10,5 Tipps“ - Beamer, Telefon und Karte sagen dasselbe.</summary>
        public string ZugText => Wertung == TeamMinigameScoring.Sum
            ? TippsprintEvaluator.Describe(Tipps)
            : $"Ø {Zug.ToString("0.0", Deutsch)} Tipps";
    }

    /// <summary>Was das Tauziehen ergeben hat.</summary>
    /// <param name="Teams">Nach Kennung geordnet - so stehen sie an Beamer und Telefon auf derselben Seite des Seils.</param>
    /// <param name="Gewinner">Die Teams, die am stärksten ziehen, bei Gleichstand alle; leer, wenn keins zieht.</param>
    public sealed record TauziehenErgebnis(IReadOnlyList<TauziehenTeam> Teams, IReadOnlySet<Guid> Gewinner)
    {
        public static TauziehenErgebnis Leer { get; } = new([], new HashSet<Guid>());

        public bool IstGewinner(Guid teamId) => Gewinner.Contains(teamId);

        /// <summary>
        /// Wo das Seil steht, nur bei genau zwei Teams: −1 ganz beim ersten, +1 ganz beim zweiten, 0 in der Mitte. Gemessen am
        /// Stärkeren - zieht eins doppelt so stark wie das andere, steht das Seil auf halbem Weg zu ihm.
        /// </summary>
        public double? Seil
        {
            get
            {
                if (Teams.Count != 2)
                    return null;

                var staerker = Math.Max(Teams[0].Zug, Teams[1].Zug);
                return staerker > 0 ? (Teams[1].Zug - Teams[0].Zug) / staerker : 0;
            }
        }
    }

    /// <summary>
    /// Das Tauziehen (M37 T9): Team gegen Team, ein Tippsprint der Teams. <b>Gezählt wird am Telefon, entschieden an der
    /// Serveruhr:</b> die Laufzeit misst der Server ab dem Ende des Countdowns, nicht das Telefon. Ein Stand gilt nur mit
    /// höchstens <see cref="MaxTapsPerSecond"/> Tipps je Sekunde dieser Laufzeit, nie sinkend und bis <see cref="NachlaufMs"/>
    /// nach Ablauf - mehr als Grenze mal Dauer zählt nie. Das stärkere Team bekommt die Feldpunkte aufgeteilt; nie Minus (E1).
    /// </summary>
    public static class TauziehenEvaluator
    {
        /// <summary>Dieselbe Grenze wie beim Tippsprint - mehr schafft kein Finger.</summary>
        public const int MaxTapsPerSecond = TippsprintEvaluator.MaxTapsPerSecond;

        /// <summary>So oft meldet das Telefon seinen Stand - und bekommt dabei das Seil zurück.</summary>
        public const int ReportIntervalMs = TippsprintEvaluator.ReportIntervalMs;

        /// <summary>So lange nach Ablauf nimmt der Server noch an: der letzte Stand ist unterwegs, wenn die Zeit um ist.</summary>
        public const int NachlaufMs = 1500;

        /// <summary>Ab wann an der Serveruhr nichts mehr gilt: Dauer plus Nachlauf.</summary>
        public static TimeSpan Frist(int dauerSekunden) => TimeSpan.FromMilliseconds(Math.Max(0, dauerSekunden) * 1000L + NachlaufMs);

        /// <summary>Ob so viele Tipps nach dieser Laufzeit an der Serveruhr möglich sind - gerechnet höchstens bis zum Ablauf.</summary>
        public static bool IstPlausibel(int tipps, TimeSpan laufzeit, int dauerSekunden)
        {
            if (tipps < 0 || laufzeit < TimeSpan.Zero)
                return false;

            var ms = Math.Min((long)laufzeit.TotalMilliseconds, Math.Max(0, dauerSekunden) * 1000L);
            return (long)tipps * 1000 <= (long)MaxTapsPerSecond * ms;
        }

        /// <summary>Prüft einen gemeldeten Stand gegen den bisherigen; <c>null</c> heißt verwerfen.</summary>
        /// <param name="bisher">Der zuletzt angenommene Stand des Spielers, <c>null</c> vor dem ersten.</param>
        /// <param name="neu">Was das Telefon meldet, mit dem Team, das der Server kennt.</param>
        /// <param name="laufzeit">Seit dem Start an der Serveruhr; im Countdown negativ.</param>
        /// <param name="dauerSekunden">So lange wird gezogen.</param>
        public static TauziehenZug? Pruefe(TauziehenZug? bisher, TauziehenZug neu, TimeSpan laufzeit, int dauerSekunden)
        {
            ArgumentNullException.ThrowIfNull(neu);

            if (bisher?.Ende == true || laufzeit > Frist(dauerSekunden) || !IstPlausibel(neu.Tipps, laufzeit, dauerSekunden))
                return null;

            return bisher != null && neu.Tipps < bisher.Tipps ? null : neu;
        }

        /// <summary>Je Team Mitglieder, Tipps und Zug; vorn liegt, wer am stärksten zieht. Ein Zug von 0 gewinnt nie.</summary>
        /// <param name="zuege">Je Spieler sein Stand - jeder, der beim Start mitzog, auch ohne einen Tipp.</param>
        /// <param name="wertung">Summe oder Durchschnitt (F-M37-11).</param>
        /// <param name="rang">
        /// Der Platz eines Teams, <c>GameTeam.SortOrder</c> (V27): danach stehen die Teams, das erste links am Seil - Hub und Beamer
        /// geben denselben, sonst zöge Rot am Telefon links und am Beamer rechts. Ohne: nach Kennung.
        /// </param>
        public static TauziehenErgebnis Werte(IReadOnlyDictionary<Guid, TauziehenZug> zuege, TeamMinigameScoring wertung, Func<Guid, int>? rang = null)
        {
            ArgumentNullException.ThrowIfNull(zuege);

            var teams = zuege.Values
                .GroupBy(z => z.TeamId)
                .Select(g => new TauziehenTeam(g.Key, g.Count(), g.Sum(z => z.Tipps), wertung))
                .OrderBy(t => rang?.Invoke(t.TeamId) ?? 0)
                .ThenBy(t => t.TeamId)
                .ToList();

            // Ein Gleichstand im Durchschnitt (10 auf drei, 20 auf sechs) bleibt einer: die Division rundet beide gleich.
            // R3-2: ohne Gegner gewinnt niemand - wie bei Staffel und Stellvertreter-Duell, sonst wären die Punkte geschenkt.
            var vorn = teams
                .Where(t => teams.Count > 1 && t.Zug > 0 && teams.All(anderes => t.Zug >= anderes.Zug))
                .Select(t => t.TeamId)
                .ToHashSet();

            return new TauziehenErgebnis(teams, vorn);
        }
    }
}
