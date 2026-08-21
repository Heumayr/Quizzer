using Microsoft.Win32;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.QuestionTypes;

public class EditStepViewModel : ViewModelBase
{
    public Array ResourceTyps { get; } = Enum.GetValues(typeof(ResourceType));

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

        if (step.Id == Guid.Empty || !PersistDirectly)
        {
            Step = step;
            return;
        }

        using var ctrl = new QuestionStepResourcesController();
        var m = await ctrl.GetAsync(step.Id);
        Step = m;

        if (Step == null) throw new Exception("Not able to load model");
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

    /// <summary>
    /// Ob der Schritt beim Speichern selbst in die Datenbank geschrieben wird.
    /// <para>
    /// Wird der Dialog aus dem Frage-Editor geoeffnet, steht das auf <c>false</c>: die Schritte
    /// gehoeren dann zur Frage und werden mit ihr zusammen ueber
    /// <c>QuestionBasesController.SaveWithStepsAsync</c> geschrieben. Frueher schrieb der
    /// Schritt-Dialog immer sofort - und weil dafuer die Frage schon existieren musste, hat der
    /// Frage-Editor sie vor jedem Schritt still vorab gespeichert.
    /// </para>
    /// </summary>
    public bool PersistDirectly { get; set; } = true;

    public override async Task VMSaveAsync()
    {
        if (Step == null) return;

        if (!PersistDirectly)
        {
            ResultState = EditResultState.Updated;
            return;
        }

        using var ctrl = new QuestionStepResourcesController();
        var result = await ctrl.UpsertAsync(Step);
        await ctrl.SaveChangesAsync();

        ResultState = result.Created ? EditResultState.New : EditResultState.Updated;
    }


    private AsyncRelayCommand? saveCommand;
    public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

    private async Task SaveAsync(object? commandParameter)
    {
        await VMSaveAsync();
    }

    private AsyncRelayCommand? saveAndCloseCommand;
    public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

    private async Task SaveAndCloseAsync(object? commandParameter)
    {
        await VMSaveAsync();
        Window?.Close();
    }

    private AsyncRelayCommand? selectResourceCommnad;
    public ICommand SelectResourceCommnad => selectResourceCommnad ??= new AsyncRelayCommand(PerformSelectResourceCommnadAsync);

    private Task PerformSelectResourceCommnadAsync(object? commandParameter)
    {
        if (Step == null)
            return Task.CompletedTask;

        var rootFolder = Settings.ResourceRootFolder;

        if (string.IsNullOrWhiteSpace(rootFolder))
            throw new InvalidOperationException("Root folder was not provided.");

        var dialog = new OpenFileDialog
        {
            Title = "Ressource auswählen",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Filter =
                "Alle unterstützten Dateien|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.ogg;*.flac;*.pdf;*.doc;*.docx;*.txt|" +
                "Bilder|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|" +
                "Videos|*.mp4;*.avi;*.mov;*.wmv;*.mkv|" +
                "Audio|*.mp3;*.wav;*.ogg;*.flac|" +
                "Dokumente|*.pdf;*.doc;*.docx;*.txt|" +
                "Alle Dateien|*.*"
        };

        var result = dialog.ShowDialog();

        if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
            return Task.CompletedTask;

        var file = FileHelper.HandleSelectedResourceFile(dialog.FileName, rootFolder);

        Step.ResourceFileName = file.Filename;
        Step.ResourceTyp = file.Type;

        OnPropertyChanged(nameof(Step));

        return Task.CompletedTask;
    }
}