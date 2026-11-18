using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>Die Abstimmung eines Teams, wie Spielleiter, Beamer und Telefone sie sehen.</summary>
    /// <param name="Ergebnis">Die Mehrheit; <c>null</c>, wenn niemand abgestimmt hat.</param>
    /// <param name="Abstimmende">Wie viele des Teams abstimmen durften - die Zahl hinter „2 von 3".</param>
    internal sealed record TeamAbstimmungTeam(Teamaufstellung Team, TeamMehrheitErgebnis? Ergebnis, int Abstimmende);

    /// <summary>
    /// Team-Abstimmung (M37 T7, Entwurf Abschnitte 3 und 4): jedes Mitglied wählt am Telefon eine Antwort, die Mehrheit im Team
    /// ist seine (<see cref="TeamMehrheit"/>) - bei Gleichstand die Stimme des Sprechers, sonst entscheidet die Spielleitung (E1).
    /// Gewertet wird erst, wenn die Abstimmung zu ist; bis dahin sieht der Beamer nichts davon (F-M37-13). Die Wertung des Teams
    /// ist die der ersten bewerteten Karte unter denen, die für die Antwort gestimmt haben, der Sprecher zuerst; der Wert geht
    /// aufgeteilt an alle Mitglieder (F-M37-06). Ohne Stimme wird das Team nicht bewertet.
    /// </summary>
    internal sealed class TeamAbstimmungAblauf : JederAntwortetAblauf
    {
        private const string Regelname = "Team-Abstimmung";

        private IReadOnlyDictionary<Guid, string>? stimmen;
        private readonly Dictionary<Guid, string> entscheide = new();

        public TeamAbstimmungAblauf()
            : base(CellSpecialRule.TeamVote)
        {
        }

        /// <summary>Bei Gleichstand zählt die Stimme des Sprechers dieser Frage - die Telefone nennen ihn.</summary>
        public override IReadOnlyDictionary<Guid, Guid>? SprecherAmTelefon => Sprecher;

        public override string Standtext => "Jeder stimmt geheim ab – die Mehrheit im Team gilt";

        public override string Hinweis => Teams.Count == 0 ? string.Empty
            : "Jeder stimmt für sich – es zählt die Mehrheit im Team, bei Gleichstand die Stimme des Sprechers";

        /// <summary>Ob die Abstimmung zu ist - erst dann wird gezählt.</summary>
        public bool StimmenZu => stimmen != null;

        /// <summary>Die Abstimmung ist zu: je Spieler seine Auswahl; eine leere ist keine Stimme. Frühere Entscheide gelten nicht mehr.</summary>
        public void NimmStimmen(IReadOnlyDictionary<Guid, IReadOnlyList<string>> wahl)
        {
            stimmen = wahl.Select(e => (Spieler: e.Key, Stimme: TeamMehrheit.Schluessel(e.Value)))
                .Where(e => e.Stimme.Length > 0)
                .ToDictionary(e => e.Spieler, e => e.Stimme);
            entscheide.Clear();
        }

        /// <summary>„Runde zurücksetzen" (V22): es wird neu abgestimmt - alte Stimmen und Entscheide der Spielleitung gelten nicht mehr.</summary>
        public void VerwirfStimmen()
        {
            stimmen = null;
            entscheide.Clear();
        }

        /// <summary>Je Team die Mehrheit; leer, solange die Abstimmung läuft.</summary>
        public IReadOnlyList<TeamAbstimmungTeam> Ergebnisse
            => stimmen is { } alle ? Teams.Select(t => ErgebnisDes(t, alle)).ToList() : [];

        private TeamAbstimmungTeam ErgebnisDes(Teamaufstellung team, IReadOnlyDictionary<Guid, string> alle)
        {
            var sprecher = Sprecher.TryGetValue(team.TeamId, out var id) ? id : (Guid?)null;
            var ergebnis = TeamMehrheit.Ermittle(alle, team.Folge, sprecher, entscheide.GetValueOrDefault(team.TeamId));

            return new(team, ergebnis, team.Folge.Count(m => !Ausgelassen.ContainsKey(m)));
        }

        /// <summary>
        /// Die Spielleitung wählt bei Gleichstand die Antwort eines Teams (E1), auch gegen die Sprecherstimme. <c>false</c>, wenn
        /// das Team keinen Gleichstand hat oder die Antwort nicht darunter ist.
        /// </summary>
        public bool Entscheide(Guid teamId, string antwort)
        {
            if (Ergebnisse.FirstOrDefault(e => e.Team.TeamId == teamId)?.Ergebnis is not { Gleichstand: true } ergebnis
                || !ergebnis.Gleichauf.Contains(antwort, StringComparer.Ordinal))
                return false;

            entscheide[teamId] = antwort;
            return true;
        }

        /// <summary>„B (2 von 3)", „Gleichstand A / B – die Spielleitung entscheidet" oder „keine Stimme – nicht bewertet".</summary>
        public static string ErgebnisText(TeamAbstimmungTeam abstimmung) => abstimmung.Ergebnis switch
        {
            null => "keine Stimme – nicht bewertet",
            { Antwort: null } offen => $"Gleichstand {string.Join(" / ", offen.Gleichauf.Select(TeamMehrheit.Anzeige))} – die Spielleitung entscheidet",
            { Antwort: { } antwort } e => $"{TeamMehrheit.Anzeige(antwort)} ({e.Stimmen} von {abstimmung.Abstimmende})" + e.Entscheid switch
            {
                TeamMehrheitEntscheid.Sprecher => " – Gleichstand, die Stimme des Sprechers zählt",
                TeamMehrheitEntscheid.Spielleitung => " – Gleichstand, die Spielleitung hat entschieden",
                _ => string.Empty,
            },
        };

        /// <summary>Je Team, was sein Telefon nach dem Schluss zeigt - der Hub gibt jedem nur das seines Teams.</summary>
        public IReadOnlyDictionary<Guid, string> ErgebnisseFuerTelefone() => Ergebnisse.ToDictionary(e => e.Team.TeamId, ErgebnisText);

        /// <summary>Was der Beamer nach dem Schluss zeigt - je Team eine Zeile, alle Teams: „✚ Rot – B (2 von 3)".</summary>
        public IReadOnlyList<string> Aufloesung() => Ergebnisse.Select(e => $"{e.Team.Symbol} {e.Team.Name} – {ErgebnisText(e)}").ToList();

        /// <summary>Eine Zeile in der Liste des Spielleiters bei Gleichstand: „✚ Rot: A (Anna, Bert) – gilt".</summary>
        public string Beschriftung(TeamAbstimmungTeam abstimmung, string antwort)
        {
            var namen = abstimmung.Team.Folge.Where(m => stimmen?.GetValueOrDefault(m) == antwort).Select(NameVon);
            var gilt = abstimmung.Ergebnis?.Antwort == antwort ? " – gilt" : string.Empty;

            return $"{abstimmung.Team.Symbol} {abstimmung.Team.Name}: {TeamMehrheit.Anzeige(antwort)} ({string.Join(", ", namen)}){gilt}";
        }

        /// <summary>Die Karte: vor dem Schluss, für wen abgestimmt wird; danach die eigene Stimme und die Antwort des Teams.</summary>
        protected override string AbgabeText(Guid spieler)
        {
            if (Teams.FirstOrDefault(t => t.Folge.Contains(spieler)) is not { } team)
                return string.Empty;

            if (stimmen is not { } alle)
                return Ausgelassen.ContainsKey(spieler) ? "Stimmt diesmal nicht ab" : $"Stimmt ab für {team.Name}";

            var eigene = alle.TryGetValue(spieler, out var stimme) ? $"Stimme: {TeamMehrheit.Anzeige(stimme)}" : "Keine Stimme";
            return $"{eigene} · {team.Name}: {ErgebnisText(ErgebnisDes(team, alle))}";
        }

        protected override IReadOnlyDictionary<Guid, Teamanteil> Anteile(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var anteile = new Dictionary<Guid, Teamanteil>();

            foreach (var team in Teams)
            {
                var folge = MitKarte(team, eingaenge);

                if (folge.Count == 0)
                    continue;

                var (wertung, grund) = stimmen is { } alle ? WertungDes(ErgebnisDes(team, alle), eingaenge) : (null, "die Abstimmung läuft");

                // Offen, ohne Stimme oder unbewertet: eine schon bewertete Karte trägt 0 statt ihrer eigenen Wertung.
                if (wertung == null)
                {
                    foreach (var mitglied in folge.Where(m => eingaenge[m].Grundwert != 0))
                        anteile[mitglied] = new Teamanteil(RatingKind.None, 0, $"{Regelname} {team.Name}: {grund}");

                    continue;
                }

                grund = $"{Regelname} {team.Name}: {grund} – Anteil an {Punktewechsel.Text(wertung.Grundwert)}";

                foreach (var (mitglied, punkte) in Aufteilung.Verteile(wertung.Grundwert, folge))
                    anteile[mitglied] = new Teamanteil(wertung.Bewertung, punkte, grund);
            }

            return anteile;
        }

        /// <summary>Die Wertung der Antwort: die erste bewertete Karte unter denen, die dafür gestimmt haben, der Sprecher zuerst (E1).</summary>
        private (SonderregelEingang? Wertung, string Grund) WertungDes(TeamAbstimmungTeam abstimmung, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            if (abstimmung.Ergebnis is not { Antwort: { } antwort })
                return (null, ErgebnisText(abstimmung));

            IEnumerable<Guid> zuerst = Sprecher.TryGetValue(abstimmung.Team.TeamId, out var sprecher) ? [sprecher] : [];
            var wertung = zuerst.Concat(abstimmung.Team.Folge)
                .Where(m => stimmen?.GetValueOrDefault(m) == antwort)
                .Select(eingaenge.GetValueOrDefault)
                .FirstOrDefault(e => e is { Bewertung: not RatingKind.None });

            return (wertung, wertung == null ? $"{ErgebnisText(abstimmung)} – noch nicht bewertet" : ErgebnisText(abstimmung));
        }
    }
}
