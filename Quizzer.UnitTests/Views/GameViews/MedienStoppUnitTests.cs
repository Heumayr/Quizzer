using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// <b>Es spielt immer höchstens ein Medium, und beim Buzzern hört es auf.</b>
    /// <para>
    /// <b>Das Hörbarste des ganzen Abends hatte bis zum 2026-09-09 keine einzige Zusicherung.</b>
    /// In der Musikrunde läuft ein Lied, jemand drückt - und wenn es weiterläuft, redet der
    /// Spielleiter dagegen an. Der Weg dafür ist
    /// <c>CurrentQuestionViewModel.OnWinnerDeclared</c> → <see cref="MediaPreviewCoordinator"/>.
    /// </para>
    /// <para>
    /// <b>Nachgemessen am selben Tag:</b> <c>ResourceViewerControl</c> ist der <i>einzige</i>
    /// Anmelder im ganzen Programm - der Vermerk „TODO Handle other registrations" beschreibt
    /// keinen offenen Fall, sondern eine Vorsichtsmaßnahme.
    /// </para>
    /// <para>
    /// Der Koordinator hält seinen Stand <b>statisch</b>; deshalb räumt jede Probe hinter sich
    /// auf, sonst trägt sie ihn in die nächste.
    /// </para>
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MedienStoppUnitTests
    {
        [TestInitialize]
        [TestCleanup]
        public void Aufraeumen() => MediaPreviewCoordinator.StopRegisterdMedia();

        /// <summary>Was angemeldet wurde, ist auch der Stand.</summary>
        [TestMethod]
        public void WhatIsRegisteredIsRemembered()
        {
            var lied = new object();

            MediaPreviewCoordinator.RegisterStartedMedia(lied);

            Assert.AreSame(lied, MediaPreviewCoordinator.StartedMedia);
        }

        /// <summary>
        /// <b>Ein zweites Medium löst das erste ab</b> - sonst liefen in einer Bilderrunde mit
        /// Ton zwei Spuren gleichzeitig, und niemand könnte sie einzeln anhalten.
        /// </summary>
        [TestMethod]
        public void ASecondMediaReplacesTheFirst()
        {
            var erstes = new object();
            var zweites = new object();

            MediaPreviewCoordinator.RegisterStartedMedia(erstes);
            MediaPreviewCoordinator.RegisterStartedMedia(zweites);

            Assert.AreSame(zweites, MediaPreviewCoordinator.StartedMedia,
                "Das zweite Medium hat das erste nicht abgeloest - dann laufen beide.");
        }

        /// <summary>Das Anhalten räumt den Stand - danach gibt es nichts mehr zu stoppen.</summary>
        [TestMethod]
        public void StoppingClearsTheState()
        {
            MediaPreviewCoordinator.RegisterStartedMedia(new object());
            MediaPreviewCoordinator.StopRegisterdMedia();

            Assert.IsNull(MediaPreviewCoordinator.StartedMedia,
                "Nach dem Anhalten steht immer noch ein Medium als laufend da - der naechste "
                + "Aufruf haelt dann etwas an, das gar nicht spielt.");
        }

        /// <summary>
        /// <b>Abmelden gilt nur für das eigene Medium.</b> Meldet sich ein Steuerelement ab, das
        /// längst abgelöst wurde, darf es das <i>laufende</i> nicht mitnehmen - sonst hört beim
        /// Buzzern nichts mehr auf, weil der Koordinator meint, es spiele nichts.
        /// </summary>
        [TestMethod]
        public void AnOldControlDoesNotUnregisterTheRunningOne()
        {
            var altes = new object();
            var laufendes = new object();

            MediaPreviewCoordinator.RegisterStartedMedia(altes);
            MediaPreviewCoordinator.RegisterStartedMedia(laufendes);

            MediaPreviewCoordinator.UnsignStartedMedia(altes);

            Assert.AreSame(laufendes, MediaPreviewCoordinator.StartedMedia,
                "Ein abgeloestes Steuerelement hat das laufende Medium abgemeldet.");
        }

        /// <summary>Das eigene abzumelden räumt dagegen sehr wohl.</summary>
        [TestMethod]
        public void ItsOwnControlDoesUnregister()
        {
            var lied = new object();

            MediaPreviewCoordinator.RegisterStartedMedia(lied);
            MediaPreviewCoordinator.UnsignStartedMedia(lied);

            Assert.IsNull(MediaPreviewCoordinator.StartedMedia);
        }

        /// <summary>
        /// Anhalten ohne laufendes Medium tut nichts und wirft nicht - der Aufruf kommt bei
        /// <b>jedem</b> Buzzer-Gewinner, auch bei einer Frage ganz ohne Ton.
        /// </summary>
        [TestMethod]
        public void StoppingNothingIsHarmless()
        {
            MediaPreviewCoordinator.StopRegisterdMedia();
            MediaPreviewCoordinator.StopRegisterdMedia();

            Assert.IsNull(MediaPreviewCoordinator.StartedMedia);
        }
    }
}
