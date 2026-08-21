using Quizzer.Views.BuzzerViews;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Views.StaticRessources
{
    public static class StaticManager
    {
        private static BuzzerServerViewModel? buzzerServerViewModel;

        /// <summary>
        /// Wird erst beim ersten Zugriff erzeugt. Frueher stand hier ein Feldinitialisierer - der
        /// baute schon beim blossen Beruehren des Typs ein ViewModel und riss damit alles mit,
        /// was ohne laufende Anwendung lief.
        /// </summary>
        public static BuzzerServerViewModel BuzzerServerViewModel
        {
            get => buzzerServerViewModel ??= new();
            set => buzzerServerViewModel = value;
        }

        /// <summary>Nur fuer Tests: gibt den Buzzer-Server wieder frei.</summary>
        internal static void Reset() => buzzerServerViewModel = null;
    }
}