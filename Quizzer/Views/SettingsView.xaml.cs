using Quizzer.Base;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Einstellungsmaske. Auch vor dem Hauptfenster erreichbar - siehe
    /// <see cref="SettingsViewModel"/>.
    /// </summary>
    public partial class SettingsView : WindowBase
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        /// <summary>Ob gespeichert wurde.</summary>
        public bool Saved => (DataContext as SettingsViewModel)?.Saved == true;
    }
}
