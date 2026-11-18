using LocalBuzzer.Service.Base.States;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Extentions;
using Quizzer.Views.StaticRessources;
using System.Collections.Concurrent;
using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Buzzer-Teil der Frageansicht: welches Layout die Frage auf die Telefone bringt und was
    /// mit den Rueckmeldungen geschieht.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>
        /// Ob diese Frage die Telefone braucht, der Buzzer-Server aber nicht laeuft.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> wurde eine Frage geoeffnet, ohne dass der Server lief,
        /// stieg die Vorbereitung <b>stumm</b> aus. Kein Layout ging auf die Telefone, die
        /// Buzzer-Zeile blieb leer, und "Runde zuruecksetzen" war tot. Das Fragefenster ist
        /// modal - der Spielleiter kam an das Buzzer-Fenster gar nicht mehr heran und musste
        /// erst die Frage schliessen. Auf dem Bildschirm stand kein Grund.
        /// </para>
        /// <para>
        /// <b>Gemessen wird der SERVERZUSTAND, nicht das Vorhandensein des ViewModels.</b> Der
        /// erste Anlauf am selben Tag prüfte <c>BuzzerControlsViewModel == null</c> - und das
        /// wird beim Stoppen nie null, es wird nur entsorgt. Nach „Server beenden" (oder nach
        /// dem Schließen des Spielfensters, das ihn mit beendet) blieb der Hinweis damit aus,
        /// obwohl genau dann nichts mehr läuft.
        /// </para>
        /// </summary>
        public bool BuzzerFehlt =>
            StaticManager.BuzzerServerViewModel?.IsBuzzerServerRunning != true
            && ((Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None) != BuzzerControlsLayout.None
                || (Coordinate != null && DataModels.Questions.Sonderregeln.SpecialRuleProfiles.Applies(Coordinate, Question, Teamspiel)));

        /// <summary>Der Satz dazu - leer, solange nichts fehlt.</summary>
        public string BuzzerFehltText => BuzzerFehlt
            ? "Der Buzzer-Server läuft nicht. Diese Frage geht nicht auf die Telefone - "
              + "Frage schließen, im Spielfeld „Buzzer-Server öffnen“ drücken, dann erneut öffnen."
            : string.Empty;

        public Visibility BuzzerFehltVisibility =>
            BuzzerFehlt ? Visibility.Visible : Visibility.Collapsed;

        private async Task PrepareBuzzerlayoutAsync()
        {
            if (BuzzerControlsViewModel == null || Coordinate == null)
            {
                // Nicht mehr stumm: die drei Eigenschaften oben tragen den Grund ins Fenster.
                OnPropertyChanged(nameof(BuzzerFehlt));
                OnPropertyChanged(nameof(BuzzerFehltText));
                OnPropertyChanged(nameof(BuzzerFehltVisibility));

                return;
            }

            // Sonderregel (M35 C3): erst die Vorphase - die Frage kommt, wenn sie zu ist.
            if (await StarteVorphaseAsync())
                return;

            // Joker-Runde (M35 C7b): nur ohne Sonderregel und Minispiel - die Frage kommt, wenn sie zu ist.
            if (await StarteJokerRundeAsync())
                return;

            // Teamregel (M37 T5): nach den Jokern die Sprecher - an Hub und Telefone, dazu die Beratungszeit.
            await BereiteTeamregelVorAsync();

            // Auktion, Experte, Allianz (M35 C3b), Teamregel: nur einige duerfen antworten - am Server geprueft.
            await SetzeAntwortendeAsync();

            var layout = AntwortLayout;

            // R5-2: eine Teamregel ohne Vorphase steht erst jetzt - sonst schickte „Runde zuruecksetzen" weiter das Layout der Frage.
            OnPropertyChanged(nameof(CurrentBuzzerLayout));

            // Der Spielleiter soll waehrend der Runde sehen, worauf er wartet und was richtig
            // waere - bis hierher stand beides erst im Ergebnisfenster, also erst danach.
            BuzzerControlsViewModel.SetRoundInfo(Question);

            switch (layout)
            {
                case BuzzerControlsLayout.None:
                    break;

                case BuzzerControlsLayout.Buzzer:
                    BuzzerControlsViewModel.WinnerDeclared = OnWinnerDeclared;
                    break;

                case BuzzerControlsLayout.KeySelect:
                    var tasten = Question?.Steps.GetKeyDictionary();
                    var reihenfolge = Question is OrderingQuestion or MatchingQuestion;
                    var wahrFalsch = Question is TrueFalseQuestion;

                    // Zuordnung: das Telefon zeigt, zu welchem Begriff gerade gewaehlt wird.
                    var begriffe = Question is MatchingQuestion
                        ? Question.Steps
                            .Where(s => !s.IsStart && !s.IsFinish && s.SolutionNumber > 0)
                            .OrderBy(s => s.SolutionNumber)
                            .Select(s => s.PartnerText)
                            .ToList()
                        : null;

                    // Bei der Reihenfolge muessen alle Tasten gewaehlt werden - die Zahl kommt
                    // aus den Tasten selbst, nicht aus dem gespeicherten Wert, der hinter einer
                    // umgewandelten Frage zuruecksein kann. Minispiel Mehrheit: genau eine Option
                    // (M35 B2) - fest, damit eine umgewandelte Frage keine Mehrfachstimme behaelt.
                    // M37 T7: die Team-Abstimmung zeigt ab diesem Ausspielen Stimme und Stand des eigenen Teams.
                    StarteTeamAbstimmung();

                    var erlaubt = Question is MinigameQuestion ? 1
                        : reihenfolge || wahrFalsch ? Math.Max(tasten?.Count ?? 1, 1)
                        : Question?.BuzzerMaxAllowedKeySelect ?? 1;

                    await BuzzerControlsViewModel.SetKeySelectorDictionary(
                        tasten,
                        erlaubt,
                        Question?.ShowTextOnKeySelect ?? true,
                        Question?.Id,
                        reihenfolge,
                        begriffe,
                        wahrFalsch);

                    BuzzerControlsViewModel.PlayerSelectedKeys = OnPlayerSelectedKeys;
                    BuzzerControlsViewModel.AllPlayersSelectedKeys = OnAllPlayersSelectedKeys;

                    break;

                case BuzzerControlsLayout.Input:
                    if (Question is AppreciateQestion appreciate)
                    {
                        await BuzzerControlsViewModel.SetInputInfo(
                            appreciate.ValueKind, appreciate.Unit, appreciate.Id);
                    }
                    else if (Teamregel != null || Finale != null)
                        BuzzerControlsViewModel.SetTextInputInfo(Teamregel?.Eingabehinweis ?? FinaleEingabe, Question?.Id);

                    // M37 E4: im Finale gibt jeder genau einmal ab.
                    BereiteFinaleAntwortenVor();

                    BuzzerControlsViewModel.PlayerSubmittedInput = OnPlayerSubmittedInput;
                    BuzzerControlsViewModel.AllPlayersSubmittedInput = OnAllPlayersSubmittedInput;

                    break;

                case BuzzerControlsLayout.Minigame:
                    PrepareMinigame();

                    break;
            }

            // Erst das Ausspielen bringt das Layout auf die Telefone: nur ResetRoundAsync ruft
            // StateManager.ResetLayouts und schickt StateChanged an alle Clients.
            //
            // Frueher stand das allein im Buzzer-Zweig. Bei Multiple Choice und bei der
            // Schaetzfrage wurden die Angaben zwar gesetzt, aber nie verschickt - CurrentLayout
            // blieb None, CurrentState null, und damit blieb jeder Spieler gesperrt. Am Telefon
            // sah das aus, als bestuende keine Verbindung.
            if (layout != BuzzerControlsLayout.None && !Coordinate.IsDone)
            {
                await BuzzerControlsViewModel.ResetRoundAsync(CurrentBuzzerLayout);
            }

            // V22: erst nach dem Ausspielen - das setzt selbst zurueck.
            MeldeAntwortRundeAn(layout);
        }

        private async Task OnAllPlayersSelectedKeys(ConcurrentDictionary<Guid, BuzzerKeySelector.SelectionResult> dictionary)
        {
            BeratungszeitAbgegeben(); // R5-5
            PlayersResultViewModel?.SelectionResultsDic = dictionary;
            await TeamAbstimmungZuAsync(dictionary);
            await OpenResultsAsync();
        }

        private async Task OnPlayerSelectedKeys(BuzzerKeySelector.SelectionResult result)
        {
            PlayersResultViewModel?.SetSelectionResult(result);
            await OpenResultsAsync();
        }

        private async Task ClearBuzzerLayouts()
        {
            // M35 C7b: ohne Wertung gehen die Joker dieser Frage wieder auf - auch ohne laufenden Server.
            await RaeumeJokerAufAsync();

            if (BuzzerControlsViewModel != null)
            {
                //buzzer
                BuzzerControlsViewModel.WinnerDeclared = null;

                //kex select
                await BuzzerControlsViewModel.SetKeySelectorDictionary(null);
                BuzzerControlsViewModel.PlayerSelectedKeys = null;
                BuzzerControlsViewModel.AllPlayersSelectedKeys = null;

                //schaetzfrage
                BuzzerControlsViewModel.PlayerSubmittedInput = null;
                BuzzerControlsViewModel.AllPlayersSubmittedInput = null;
                BuzzerControlsViewModel.AnswerRoundReset = null; // V22: vor dem Zuruecksetzen unten - das Schliessen leert keine Karten
                ClearAppreciateOutcome();
                ClearMinigame();
                ClearSonderregel();
                await RaeumeTeamregelAufAsync();
                BuzzerControlsViewModel.ClearTeamLock(); // M37 T4: die Team-Sperre gilt nur dieser Frage (F-M37-09)
                BuzzerControlsViewModel.BeendeTeamAbstimmung(); // M37 T7
                BuzzerControlsViewModel.SetRoundInfo(null);

                await BuzzerControlsViewModel.ResetRoundAsync(null);
            }
        }

        /// <summary>Was gerade auf den Telefonen liegt - in der Vorphase einer Sonderregel deren Rahmen (M35 C3).</summary>
        public BuzzerControlsLayout CurrentBuzzerLayout
            => VorphaseLaeuft ? BuzzerControlsLayout.Minigame : AntwortLayout;
    }
}
