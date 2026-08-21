using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Helpers
{
    /// <summary>
    /// Zentraler Einstiegspunkt zum Erzeugen neuer Entitäten. Kapselt die Instanziierung
    /// konkreter Unterklassen, sodass Aufrufer nur mit dem abstrakten Typ arbeiten müssen.
    /// </summary>
    public static class Factory
    {
        /// <summary>
        /// Erzeugt eine neue Frage-Instanz des angegebenen Typs mit voreingestellten Standardwerten.
        /// </summary>
        /// <param name="type">Der gewünschte Fragetyp.</param>
        /// <returns>
        /// Eine neue Instanz von <see cref="MultipleChoiceQuestion"/>, <see cref="PropertiesQuestion"/>,
        /// <see cref="AppreciateQestion"/> oder <see cref="DefaultQuestion"/> (Fallback).
        /// </returns>
        public static QuestionBase CreateNewQuestion(QuestionType type)
        {
            return type switch
            {
                QuestionType.MultipleChoice => new MultipleChoiceQuestion(),
                QuestionType.Properties => new PropertiesQuestion(),
                QuestionType.Appreciate => new AppreciateQestion(),
                _ => new DefaultQuestion()
            };
        }
    }
}