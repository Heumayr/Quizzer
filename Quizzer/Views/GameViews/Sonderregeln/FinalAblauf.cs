using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>Eine Zeile der Auflösung: der Platz - Spieler oder Team - und was der Beamer zu ihm zeigt.</summary>
    internal sealed record FinalZeile(FinalPlatz Platz, string Text);

    /// <summary>
    /// Was das Fragefenster vom Finale braucht (M37 E4), mit und ohne Teams: die Auflösung in ihrer Reihenfolge - aufsteigend
    /// nach Stand -, je mit Einsatz und Antwort. Die Antworten liest das Fenster von den Karten; die Regel kennt nur die Einsätze.
    /// </summary>
    internal interface IFinalrunde
    {
        IReadOnlyList<FinalZeile> Aufloesung(Func<Guid, string?> antwortVon);
    }

    /// <summary>
    /// Finale ohne Teams (M37 E4, E01): die Vorphase des Einsatzes, dann antwortet jeder selbst per Texteingabe. Richtig gewinnt
    /// den eigenen Einsatz, falsch verliert ihn (<see cref="FinalRegel"/>); wer nichts gesetzt hat, setzt 0. Im Teamspiel spielt
    /// <c>FinalTeamAblauf</c>.
    /// </summary>
    internal sealed class FinalAblauf : SonderregelAblauf<int>, IFinalrunde
    {
        public FinalAblauf()
            : base(CellSpecialRule.Final)
        {
        }

        protected override Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang)
            => FinalRegel.Werte(Von(spieler), eingang);

        protected override string AbgabeText(Guid spieler)
            => Von(spieler) is { } einsatz ? $"Einsatz: {einsatz}" : "Kein Einsatz – setzt 0";

        /// <summary>„Anna – Stand 300 · Einsatz 200 · „Ottawa““, vom Letzten zum Führenden.</summary>
        public IReadOnlyList<FinalZeile> Aufloesung(Func<Guid, string?> antwortVon)
        {
            var plaetze = Grundlage.Spielerfolge
                .Select(id => new FinalPlatz(id, Grundlage.NameVon(id), Grundlage.Kontostaende.GetValueOrDefault(id)));

            return FinalRegel.Reihenfolge(plaetze)
                .Select(p => new FinalZeile(p,
                    $"{p.Name} – Stand {p.Stand} · Einsatz {FinalRegel.EinsatzOder0(Von(p.Id))} · {FinalRegel.AntwortText(antwortVon(p.Id))}"))
                .ToList();
        }
    }
}
