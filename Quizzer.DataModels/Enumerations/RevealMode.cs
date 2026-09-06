namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Wie eine Aufdeckfrage ihr Bild freigibt.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „man laedt ein bild rein im frageeditor ... dann kann
    /// man nach und nach bildauschitte verdcken ... im spiel wird dann nach und nach das bild
    /// aufgedeckt bis es eraten wird" und „2 modus ... es wir mittels filter extrem unscharf
    /// gemacht ... jeder step macht es schaerfer".
    /// </para>
    /// </summary>
    public enum RevealMode
    {
        /// <summary>Flaechen liegen ueber dem Bild; je Schritt faellt eine weg.</summary>
        Areas = 0,

        /// <summary>Das Bild ist unscharf; je Schritt wird es schaerfer.</summary>
        Blur = 1,
    }
}
