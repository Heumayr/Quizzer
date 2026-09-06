using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Anmeldung am Anfang: wer leitet heute das Quiz?
    /// <para>
    /// Die Wahl entscheidet, welche Fragen zur Verfügung stehen - jeder sieht seine eigenen und
    /// den gemeinsamen Bestand. Deshalb steht sie am Anfang und nicht irgendwo in den
    /// Einstellungen.
    /// </para>
    /// <para>
    /// <b>Ein Kennwort ist vorbereitet, aber nicht verlangt.</b> Das Feld erscheint nur bei
    /// jemandem, der eines gesetzt hat; sonst genügt die Auswahl.
    /// </para>
    /// </summary>
    public class LoginViewModel : ViewModelBase
    {
        private ObservableCollection<Player> moderatoren = new();
        private Player? gewaehlt;
        private string kennwort = string.Empty;
        private string fehler = string.Empty;

        /// <summary>Ob die Anmeldung gelungen ist. Der Aufrufer liest das nach dem Schließen.</summary>
        public bool SignedIn { get; private set; }

        private bool istWechsel;

        /// <summary>
        /// Ob das Fenster als Wechsel im laufenden Betrieb geöffnet wurde statt als Anmeldung
        /// beim Start.
        /// <para>
        /// Es ändert nur zwei Beschriftungen. „Beenden" wäre beim Wechsel schlicht falsch: das
        /// Programm läuft weiter, wenn man abbricht, und die bisherige Anmeldung bleibt stehen.
        /// </para>
        /// </summary>
        public bool IstWechsel
        {
            get => istWechsel;
            set
            {
                istWechsel = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(Untertitel));
                OnPropertyChanged(nameof(AbbruchText));
            }
        }

        /// <summary>Die Zeile unter dem Titel.</summary>
        public string Untertitel =>
            IstWechsel ? "Wer übernimmt die Spielleitung?" : "Wer leitet heute das Quiz?";

        /// <summary>Was auf dem linken Knopf steht.</summary>
        public string AbbruchText => IstWechsel ? "Abbrechen" : "Beenden";

        /// <summary>Alle, die das Quiz leiten dürfen.</summary>
        public ObservableCollection<Player> Moderators
        {
            get => moderatoren;
            private set
            {
                moderatoren = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasModerators));
                OnPropertyChanged(nameof(EmptyHintVisibility));
            }
        }

        public bool HasModerators => Moderators.Count > 0;

        /// <summary>
        /// Der Hinweis, wenn noch niemand als Spielleiter angelegt ist. Ohne ihn stünde die
        /// Anmeldung leer da und niemand wüsste, was zu tun ist.
        /// </summary>
        public Visibility EmptyHintVisibility =>
            HasModerators ? Visibility.Collapsed : Visibility.Visible;

        public Player? SelectedModerator
        {
            get => gewaehlt;
            set
            {
                gewaehlt = value;
                ErrorText = string.Empty;

                OnPropertyChanged();
                OnPropertyChanged(nameof(PasswordVisibility));

                signInCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Das Kennwortfeld erscheint nur, wo eines gesetzt ist.</summary>
        public Visibility PasswordVisibility =>
            Session.RequiresPassword(SelectedModerator) ? Visibility.Visible : Visibility.Collapsed;

        public string Password
        {
            get => kennwort;
            set
            {
                kennwort = value;
                ErrorText = string.Empty;
                OnPropertyChanged();
            }
        }

        public string ErrorText
        {
            get => fehler;
            private set
            {
                fehler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }

        public Visibility ErrorVisibility =>
            string.IsNullOrEmpty(ErrorText) ? Visibility.Collapsed : Visibility.Visible;

        protected override async Task OnloadAsync()
        {
            using var ctrl = new PlayersController();

            var alle = await ctrl.GetAllAsync();

            Moderators = new ObservableCollection<Player>(
                alle.Where(p => p.IsModerator)
                    .OrderBy(p => p.CalculatedDisplayName, StringComparer.CurrentCultureIgnoreCase));

            // Beim Wechsel steht der bisher Angemeldete vorgewaehlt - sonst waehlt das Fenster
            // stillschweigend jemand anderen aus, nur weil er alphabetisch vorn steht.
            SelectedModerator =
                Moderators.FirstOrDefault(m => m.Id == Session.CurrentModeratorId)
                ?? Moderators.FirstOrDefault();
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private RelayCommand? signInCommand;

        public ICommand SignInCommand =>
            signInCommand ??= new RelayCommand(SignIn, _ => SelectedModerator != null);

        private void SignIn(object? _)
        {
            if (SelectedModerator == null)
                return;

            if (!Session.SignIn(SelectedModerator, Password))
            {
                ErrorText = "Das Kennwort stimmt nicht.";
                return;
            }

            SignedIn = true;

            Window?.Close();
        }

        private RelayCommand? cancelCommand;

        /// <summary>Abbrechen heißt: das Programm nicht starten. Ohne Anmeldung gibt es nichts zu tun.</summary>
        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            SignedIn = false;
            Window?.Close();
        });
    }
}
