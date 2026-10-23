using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Transfer.Ki;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Server
{
    /// <summary>
    /// „KI einrichten" für den Admin (M31, 2026-10-23): welcher Anbieter, welches Modell, der
    /// Schlüssel, das Tageslimit - und wer sie benutzen darf.
    /// <para>
    /// <b>Der Schlüssel kommt nie zurück.</b> Das Feld bleibt leer; „gesetzt" sagt nur, dass
    /// einer hinterlegt ist. Ein leeres Feld beim Speichern lässt ihn, wie er ist.
    /// </para>
    /// </summary>
    public sealed class KiEinstellungenViewModel : ViewModelBase
    {
        public sealed record AnbieterWahl(KiAnbieter Wert, string Name, string Hilfe);

        /// <summary>Ein Konto mit Haken „darf KI-Entwürfe".</summary>
        public sealed class Freigabe(Guid konto, string name, bool istAdmin, bool frei)
        {
            public Guid Konto { get; } = konto;

            public string Name { get; } = name;

            public bool IstAdmin { get; } = istAdmin;

            /// <summary>Ein Admin darf immer - sein Haken ist gesetzt und gesperrt.</summary>
            public bool Frei { get; set; } = istAdmin || frei;

            public bool Aenderbar => !IstAdmin;
        }

        public ObservableCollection<AnbieterWahl> AnbieterListe { get; } =
        [
            new(KiAnbieter.Aus, "Aus", "Niemand bekommt KI-Entwürfe."),
            new(KiAnbieter.Claude, "Claude (Anthropic)", "Adresse leer lassen. Modell etwa claude-opus-5-5. Der API-Schlüssel kommt aus der Claude Console."),
            new(KiAnbieter.OpenAiKompatibel, "OpenAI-kompatibel (OpenAI, Ollama, LM Studio, vLLM)",
                "Adresse bis einschließlich /v1, etwa http://ollama:11434/v1 im selben Docker-Netz oder https://api.openai.com/v1. Schlüssel nur, wenn der Dienst einen verlangt."),
        ];

        public ObservableCollection<Freigabe> Konten { get; } = new();

        private AnbieterWahl? anbieter;
        private string message = string.Empty;
        private bool schluesselGesetzt;

        public AnbieterWahl? Anbieter
        {
            get => anbieter;
            set
            {
                anbieter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Hilfe));
                OnPropertyChanged(nameof(AdresseVisibility));
            }
        }

        public string Hilfe => Anbieter?.Hilfe ?? string.Empty;

        public Visibility AdresseVisibility => Anbieter?.Wert == KiAnbieter.Aus ? Visibility.Collapsed : Visibility.Visible;

        public string Adresse { get; set; } = string.Empty;

        public string Modell { get; set; } = string.Empty;

        /// <summary>Aus dem Kennwortfeld - leer heißt „unverändert".</summary>
        public string NeuerSchluessel { get; set; } = string.Empty;

        public bool SchluesselLoeschen { get; set; }

        public string Tageslimit { get; set; } = "20";

        public bool LinksAbrufen { get; set; } = true;

        public string SchluesselStand => schluesselGesetzt ? "Ein Schlüssel ist hinterlegt." : "Kein Schlüssel hinterlegt.";

        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        protected override async Task OnloadAsync()
        {
            if (ServerSession.Api is not { } api)
                return;

            try
            {
                var e = await api.GetKiEinstellungenAsync();
                var konten = await api.GetAccountsAsync();

                Anbieter = AnbieterListe.FirstOrDefault(a => a.Wert == e.Anbieter) ?? AnbieterListe[0];
                Adresse = e.Adresse ?? string.Empty;
                Modell = e.Modell;
                Tageslimit = e.Tageslimit.ToString();
                LinksAbrufen = e.LinksAbrufen;
                schluesselGesetzt = e.SchluesselGesetzt;

                Konten.Clear();
                foreach (var k in konten.Where(k => !k.IsDisabled).OrderBy(k => k.UserName))
                    Konten.Add(new Freigabe(k.AccountId, $"{k.UserName} ({k.PlayerName})", k.Role == AccountRole.Admin, e.FreigegebeneKonten.Contains(k.AccountId)));

                OnPropertyChanged(nameof(Adresse));
                OnPropertyChanged(nameof(Modell));
                OnPropertyChanged(nameof(Tageslimit));
                OnPropertyChanged(nameof(LinksAbrufen));
                OnPropertyChanged(nameof(SchluesselStand));
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private async Task<bool> SpeichereAsync()
        {
            if (ServerSession.Api is not { } api || Anbieter == null)
                return false;

            if (!int.TryParse(Tageslimit, out var limit))
            {
                Message = "Das Tageslimit ist keine Zahl.";
                return false;
            }

            if (Anbieter.Wert == KiAnbieter.Claude && string.IsNullOrWhiteSpace(Modell))
                Modell = "claude-opus-5-5";

            try
            {
                await api.PutKiEinstellungenAsync(new KiEinstellungenDto
                {
                    Anbieter = Anbieter.Wert,
                    Adresse = string.IsNullOrWhiteSpace(Adresse) ? null : Adresse.Trim(),
                    Modell = Modell.Trim(),
                    NeuerSchluessel = SchluesselLoeschen ? string.Empty : (string.IsNullOrWhiteSpace(NeuerSchluessel) ? null : NeuerSchluessel.Trim()),
                    Tageslimit = limit,
                    LinksAbrufen = LinksAbrufen,
                    FreigegebeneKonten = Konten.Where(k => k.Frei && !k.IstAdmin).Select(k => k.Konto).ToList(),
                });

                schluesselGesetzt = SchluesselLoeschen ? false : schluesselGesetzt || !string.IsNullOrWhiteSpace(NeuerSchluessel);
                OnPropertyChanged(nameof(SchluesselStand));
                OnPropertyChanged(nameof(Modell));
                Message = "Gespeichert.";
                return true;
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
                return false;
            }
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(async _ => await SpeichereAsync());

        private AsyncRelayCommand? probeCommand;

        /// <summary>Speichert und schickt eine kleine Anfrage - zählt nicht aufs Tageslimit.</summary>
        public ICommand ProbeCommand => probeCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (!await SpeichereAsync() || ServerSession.Api is not { } api)
                return;

            Message = "Gespeichert. Die KI wird gefragt …";

            try
            {
                await api.PostKiProbeAsync();
                Message = "Gespeichert - die KI antwortet und liefert einen Entwurf.";
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = "Gespeichert, aber die Probe scheiterte: " + ex.Message;
            }
        });
    }
}
