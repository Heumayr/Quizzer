using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Spielablauf
{
    /// <summary>
    /// Die letzte Wertung zurücknehmen (Verbesserung V5, 2026-10-22): am Spielabend hat man sich
    /// schnell verklickt - falscher Spieler richtig, Frage zu früh abgeschlossen. Bisher ging das
    /// nur über erneutes Öffnen der Zelle und Nachtragen jeder Zahl von Hand.
    /// <para>
    /// <b>Eine Stufe, keine Geschichte.</b> Vor dem Öffnen einer Zelle merkt sich der
    /// Spielleiter mit <see cref="Merke"/> deren Ergebnisse, ob sie erledigt war und wer auswählt;
    /// <see cref="NimmZurueckAsync"/> stellt genau das wieder her. Eine zweite Frage danach ersetzt
    /// die gemerkte - weiter zurück geht es über die Zelle selbst.
    /// </para>
    /// <para>
    /// <b>Die Phase bleibt.</b> Ein Phasenwechsel nach der Frage hat die Punkte der Zellen schon
    /// umgestellt; das zurückzudrehen ist „Phase −", nicht diese Klasse.
    /// </para>
    /// </summary>
    public sealed class LetzteWertung
    {
        /// <summary>Was an einer Ergebniszeile zur Wertung gehört - samt Klaus und Bestwert (R4-2), die das Speichern mitschreibt.</summary>
        private sealed record Stand(bool CorrectAnswered, int RightCount, int WrongCount, int CorrectionsCount, int Score, int MinusScore, int Correction,
            int Steals, int? RecordValue, DateTimeOffset? RecordedAt)
        {
            public static readonly Stand Leer = new(false, 0, 0, 0, 0, 0, 0, 0, null, null);

            public static Stand Von(QuestionResult r)
                => new(r.CorrectAnswered, r.RightCount, r.WrongCount, r.CorrectionsCount, r.Score, r.MinusScore, r.Correction, r.Steals, r.RecordValue, r.RecordedAt);

            public int Punkte => Score + Correction - MinusScore;

            public void Auf(QuestionResult r)
            {
                r.CorrectAnswered = CorrectAnswered;
                r.RightCount = RightCount;
                r.WrongCount = WrongCount;
                r.CorrectionsCount = CorrectionsCount;
                r.Score = Score;
                r.MinusScore = MinusScore;
                r.Correction = Correction;
                r.Steals = Steals;
                r.RecordValue = RecordValue;
                r.RecordedAt = RecordedAt;
            }
        }

        // Nach Spieler, nicht nach Zeile: die Zeilen entstehen erst beim Öffnen der Zelle, und das
        // Ergebnisfenster ersetzt die Liste an der Zelle durch frisch geladene Objekte.
        private readonly Dictionary<Guid, Stand> vorher;
        private readonly bool warErledigt;
        private readonly DateTimeOffset? warGespieltUm;
        private readonly Guid auswahl;
        private readonly Guid regulaereAuswahl;

        private LetzteWertung(Game spiel, GameGridCoordinate zelle, IEnumerable<QuestionResult>? zeilen = null)
        {
            Zelle = zelle;
            vorher = (zeilen ?? zelle.QuestionResults).GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => Stand.Von(g.First()));
            warErledigt = zelle.IsDone;
            warGespieltUm = zelle.PlayedAt;
            auswahl = spiel.CurrentChoosingPlayerId;
            regulaereAuswahl = spiel.RegularChoosingPlayerId;
        }

        public GameGridCoordinate Zelle { get; }

        /// <summary>Merkt sich den Stand einer Zelle, bevor sie geöffnet wird.</summary>
        public static LetzteWertung Merke(Game spiel, GameGridCoordinate zelle) => new(spiel, zelle);

        /// <summary>Merkt sich den Stand der Zeilen, die das Ergebnisfenster beim Öffnen frisch gelesen hat (R5-1).</summary>
        public static LetzteWertung Merke(Game spiel, GameGridCoordinate zelle, IEnumerable<QuestionResult> zeilen) => new(spiel, zelle, zeilen);

        /// <summary>
        /// Ob seit dem Merken an einer dieser Zeilen gebucht wurde (R5-1, R4-1). „Richtig“ setzt <c>CorrectAnswered</c> schon als
        /// Vorschlag - gebucht ist erst, was Zähler, Punkte, Klaus oder Bestwert ändert. Dieselben Felder wie
        /// <c>Teamwertung.OhneBuchung</c>, das „richtig beantwortet“ an der gespeicherten Zeile mitzählt (P8-5, <c>BuchungsmassUnitTests</c>).
        /// </summary>
        public bool HatGebucht(IEnumerable<QuestionResult> zeilen)
            => zeilen.Any(r => Stand.Von(r) with { CorrectAnswered = false } != VorherVon(r.PlayerId) with { CorrectAnswered = false });

        /// <summary>Wie viele Punkte an dieser Zeile seit dem Merken gebucht sind (V26).</summary>
        public int GebuchtSeitDemMerken(QuestionResult zeile) => zeile.FinalScore - VorherVon(zeile.PlayerId).Punkte;

        private Stand VorherVon(Guid spielerId) => vorher.GetValueOrDefault(spielerId, Stand.Leer);

        /// <summary>
        /// Ob das Öffnen etwas geändert hat. Nur dann lohnt es sich, die Wertung anzubieten - wer
        /// eine Zelle nur ansieht und schließt, soll die vorige Wertung nicht verlieren.
        /// </summary>
        public bool HatSichGeaendert(Game spiel)
            => Zelle.IsDone != warErledigt
               || spiel.CurrentChoosingPlayerId != auswahl
               || spiel.RegularChoosingPlayerId != regulaereAuswahl
               || Zelle.QuestionResults.Any(r => Stand.Von(r) != VorherVon(r.PlayerId));

        /// <summary>Je Spieler, dessen Punkte sich an dieser Zelle geändert haben: wie viele mehr oder weniger.</summary>
        public IReadOnlyList<(Guid SpielerId, int Unterschied)> Aenderungen()
            => Zelle.QuestionResults
                .Select(r => (r.PlayerId, Unterschied: r.FinalScore - VorherVon(r.PlayerId).Punkte))
                .Where(a => a.Unterschied != 0)
                .ToList();

        /// <summary>Für die Rückfrage: welche Frage, wer wie viel verliert oder zurückbekommt.</summary>
        public string Beschreibung(Game spiel)
        {
            var frage = Zelle.QuestionBase?.Designation is { Length: > 0 } name ? $"„{name}\"" : "die zuletzt gespielte Frage";
            var zeilen = Aenderungen()
                .Select(a =>
                {
                    var wer = spiel.Players.FirstOrDefault(p => p.Id == a.SpielerId)?.CalculatedDisplayName ?? "?";
                    var zurueck = -a.Unterschied;
                    return $"  {wer}: {(zurueck > 0 ? "+" : "−")}{Math.Abs(zurueck)}";
                })
                .ToList();

            var text = $"Die Wertung für {frage} zurücknehmen?";

            if (zeilen.Count > 0)
                text += Environment.NewLine + Environment.NewLine + "Änderung beim Zurücknehmen:" + Environment.NewLine + string.Join(Environment.NewLine, zeilen);

            if (Zelle.IsDone && !warErledigt)
                text += Environment.NewLine + Environment.NewLine + "Die Zelle wird wieder offen.";

            return text;
        }

        /// <summary>
        /// Stellt den gemerkten Stand wieder her und schreibt die Ergebnisse. Zelle und Spiel
        /// schreibt der Aufrufer - der Spielleiter hat dafür schon sein <c>VMSaveAsync</c>.
        /// </summary>
        public async Task NimmZurueckAsync(Game spiel)
        {
            using var ctrl = new QuestionResultsController();

            foreach (var ergebnis in Zelle.QuestionResults)
            {
                VorherVon(ergebnis.PlayerId).Auf(ergebnis);
                await ctrl.UpsertAsync(ergebnis);
            }

            await ctrl.SaveChangesAsync();

            // Der Zeitpunkt mit (M37 E06): eine wieder offene Zelle hat keinen, eine nachgebesserte behaelt ihren.
            Zelle.IsDone = warErledigt;
            Zelle.PlayedAt = warGespieltUm;
            spiel.CurrentChoosingPlayerId = auswahl;
            spiel.RegularChoosingPlayerId = regulaereAuswahl;
        }
    }
}
