using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>Die Gruppen eines Teams bei „Gleich gedacht", wie Spielleiter und Beamer sie sehen.</summary>
    internal sealed record GleichGedachtTeam(Teamaufstellung Team, GleichGedachtErgebnis Ergebnis);

    /// <summary>
    /// „Gleich gedacht" (M37 T6, F-M37-12): jedes Mitglied schreibt für sich, immer per Text. Verglichen wird, sobald die
    /// Antworten zu sind (alle abgegeben oder „Runde schließen") - vorher stünde am Beamer, was andere schreiben. Je passendem
    /// Mitglied sein Anteil (<see cref="GleichGedacht"/>); die Bewertung der Karte zählt nicht, ein Minus gibt es nicht. Die
    /// Spielleitung legt Gruppen eines Teams zusammen (E1) und korrigiert sonst von Hand auf der Karte.
    /// </summary>
    internal sealed class GleichGedachtAblauf : JederAntwortetAblauf
    {
        /// <summary>Platzhalter der Texteingabe am Telefon.</summary>
        internal const string Eingabe = "Schreib, was dein Team schreibt";

        private IReadOnlyDictionary<Guid, string?>? antworten;
        private IReadOnlyDictionary<Guid, string> zusammengelegt = new Dictionary<Guid, string>();

        public GleichGedachtAblauf()
            : base(CellSpecialRule.SameThought)
        {
        }

        public override string Eingabehinweis => Eingabe;

        public override bool NurTexteingabe => true;

        public override string Standtext => "Jeder schreibt für sich – verglichen wird, sobald alle abgegeben haben oder die Runde geschlossen ist";

        public override string Hinweis => Teams.Count == 0 ? string.Empty : "Jeder schreibt für sich – Punkte gibt es, wo im Team gleich gedacht wurde";

        /// <summary>Ob verglichen wird - erst, wenn die Antworten zu sind.</summary>
        public bool AntwortenZu => antworten != null;

        /// <summary>Die Antworten sind zu: ab jetzt wird verglichen; ein früheres Zusammenlegen gilt nicht mehr.</summary>
        public void NimmAntworten(IReadOnlyDictionary<Guid, string?> neu)
        {
            antworten = neu.ToDictionary();
            zusammengelegt = new Dictionary<Guid, string>();
        }

        /// <summary>„Runde zurücksetzen" (V22): die Antworten sind wieder offen - verglichen wird erst nach den neuen.</summary>
        public void VerwirfAntworten()
        {
            antworten = null;
            zusammengelegt = new Dictionary<Guid, string>();
        }

        /// <summary>Je Team die Gruppen, größte zuerst; leer, solange die Antworten offen sind.</summary>
        public IReadOnlyList<GleichGedachtTeam> Gruppen
            => antworten is { } offen
                ? Teams.Select(t => new GleichGedachtTeam(t, GleichGedacht.Werte(Grundlage.Feldpunkte, t.Folge, offen, zusammengelegt))).ToList()
                : [];

        /// <summary>
        /// Die Spielleitung legt Gruppen eines Teams zusammen (E1): die weiteren zur ersten. <c>false</c>, wenn das Team keine zwei
        /// der genannten Gruppen hat - Gruppen verschiedener Teams lassen sich nicht zusammenlegen.
        /// </summary>
        public bool LegeZusammen(Guid teamId, IReadOnlyList<string> schluessel)
        {
            if (Gruppen.FirstOrDefault(g => g.Team.TeamId == teamId) is not { } team)
                return false;

            var gewaehlt = schluessel.Distinct()
                .Select(s => team.Ergebnis.Gruppen.FirstOrDefault(g => g.Schluessel == s))
                .OfType<GleichGedachtGruppe>()
                .ToList();

            if (gewaehlt.Count < 2 || gewaehlt.Count != schluessel.Distinct().Count())
                return false;

            zusammengelegt = GleichGedacht.LegeZusammen(gewaehlt, zusammengelegt);
            return true;
        }

        /// <summary>Was die Spielleitung zusammengelegt hat, gilt nicht mehr.</summary>
        public bool HebeZusammenlegenAuf()
        {
            if (zusammengelegt.Count == 0)
                return false;

            zusammengelegt = new Dictionary<Guid, string>();
            return true;
        }

        protected override IReadOnlyDictionary<Guid, Teamanteil> Anteile(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var anteile = new Dictionary<Guid, Teamanteil>();

            if (antworten is not { } offen)
                return anteile;

            foreach (var team in Teams)
            {
                var folge = MitKarte(team, eingaenge);

                if (folge.Count == 0)
                    continue;

                var ergebnis = GleichGedacht.Werte(eingaenge[folge[0]].Feldpunkte, folge, offen, zusammengelegt);

                foreach (var mitglied in folge)
                {
                    if (ergebnis.Anteile.TryGetValue(mitglied, out var punkte))
                        anteile[mitglied] = new Teamanteil(RatingKind.Right, punkte, $"Gleich gedacht {team.Name}: {GruppeVon(ergebnis, mitglied)}");
                    else if (eingaenge[mitglied].Grundwert != 0)
                        anteile[mitglied] = new Teamanteil(RatingKind.None, 0, $"Gleich gedacht {team.Name}: keine Übereinstimmung");
                }
            }

            return anteile;
        }

        private static string GruppeVon(GleichGedachtErgebnis ergebnis, Guid mitglied)
        {
            var gruppe = ergebnis.Gruppen.First(g => g.Mitglieder.Contains(mitglied));
            return $"{Zitat(gruppe)} – {gruppe.Mitglieder.Count} gleich";
        }

        /// <summary>„„Paris“" bzw. „„Paris“ / „Pariss“" - jede Schreibweise einmal.</summary>
        internal static string Zitat(GleichGedachtGruppe gruppe) => string.Join(" / ", gruppe.Texte.Select(t => $"„{t}“"));

        /// <summary>
        /// Was der Beamer nach dem Vergleich zeigt, je Team eine Zeile: „✚ Rot – gleich gedacht: „Paris“ (Anna, Bert) · außerdem:
        /// „Rom“ (Carl) · ohne Antwort: Dora". Nie allein über Farbe - die Zeile sagt, was zählt.
        /// </summary>
        public IReadOnlyList<string> Aufloesung()
            => Gruppen.Select(Zeile).ToList();

        private string Zeile(GleichGedachtTeam team)
        {
            var gruppen = team.Ergebnis.Gruppen;
            var zaehlen = gruppen.Where(g => g.Zaehlt).ToList();
            var uebrige = gruppen.Where(g => !g.Zaehlt).ToList();
            var ohne = team.Team.Folge.Where(m => !gruppen.Any(g => g.Mitglieder.Contains(m))).ToList();

            var uebrigeText = string.Join(" · ", uebrige.Select(Gruppe));
            var teile = new List<string>
            {
                zaehlen.Count > 0 ? "gleich gedacht: " + string.Join(" und ", zaehlen.Select(Gruppe))
                    : uebrige.Count > 0 ? "keine Übereinstimmung: " + uebrigeText
                    : "keine Übereinstimmung",
            };

            if (zaehlen.Count > 0 && uebrige.Count > 0)
                teile.Add("außerdem: " + uebrigeText);

            if (ohne.Count > 0)
                teile.Add("ohne Antwort: " + string.Join(", ", ohne.Select(NameVon)));

            return $"{team.Team.Symbol} {team.Team.Name} – {string.Join(" · ", teile)}";
        }

        private string Gruppe(GleichGedachtGruppe gruppe) => $"{Zitat(gruppe)} ({string.Join(", ", gruppe.Mitglieder.Select(NameVon))})";

        /// <summary>Eine Zeile in der Liste des Spielleiters: „✚ Rot: „Paris“ (Anna, Bert) – zählt".</summary>
        public string Beschriftung(Teamaufstellung team, GleichGedachtGruppe gruppe)
            => $"{team.Symbol} {team.Name}: {Gruppe(gruppe)} – {(gruppe.Zaehlt ? "zählt" : "zählt nicht")}";
    }
}
