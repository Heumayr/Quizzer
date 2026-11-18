namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Ein Team in der Staffel: seine Kennung und seine Läufer in der Reihenfolge, in der sie laufen.</summary>
    public sealed record StaffelTeam(Guid TeamId, IReadOnlyList<Guid> Laeufer);

    /// <summary>Ein Versuch an einem Teil.</summary>
    /// <param name="Teil">Der Teil, 1-basiert.</param>
    /// <param name="Gepasst">Die Spielleitung hat für das Team weitergegeben - es gab keine Antwort.</param>
    /// <param name="Gegolten">Die Spielleitung hat eine abgelehnte Antwort gelten lassen (E1).</param>
    public sealed record StaffelVersuch(int Teil, Guid TeamId, Guid SpielerId, bool Richtig, bool Gepasst = false, bool Gegolten = false);

    /// <summary>Eine angenommene Antwort, gemeldet an die Spielleiterseite - der Text auf 80 Zeichen gekürzt.</summary>
    public sealed record StaffelAntwort(int Teil, Guid TeamId, string Text, bool Richtig);

    /// <summary>
    /// Wo die Staffel steht (M37 T11), unveränderlich - jeder Schritt in <see cref="StaffelEvaluator"/> gibt einen neuen Stand.
    /// Der Server hält ihn; Fragefenster und Beamer lesen ihn unter der Sperre des Rahmens.
    /// </summary>
    public sealed record StaffelStand
    {
        /// <summary>Kein Team, kein Teil - vor dem Start.</summary>
        public static StaffelStand Leer { get; } = new() { Teams = [], Zeiger = [], Geloest = [] };

        /// <summary>Die Teams in der Reihenfolge, in der sie an die Reihe kommen.</summary>
        public required IReadOnlyList<StaffelTeam> Teams { get; init; }

        /// <summary>So viele Teile hat die Staffel.</summary>
        public int Teile => Geloest.Count;

        /// <summary>Der laufende Teil, 0-basiert; steht er bei <see cref="Teile"/>, ist die Staffel durch.</summary>
        public int Teil { get; init; }

        /// <summary>Welches Team am Zug ist - Platz in <see cref="Teams"/>.</summary>
        public int AmZug { get; init; }

        /// <summary>Je Team der Platz seines nächsten Läufers.</summary>
        public required IReadOnlyList<int> Zeiger { get; init; }

        /// <summary>Die Teams, die den laufenden Teil schon verfehlt haben.</summary>
        public IReadOnlyList<Guid> Gescheitert { get; init; } = [];

        /// <summary>Je Teil das Team, das ihn gelöst hat; <c>null</c>: offen oder von allen verfehlt.</summary>
        public required IReadOnlyList<Guid?> Geloest { get; init; }

        public IReadOnlyList<StaffelVersuch> Verlauf { get; init; } = [];

        /// <summary>
        /// Steigt mit jedem Versuch, auch mit einem Eingriff der Spielleitung. Das Telefon des Läufers schickt ihn mit - ein anderer
        /// ist alt (doppelt gesendet, nach dem Weitergeben) oder gefälscht.
        /// </summary>
        public int Zug { get; init; } = 1;

        /// <summary>Der Stand vor der zuletzt abgelehnten Antwort - nur, solange sie der letzte Versuch ist; sonst <c>null</c>.</summary>
        public StaffelStand? VorDemFehler { get; init; }

        public bool Fertig => Teams.Count == 0 || Teil >= Teile;

        public Guid? TeamAmZug => Fertig ? null : Teams[AmZug].TeamId;

        /// <summary>Wer jetzt antwortet; <c>null</c>, wenn die Staffel durch ist.</summary>
        public Guid? Laeufer => Fertig ? null : Teams[AmZug].Laeufer[Zeiger[AmZug]];

        public StaffelVersuch? Letzter => Verlauf.Count > 0 ? Verlauf[^1] : null;

        public int GeloestVon(Guid teamId) => Geloest.Count(t => t == teamId);
    }

    /// <summary>Ein Team im Ergebnis der Staffel.</summary>
    public sealed record StaffelTeamwert(Guid TeamId, int Teile);

    /// <summary>Wie die Staffel ausging: je Team die gelösten Teile, die Siegerteams.</summary>
    public sealed record StaffelErgebnis(IReadOnlyList<StaffelTeamwert> Teams, IReadOnlyList<Guid> Sieger)
    {
        public static StaffelErgebnis Leer { get; } = new([], []);

        public bool IstGewinner(Guid teamId) => Sieger.Contains(teamId);
    }

    /// <summary>
    /// Die Staffel als reine Funktion (M37 T11). Ein Team ist am Zug, sein Läufer antwortet auf den laufenden Teil. Richtig: der Teil
    /// gehört dem Team, sein nächstes Mitglied läuft den nächsten Teil. Falsch: der Teil geht ans nächste Team, das ihn noch nicht
    /// verfehlt hat; haben ihn alle verfehlt, bleibt er offen, und das Team nach dem letzten beginnt den nächsten. Jedes Team läuft
    /// reihum - wer geantwortet hat, ist beim nächsten Mal nicht wieder zuerst dran. Gewonnen hat, wer die meisten Teile hat.
    /// </summary>
    public static class StaffelEvaluator
    {
        /// <summary>Mit weniger als drei Teilen ist es keine Staffel, sondern eine Buzzerfrage (Annahme T11).</summary>
        public const int MinTeile = 3;

        /// <summary>Höchstens so viele Antworten je Telefon in <see cref="FensterSekunden"/> (Entwurf Abschnitt 4).</summary>
        public const int MaxAntwortenJeFenster = 5;

        public const int FensterSekunden = 10;

        /// <summary>
        /// Der Anfang: Teams ohne Läufer laufen nicht mit; es beginnt <paramref name="startTeam"/> (das Team des Auslösers), sonst das
        /// erste. <paramref name="erreichbar"/>: wer gerade antworten kann - ein getrennter Läufer wird übersprungen, wenn sein Team drankommt.
        /// </summary>
        public static StaffelStand Beginne(IReadOnlyList<StaffelTeam> teams, int teile, Guid? startTeam = null, Func<Guid, bool>? erreichbar = null)
        {
            ArgumentNullException.ThrowIfNull(teams);

            var mit = teams.Where(t => t.Laeufer.Count > 0).ToList();
            var stand = new StaffelStand { Teams = mit, Zeiger = new int[mit.Count], Geloest = new Guid?[Math.Max(0, teile)] };

            return mit.Count == 0 ? stand : Rufe(stand, Math.Max(0, mit.FindIndex(t => t.TeamId == startTeam)), erreichbar);
        }

        /// <summary>Der Läufer hat geantwortet - die Prüfung der Antwort macht der Aufrufer.</summary>
        public static StaffelStand Antworte(StaffelStand stand, bool richtig, Func<Guid, bool>? erreichbar = null)
            => Weiter(stand, richtig, gepasst: false, erreichbar);

        /// <summary>Die Spielleitung gibt für das Team am Zug weiter (E1) - wie ein Fehler, nur nicht mehr umkehrbar.</summary>
        public static StaffelStand GibWeiter(StaffelStand stand, Func<Guid, bool>? erreichbar = null)
            => Weiter(stand, richtig: false, gepasst: true, erreichbar);

        /// <summary>
        /// Die Spielleitung lässt die zuletzt abgelehnte Antwort gelten (E1): ab dem Stand davor zählt sie als richtig, der Zug steigt
        /// trotzdem weiter. <c>null</c>, wenn der letzte Versuch keine abgelehnte Antwort war.
        /// </summary>
        public static StaffelStand? LasseGelten(StaffelStand stand, Func<Guid, bool>? erreichbar = null)
        {
            ArgumentNullException.ThrowIfNull(stand);

            if (stand.VorDemFehler is not { } vorher)
                return null;

            var neu = Weiter(vorher, richtig: true, gepasst: false, erreichbar);

            return neu with { Zug = stand.Zug + 1, Verlauf = [.. vorher.Verlauf, neu.Verlauf[^1] with { Gegolten = true }] };
        }

        /// <summary>Je Team die gelösten Teile; Sieger sind die mit den meisten, mindestens einem - ohne Gegner gewinnt niemand.</summary>
        public static StaffelErgebnis Werte(StaffelStand stand)
        {
            ArgumentNullException.ThrowIfNull(stand);

            var teams = stand.Teams.Select(t => new StaffelTeamwert(t.TeamId, stand.GeloestVon(t.TeamId))).ToList();
            var bestes = teams.Count < 2 ? 0 : teams.Max(t => t.Teile);

            return new(teams, bestes > 0 ? teams.Where(t => t.Teile == bestes).Select(t => t.TeamId).ToList() : []);
        }

        private static StaffelStand Weiter(StaffelStand stand, bool richtig, bool gepasst, Func<Guid, bool>? erreichbar)
        {
            ArgumentNullException.ThrowIfNull(stand);

            if (stand.Laeufer is not { } laeufer)
                return stand;

            var team = stand.Teams[stand.AmZug];
            var zeiger = stand.Zeiger.ToArray();
            zeiger[stand.AmZug] = (zeiger[stand.AmZug] + 1) % team.Laeufer.Count;

            var neu = stand with
            {
                Zeiger = zeiger,
                Verlauf = [.. stand.Verlauf, new StaffelVersuch(stand.Teil + 1, team.TeamId, laeufer, richtig, gepasst)],
                Zug = stand.Zug + 1,
                VorDemFehler = richtig || gepasst ? null : stand with { VorDemFehler = null },
            };

            if (richtig)
            {
                var geloest = stand.Geloest.ToArray();
                geloest[stand.Teil] = team.TeamId;
                return Rufe(neu with { Geloest = geloest, Teil = stand.Teil + 1, Gescheitert = [] }, stand.AmZug, erreichbar);
            }

            List<Guid> gescheitert = [.. stand.Gescheitert, team.TeamId];
            var naechstes = Enumerable.Range(1, stand.Teams.Count)
                .Select(i => (stand.AmZug + i) % stand.Teams.Count)
                .FirstOrDefault(i => !gescheitert.Contains(stand.Teams[i].TeamId), -1);

            // Alle haben den Teil verfehlt: er bleibt offen, den nächsten beginnt das Team nach dem, das zuletzt verfehlt hat.
            return naechstes >= 0
                ? Rufe(neu with { Gescheitert = gescheitert }, naechstes, erreichbar)
                : Rufe(neu with { Gescheitert = [], Teil = stand.Teil + 1 }, (stand.AmZug + 1) % stand.Teams.Count, erreichbar);
        }

        /// <summary>Gibt dem Team den Zug; es läuft sein nächstes erreichbares Mitglied - ist keines erreichbar, bleibt es beim Platz.</summary>
        private static StaffelStand Rufe(StaffelStand stand, int team, Func<Guid, bool>? erreichbar)
        {
            var laeufer = stand.Teams[team].Laeufer;
            var zeiger = stand.Zeiger.ToArray();

            for (var i = 0; erreichbar != null && i < laeufer.Count; i++)
            {
                var platz = (zeiger[team] + i) % laeufer.Count;

                if (erreichbar(laeufer[platz]))
                {
                    zeiger[team] = platz;
                    break;
                }
            }

            return stand with { AmZug = team, Zeiger = zeiger };
        }
    }
}
