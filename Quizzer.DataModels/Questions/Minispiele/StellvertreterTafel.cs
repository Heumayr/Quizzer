namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>
    /// Die Tafel des Stellvertreter-Duells am Beamer (M37 T10): je Team eine Zeile „✚ Rot – Anna“ - während der Wahl, wer
    /// noch wählt, nach einer Runde die Zeit bzw. der Zug, am Ende Bestzeit bzw. Siege mit dem Siegerteam markiert.
    /// </summary>
    public static class StellvertreterTafel
    {
        /// <summary>Während der Wahl: je Team sein Stellvertreter oder „wählt …“; gelost steht dabei.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Wahl(
            IEnumerable<Guid> teams, IReadOnlyList<Stellvertreter> vertreter, Func<Guid, string> teamName, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(teams);
            ArgumentNullException.ThrowIfNull(vertreter);
            ArgumentNullException.ThrowIfNull(teamName);
            ArgumentNullException.ThrowIfNull(name);

            return teams
                .Select(t => (Team: teamName(t), Vertreter: vertreter.FirstOrDefault(v => v.TeamId == t)))
                .OrderBy(z => z.Team, StringComparer.CurrentCulture)
                .Select(z => new ReaktionTafelZeile(z.Team, z.Vertreter is { } v ? Gewaehlt(v, name) : "wählt …", false))
                .ToList();
        }

        /// <summary>Eine gespielte Runde: je Stellvertreter seine Zeit bzw. sein Zug; bei Schere-Stein-Papier der Rundensieger markiert.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Runde(
            StellvertreterErgebnis ergebnis, IReadOnlyDictionary<Guid, object> abgaben, Func<Guid, string> teamName, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);
            ArgumentNullException.ThrowIfNull(abgaben);

            Guid? rundensieger = ergebnis is { IstSchereSteinPapier: true, Vertreter: [var a, var b] }
                ? DuellEvaluator.RundenSieger(a.SpielerId, b.SpielerId, abgaben
                    .Where(e => e.Value is SchereSteinPapier).ToDictionary(e => e.Key, e => (SchereSteinPapier)e.Value))
                : null;

            return Zeilen(ergebnis, teamName, name, v => abgaben.GetValueOrDefault(v.SpielerId) switch
            {
                SchereSteinPapier zug => DuellEvaluator.Describe(zug),
                ReactionAttempt versuch => ReaktionEvaluator.Describe(versuch),
                _ => "keine Abgabe",
            }, v => v.SpielerId == rundensieger);
        }

        /// <summary>Der Stand über alle Runden; am Ende das Siegerteam markiert, mit Symbol und Text.</summary>
        public static IReadOnlyList<ReaktionTafelZeile> Stand(
            StellvertreterErgebnis ergebnis, Func<Guid, string> teamName, Func<Guid, string> name, bool ende)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return Zeilen(ergebnis, teamName, name, v => ergebnis.Leistung(v.SpielerId), v => ende && ergebnis.Sieger.Contains(v.SpielerId));
        }

        /// <summary>„Anna“ bzw. „Anna (gelost)“.</summary>
        public static string Gewaehlt(Stellvertreter vertreter, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(vertreter);
            ArgumentNullException.ThrowIfNull(name);

            return name(vertreter.SpielerId) + (vertreter.Gelost ? " (gelost)" : string.Empty);
        }

        private static IReadOnlyList<ReaktionTafelZeile> Zeilen(
            StellvertreterErgebnis ergebnis, Func<Guid, string> teamName, Func<Guid, string> name,
            Func<Stellvertreter, string> wert, Func<Stellvertreter, bool> sieger)
        {
            ArgumentNullException.ThrowIfNull(teamName);
            ArgumentNullException.ThrowIfNull(name);

            return ergebnis.Vertreter
                .Select(v => new ReaktionTafelZeile($"{teamName(v.TeamId)} – {name(v.SpielerId)}", wert(v), sieger(v)))
                .OrderBy(z => z.Sieger ? 0 : 1)
                .ThenBy(z => z.Name, StringComparer.CurrentCulture)
                .ToList();
        }
    }
}
