using QRCoder;
using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Enumerations;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Server
{
    /// <summary>
    /// Zwei-Faktor-Anmeldung einrichten oder abschalten (M36 A5). Fuer Admins Pflicht - ohne sie
    /// laesst der Server einen Admin nichts tun ausser der Einrichtung.
    /// </summary>
    public sealed class TwoFactorViewModel : ViewModelBase
    {
        public enum Stage { Loading, Setup, Codes, Disable, Required }

        private Stage stage = Stage.Loading;
        private string message = string.Empty;
        private string code = string.Empty;
        private string secret = string.Empty;
        private string recoveryCodes = string.Empty;
        private ImageSource? qrImage;
        private bool isAdmin;

        /// <summary>Ob in diesem Dialog eingerichtet wurde - die Anmeldung fragt danach.</summary>
        public bool Activated { get; private set; }

        /// <summary>Kommt aus dem Code-Behind; eine PasswordBox laesst sich nicht binden.</summary>
        public string Password { get; set; } = string.Empty;

        public Stage Current
        {
            get => stage;
            private set
            {
                stage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Intro));
                OnPropertyChanged(nameof(SetupVisibility));
                OnPropertyChanged(nameof(CodesVisibility));
                OnPropertyChanged(nameof(DisableVisibility));
                OnPropertyChanged(nameof(CodeInputVisibility));
                OnPropertyChanged(nameof(CloseText));
            }
        }

        public string Intro => stage switch
        {
            Stage.Setup when isAdmin => "Für Admin-Konten ist die Zwei-Faktor-Anmeldung Pflicht. Bis sie eingerichtet ist, lässt der Server nichts anderes zu.",
            Stage.Setup => "Mit der Zwei-Faktor-Anmeldung braucht man zum Anmelden außer dem Kennwort einen Code vom Handy.",
            Stage.Codes => "Eingerichtet. Schreib diese Wiederherstellungscodes jetzt auf und bewahre sie getrennt vom Handy auf. "
                           + "Jeder gilt genau einmal, falls das Handy verloren geht. Sie werden nie wieder angezeigt.",
            Stage.Disable => "Die Zwei-Faktor-Anmeldung ist eingeschaltet. Zum Abschalten Kennwort und einen aktuellen Code eingeben.",
            Stage.Required => "Die Zwei-Faktor-Anmeldung ist eingerichtet. Für Admin-Konten ist sie Pflicht und lässt sich nicht abschalten. "
                              + "Geht das Handy verloren: mit einem Wiederherstellungscode anmelden, oder ein anderer Admin setzt sie zurück.",
            _ => "Wird geladen …",
        };

        public Visibility SetupVisibility => stage == Stage.Setup ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CodesVisibility => stage == Stage.Codes ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DisableVisibility => stage == Stage.Disable ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CodeInputVisibility => stage is Stage.Setup or Stage.Disable ? Visibility.Visible : Visibility.Collapsed;

        public string CloseText => stage == Stage.Codes ? "Codes aufgeschrieben" : "Schließen";

        public string Code
        {
            get => code;
            set { code = value; Message = string.Empty; OnPropertyChanged(); }
        }

        public string Secret
        {
            get => secret;
            private set { secret = value; OnPropertyChanged(); }
        }

        public ImageSource? QrImage
        {
            get => qrImage;
            private set { qrImage = value; OnPropertyChanged(); }
        }

        public string RecoveryCodes
        {
            get => recoveryCodes;
            private set { recoveryCodes = value; OnPropertyChanged(); }
        }

        public string Message
        {
            get => message;
            private set { message = value; OnPropertyChanged(); }
        }

        protected override async Task OnloadAsync()
        {
            if (ServerSession.Api is not { Account: { } account } api)
            {
                Message = "Nicht am Server angemeldet.";
                return;
            }

            isAdmin = account.Role == AccountRole.Admin;

            if (account.TotpEnabled)
            {
                Current = isAdmin ? Stage.Required : Stage.Disable;
                return;
            }

            try
            {
                var setup = await api.BeginTotpSetupAsync();
                Secret = InGroups(setup.Secret);
                QrImage = CreateQr(setup.OtpAuthUri);
                Current = Stage.Setup;
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private AsyncRelayCommand? confirmCommand;

        public ICommand ConfirmCommand => confirmCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Api is not { } api)
                return;

            if (string.IsNullOrWhiteSpace(Code))
            {
                Message = "Bitte den sechsstelligen Code aus der App eingeben.";
                return;
            }

            try
            {
                var codes = await api.ConfirmTotpAsync(Code);
                RecoveryCodes = string.Join(Environment.NewLine, codes);
                Activated = true;
                Code = string.Empty;
                Current = Stage.Codes;
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        });

        private AsyncRelayCommand? disableCommand;

        public ICommand DisableCommand => disableCommand ??= new AsyncRelayCommand(async _ =>
        {
            if (ServerSession.Api is not { } api)
                return;

            try
            {
                await api.DisableTotpAsync(Password, Code);
                UserPrompt.Inform("Die Zwei-Faktor-Anmeldung ist abgeschaltet. Angemeldet wird wieder nur mit dem Kennwort.", "Zwei-Faktor-Anmeldung");
                Window?.Close();
            }
            catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
            {
                Message = ex.Message;
            }
        });

        private RelayCommand? closeCommand;

        public ICommand CloseCommand => closeCommand ??= new RelayCommand(_ => Window?.Close());

        /// <summary>Zum Abtippen in Vierergruppen; die Apps ueberlesen die Leerzeichen.</summary>
        internal static string InGroups(string text)
            => string.Join(' ', Enumerable.Range(0, (text.Length + 3) / 4).Select(i => text.Substring(i * 4, Math.Min(4, text.Length - i * 4))));

        private static BitmapImage CreateQr(string payload)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            var bytes = new PngByteQRCode(data).GetGraphic(8);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
