using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Logic.Controller.TypedControllers;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Umwandeln einer Frage in einen anderen Fragetyp.
    /// <para>
    /// Der Typ war bisher unumkehrbar: wer mitten im Schreiben merkte, dass die Frage besser
    /// Multiple Choice waere, musste sie loeschen und samt allen Schritten neu anlegen.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Umwandeln geht erst, wenn die Frage schon gespeichert ist.</summary>
        public bool CanConvert => Question != null && Question.Id != Guid.Empty;

        private AsyncRelayCommand? convertTypeCommand;

        public ICommand ConvertTypeCommand
            => convertTypeCommand ??= new AsyncRelayCommand(ConvertTypeAsync, _ => CanConvert);

        private async Task ConvertTypeAsync(object? commandParameter)
        {
            if (Question == null || Question.Id == Guid.Empty)
                return;

            var target = AskForTargetType(Question.Typ);

            if (target == null || target == Question.Typ)
                return;

            var effects = QuestionBasesController.DescribeConversionEffects(Question.Typ, target.Value);

            var message = "Frage umwandeln?\n\n" + string.Join("\n", effects.Select(e => "- " + e));

            if (!UserPrompt.Confirm(message, "Fragetyp ändern"))
                return;

            // Vor dem Umwandeln sichern, sonst gehen offene Aenderungen an der Maske verloren.
            await VMSaveAsync();

            using (var ctrl = new QuestionBasesController())
            {
                await ctrl.ConvertTypeAsync(Question.Id, target.Value);
                await ctrl.SaveChangesAsync();
            }

            await LoadModel(Question);
            Revalidate();
        }

        /// <summary>
        /// Fragt den Zieltyp ab. Der aktuelle Typ wird dabei ausgelassen - ihn zu waehlen waere
        /// wirkungslos.
        /// </summary>
        private static QuestionType? AskForTargetType(QuestionType currentType)
        {
            var window = new NewQuestionView
            {
                Owner = Application.Current?.MainWindow,
                Title = "In anderen Typ umwandeln",
            };

            if (window.ViewModel is { } vm)
            {
                var current = vm.Profiles.FirstOrDefault(p => p.Typ == currentType);

                if (current != null)
                    vm.Profiles.Remove(current);
            }

            window.ShowDialog();

            return window.ViewModel?.ChosenType;
        }
    }
}
