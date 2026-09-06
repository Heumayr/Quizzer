using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Wo die Frage auf dem Beamer steht - und wo nicht.
    /// <para>
    /// <b>Die Abfolge, entschieden vom Nutzer am 2026-09-06 (Fragen F02 und F05):</b>
    /// </para>
    /// <list type="table">
    /// <item>
    ///   <term>Bildschirm 1 (Startschritt)</term>
    ///   <description>
    ///     <b>nur die Fragenart</b> groß in der Mitte - „Schätzfrage", „Multiple Choice". Kein
    ///     Fragetext. Das ist die Spielregel, nicht Geschmack: der Spielleiter liest die Frage
    ///     vor, und wer buzzert, bevor sie zu Ende gelesen ist, darf sie nicht lesen können.
    ///   </description>
    /// </item>
    /// <item>
    ///   <term>Bildschirm 2 (erster Inhaltsschritt)</term>
    ///   <description>der <b>Fragetext</b> groß in der Mitte - der Augenblick der Freigabe.</description>
    /// </item>
    /// <item>
    ///   <term>ab Bildschirm 3</term>
    ///   <description>Fragetext schmal <b>oben</b>, darunter der Schrittinhalt.</description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>Warum „erster Inhaltsschritt" und nicht „zweiter Schritt":</b> drei der zwölf
    /// Demofragen haben zwischen Start und Auflösung gar nichts. Zählte man bloß die
    /// vorangegangenen Schritte, würde bei ihnen der <b>Abschlussschritt</b> zu Bildschirm 2 -
    /// und der Fragetext läge groß und deckend über der Antwort. Bei einer Schätzfrage ist das
    /// genau der Bildschirm, auf dem der Sollwert erstmals freigegeben wird; die Mitspieler
    /// sähen die Antwort nie.
    /// </para>
    /// <para>
    /// <b>Diese Regel gilt nur für den Beamer.</b> Der Spielleiter sieht die Frage von Anfang an
    /// - seine Kopfleiste bindet <c>Question.QuestionText</c> ohne jede Sichtbarkeitsregel. Eine
    /// Überdeckung darf deshalb <b>nie</b> in die geteilten Schritt-Ansichten wandern, sondern
    /// nur hierher und in <c>GamePlayerView.xaml</c>.
    /// </para>
    /// </summary>
    public partial class GamePlayerViewModel
    {
        private string questionTypeName = string.Empty;

        /// <summary>
        /// Der Anzeigename der Fragenart, etwa „Schätzfrage". Kommt aus
        /// <c>QuestionBase.TypDisplayName</c> und damit aus den Fragetyp-Profilen - dem einzigen
        /// Ort, an dem Typwissen steht.
        /// </summary>
        public string QuestionTypeName
        {
            get => questionTypeName;
            set
            {
                questionTypeName = value ?? string.Empty;
                OnPropertyChanged();

                RaiseQuestionPlacementChanged();
            }
        }

        /// <summary>Ob gerade der Startschritt läuft - der erste Bildschirm einer Frage.</summary>
        public bool IsStartStep => QuestionStepResource?.IsStart == true;

        /// <summary>
        /// Ob gerade der Fragebildschirm läuft - der, auf dem nur die Frage steht.
        /// <para>
        /// Es ist ein eigener, immer ergänzter Schritt (<c>IsQuestionOnly</c>), kein
        /// hergeleiteter Zustand. Bis 2026-09-06 wurde stattdessen „der erste Schritt mit
        /// Inhalt" ausgerechnet - und bei einer Schätzfrage, die zwischen Start und Auflösung
        /// nichts hat, war das die <b>Auflösung</b>: Frage und Antwort standen zugleich da.
        /// </para>
        /// </summary>
        public bool IsQuestionScreen => QuestionStepResource?.IsQuestionOnly == true;

        /// <summary>Die Fragenart groß in der Mitte - nur auf dem Startbildschirm.</summary>
        public Visibility ShowQuestionTypeCentered =>
            IsStartStep && !string.IsNullOrEmpty(QuestionTypeName)
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>Die Frage groß in der Mitte - nur auf dem ersten Inhaltsbildschirm.</summary>
        public Visibility ShowQuestionCentered =>
            !string.IsNullOrEmpty(QuestionText) && IsQuestionScreen
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>
        /// Die Frage als schmale Zeile über dem Geschehen - überall dort, wo sie nicht groß in
        /// der Mitte steht und der Startbildschirm sie nicht verbietet.
        /// </summary>
        public Visibility ShowQuestionTop =>
            string.IsNullOrEmpty(QuestionText) || IsStartStep || IsQuestionScreen
                ? Visibility.Collapsed
                : Visibility.Visible;

        /// <summary>
        /// Meldet alle vier Größen zugleich.
        /// <para>
        /// <b>Bewusst eine einzige Stelle.</b> Sie hingen vorher an zwei Settern, die jeder für
        /// sich eine Liste von Namen führten; eine neue Größe, die dort nicht mitgemeldet wird,
        /// friert auf dem Wert des ersten Schritts ein - und das fällt in einer Zusicherung, die
        /// nur einen Schritt setzt, nicht auf.
        /// </para>
        /// </summary>
        private void RaiseQuestionPlacementChanged()
        {
            OnPropertyChanged(nameof(IsStartStep));
            OnPropertyChanged(nameof(IsQuestionScreen));
            OnPropertyChanged(nameof(ShowQuestionTypeCentered));
            OnPropertyChanged(nameof(ShowQuestionCentered));
            OnPropertyChanged(nameof(ShowQuestionTop));
        }
    }
}
