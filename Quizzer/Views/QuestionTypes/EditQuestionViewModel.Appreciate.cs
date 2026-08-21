using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Collections.ObjectModel;
using System.Windows;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Schaetzfrage-Teil des Editors: was geschaetzt wird, in welcher Einheit, und welcher Wert
    /// richtig ist. Daraus ergibt sich, was die Spieler am Telefon eintippen koennen und wer
    /// hinterher als naechstliegender Tipp gewinnt.
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Die bearbeitete Frage als Schaetzfrage, sofern es eine ist.</summary>
        public AppreciateQestion? Appreciate => Question as AppreciateQestion;

        /// <summary>Die Arten von Groessen, die geschaetzt werden koennen.</summary>
        public Array AppreciateValueKinds { get; } = Enum.GetValues<AppreciateValueKind>();

        /// <summary>Die Einheiten, die zur gewaehlten Art passen.</summary>
        public ObservableCollection<AppreciateUnitInfo> AppreciateUnitsForKind { get; } = new();

        /// <summary>Was geschaetzt wird. Beim Wechsel wird die Einheit passend nachgezogen.</summary>
        public AppreciateValueKind AppreciateValueKind
        {
            get => Appreciate?.ValueKind ?? DataModels.Enumerations.AppreciateValueKind.Number;
            set
            {
                if (Appreciate == null || Appreciate.ValueKind == value)
                    return;

                Appreciate.ValueKind = value;

                // Beim Wechsel der Art immer die erste Einheit der neuen Liste einsetzen.
                // Frueher blieb eine passende Einheit stehen - das war unvorhersehbar, weil man
                // der Auswahlliste nicht ansieht, ob sie gerade neu gesetzt wurde oder nicht.
                Appreciate.Unit = AppreciateUnits.DefaultUnitFor(value);

                RefreshAppreciateUnits();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AppreciateUnit));
                OnPropertyChanged(nameof(ExpectedValueIsDate));
                OnPropertyChanged(nameof(ExpectedNumberVisibility));
                OnPropertyChanged(nameof(ExpectedDateVisibility));
                OnPropertyChanged(nameof(AppreciateHintText));
                Revalidate();
            }
        }

        /// <summary>Die Einheit des Sollwerts.</summary>
        public AppreciateUnitInfo? AppreciateUnit
        {
            get => Appreciate == null ? null : AppreciateUnits.Info(Appreciate.Unit);
            set
            {
                if (Appreciate == null || value == null || Appreciate.Unit == value.Unit)
                    return;

                Appreciate.Unit = value.Unit;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AppreciateHintText));
                Revalidate();
            }
        }

        /// <summary>Ob ein Datum statt einer Zahl geschaetzt wird.</summary>
        public bool ExpectedValueIsDate
            => AppreciateValueKind == DataModels.Enumerations.AppreciateValueKind.Date;

        /// <summary>Das Zahlenfeld fuer den Sollwert.</summary>
        public Visibility ExpectedNumberVisibility
            => Appreciate != null && !ExpectedValueIsDate ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Das Datumsfeld fuer den Sollwert.</summary>
        public Visibility ExpectedDateVisibility
            => Appreciate != null && ExpectedValueIsDate ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Sollwert als Zahl.</summary>
        public double ExpectedValue
        {
            get => Appreciate?.ExpectedValue ?? 0;
            set
            {
                if (Appreciate == null || Math.Abs(Appreciate.ExpectedValue - value) < double.Epsilon)
                    return;

                Appreciate.ExpectedValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AppreciateHintText));
                Revalidate();
            }
        }

        /// <summary>Der Sollwert als Datum.</summary>
        public DateTime? ExpectedDate
        {
            get => Appreciate?.ExpectedDate;
            set
            {
                if (Appreciate == null || Appreciate.ExpectedDate == value)
                    return;

                Appreciate.ExpectedDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AppreciateHintText));
                Revalidate();
            }
        }

        /// <summary>Erklaert im Klartext, was die Spieler sehen und wie gewertet wird.</summary>
        public string AppreciateHintText
        {
            get
            {
                if (Appreciate == null)
                    return string.Empty;

                var placeholder = AppreciateEvaluator.PlaceholderFor(
                    Appreciate.ValueKind, Appreciate.Unit);

                return $"Die Spieler tippen: {placeholder}. Richtig ist "
                     + $"{AppreciateEvaluator.DescribeExpected(Appreciate)}. "
                     + "Wer am naechsten dran liegt, gewinnt; bei Gleichstand gewinnen alle "
                     + "Gleichauf-Spieler.";
            }
        }

        private void RefreshAppreciateUnits()
        {
            AppreciateUnitsForKind.Clear();

            if (Appreciate == null)
                return;

            foreach (var unit in AppreciateUnits.For(Appreciate.ValueKind))
                AppreciateUnitsForKind.Add(unit);
        }

        /// <summary>Meldet alle Schaetzfrage-Werte als geaendert. Aufgerufen aus Revalidate.</summary>
        private void RaiseAppreciateChanged()
        {
            RefreshAppreciateUnits();

            OnPropertyChanged(nameof(Appreciate));
            OnPropertyChanged(nameof(AppreciateValueKind));
            OnPropertyChanged(nameof(AppreciateUnit));
            OnPropertyChanged(nameof(ExpectedValue));
            OnPropertyChanged(nameof(ExpectedDate));
            OnPropertyChanged(nameof(ExpectedValueIsDate));
            OnPropertyChanged(nameof(ExpectedNumberVisibility));
            OnPropertyChanged(nameof(ExpectedDateVisibility));
            OnPropertyChanged(nameof(AppreciateHintText));
        }
    }
}
