using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Aufdeckfrage: die Schritte entstehen aus der Einrichtung, nicht von Hand.
    /// <para>
    /// <b>Deshalb kein „Zeile hinzufügen".</b> Bisher bot die Maske es an, und der nächste
    /// „Übernehmen"-Klick im Aufdeck-Editor entfernte den Schritt wortlos wieder.
    /// </para>
    /// </summary>
    public sealed class AufdeckViewModel : ZeileneditorViewModel
    {
        private readonly QuestionBase frage;

        internal AufdeckViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
            this.frage = frage;
        }

        public override bool DarfSortieren => false;

        public override bool DarfZeilenAendern => false;

        /// <summary>Was eingerichtet ist - eine Zeile für die Maske.</summary>
        public string Zusammenfassung
        {
            get
            {
                if (frage is not RevealQuestion aufdeck)
                    return string.Empty;

                if (string.IsNullOrWhiteSpace(aufdeck.ImageFileName))
                    return "Noch kein Bild hinterlegt.";

                return aufdeck.Mode switch
                {
                    DataModels.Enumerations.RevealMode.Blur
                        => $"Unschärfe von {aufdeck.BlurStart:0} an, je Schritt schärfer.",
                    DataModels.Enumerations.RevealMode.Pixelate
                        => $"Raster von {aufdeck.BlurStart:0} an, je Schritt feiner.",
                    _ => $"{DataModels.Questions.RevealAreas.Parse(aufdeck.AreasJson).Count} "
                         + "Fläche(n) über dem Bild.",
                };
            }
        }

        /// <summary>Was daraus im Spiel wird - schreibgeschützt, damit man es vorher sieht.</summary>
        public string Schrittzeile
            => Zeilen.Count == 0
                ? "Daraus entsteht noch kein Aufdeckschritt."
                : $"Das ergibt {Zeilen.Count} Aufdeckschritt(e).";

        public override string Zeilenhinweis => Zusammenfassung + " " + Schrittzeile;

        /// <summary>Nach dem Einrichten neu einlesen - die Zahl der Schritte kann sich geändert haben.</summary>
        internal void MeldeEingerichtet()
        {
            OnPropertyChanged(nameof(Zusammenfassung));
            OnPropertyChanged(nameof(Schrittzeile));
        }

        protected override void Melde()
        {
            base.Melde();

            MeldeEingerichtet();
        }
    }
}
