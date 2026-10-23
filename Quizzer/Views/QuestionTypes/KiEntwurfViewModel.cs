using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Transfer.Ki;
using Quizzer.Server;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// „Entwurf mit KI" (M31, 2026-10-23): Thema, Links, Text oder Bilder hinein, Entwürfe
    /// heraus. Gefragt wird die KI, die der Admin am Server eingerichtet hat - dieses Programm
    /// kennt weder Anbieter noch Schlüssel.
    /// </summary>
    public sealed class KiEntwurfViewModel : ViewModelBase
    {
        /// <summary>Eine Zeile der Typauswahl - <c>null</c> heißt „die KI wählt".</summary>
        public sealed record TypWahl(QuestionType? Typ, string Name);

        /// <summary>Ein Entwurf in der Ergebnisliste, mit Haken zum Übernehmen.</summary>
        public sealed class Vorschlag(KiFrageEntwurf entwurf)
        {
            public KiFrageEntwurf Entwurf { get; } = entwurf;

            public bool Uebernehmen { get; set; } = true;

            public string Kopf => $"{QuestionTypeProfiles.For(Entwurf.Typ).DisplayName} · {Entwurf.Punkte} Punkte";

            public string Inhalt => string.Join("\n", Entwurf.Zeilen.Select(z =>
                (z.Richtig ? "✓ " : "· ") + (string.IsNullOrWhiteSpace(z.Partner) ? z.Text : $"{z.Text} ↔ {z.Partner}")))
                + (Entwurf.Sollwert is { } w ? $"\nSollwert: {w} {Entwurf.Einheit}" : string.Empty)
                + (Entwurf.SollDatum is { } d ? $"\nSolldatum: {d}" : string.Empty)
                + (string.IsNullOrWhiteSpace(Entwurf.Aufloesung) ? string.Empty : "\nAuflösung: " + Entwurf.Aufloesung);

            public string Quellen => Entwurf.Quellen.Count == 0 ? string.Empty : "Quellen: " + string.Join(", ", Entwurf.Quellen);
        }

        public ObservableCollection<TypWahl> Typen { get; } = new(
            new[] { new TypWahl(null, "passend wählen lassen") }
                .Concat(QuestionTypeProfiles.All.Select(p => new TypWahl(p.Typ, p.DisplayName))));

        public int[] Anzahlen { get; } = Enumerable.Range(1, KiEntwurfAnfrage.MaxAnzahl).ToArray();

        public Array Schwierigkeiten { get; } = Enum.GetValues(typeof(Difficulty));

        public ObservableCollection<Category> Kategorien { get; } = new();

        public ObservableCollection<KiBild> BildListe { get; } = new();

        public ObservableCollection<Vorschlag> Vorschlaege { get; } = new();

        public ObservableCollection<string> Hinweise { get; } = new();

        public string Thema { get; set; } = string.Empty;

        public TypWahl? Typ { get; set; }

        public int Anzahl { get; set; } = 1;

        public Difficulty Schwierigkeit { get; set; } = Difficulty.Level1;

        public Category? Kategorie { get; set; }

        public string Links { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public string Wuensche { get; set; } = string.Empty;

        /// <summary>Was übernommen wird - gefüllt erst mit „Übernehmen".</summary>
        public List<KiFrageEntwurf> Gewaehlt { get; } = new();

        /// <summary>Die mitgeschickten Bilder - ein Entwurf verweist mit seinem Index darauf.</summary>
        public IReadOnlyList<KiBild> Bilder { get; private set; } = [];

        private string status = "Frage den Server …";
        private bool verfuegbar;
        private bool arbeitet;
        private CancellationTokenSource? abbruch;

        public string Status
        {
            get => status;
            private set { status = value; OnPropertyChanged(); }
        }

        public bool Verfuegbar
        {
            get => verfuegbar;
            private set { verfuegbar = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public bool Arbeitet
        {
            get => arbeitet;
            private set
            {
                arbeitet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ArbeitetVisibility));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Visibility ArbeitetVisibility => Arbeitet ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Vom Editor: was die Frage dort schon hat, ist die Vorgabe.</summary>
        public void Vorgabe(QuestionType typ, Category? kategorie, IEnumerable<Category> kategorien, Difficulty schwierigkeit)
        {
            foreach (var k in kategorien)
                Kategorien.Add(k);

            Kategorie = kategorie == null ? null : Kategorien.FirstOrDefault(k => k.Id == kategorie.Id);

            // Eine frische Standardfrage ist meist noch gar nicht entschieden - dann wählt die KI.
            Typ = typ == QuestionType.Default ? Typen[0] : Typen.FirstOrDefault(t => t.Typ == typ) ?? Typen[0];
            Schwierigkeit = schwierigkeit;
        }

        protected override async Task OnloadAsync()
        {
            if (ServerSession.Api is not { } api)
            {
                Status = "Nur im Servermodus: die KI richtet der Admin am Server ein.";
                return;
            }

            try
            {
                var s = await api.GetKiStatusAsync();
                Verfuegbar = s.Verfuegbar;
                Status = s.Verfuegbar
                    ? $"{s.Beschreibung} · heute noch {s.VerbleibendHeute} Aufrufe" + (s.LinksAbrufen ? string.Empty : " · Links holt dieser Server nicht ab")
                    : s.Grund;
            }
            catch (Exception e) when (e is ApiException or ServerUnreachableException)
            {
                Status = e.Message;
            }
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        // ---- Bilder ----

        private RelayCommand? bildCommand;

        public ICommand BildCommand => bildCommand ??= new RelayCommand(_ =>
        {
            if (FilePicker.AskForExistingFile("Bild für die KI", "Bilder|*.png;*.jpg;*.jpeg;*.gif;*.webp") is { } datei)
                NimmBild(datei);
        });

        private RelayCommand<KiBild>? bildEntfernenCommand;

        public ICommand BildEntfernenCommand => bildEntfernenCommand ??= new RelayCommand<KiBild>(b =>
        {
            if (b != null)
                BildListe.Remove(b);
        });

        /// <summary>Aus Dateidialog, Ziehen oder Zwischenablage.</summary>
        public void NimmBild(string datei)
        {
            var typ = Path.GetExtension(datei).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => null,
            };

            if (typ == null)
            {
                Status = $"„{Path.GetFileName(datei)}“: nur PNG, JPEG, GIF oder WebP.";
                return;
            }

            NimmBild(Path.GetFileName(datei), typ, File.ReadAllBytes(datei));
        }

        public void NimmBild(string name, string mediaType, byte[] daten)
        {
            if (BildListe.Count >= KiEntwurfAnfrage.MaxBilder)
            {
                Status = $"Höchstens {KiEntwurfAnfrage.MaxBilder} Bilder.";
                return;
            }

            if (daten.Length > KiEntwurfAnfrage.MaxBildBytes)
            {
                Status = $"„{name}“ ist größer als 5 MB.";
                return;
            }

            BildListe.Add(new KiBild { Name = name, MediaType = mediaType, Base64 = Convert.ToBase64String(daten) });
        }

        // ---- Erzeugen, Übernehmen, Abbrechen ----

        private AsyncRelayCommand? erzeugeCommand;

        public ICommand ErzeugeCommand => erzeugeCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Api is not { } api)
                return;

            var anfrage = new KiEntwurfAnfrage
            {
                Thema = Thema.Trim(),
                Typ = Typ?.Typ,
                Anzahl = Anzahl,
                Schwierigkeit = Schwierigkeit,
                Links = Links.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Text = string.IsNullOrWhiteSpace(Text) ? null : Text,
                Bilder = [.. BildListe],
                Wuensche = string.IsNullOrWhiteSpace(Wuensche) ? null : Wuensche.Trim(),
            };

            abbruch = new CancellationTokenSource();
            Arbeitet = true;
            Status = "Die KI arbeitet - das dauert meist 20 Sekunden bis zwei Minuten …";

            try
            {
                var antwort = await api.PostKiEntwurfAsync(anfrage, abbruch.Token);

                Vorschlaege.Clear();
                foreach (var e in antwort.Entwuerfe)
                    Vorschlaege.Add(new Vorschlag(e));

                Hinweise.Clear();
                foreach (var h in antwort.Hinweise)
                    Hinweise.Add(h);

                Bilder = anfrage.Bilder;
                Status = antwort.Entwuerfe.Count == 0
                    ? "Die KI hat keinen Entwurf geliefert. Etwas mehr Stoff hilft meistens."
                    : $"{antwort.Entwuerfe.Count} Entwurf/Entwürfe · heute noch {antwort.VerbleibendHeute} Aufrufe. Fakten bitte prüfen.";
            }
            catch (OperationCanceledException)
            {
                Status = "Abgebrochen.";
            }
            catch (Exception e) when (e is ApiException or ServerUnreachableException)
            {
                Status = e.Message;
            }
            finally
            {
                Arbeitet = false;
                abbruch.Dispose();
                abbruch = null;
            }
        }, _ => Verfuegbar && !Arbeitet);

        private RelayCommand? uebernehmeCommand;

        public ICommand UebernehmeCommand => uebernehmeCommand ??= new RelayCommand(_ =>
        {
            Gewaehlt.Clear();
            Gewaehlt.AddRange(Vorschlaege.Where(v => v.Uebernehmen).Select(v => v.Entwurf));
            Window?.Close();
        }, _ => !Arbeitet && Vorschlaege.Count > 0);

        private RelayCommand? abbrechenCommand;

        public ICommand AbbrechenCommand => abbrechenCommand ??= new RelayCommand(_ =>
        {
            if (abbruch != null)
            {
                abbruch.Cancel();
                return;
            }

            Gewaehlt.Clear();
            Window?.Close();
        });
    }
}
