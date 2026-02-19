using Quizzer.Base;
using Quizzer.Controller;
using Quizzer.Controller.TypedHelper;
using Quizzer.Datamodels;
using Quizzer.Datamodels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class EditStepViewModel : ViewModelBase
    {
        public Array ResourceTyps { get; } = Enum.GetValues(typeof(ResourceTyp));

        public EditResultState ResultState { get; set; } = EditResultState.Cancelled;

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

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}