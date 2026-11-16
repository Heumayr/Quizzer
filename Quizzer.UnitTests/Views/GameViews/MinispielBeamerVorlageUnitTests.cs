using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.QuestionViews;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Welche Beamer-Ansicht ein Minispiel bekommt (M35 C1): Arten im Telefonrahmen die Tafel, die
    /// Mehrheit ihre Optionen. Sonst stuende beim Tippsprint ein leeres Optionsraster am Beamer.
    /// </summary>
    [TestClass]
    public class MinispielBeamerVorlageUnitTests
    {
        private static readonly string[] Vorlagen = ["DefaultStepTemplate", "MultipleChoiceStepTemplate", "ReaktionStepTemplate"];

        [TestMethod]
        [DataRow(MinigameKind.Majority, "MultipleChoiceStepTemplate")]
        [DataRow(MinigameKind.Reaction, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.TapSprint, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.Stopwatch, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.Duel, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.RobberAndGuard, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.ShareOrTake, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.Crash, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.WheelOfFortune, "ReaktionStepTemplate")]
        [DataRow(MinigameKind.MentalArithmetic, "ReaktionStepTemplate")]
        public void EachKindGetsItsBeamerView(MinigameKind art, string erwartet)
        {
            UiTestHost.Run(() =>
            {
                var host = new ContentControl();
                foreach (var name in Vorlagen)
                    host.Resources[name] = new DataTemplate();

                var frage = (MinigameQuestion)Factory.CreateNewQuestion(QuestionType.Minigame);
                MinigameProfiles.For(art).ApplyDefaults(frage);

                var ctx = new QuestionStepViewContext
                {
                    Owner = new CurrentQuestionViewModel { Coordinate = new GameGridCoordinate { QuestionBase = frage } },
                    Step = new QuestionStepResource { Id = Guid.NewGuid() },
                };

                var gewaehlt = new QuestionStepTemplateSelector().SelectTemplate(ctx, host);

                Assert.AreSame(host.Resources[erwartet], gewaehlt,
                    $"{art} bekommt am Beamer nicht {erwartet}.");
            });
        }
    }
}
