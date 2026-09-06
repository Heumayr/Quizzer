using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Der Übersetzer je Fragetyp - einzige Stelle, an der die Zuordnung steht.
    /// <para>
    /// Gebaut wie <see cref="QuestionTypeProfiles"/>, und aus demselben Grund: eine zweite Liste
    /// derselben Typen läuft der ersten davon.
    /// </para>
    /// </summary>
    public static class StepComposers
    {
        /// <summary>Alle Übersetzer, in der Reihenfolge der Fragetypen.</summary>
        public static IReadOnlyList<IStepComposer> All { get; } =
        [
            new HinweisComposer(),
            new MultipleChoiceComposer(),
            new EigenschaftComposer(),
            new AppreciateComposer(),
            new RevealComposer(),
        ];

        /// <summary>Liefert den Übersetzer zu einem Fragetyp.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Bei einem unbekannten Typ.</exception>
        public static IStepComposer For(QuestionType typ)
            => All.FirstOrDefault(c => c.Typ == typ)
               ?? throw new ArgumentOutOfRangeException(nameof(typ), typ, "Unbekannter Fragetyp.");
    }
}
