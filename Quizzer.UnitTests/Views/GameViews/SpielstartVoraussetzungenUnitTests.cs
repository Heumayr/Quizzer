using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// <b>Die drei harten Voraussetzungen des Spielstarts.</b>
    /// <para>
    /// <b>Sie waren bis zum 2026-09-09 ungeprüft</b> - jede von ihnen hält den Abend an, und
    /// keine wurde gemessen. Der einzige Hinweis auf sie im ganzen Testbestand war ein
    /// <i>Kommentar</i> in einer anderen Klasse („Ohne Moderator steigt LoadModel vor allem
    /// anderen aus - Startbar() verlangt ihn").
    /// </para>
    /// <para>
    /// Besonders die dritte hat Geschichte: sie prüfte einmal <c>GameGridCoordinates.Count</c>
    /// statt der belegten Zellen und feuerte deshalb <b>nie</b> - ein Spielfeld ganz ohne Frage
    /// liess sich starten.
    /// </para>
    /// </summary>
    [TestClass]
    public class SpielstartVoraussetzungenUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void Setup()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void Cleanup() => UserPrompt.Reset();

        /// <summary>Ein Spiel, das alle drei Voraussetzungen erfüllt.</summary>
        private static Game Startbereit()
        {
            var moderator = new Player { Id = Guid.NewGuid(), Designation = "Der Spielleiter" };
            var mitspieler = new Player { Id = Guid.NewGuid(), Designation = "Anna" };
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Probe" };

            spiel.Moderator = moderator;
            spiel.ModeratorPlayerId = moderator.Id;

            spiel.PlayerXGames.Add(new PlayerXGame
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                PlayerId = mitspieler.Id,
                Player = mitspieler,
            });

            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                QuestionBaseId = Guid.NewGuid(),
            });

            return spiel;
        }

        private string LetzterHinweis => prompt.Informs[^1].Message;

        /// <summary>
        /// Der Ausgangspunkt: ein vollständiges Spiel startet, und es wird nichts gemeldet.
        /// <para>
        /// <b>Ohne diese Zusicherung sagen die drei darunter nichts</b> - eine Prüfung, die
        /// immer „nein" sagt, erfüllt sie alle.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ACompleteGameStarts()
        {
            Assert.IsTrue(GameMasterViewModel.Startbar(Startbereit()));

            Assert.AreEqual(0, prompt.Informs.Count,
                "Es wurde gemeldet, obwohl alles da ist: " + string.Join(" | ",
                    prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>Ohne Spielleiter geht nichts - er bedient den ganzen Abend.</summary>
        [TestMethod]
        public void WithoutAModeratorItDoesNotStart()
        {
            var spiel = Startbereit();

            spiel.Moderator = null;
            spiel.ModeratorPlayerId = null;

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel));
            StringAssert.Contains(LetzterHinweis, "Moderator");
        }

        /// <summary>
        /// <b>Die Kennung allein genügt nicht.</b> Steht sie da, ohne dass der Spieler geladen
        /// wurde, fehlt der Moderator trotzdem - genau der Fall, den eine Include-Kette
        /// verursacht, die jemand vergisst.
        /// </summary>
        [TestMethod]
        public void AnIdWithoutTheLoadedPlayerIsNotEnough()
        {
            var spiel = Startbereit();

            spiel.Moderator = null;

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel),
                "Eine Kennung ohne geladenen Spieler laesst das Spiel starten - im Fenster "
                + "steht dann kein Name.");
        }

        /// <summary>Ohne Mitspieler gäbe es niemanden, der buzzert.</summary>
        [TestMethod]
        public void WithoutPlayersItDoesNotStart()
        {
            var spiel = Startbereit();

            spiel.PlayerXGames.Clear();

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel));
            StringAssert.Contains(LetzterHinweis, "Mitspieler");
        }

        /// <summary>
        /// <b>Zellen ohne Frage zählen nicht.</b> Der Rasteraufbau legt für jede Position eine
        /// Zeile an; gegen deren Zahl geprüft feuerte diese Stelle nie.
        /// </summary>
        [TestMethod]
        public void EmptyCellsDoNotCountAsQuestions()
        {
            var spiel = Startbereit();

            foreach (var zelle in spiel.GameGridCoordinates)
                zelle.QuestionBaseId = null;

            Assert.IsTrue(spiel.GameGridCoordinates.Count > 0,
                "Die Probe braucht Zellen - sonst misst sie die leere Liste statt der Regel.");

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel),
                "Ein Spielfeld ohne eine einzige zugewiesene Frage darf nicht starten.");

            StringAssert.Contains(LetzterHinweis, "keine Frage zugewiesen");
        }

        /// <summary>
        /// Fehlen mehrere Voraussetzungen, werden sie <b>zusammen</b> gemeldet - sonst schickt
        /// das Programm den Spielleiter dreimal hintereinander in den Spielaufbau.
        /// </summary>
        [TestMethod]
        public void AllMissingThingsAreNamedAtOnce()
        {
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Leer" };

            Assert.IsFalse(GameMasterViewModel.Startbar(spiel));

            var text = LetzterHinweis;

            StringAssert.Contains(text, "Moderator");
            StringAssert.Contains(text, "Mitspieler");
            StringAssert.Contains(text, "keine Frage zugewiesen");
        }
    }
}
