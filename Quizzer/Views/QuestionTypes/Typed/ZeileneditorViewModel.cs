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

        internal ZeileneditorViewModel(IStepComposer composer, QuestionBase frage, Action melde)
        {
            this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
            this.melde = melde ?? (() => { });

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
        /// <b>Der Schritt-Dialog („Erweitert …") legt weiterhin direkt in <c>Question.Steps</c>
        /// an.</b> Ohne diese Übernahme schriebe die Maske ihr beim Öffnen gelesenes Bild darüber
        /// und der neue Schritt wäre weg - vier bestehende Zusicherungen haben genau das gemeldet,
        /// bevor es jemand am Quizabend gemerkt hätte.
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

        private RelayCommand<StepZeile>? removeRowCommand;

        /// <summary>Eine Zeile entfernen.</summary>
        public ICommand RemoveRowCommand => removeRowCommand ??= new RelayCommand<StepZeile>(
            zeile =>
            {
                if (zeile != null && Zeilen.Remove(zeile))
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
