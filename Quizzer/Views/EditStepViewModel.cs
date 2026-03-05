using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class EditStepViewModel : ViewModelBase
    {
        public Array ResourceTyps { get; } = Enum.GetValues(typeof(ResourceTyp));

        private QuestionStepResource? _step;

        public QuestionStepResource? Step
        {
            get => _step;
            set
            {
                if (!Equals(_step, value))
                {
                    _step = value;
                    OnModelChanged();
                }
            }
        }

        public async Task SetModel(QuestionStepResource? step)
        {
            ResultState = EditResultState.Canceled;

            if (step == null)
                step = new QuestionStepResource();

            if (step.Id == Guid.Empty)
            {
                Step = step;
                return;
            }

            using var ctrl = new QuestionStepResourcesController();
            var m = await ctrl.GetAsync(step.Id);
            Step = step;
        }

        protected override Task OnloadAsync()
        {
            return Task.CompletedTask;
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(Step));
        }

        private RelayCommand? closeCommand;

        public ICommand CloseCommand => closeCommand ??= new RelayCommand(Close);

        private void Close(object? commandParameter)
        {
            Window?.Close();
        }

        public override async Task VMSaveAsync()
        {
            if (Step == null) return;

            using var ctrl = new QuestionStepResourcesController();
            var result = await ctrl.UpsertAsync(Step);
            await ctrl.SaveChangesAsync();

            ResultState = result.Created ? EditResultState.New : EditResultState.Updated;
        }
    }
}