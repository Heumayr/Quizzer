namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Heisse Kartoffel am Beamer (M35 C6). Waehrend der Runde nur, wer sie haelt und wie oft jeder sie weitergab - nie
    /// eine Aussage, nie eine Zeit. Nach dem Knall die Punkte und der Verlauf samt Wahrheit (<see cref="Aufloesung"/>).
    /// </summary>
    public static class KartoffelTafel
    {
        /// <summary>Die Kopfzeile waehrend der Runde.</summary>
        public static string Kopfzeile(string? halter)
            => halter == null
                ? "Heiße Kartoffel – gleich geht es los"
                : $"Heiße Kartoffel – {halter} hält sie! Nur wer richtig antwortet, wird sie los.";

        /// <summary>Waehrend der Runde: der Halter vorn, dann wie oft jeder weitergegeben hat. Immer als Text, nie nur Farbe.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Laufend(
            Guid? halter, IReadOnlyDictionary<Guid, int> weitergaben, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(weitergaben);
            ArgumentNullException.ThrowIfNull(spieler);

            return spieler
                .Select(s => (s.Id, s.Name, Haelt: s.Id == halter, Zahl: weitergaben.GetValueOrDefault(s.Id)))
                .OrderByDescending(z => z.Haelt)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, z.Haelt ? "hält die Kartoffel!" : $"{z.Zahl}× weitergegeben", false))
                .ToList();
        }

        /// <summary>Nach der Runde: je Spieler, was der Knall fuer ihn bedeutet; markiert ist, wer Punkte gewinnt.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Ende(KartoffelErgebnis ergebnis, IEnumerable<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(spieler);

            var liste = spieler.ToList();
            string Name(Guid id) => liste.FirstOrDefault(s => s.Id == id).Name ?? "?";

            return liste
                .Select(s => (s.Id, s.Name, Punkte: ergebnis.PunkteVon(s.Id)))
                .OrderByDescending(z => z.Punkte)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Name, KartoffelEvaluator.Karte(ergebnis, z.Id, Name), z.Punkte > 0))
                .ToList();
        }

        /// <summary>Nach der Runde: jede gezeigte Aussage mit ihrer Wahrheit, wer sie hatte und was daraus wurde.</summary>
        public static IReadOnlyList<string> Aufloesung(IEnumerable<KartoffelZug> verlauf, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(verlauf);
            ArgumentNullException.ThrowIfNull(name);

            return verlauf.Select(z => $"{(z.Wahr ? "✓ wahr" : "✗ falsch")} – {z.Aussage} · {name(z.Halter)}: {Folge(z, name)}").ToList();
        }

        private static string Folge(KartoffelZug zug, Func<Guid, string> name)
            => zug.Richtig switch
            {
                null => "offen beim Knall",
                false => "daneben",
                true when zug.WeiterAn is { } an => $"richtig, weiter an {name(an)}",
                true => "richtig, aber nicht mehr weitergegeben",
            };
    }
}
