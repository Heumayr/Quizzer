using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Transfer
{
    /// <summary>
    /// Ein Spiel als Bauplan - alles, was man braucht, um es woanders wieder aufzubauen.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „ich möchte einen export und import für ein spiel ...
    /// der export soll alle nötigen daten sinvoll in ein json exportieren inklusive medienfiles
    /// design und alles was nötig ist", ergänzt um „die medien können auch neben dem json liegen
    /// aber müssen quasi mitgenommen werden".
    /// </para>
    /// <para>
    /// <b>Es ist ein Bauplan, kein Spielstand.</b> Was zum Verlauf gehört, steht bewusst nicht
    /// darin: Punktestände, wer gerade auswählt, welche Zelle gespielt ist, die laufende Runde,
    /// die Phase. Eine Exportdatei geht an einen anderen Menschen - sie soll ihm das Spiel geben,
    /// nicht den Abend.
    /// </para>
    /// <para>
    /// <b>Und keine Ids.</b> Weder die des Spiels noch die der Fragen, Kategorien oder Spieler.
    /// Verweise laufen über die <c>…Index</c>-Felder innerhalb des Dokuments. Eine übernommene
    /// Id zeigt auf dem Zielrechner entweder ins Leere oder - schlimmer - zufällig auf etwas
    /// Fremdes.
    /// </para>
    /// <para>
    /// <b>Typoffen gebaut.</b> Je Frage steht der Typ als eigenes Feld und die typeigenen Werte
    /// in einem eigenen Block. Ein später hinzukommender Fragetyp bricht damit keine Datei, die
    /// vorher entstanden ist.
    /// </para>
    /// </summary>
    public sealed class GameExportDocument
    {
        /// <summary>
        /// Version des Formats. Steigt, wenn sich die Bedeutung eines Feldes ändert - nicht, wenn
        /// eines dazukommt.
        /// </summary>
        public int FormatVersion { get; set; } = 1;

        /// <summary>Womit die Datei erzeugt wurde. Reine Auskunft, wird beim Import nicht gelesen.</summary>
        public string ErzeugtVon { get; set; } = "Quizzer";

        public GameData Spiel { get; set; } = new();

        public List<CategoryData> Kategorien { get; set; } = new();

        public List<QuestionData> Fragen { get; set; } = new();

        public List<HeaderData> Kopfzeilen { get; set; } = new();

        public List<CoordinateData> Zellen { get; set; } = new();

        /// <summary>Das Design des Spiels. <c>null</c> heißt: der Auslieferungsstand.</summary>
        public ThemeData? Design { get; set; }

        /// <summary>
        /// Die Mediendateien, die im Bündel neben dem Dokument liegen - je Eintrag der Name im
        /// Bündel. Die Fragen und das Design verweisen über den <c>Schluessel</c> darauf.
        /// </summary>
        public List<MediaData> Medien { get; set; } = new();

        /// <summary>Der Aufbau des Spiels - ohne alles, was zum Verlauf gehört.</summary>
        public sealed class GameData
        {
            public string Designation { get; set; } = string.Empty;

            public int Height { get; set; } = 5;
            public int Width { get; set; } = 5;
            public int Depth { get; set; }

            public double CellHeight { get; set; } = 100;
            public double CellWidth { get; set; } = 120;

            public double DifficultyMultiplier { get; set; } = 1;
            public int DifficultyAddition { get; set; }
            public double DifficultyMinusMultiplier { get; set; } = 0.1;
            public int DifficultyMinusAddition { get; set; }
            public double PhaseMultiplier { get; set; } = 1;
            public int PhaseAddition { get; set; }

            public int SuggestedPhases { get; set; } = 3;
        }

        /// <summary>Eine Kategorie - außer ihrer Bezeichnung hat sie keine Nutzlast.</summary>
        public sealed class CategoryData
        {
            public string Designation { get; set; } = string.Empty;
        }

        /// <summary>Eine Kopfzeile des Rasters.</summary>
        public sealed class HeaderData
        {
            public string Designation { get; set; } = string.Empty;
            public HeaderType HeaderType { get; set; }
            public int Index { get; set; }
        }

        /// <summary>
        /// Eine Zelle des Rasters. <c>FrageIndex</c> zeigt in <see cref="Fragen"/>; <c>null</c>
        /// heißt: leere Zelle.
        /// <para>
        /// Punkte stehen hier nicht - sie werden beim Import aus den Faktoren des Spiels und der
        /// Frage neu gerechnet. Übernommene Punkte wären die des Quell-Spiels.
        /// </para>
        /// </summary>
        public sealed class CoordinateData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }

            public int? FrageIndex { get; set; }
        }

        /// <summary>Eine Frage samt ihren Schritten.</summary>
        public sealed class QuestionData
        {
            public QuestionType Typ { get; set; }

            public string Designation { get; set; } = string.Empty;
            public string DesignationShort { get; set; } = string.Empty;
            public string QuestionText { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;

            /// <summary>Zeigt in <see cref="Kategorien"/>.</summary>
            public int KategorieIndex { get; set; }

            public int Points { get; set; }
            public int MinusPoints { get; set; }
            public Difficulty Difficulty { get; set; } = Difficulty.Level1;

            public bool UseProportionalScoreReductionOnStep { get; set; }
            public bool WarnOnResultStep { get; set; } = true;
            public bool WarnOnFinishStep { get; set; } = true;
            public bool UseRandomSequenceOnNoneFinishSteps { get; set; }

            public QuestionViewKeyType QuestionViewKeyType { get; set; } = QuestionViewKeyType.Alphabetical;
            public BuzzerControlsLayout BuzzerControlsLayout { get; set; } = BuzzerControlsLayout.Buzzer;
            public int BuzzerMaxAllowedKeySelect { get; set; } = 1;
            public bool ShowTextOnKeySelect { get; set; } = true;
            public StepDisplayLayoutMode StepDisplayLayoutMode { get; set; } = StepDisplayLayoutMode.Vertical;

            /// <summary>
            /// Was nur dieser Fragetyp hat. <c>null</c> bei Typen ohne eigene Felder.
            /// <para>
            /// Der Block ist der Grund, warum ein später hinzukommender Fragetyp keine
            /// bestehende Datei bricht: er wächst, statt die Frage umzubauen.
            /// </para>
            /// </summary>
            public TypeSpecificData? Typeigen { get; set; }

            public List<StepData> Schritte { get; set; } = new();
        }

        /// <summary>Die Felder, die nur einzelne Fragetypen tragen.</summary>
        public sealed class TypeSpecificData
        {
            // Schaetzfrage
            public AppreciateValueKind? ValueKind { get; set; }
            public AppreciateUnit? Unit { get; set; }
            public double? ExpectedValue { get; set; }
            public DateTime? ExpectedDate { get; set; }
        }

        /// <summary>Ein Schritt einer Frage.</summary>
        public sealed class StepData
        {
            public int SequenceNumber { get; set; }
            public string Designation { get; set; } = string.Empty;
            public string StepText { get; set; } = string.Empty;

            public bool IsStart { get; set; }
            public bool IsResult { get; set; }
            public bool IsFinish { get; set; }

            public ResourceType ResourceTyp { get; set; } = ResourceType.None;

            /// <summary>Zeigt in <see cref="Medien"/>. <c>null</c> heißt: kein Medium.</summary>
            public string? MedienSchluessel { get; set; }
        }

        /// <summary>Das Design samt seinen eigenen Texturen.</summary>
        public sealed class ThemeData
        {
            public string Designation { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;

            public string BackgroundColor { get; set; } = string.Empty;
            public string ForegroundColor { get; set; } = string.Empty;
            public string CellTextColor { get; set; } = string.Empty;
            public string HeaderTextColor { get; set; } = string.Empty;

            /// <summary>
            /// Je eigene Textur: der Dateiname im Design (etwa <c>CellBackground.png</c>) und der
            /// Schlüssel in <see cref="Medien"/>.
            /// </summary>
            public Dictionary<string, string> Texturen { get; set; } = new();
        }

        /// <summary>
        /// Eine Datei im Bündel.
        /// <para>
        /// <b><c>DateiImBuendel</c> wird beim Import nie als Zielpfad benutzt</b>, sondern nur,
        /// um den Eintrag im Bündel zu finden. Wohin geschrieben wird, entscheidet allein das
        /// Zielsystem - eine Datei aus fremder Hand darf das nicht bestimmen.
        /// </para>
        /// </summary>
        public sealed class MediaData
        {
            /// <summary>Der Schlüssel, über den Fragen und Design darauf zeigen.</summary>
            public string Schluessel { get; set; } = string.Empty;

            /// <summary>Der Name des Eintrags im Bündel.</summary>
            public string DateiImBuendel { get; set; } = string.Empty;

            /// <summary>Die Endung, mit der die Datei beim Import angelegt wird.</summary>
            public string Endung { get; set; } = string.Empty;

            /// <summary>
            /// Der Name, den die Datei ursprünglich trug - reine Auskunft, damit man im Bündel
            /// erkennt, was man vor sich hat.
            /// </summary>
            public string UrsprungsName { get; set; } = string.Empty;
        }
    }
}
