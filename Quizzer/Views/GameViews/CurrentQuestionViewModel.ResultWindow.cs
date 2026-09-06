using Quizzer.Views.GameViews.QuestionViews;
using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Fensterteil von <see cref="CurrentQuestionViewModel"/>. Getrennt gehalten, damit der
    /// Spielablauf selbst frei von Fensterarbeit bleibt - und damit ein Test genau diesen Teil
    /// stilllegen kann, ohne den Ablauf anzufassen.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private PlayersResultView? _resultWindow;

        /// <summary>
        /// Oeffnet das Ergebnisfenster oder holt es nach vorn. Ueberschreibbar, damit ein Test den
        /// Spielablauf durchlaufen kann, ohne dass ein Fenster aufgeht.
        /// </summary>
        protected virtual void ShowResultWindow()
        {
            if (_resultWindow == null)
            {
                _resultWindow = new PlayersResultView
                {
                    DataContext = PlayersResultViewModel
                };

                _resultWindow.Closed += async (_, _) =>
                {
                    if (PlayersResultViewModel?.IsDoneAndShowFinishState ?? false)
                    {
                        await SaveIsDoneFinishStateAsync(null);
                    }

                    _resultWindow = null;
                };

                _resultWindow.Show();

                return;
            }

            if (_resultWindow.WindowState == WindowState.Minimized)
                _resultWindow.WindowState = WindowState.Normal;

            _resultWindow.Activate();
        }

        /// <summary>
        /// Schließt das Ergebnisfenster mit dem Fragefenster.
        /// <para>
        /// <b>Gemessen 2026-09-07.</b> Es ging ohne Besitzer und ohne Dialog auf und wurde
        /// nirgends geschlossen. Wer im Fragefenster „Abschließen" und „Nächster wählt aus"
        /// drückte, ohne im Ergebnisfenster zu speichern, ließ es mit den <b>ungespeicherten
        /// Bewertungen</b> stehen. Beim nächsten Zellenklick ging ein zweites auf - und ein
        /// „Speichern" im alten schrieb die Punkte auf die <b>vorige</b> Zelle.
        /// </para>
        /// <para>
        /// <b>Überschreibbar wie <see cref="ShowResultWindow"/></b>, aus demselben Grund: ein
        /// Test durchläuft den Spielablauf, ohne dass ein Fenster aufgeht.
        /// </para>
        /// </summary>
        protected virtual void CloseResultWindow()
        {
            var fenster = _resultWindow;

            _resultWindow = null;

            fenster?.Close();
        }
    }
}
