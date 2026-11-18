using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.GameViews.Sonderregeln;

namespace Quizzer.Views.GameViews.Teamregeln
{
    /// <summary>Ein Team an einer Teamregel-Zelle: Kennung, Name, Symbol der Farbe und die Mitglieder in Sprecherfolge.</summary>
    /// <param name="TeamId">Das Team; wer ohne Team ist, ist mit seiner eigenen Kennung sein eigenes Team (fail-closed).</param>
    /// <param name="Stand">Die Teamsumme beim Öffnen (<c>Game.Teamstand</c>) - das Finale löst danach auf (M37 E4).</param>
    internal sealed record Teamaufstellung(Guid TeamId, string Name, string Symbol, IReadOnlyList<Guid> Folge, int Stand = 0);

    /// <summary>Was ein Mitglied vom Teamwert bekommt, noch ungekappt - mit der Bewertung des Teams und dem Grund für die Karte.</summary>
    internal sealed record Teamanteil(RatingKind Bewertung, int Punkte, string Grund);

    /// <summary>
    /// Der Rahmen der Teamregeln (M37 T5, Entwurf Abschnitte 1, 3 und 4): je Team ein Sprecher - der Erste der Sprecherfolge,
    /// der diese Frage sprechen kann -, nur die Sprecher antworten (<c>AnswerLock</c>), die anderen lesen, mit wem sie
    /// beraten. Die Regel rechnet je Team einen Wert und verteilt ihn über die Sprecherfolge (<see cref="Aufteilung"/>); jeder
    /// Anteil ist ein Vorschlag auf der eigenen Karte (E1), gekappt am eigenen Kontostand (E2), und ein Joker wirkt danach auf
    /// den eigenen Anteil.
    /// </summary>
    internal abstract class TeamregelAblauf : SonderregelAblauf
    {
        /// <summary>Wer beim Vorbereiten getrennt war - zwei Gründe, getrennt geführt (R5-7): das Aufheben der Joker-Sperre holt ihn nicht zurück.</summary>
        private readonly HashSet<Guid> getrennt = new();

        /// <summary>Wer per Joker gesperrt ist, mit dem Satz für sein Telefon.</summary>
        private readonly Dictionary<Guid, string> gesperrt = new();

        protected TeamregelAblauf(CellSpecialRule regel)
            : base(SpecialRuleProfiles.All.Single(p => p.Rule == regel))
        {
        }

        /// <summary>Die Teams der Zelle, wie sie beim Öffnen standen.</summary>
        public IReadOnlyList<Teamaufstellung> Teams => Grundlage.Teams ?? [];

        /// <summary>Wer diese Frage nicht antworten kann (Joker-Sperre mit Satz, getrennt ohne) - für Regeln ohne Sprecher (M37 T6).</summary>
        protected IReadOnlyDictionary<Guid, string?> Ausgelassen
        {
            get
            {
                var alle = getrennt.ToDictionary(id => id, _ => (string?)null);

                foreach (var (id, satz) in gesperrt)
                    alle[id] = satz;

                return alle;
            }
        }

        /// <summary>Der Platzhalter der Texteingabe am Telefon.</summary>
        public virtual string Eingabehinweis => CurrentQuestionViewModel.TeamregelEingabe;

        /// <summary>Was Beamer und Spielleiter unter dem Titel lesen, solange geantwortet wird.</summary>
        public virtual string Standtext => "Nur die Sprecher antworten";

        /// <summary>Ob immer per Text geantwortet wird, auch wo die Frage Tasten hat (M37 T6: Gleich gedacht vergleicht Texte).</summary>
        public virtual bool NurTexteingabe => false;

        /// <summary>Je Team der Sprecher dieser Frage für die Telefone; <c>null</c>: sie zeigen den Sprecher des Stands.</summary>
        public virtual IReadOnlyDictionary<Guid, Guid>? SprecherAmTelefon => Sprecher;

        /// <summary>Getrennt: dieser Spieler spricht bei dieser Frage nicht; der Nächste der Folge übernimmt.</summary>
        public void LasseGetrenntAus(Guid spieler) => getrennt.Add(spieler);

        /// <summary>Per Joker gesperrt: wie getrennt, mit dem Satz für sein Telefon.</summary>
        public void SperrePerJoker(Guid spieler, string satz) => gesperrt[spieler] = satz;

        /// <summary>
        /// Die Spielleitung hebt eine Joker-Sperre auf (E1): die Gesperrten sprechen wieder, wenn sie dran sind - nur die Joker-Gründe,
        /// wer getrennt ist, bleibt draußen (R5-7).
        /// </summary>
        public void NimmAuslassungZurueck(IEnumerable<Guid> spieler)
        {
            foreach (var id in spieler)
                gesperrt.Remove(id);
        }

        /// <summary>Je Team der Sprecher dieser Frage; ein Team, in dem keiner sprechen kann, fehlt.</summary>
        public IReadOnlyDictionary<Guid, Guid> Sprecher
        {
            get
            {
                var aus = Ausgelassen.Keys.ToHashSet();
                var sprecher = new Dictionary<Guid, Guid>();

                foreach (var team in Teams)
                {
                    if (Sprecherfolge.Sprecher(team.Folge, aus) is { } id)
                        sprecher[team.TeamId] = id;
                }

                return sprecher;
            }
        }

        /// <summary>Nur die Sprecher antworten - auch wenn keiner bleibt, antwortet niemand sonst (fail-closed).</summary>
        public override IReadOnlyCollection<Guid>? Antwortende => Sprecher.Values.ToList();

        /// <summary>Je Mitglied, das nicht spricht, der Satz für sein Telefon.</summary>
        public override IReadOnlyDictionary<Guid, string>? Sperrsaetze
        {
            get
            {
                var sprecher = Sprecher;
                var ausgelassen = Ausgelassen;
                var saetze = new Dictionary<Guid, string>();

                foreach (var team in Teams)
                {
                    var wer = sprecher.TryGetValue(team.TeamId, out var id) ? id : (Guid?)null;

                    foreach (var mitglied in team.Folge.Where(m => m != wer))
                        saetze[mitglied] = ausgelassen.GetValueOrDefault(mitglied) ?? BeratSatz(wer);
                }

                return saetze;
            }
        }

        private string BeratSatz(Guid? sprecher)
            => sprecher is { } id
                ? $"Berate dich mit {NameVon(id)} – {NameVon(id)} gibt eure Antwort ab."
                : "Aus deinem Team kann diesmal niemand antworten.";

        /// <summary>„Sprecher: ✚ Rot – Anna · ★ Blau – Carl" für Beamer und Spielleiter.</summary>
        public override string Hinweis
        {
            get
            {
                var sprecher = Sprecher;
                var teile = Teams.Select(t => $"{t.Symbol} {t.Name} – {(sprecher.TryGetValue(t.TeamId, out var id) ? NameVon(id) : "niemand")}");

                return Teams.Count == 0 ? string.Empty : "Sprecher: " + string.Join(" · ", teile);
            }
        }

        /// <summary>Was die Karte zeigt: wer spricht, mit wem beraten wird.</summary>
        protected override string AbgabeText(Guid spieler)
        {
            if (Teams.FirstOrDefault(t => t.Folge.Contains(spieler)) is not { } team)
                return string.Empty;

            var sprecher = Sprecher.TryGetValue(team.TeamId, out var id) ? id : (Guid?)null;

            return sprecher == spieler ? $"Sprecher für {team.Name}"
                : sprecher is { } wer ? $"Berät mit {NameVon(wer)}"
                : "Kein Sprecher – das Team antwortet nicht";
        }

        /// <summary>Teamregeln spielen keine Vorphase - es gibt nichts zu übernehmen. Das Finale (M37 E4) übernimmt die Einsätze.</summary>
        protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
        {
        }

        /// <summary>Je Mitglied sein Anteil am Teamwert, noch ungekappt; ein Team ohne Wertung bewegt nichts.</summary>
        protected abstract IReadOnlyDictionary<Guid, Teamanteil> Anteile(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge);

        /// <summary>
        /// Die Antwort des Sprechers gilt fürs Team: die Bewertung seiner Karte, sonst die erste bewertete in Sprecherfolge (E1);
        /// <c>null</c>, solange keine Karte des Teams bewertet ist. Teamberatung und Finale (M37 E4) breiten sie auf alle aus.
        /// </summary>
        protected static SonderregelEingang? WertungDes(
            Teamaufstellung team, IReadOnlyDictionary<Guid, Guid> sprecher, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            IEnumerable<Guid> zuerst = sprecher.TryGetValue(team.TeamId, out var id) ? [id] : [];

            return zuerst.Concat(team.Folge).Select(eingaenge.GetValueOrDefault).FirstOrDefault(e => e is { Bewertung: not RatingKind.None });
        }

        protected sealed override IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => WerteMitJoker(eingaenge, null);

        /// <summary>
        /// Erst die Teamregel, dann der Joker auf den eigenen Anteil (Entwurf Abschnitt 3): Doppelt heißt zweimal der Anteil,
        /// Schutz nimmt das Minus des Anteils. Gekappt je Mitglied (E2); ein gekappter Rest wird nicht umgelegt.
        /// </summary>
        public IReadOnlyDictionary<Guid, Punktevorschlag> WerteMitJoker(
            IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge, Func<Guid, SonderregelEingang, Punktevorschlag?>? joker)
        {
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            if (!VorphaseZu)
                return vorschlaege;

            foreach (var (spieler, anteil) in Anteile(eingaenge))
            {
                if (!eingaenge.TryGetValue(spieler, out var eigen))
                    continue;

                var eingang = eigen with { Bewertung = anteil.Bewertung, Grundwert = anteil.Punkte };

                vorschlaege[spieler] = joker?.Invoke(spieler, eingang) is { } mitJoker
                    ? mitJoker with { Grund = $"{anteil.Grund} · {mitJoker.Grund}" }
                    : Sonderregelwertung.Gekappt(anteil.Punkte, anteil.Grund, eigen.Konto);
            }

            return vorschlaege;
        }

        protected string NameVon(Guid spieler) => Grundlage.NameVon(spieler);
    }
}
