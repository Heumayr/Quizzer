namespace Quizzer.DataModels.Questions
{
    /// <summary>Gewicht einer Beanstandung.</summary>
    public enum ValidationSeverity
    {
        /// <summary>Die Frage ist so spielbar, es koennte aber gemeint gewesen sein.</summary>
        Warning = 0,

        /// <summary>Die Frage ist so nicht spielbar. Speichern wird verhindert.</summary>
        Error = 1,
    }

    /// <summary>
    /// Eine einzelne Beanstandung an einer Frage. <see cref="Code"/> ist die stabile Kennung,
    /// an der sich ein Test festmacht; <see cref="Message"/> ist der Satz fuer den Spielleiter.
    /// </summary>
    /// <param name="Code">Stabile Kennung der Regel.</param>
    /// <param name="Severity">Ob die Frage dadurch unspielbar wird.</param>
    /// <param name="Message">Klartext fuer den Spielleiter.</param>
    /// <param name="Member">Das betroffene Feld, sofern es eines gibt.</param>
    public sealed record ValidationIssue(
        string Code,
        ValidationSeverity Severity,
        string Message,
        string? Member = null)
    {
        /// <summary>Ob diese Beanstandung das Speichern verhindert.</summary>
        public bool IsError => Severity == ValidationSeverity.Error;

        public override string ToString() => $"{Severity}: {Message}";
    }
}
