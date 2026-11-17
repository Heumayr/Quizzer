using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Views.GameViews.Sonderregeln;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>
    /// Finale im Teamspiel (M37 E4, Entwurf Abschnitt 7): jedes Mitglied setzt in der Vorphase verdeckt von seinem eigenen
    /// Kontostand, dann antwortet der Sprecher fürs Team - Sprecher, Sperrsätze und die Ausbreitung der Teamwertung kommen aus
    /// dem Rahmen der Teamregeln. Je Mitglied gilt <see cref="EinsatzRegel"/> auf den eigenen Einsatz mit der Bewertung des
    /// Teams, gekappt am eigenen Kontostand (E2). Der Beamer löst Team für Team auf, aufsteigend nach Teamstand.
    /// </summary>
    internal sealed class FinalTeamAblauf : TeamregelAblauf, IFinalrunde
    {
        private Dictionary<Guid, int> einsaetze = new();

        public FinalTeamAblauf()
            : base(CellSpecialRule.Final)
        {
        }

        private int Einsatz(Guid spieler) => FinalRegel.EinsatzOder0(einsaetze.TryGetValue(spieler, out var wert) ? wert : null);

        /// <summary>Anders als die übrigen Teamregeln spielt das Finale eine Vorphase: die Einsätze.</summary>
        protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
            => einsaetze = abgaben.Where(e => e.Value is int).ToDictionary(e => e.Key, e => (int)e.Value);

        protected override IReadOnlyDictionary<Guid, Teamanteil> Anteile(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var anteile = new Dictionary<Guid, Teamanteil>();
            var sprecher = Sprecher;

            foreach (var team in Teams)
            {
                if (WertungDes(team, sprecher, eingaenge) is not { } wertung)
                    continue;

                foreach (var mitglied in team.Folge.Where(eingaenge.ContainsKey))
                {
                    var einsatz = Einsatz(mitglied);

                    if (EinsatzRegel.Rohwert(einsatz, wertung) is { } punkte)
                        anteile[mitglied] = new Teamanteil(wertung.Bewertung, punkte, $"Finale {team.Name}: Einsatz {einsatz}");
                }
            }

            return anteile;
        }

        /// <summary>Was die Karte zeigt: wer spricht, dazu der eigene Einsatz.</summary>
        protected override string AbgabeText(Guid spieler)
        {
            var rolle = base.AbgabeText(spieler);
            var einsatz = einsaetze.TryGetValue(spieler, out var wert) ? $"Einsatz: {wert}" : "Kein Einsatz – setzt 0";

            return rolle.Length > 0 ? $"{rolle} · {einsatz}" : einsatz;
        }

        /// <summary>„✚ Rot – Stand 500 · „Ottawa“ · Einsätze: Anna 100, Bert 0“ - Team für Team, das führende zuletzt.</summary>
        public IReadOnlyList<FinalZeile> Aufloesung(Func<Guid, string?> antwortVon)
        {
            var sprecher = Sprecher;
            var teams = Teams.ToDictionary(t => t.TeamId);
            var plaetze = Teams.Select(t => new FinalPlatz(t.TeamId, $"{t.Symbol} {t.Name}", t.Stand));

            return FinalRegel.Reihenfolge(plaetze)
                .Select(p => new FinalZeile(p, Zeile(teams[p.Id], p, sprecher, antwortVon)))
                .ToList();
        }

        private string Zeile(Teamaufstellung team, FinalPlatz platz, IReadOnlyDictionary<Guid, Guid> sprecher, Func<Guid, string?> antwortVon)
        {
            IEnumerable<Guid> zuerst = sprecher.TryGetValue(team.TeamId, out var id) ? [id] : [];
            var antwort = zuerst.Concat(team.Folge).Select(antwortVon).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            var einsaetzeText = string.Join(", ", team.Folge.Select(m => $"{NameVon(m)} {Einsatz(m)}"));

            return $"{platz.Name} – Stand {platz.Stand} · {FinalRegel.AntwortText(antwort)} · Einsätze: {einsaetzeText}";
        }
    }
}
