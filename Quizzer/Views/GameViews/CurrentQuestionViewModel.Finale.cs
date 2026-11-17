using Quizzer.Base;
using Quizzer.Views.GameViews.Sonderregeln;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Das Finale im Fragefenster (M37 E4, E01): nach der Vorphase des Einsatzes antwortet jeder - im Teamspiel der Sprecher -
    /// genau einmal per Eingabe. Sind die Antworten zu (alle abgegeben oder „Runde schließen“), deckt der Spielleiter am Beamer
    /// einen nach dem anderen auf, aufsteigend nach Stand; Einsatz und Antwort stehen erst dann dort. Gewertet wird wie immer
    /// über die Karte (E1).
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Platzhalter der Texteingabe im Finale ohne Teams.</summary>
        internal const string FinaleEingabe = "Deine Antwort fürs Finale";

        /// <summary>Das Finale dieser Zelle, sobald es steht; <c>null</c> ohne.</summary>
        private IFinalrunde? Finale => sonderregel as IFinalrunde;

        private bool finaleAntwortenZu;
        private int finaleAufgedeckt;
        private RelayCommand? finaleAufloesenCommand;

        /// <summary>„Nächsten auflösen“ - einer nach dem anderen bzw. Team für Team.</summary>
        public ICommand FinaleAufloesenCommand => finaleAufloesenCommand ??= new RelayCommand(_ => LoeseFinaleWeiterAuf());

        /// <summary>Der Knopf steht da, solange die Antworten zu sind und noch jemand aufzudecken ist.</summary>
        public Visibility FinaleAufloesenVisibility
            => Finale is { } finale && finaleAntwortenZu && finaleAufgedeckt < finale.Aufloesung(AntwortVon).Count
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>Die Antworten gehen auf die Telefone: genau eine je Spieler, und der Spielleiter sieht, worauf er wartet.</summary>
        private void BereiteFinaleAntwortenVor()
        {
            if (Finale == null || BuzzerControlsViewModel == null)
                return;

            BuzzerControlsViewModel.SetzeEinmalAbgabe();
            ZeigeFinale();
        }

        /// <summary>Alle haben abgegeben oder die Runde ist geschlossen - ab jetzt darf aufgelöst werden.</summary>
        private void FinaleAntwortenGeschlossen()
        {
            if (Finale == null || finaleAntwortenZu)
                return;

            finaleAntwortenZu = true;
            ZeigeFinale();
        }

        /// <summary>Deckt den Nächsten auf; <c>false</c>, solange die Antworten offen sind oder alle aufgedeckt.</summary>
        internal bool LoeseFinaleWeiterAuf()
        {
            if (Finale is not { } finale || !finaleAntwortenZu || finaleAufgedeckt >= finale.Aufloesung(AntwortVon).Count)
                return false;

            finaleAufgedeckt++;
            ZeigeFinale();
            return true;
        }

        /// <summary>Die Antwort auf der Karte - dorthin bringt sie der Buzzer.</summary>
        private string? AntwortVon(Guid spieler) => Karten.FirstOrDefault(k => k.Player.Id == spieler)?.InputResult?.Value;

        private void ZeigeFinale()
        {
            if (Finale is not { } finale || sonderregel is not { } regel)
                return;

            var zeilen = finaleAntwortenZu ? finale.Aufloesung(AntwortVon) : [];
            var stand = !finaleAntwortenZu ? "Antworten am Telefon – aufgelöst wird, sobald alle abgegeben haben oder die Runde geschlossen ist"
                : finaleAufgedeckt < zeilen.Count ? $"Aufgelöst: {finaleAufgedeckt} von {zeilen.Count} – als Nächstes {zeilen[finaleAufgedeckt].Platz.Name}"
                : $"Alle {zeilen.Count} aufgelöst";

            Sonderregel.Zeige(regel.Titel, regel.Regeltext, stand, false, regel.Hinweis);
            Sonderregel.ZeigeAufloesung(zeilen.Take(finaleAufgedeckt).Select(z => z.Text).ToList());
            OnPropertyChanged(nameof(FinaleAufloesenVisibility));
        }

        private void LeereFinale()
        {
            finaleAntwortenZu = false;
            finaleAufgedeckt = 0;
            OnPropertyChanged(nameof(FinaleAufloesenVisibility));
        }
    }
}
