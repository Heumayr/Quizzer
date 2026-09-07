using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Was vor dem Start eines Spiels geprüft wird.
    /// <para>
    /// Eigene Teildatei, weil <c>GameMasterViewModel.cs</c> über der Größen-Obergrenze liegt und
    /// durch eine Änderung nicht länger werden darf.
    /// </para>
    /// </summary>
    public partial class GameMasterViewModel
    {
        /// <summary>
        /// Die harten Voraussetzungen. Fehlt eine, startet das Spiel nicht.
        /// </summary>
        private static bool Startbar(Game dbGame)
        {
            // Ueber UserPrompt statt MessageBox.Show: der Weg ist in Tests austauschbar, und
            // ein ViewModel soll kein Fenster kennen. Umgestellt 2026-09-06, dabei ins Deutsche.
            var errors = new List<string>();

            if (dbGame.ModeratorPlayerId == null || dbGame.ModeratorPlayerId == Guid.Empty || dbGame.Moderator == null)
            {
                errors.Add("Dem Spiel fehlt der Moderator. Er wird im Spielaufbau ausgewählt.");
            }

            if (!dbGame.Players.Any())
            {
                errors.Add("Dem Spiel ist kein Mitspieler zugeordnet. Mindestens einer muss im "
                         + "Spielaufbau hinzugefügt werden.");
            }

            // Nur die belegten Zellen zaehlen. Der Rasteraufbau legt fuer JEDE Position eine
            // Zeile an - gegen GameGridCoordinates.Count geprueft feuerte diese Stelle nie, und
            // ein Spielfeld ganz ohne Frage liess sich starten. Gemessen 2026-09-07, dieselbe
            // Familie wie IsGameFinished und CalculatetThreshold.
            if (!dbGame.SpielbareZellen.Any())
            {
                errors.Add("Dem Spielfeld ist keine Frage zugewiesen. Mindestens eine muss im "
                         + "Spielaufbau auf eine Zelle gelegt werden.");
            }

            if (errors.Any())
            {
                UserPrompt.Inform(
                    string.Join(Environment.NewLine + Environment.NewLine, errors),
                    "Spiel lässt sich nicht starten");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Prüft die Voraussetzungen und danach die zugewiesenen Fragen.
        /// </summary>
        private static async Task<bool> StartbarAsync(Game dbGame)
        {
            if (!Startbar(dbGame))
                return false;

            return await UnspielbareFragenAbgeklaertAsync(dbGame);
        }

        /// <summary>
        /// Nennt die Fragen im Spielfeld, die sich nicht ordentlich spielen lassen - und fragt,
        /// ob trotzdem gestartet werden soll.
        /// <para>
        /// <b>Der Fragenprüfer hält nur das Speichern an.</b> Eine Frage kann nach dem Zuweisen
        /// ungültig werden - durch eine Typumwandlung, durch das Entfernen ihres letzten
        /// Lösungsschritts, oder weil sie aus einer Zeit vor dem Prüfer stammt. Bis zum
        /// 2026-09-07 fiel das erst auf, wenn der Spielleiter die Zelle vor Gästen öffnete.
        /// </para>
        /// <para>
        /// <b>Gemessen an der Spieldatenbank am 2026-09-07:</b> von 23 zugewiesenen Fragen
        /// wurden zwei beanstandet - eine Multiple-Choice-Frage ohne markierte Lösung und eine
        /// Schätzfrage, die von ihrem Profil abweicht.
        /// </para>
        /// <para>
        /// <b>Es wird gefragt, nicht abgewiesen.</b> Die übrigen Fragen sind spielbar, und der
        /// Spielleiter kann die eine Zelle auslassen - das ist seine Entscheidung, nicht die des
        /// Programms.
        /// </para>
        /// </summary>
        private static async Task<bool> UnspielbareFragenAbgeklaertAsync(Game dbGame)
        {
            var beanstandet = new List<string>();

            using (var ctrl = new QuestionBasesController())
            {
                foreach (var zelle in dbGame.SpielbareZellen)
                {
                    var frage = await ctrl.GetAsync(zelle.QuestionBaseId!.Value);

                    if (frage == null)
                    {
                        beanstandet.Add($"Zelle {zelle.X + 1}/{zelle.Y + 1}: die Frage gibt es nicht mehr.");
                        continue;
                    }

                    var fehler = QuestionValidator.Validate(frage)
                        .Where(i => i.IsError)
                        .Select(i => i.Message)
                        .ToList();

                    if (fehler.Count > 0)
                        beanstandet.Add($"{frage.Designation}: {string.Join(" ", fehler)}");
                }
            }

            if (beanstandet.Count == 0)
                return true;

            var satz = beanstandet.Count == 1
                ? "Eine Frage im Spielfeld lässt sich nicht ordentlich spielen:"
                : $"{beanstandet.Count} Fragen im Spielfeld lassen sich nicht ordentlich spielen:";

            return UserPrompt.Confirm(
                satz + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, beanstandet)
                + Environment.NewLine + Environment.NewLine
                + "Trotzdem starten? Die übrigen Zellen sind davon nicht betroffen.",
                "Spiel starten");
        }
    }
}
