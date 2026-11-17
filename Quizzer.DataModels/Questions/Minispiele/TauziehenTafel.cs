namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Das Seil am Beamer bei genau zwei Teams (M37 T9): links das erste, rechts das zweite, dazwischen der Knoten. Die Lage
    /// steht immer auch als Satz da - der Knoten allein wäre Information nur über die Position.
    /// </summary>
    /// <param name="Links">Symbol und Name des ersten Teams.</param>
    /// <param name="Rechts">Symbol und Name des zweiten.</param>
    /// <param name="Position">−1 ganz links, +1 ganz rechts (<see cref="TauziehenErgebnis.Seil"/>).</param>
    /// <param name="Lage">„✚ Rot zieht stärker“ oder „Gleichstand“.</param>
    public sealed record TauziehenSeil(string Links, string Rechts, double Position, string Lage)
    {
        /// <summary>Die halbe Seillänge in Punkten der Beamertafel (900 breit) - so weit wandert der Knoten höchstens.</summary>
        public const double HalbeLaenge = 330;

        /// <summary>Wie weit der Knoten neben der Mitte steht.</summary>
        public double Versatz => Math.Clamp(Position, -1, 1) * HalbeLaenge;
    }

    /// <summary>Die Tafel des Tauziehens am Beamer (M37 T9): je Team eine Zeile mit Zug und Balken, bei zwei Teams dazu das Seil.</summary>
    public static class TauziehenTafel
    {
        /// <summary>Je Team Zug und Balken, das stärkste vorn und voll; erst am Ende sind die Sieger markiert.</summary>
        /// <param name="teamName">Symbol und Name zu einer Teamkennung.</param>
        /// <param name="ende">Ob die Runde zu ist - vorher gewinnt niemand.</param>
        public static IReadOnlyList<ReaktionTafelZeile> Zeilen(TauziehenErgebnis ergebnis, Func<Guid, string> teamName, bool ende)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(teamName);

            var staerkster = Math.Max(1, ergebnis.Teams.Select(t => t.Zug).DefaultIfEmpty(0).Max());

            return ergebnis.Teams
                .OrderByDescending(t => t.Zug)
                .ThenBy(t => teamName(t.TeamId), StringComparer.CurrentCulture)
                .Select(t => new ReaktionTafelZeile(
                    teamName(t.TeamId), $"{t.ZugText} ({Mitzieher(t.Mitglieder)})", ende && ergebnis.IstGewinner(t.TeamId), t.Zug / staerkster))
                .ToList();
        }

        /// <summary>Das Seil bei genau zwei Teams; sonst <c>null</c> - bei drei und mehr trägt die Tafel allein.</summary>
        public static TauziehenSeil? Seil(TauziehenErgebnis ergebnis, Func<Guid, string> teamName)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(teamName);

            if (ergebnis.Seil is not { } position)
                return null;

            var (links, rechts) = (teamName(ergebnis.Teams[0].TeamId), teamName(ergebnis.Teams[1].TeamId));
            var lage = position switch
            {
                < 0 => $"{links} zieht stärker",
                > 0 => $"{rechts} zieht stärker",
                _ => "Gleichstand",
            };

            return new TauziehenSeil(links, rechts, position, lage);
        }

        /// <summary>„1 zieht“, „3 ziehen“ - wie viele beim Start dabei waren.</summary>
        public static string Mitzieher(int mitglieder) => mitglieder == 1 ? "1 zieht" : $"{mitglieder} ziehen";
    }
}
