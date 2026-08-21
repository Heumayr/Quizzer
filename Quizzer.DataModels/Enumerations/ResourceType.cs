using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Gibt den Medientyp der Ressource an, die einem <c>QuestionStepResource</c>-Schritt
    /// beigefügt ist. Anhand dieses Werts entscheidet die Oberfläche, welchen Player
    /// oder welches Steuerelement sie zur Darstellung verwendet.
    /// </summary>
    public enum ResourceType
    {
        /// <summary>Kein Medium vorhanden; der Schritt enthält nur Text.</summary>
        None = 0,

        /// <summary>Bilddatei (z.B. PNG, JPG).</summary>
        Image = 1,

        /// <summary>Videodatei (z.B. MP4).</summary>
        Video = 2,

        /// <summary>Audiodatei (z.B. MP3, WAV).</summary>
        Audio = 3,

        /// <summary>Dokumentdatei (z.B. PDF).</summary>
        Document = 4
    }
}