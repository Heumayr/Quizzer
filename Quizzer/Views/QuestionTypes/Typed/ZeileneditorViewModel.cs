using Quizzer.Base;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Was alle Typmasken teilen: eine Zeilenliste, ein Abschlussfeld und die beiden Wege
    /// dazwischen.
    /// <para>
    /// <b><c>UcViewModelBase</c>, nicht <c>ViewModelBase</c>:</b> diese Modelle gehören zu keinem
    /// Fenster, sie hängen im <c>ContentControl</c> der Schale. Ein Lebenszyklus mit
    /// <c>OnloadAsync</c> und <c>VMSaveAsync</c> wäre hier eine Zusage, die niemand einlöst.
    /// </para>
    /// </summary>
    public class ZeileneditorViewModel : UcViewModelBase
    {
        private readonly IStepComposer composer;
        private readonly Action melde;
        private readonly QuestionBase frage;

        internal ZeileneditorViewModel(IStepComposer composer, QuestionBase frage, Action melde)
        {
            this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
            this.melde = melde ?? (() => { });

            this.frage = frage ?? throw new ArgumentNullException(nameof(frage));

            Bild = composer.Lies(frage);
            Zeilen = new ObservableCollection<StepZeile>(Bild.Zeilen);

            foreach (var zeile in Zeilen)
                Beobachte(zeile);

            foreach (var schritt in frage.Steps)
                bekannt.Add(schritt.Id);
        }

        /// <summary>Das gelesene Schrittbild - hier hängen auch die mitgeführten Schritte dran.</summary>
        protected Schrittbild Bild { get; }

        /// <summary>Die Zeilen, wie die Maske sie zeigt.</summary>
        public ObservableCollection<StepZeile> Zeilen { get; }

        /// <summary>Das Abschlussfeld - <c>null</c>, wenn der Typ keines hat.</summary>
        public StepZeile? Abschluss => Bild.Abschluss;

        /// <summary>Das Startfeld - <c>null</c>, wenn der Typ keines hat.</summary>
        public StepZeile? Start => Bild.Start;

        /// <summary>Die Überschrift über dem Startfeld.</summary>
        public string StartTitel => composer.StartTitel;

        public Visibility StartVisibility
            => Start == null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Ob die Kurzform für das Telefon angeboten wird.
        /// <para>
        /// <b>Nur, wo das Telefon überhaupt Text zeigt.</b> Das sagt dasselbe Profilmerkmal, das
        /// den Schalter „Text auf den Tasten" steuert - bei den übrigen Typen wäre es ein Feld
        /// ohne Wirkung.
        /// </para>
        /// </summary>
        public Visibility KurzformVisibility
            => QuestionTypeProfiles.For(composer.Typ).ShowShowTextOnKeySelect
                ? Visibility.Visible
                : Visibility.Collapsed;

        /// <summary>Die Überschrift über der Zeilenliste.</summary>
        public string ZeilenTitel => composer.ZeilenTitel;

        /// <summary>Die Überschrift über dem Abschlussfeld.</summary>
        public string AbschlussTitel => composer.AbschlussTitel;

        /// <summary>Ob dieser Typ überhaupt eine Zeilenliste zeigt.</summary>
        public Visibility ZeilenVisibility
            => string.IsNullOrEmpty(ZeilenTitel) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Ob dieser Typ ein Abschlussfeld zeigt.</summary>
        public Visibility AbschlussVisibility
            => Abschluss == null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Ob die Zeilen von Hand umsortiert werden dürfen.</summary>
        public virtual bool DarfSortieren => true;

        /// <summary>Ob die Maske Zeilen anlegen und entfernen lässt.</summary>
        public virtual bool DarfZeilenAendern => true;

        public Visibility SortierenVisibility
            => DarfSortieren ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ZeilenAendernVisibility
            => DarfZeilenAendern ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Ob je Zeile ein Häkchen „richtig" steht. Der Typ sagt es über sein Profil - bei der
        /// Schätzfrage kann es nie etwas bedeuten, und heute steht die Spalte trotzdem da.
        /// </summary>
        public virtual bool ZeigtRichtig
            => QuestionTypeProfiles.For(composer.Typ).ShowIsResultPerStep;

        public Visibility RichtigVisibility
            => ZeigtRichtig ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Satz unter der Zeilenliste - was der Typ dazu zu sagen hat.</summary>
        public virtual string Zeilenhinweis => string.Empty;

        /// <summary>
        /// Was der Buchstabe neben der Zeile bedeutet.
        /// <para>
        /// <b>Er bedeutet nicht überall dasselbe</b>, und das stand vorher falsch da: bei
        /// Multiple Choice mischt <c>CalculateOrderdSteps</c> die Antworten bei jedem Spielen
        /// neu, der Buchstabe am Telefon ist also jedes Mal ein anderer.
        /// </para>
        /// </summary>
        public virtual string Tastenerklaerung
            => "Diese Taste liegt im Spiel auf dieser Zeile.";

        /// <summary>
        /// Schreibt die Zeilen in die Frage zurück.
        /// <para>
        /// <b>Die Reihenfolge der Anzeige gilt</b>, nicht die des gelesenen Bildes - deshalb wird
        /// <see cref="Schrittbild.Zeilen"/> vorher aus der beobachteten Liste neu gefüllt.
        /// </para>
        /// </summary>
        internal void SchreibZurueck(QuestionBase frage)
        {
            NimmFremdeSchritteMit(frage);

            Bild.Zeilen.Clear();
            Bild.Zeilen.AddRange(Zeilen);

            composer.Schreib(frage, Bild);
        }

        /// <summary>
        /// Schritte, die seit dem Lesen von <b>außerhalb</b> der Maske dazugekommen sind, werden
        /// mitgenommen.
        /// <para>
        /// <b>Von aussen wird weiterhin angelegt.</b> <c>SchritteAngleichen</c> der Aufdeckfrage
        /// legt direkt in <c>Question.Steps</c> an. Ohne diese Übernahme schriebe die Maske ihr
        /// beim Öffnen gelesenes Bild darüber und die Aufdeckschritte wären weg - vier bestehende
        /// Zusicherungen haben genau das gemeldet.
        /// </para>
        /// <para>
        /// <b>Über die beim Lesen bekannten Ids, nicht über die aktuellen Zeilen.</b> Sonst käme
        /// eine gerade gelöschte Zeile hier wieder herein - sie steht ja noch in
        /// <c>Question.Steps</c>.
        /// </para>
        /// </summary>
        private void NimmFremdeSchritteMit(QuestionBase frage)
        {
            foreach (var schritt in frage.Steps)
            {
                if (bekannt.Add(schritt.Id))
                    Bild.Mitgefuehrt.Add(schritt);
            }
        }

        private readonly HashSet<Guid> bekannt = [];

        /// <summary>
        /// Wie ein Medium ausgewählt wird. Die Schale hängt das ein - das ViewModel öffnet
        /// keinen Dateidialog.
        /// </summary>
        internal Func<StepZeile, bool>? MediumWaehlen { get; set; }

        private AsyncRelayCommand? mediaCommand;

        /// <summary>
        /// Ein Medium an diese Zeile hängen - <b>ohne den Schritt-Dialog</b>.
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „bei multible coice ... gut wäre ein button für
        /// media". Bisher führte der einzige Weg über ein zweites Fenster.
        /// </para>
        /// </summary>
        public ICommand MediaCommand => mediaCommand ??= new AsyncRelayCommand(parameter =>
        {
            if (parameter is StepZeile zeile && MediumWaehlen?.Invoke(zeile) == true)
            {
                zeile.MeldeAlles();

                // Ein Medium macht eine bis dahin leere Zeile nicht mehr leer - und verschiebt
                // damit die Buchstabenvergabe.
                Geaendert();
            }

            return Task.CompletedTask;
        });

        /// <summary>
        /// Hört auf eine Zeile - aber nur auf das, was der Nutzer eingibt.
        /// <para>
        /// <b>Gemessen am 2026-09-06:</b> ohne diese Einschränkung lief es rund. Eine Maske
        /// schreibt abgeleitete Angaben in <c>Zusatz</c> zurück, das meldete eine Änderung, die
        /// Maske rechnete neu, schrieb wieder - Stapelüberlauf. Der Testlauf brach dabei ab und
        /// meldete trotzdem „Bestanden", nur mit weniger Tests.
        /// </para>
        /// </summary>
        private void Beobachte(StepZeile zeile)
            => zeile.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(StepZeile.Text) or nameof(StepZeile.IstRichtig))
                    Geaendert();
            };

        /// <summary>
        /// Wie die Frage aussähe, wenn man jetzt speicherte - <b>auf einem Klon, ohne die Frage
        /// oder das gelesene Bild anzufassen</b>.
        /// <para>
        /// <b>Der Grund ist ein gemessener Defekt vom 2026-09-06:</b> die Prüfung lief gegen
        /// <c>Question.Steps</c>, und dort steht bis zum Speichern nichts - die Maske schreibt
        /// erst in <c>SaveAsync</c> zurück. Eine frische Multiple-Choice-Frage trug damit
        /// <c>TooFewSteps</c> und <c>ResultStepMissing</c>, obwohl vier Antworten dastanden;
        /// <c>CanSave</c> blieb falsch, und weil <c>UebernimmZeilen</c> nur <i>innerhalb</i> von
        /// <c>SaveAsync</c> läuft, gab es keinen Weg heraus. <b>Sie war nicht speicherbar.</b>
        /// </para>
        /// <para>
        /// <b>Warum nicht einfach laufend zurückschreiben:</b> der Übersetzer räumt leere Zeilen
        /// weg und stempelt die Reihenfolge. Liefe das bei jedem Tastendruck, verschwände eine
        /// Zeile unter dem Cursor, sobald man ihren Text löscht.
        /// </para>
        /// </summary>
        internal QuestionBase Vorschau(QuestionBase frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            var klon = frage.CloneWithoutReferences();

            var mitgefuehrt = new List<DataModels.Models.Base.QuestionStepResource>(Bild.Mitgefuehrt);

            // Was seit dem Lesen von aussen dazukam, gehoert in die Vorschau - sonst meldet die
            // Pruefung "zu wenige Schritte" fuer Schritte, die es gibt. Anders als beim Speichern
            // wird hier NICHTS gemerkt: taete es das, hielte der naechste Speichervorgang sie fuer
            // bekannt und liesse sie fallen.
            foreach (var schritt in frage.Steps)
            {
                if (!bekannt.Contains(schritt.Id) && !mitgefuehrt.Contains(schritt))
                    mitgefuehrt.Add(schritt);
            }

            var bild = new Schrittbild
            {
                Zeilen = [.. Zeilen],
                Abschluss = Bild.Abschluss,
                Mitgefuehrt = mitgefuehrt,
            };

            composer.Schreib(klon, bild);

            return klon;
        }

        /// <summary>Meldet der Schale, dass sich etwas geändert hat - sie prüft dann neu.</summary>
        protected void Geaendert()
        {
            Melde();
            melde();
        }

        /// <summary>
        /// Was die Maske nach einer Änderung neu anzeigt. Typmasken hängen ihre eigenen
        /// abgeleiteten Angaben hier an - eine vergessene Meldung fiele sonst nur daran auf,
        /// dass eine Zeile stehenbleibt, also gar nicht.
        /// </summary>
        protected virtual void Melde()
        {
            // Die Buchstaben folgen der Liste, nicht dem Zustand beim Oeffnen.
            StepComposerBase.VergibTasten(frage, Zeilen);

            OnPropertyChanged(nameof(Zeilen));
            OnPropertyChanged(nameof(Zeilenhinweis));
        }

        private RelayCommand? addRowCommand;

        /// <summary>Eine Zeile anhängen.</summary>
        public ICommand AddRowCommand => addRowCommand ??= new RelayCommand(_ =>
        {
            var zeile = new StepZeile(new DataModels.Models.Base.QuestionStepResource
            {
                Id = Guid.NewGuid(),
            });

            Beobachte(zeile);
            bekannt.Add(zeile.Schritt.Id);

            Zeilen.Add(zeile);
            Geaendert();
        });

        private RelayCommand<StepZeile>? toggleDetailsCommand;

        /// <summary>
        /// Klappt die Einzelheiten einer Zeile auf oder zu.
        /// <para>
        /// <b>Nur eine gleichzeitig.</b> Sechs offene Zeilen wären eine Maske, in der man scrollen
        /// muss, um zwei Antworten zu vergleichen - und die Zeilenliste hat keinen eigenen
        /// Rollbalken.
        /// </para>
        /// </summary>
        public ICommand ToggleDetailsCommand => toggleDetailsCommand ??= new RelayCommand<StepZeile>(
            zeile =>
            {
                if (zeile == null)
                    return;

                var offen = !zeile.IstOffen;

                foreach (var andere in Zeilen)
                    andere.IstOffen = false;

                zeile.IstOffen = offen;
            });

        private RelayCommand<StepZeile>? removeMediaCommand;

        /// <summary>
        /// Nimmt das Medium von der Zeile.
        /// <para>
        /// <b>Beide Felder in einem Zug</b> - Dateiname <i>und</i> Typ. Nur den Typ zu leeren ist
        /// genau das, was der alte Schritt-Dialog konnte, und es hinterließ
        /// <c>ResourceWithoutType</c>: einen Fehler, der das Speichern sperrte. Ein Medium war
        /// damit nirgends im Editor löschbar.
        /// </para>
        /// <para>
        /// <b>Die Datei bleibt liegen</b>, und das ist eine Entscheidung: <c>CloneWithoutReferences</c>
        /// kopiert den Dateinamen mit, zwei Fragen können also auf dieselbe Datei zeigen. Ein
        /// <c>File.Delete</c> wäre der anderen gegenüber wortlos. Aufräumen braucht einen Lauf,
        /// der alle Verweise kennt.
        /// </para>
        /// </summary>
        public ICommand RemoveMediaCommand => removeMediaCommand ??= new RelayCommand<StepZeile>(
            zeile =>
            {
                if (zeile == null || !zeile.HatMedium)
                    return;

                zeile.Schritt.ResourceFileName = string.Empty;
                zeile.Schritt.ResourceTyp = DataModels.Enumerations.ResourceType.None;

                zeile.MeldeAlles();

                Geaendert();
            });

        private RelayCommand<StepZeile>? removeRowCommand;

        /// <summary>Eine Zeile entfernen.</summary>
        public ICommand RemoveRowCommand => removeRowCommand ??= new RelayCommand<StepZeile>(
            zeile =>
            {
                if (zeile == null || !Zeilen.Remove(zeile))
                    return;

                zeile.IstOffen = false;

                Geaendert();
            });
        private RelayCommand<StepZeile>? moveUpCommand;

        /// <summary>Eine Zeile nach oben schieben - ein Klick statt vier.</summary>
        public ICommand MoveUpCommand => moveUpCommand ??= new RelayCommand<StepZeile>(
            zeile => Verschiebe(zeile, -1));

        private RelayCommand<StepZeile>? moveDownCommand;

        /// <summary>Eine Zeile nach unten schieben.</summary>
        public ICommand MoveDownCommand => moveDownCommand ??= new RelayCommand<StepZeile>(
            zeile => Verschiebe(zeile, +1));

        private void Verschiebe(StepZeile? zeile, int richtung)
        {
            if (zeile == null)
                return;

            var alt = Zeilen.IndexOf(zeile);
            var neu = alt + richtung;

            if (alt < 0 || neu < 0 || neu >= Zeilen.Count)
                return;

            Zeilen.Move(alt, neu);
            Geaendert();
        }
    }
}
