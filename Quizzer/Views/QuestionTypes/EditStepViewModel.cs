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

        var all = await ctrl.GetAllStepsOfQuestionExceptMe(Step);

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

        HandleSelectedResourceFile(dialog.FileName, rootFolder);

        return Task.CompletedTask;
    }

    private void HandleSelectedResourceFile(string sourceFilePath, string rootFolder)
    {
        if (Step == null)
            throw new InvalidOperationException("Step is null.");

        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Selected file does not exist.", sourceFilePath);

        Directory.CreateDirectory(rootFolder);

        var detectedType = DetectResourceType(sourceFilePath);

        string targetFileName;
        string targetFilePath;

        if (detectedType == ResourceType.Image)
        {
            targetFileName = CreateUniqueFileName(rootFolder, Path.GetFileNameWithoutExtension(sourceFilePath), ".png");
            targetFilePath = Path.Combine(rootFolder, targetFileName);

            SaveAsPng(sourceFilePath, targetFilePath);
        }
        else
        {
            var originalFileName = Path.GetFileName(sourceFilePath);
            targetFileName = CreateUniqueFileName(rootFolder, Path.GetFileNameWithoutExtension(originalFileName), Path.GetExtension(originalFileName));
            targetFilePath = Path.Combine(rootFolder, targetFileName);

            File.Copy(sourceFilePath, targetFilePath, overwrite: false);
        }

        Step.ResourceFileName = targetFileName;
        Step.ResourceTyp = detectedType;

        OnPropertyChanged(nameof(Step));
    }

    private void SaveAsPng(string sourceFilePath, string targetFilePath)
    {
        var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

        if (extension == ".webp")
        {
            SaveWebpAsPng(sourceFilePath, targetFilePath);
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(sourceFilePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(targetFilePath);
        encoder.Save(stream);
    }

    private void SaveWebpAsPng(string sourceFilePath, string targetFilePath)
    {
        using var input = File.OpenRead(sourceFilePath);
        using var codec = SKCodec.Create(input);

        if (codec == null)
            throw new InvalidOperationException("WEBP konnte nicht gelesen werden.");

        var info = codec.Info
            .WithColorType(SKColorType.Rgba8888)
            .WithAlphaType(SKAlphaType.Unpremul);

        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());

        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            throw new InvalidOperationException($"WEBP konnte nicht dekodiert werden: {result}");

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        using var output = File.OpenWrite(targetFilePath);
        data.SaveTo(output);
    }

    private ResourceType DetectResourceType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp"
                => ResourceType.Image,

            ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv"
                => ResourceType.Video,

            ".mp3" or ".wav" or ".ogg" or ".flac"
                => ResourceType.Audio,

            ".pdf" or ".doc" or ".docx" or ".txt"
                => ResourceType.Document,

            _ => throw new NotSupportedException($"Dateityp '{extension}' wird nicht unterstützt.")
        };
    }

    private string CreateUniqueFileName(string targetFolder, string fileNameWithoutExtension, string extension)
    {
        return $"{fileNameWithoutExtension}_{Guid.NewGuid()}{extension}";
    }
}