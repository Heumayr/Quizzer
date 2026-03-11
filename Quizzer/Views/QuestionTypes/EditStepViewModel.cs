using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes;

public class EditStepViewModel : ViewModelBase
{
    public Array ResourceTyps { get; } = Enum.GetValues(typeof(ResourceType));

    public QuestionStepResource? CmbSelectedStep { get; set; }

    public ObservableCollection<StepXStep> SelectedStepXSteps { get; set; } = new();

    public ObservableCollection<StepXStep> Froms => new ObservableCollection<StepXStep>(Step?.Froms ?? []);

    public QuestionStepResource[] CmbSource
    {
        get => cmbSource;
        set
        {
            cmbSource = value;
            OnPropertyChanged();
        }
    }

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
                OnPropertyChanged(nameof(Froms));
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
        Step = m;

        if (Step == null) throw new Exception("Not able to load model");

        var all = await ctrl.GetAllStepsOfQuestionExceptMe(m);

        var addedIds = Step.Froms.Select(x => x.ToId).ToList();

        CmbSource = all.Where(s => !addedIds.Contains(s.Id)).ToArray();
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
    private QuestionStepResource[] cmbSource = [];

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

    private AsyncRelayCommand? addStepCommnad;
    public ICommand AddStepCommnad => addStepCommnad ??= new AsyncRelayCommand(AddStepCommnadAsync);

    private async Task AddStepCommnadAsync(object? commandParameter)
    {
        if (Step == null || CmbSelectedStep == null)
        {
            return;
        }

        using var ctrl = new StepXStepsController();

        await ctrl.UpsertAsync(new StepXStep()
        {
            FromId = Step.Id,
            ToId = CmbSelectedStep.Id
        });
        await ctrl.SaveChangesAsync();
        await SetModel(Step);
    }

    private AsyncRelayCommand? removeStepCommnad;
    public ICommand RemoveStepCommnad => removeStepCommnad ??= new AsyncRelayCommand(RemoveStepCommnadAsync);

    private async Task RemoveStepCommnadAsync(object? commandParameter)
    {
        if (Step == null)
        {
            throw new InvalidOperationException("Question is null");
        }

        using var ctrl = new StepXStepsController();

        foreach (var x in SelectedStepXSteps)
        {
            await ctrl.DeleteAsync(x.Id);
        }

        await ctrl.SaveChangesAsync();
        await SetModel(Step);
    }
}