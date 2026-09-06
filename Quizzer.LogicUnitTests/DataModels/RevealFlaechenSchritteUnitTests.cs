using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Mehrere Flächen je Aufdeckschritt - und was das für den Bestand bedeutet.
    /// <para>
    /// <b>Nutzermeldung vom 2026-09-06 nachts:</b> „nicht nur eine fläch pro step ... teilweise
    /// möchte man mehrere flächen zeichen oder auch dreiecke und diese rotieren".
    /// </para>
    /// <para>
    /// <b>Die eine Richtung, in die dieser Fragetyp nie irren darf, ist „zu viel zeigen".</b>
    /// `AreasJson` steht in der Spieldatenbank <i>und</i> in exportierten Bündeln; ein Stand ohne
    /// Kenntnis der neuen Felder muss weiterhin mindestens so viel verdecken wie gewollt.
    /// </para>
    /// </summary>
    [TestClass]
    public class RevealFlaechenSchritteUnitTests
    {
        /// <summary>
        /// Eine <b>wörtlich hingeschriebene</b> Altangabe. Ein frisch serialisierter String
        /// prüfte nur den Rundlauf des neuen Formats gegen sich selbst.
        /// </summary>
        private const string Alt =
            "[{\"X\":0,\"Y\":0,\"W\":0.5,\"H\":0.5},"
            + "{\"X\":0.5,\"Y\":0,\"W\":0.5,\"H\":0.5},"
            + "{\"X\":0,\"Y\":0.5,\"W\":1,\"H\":0.5}]";

        /// <summary>
        /// <b>Die tragende Zusicherung:</b> eine Altangabe verhält sich Zeichen für Zeichen wie
        /// vorher. Ohne eigene Schrittangabe ist die Position der Schritt.
        /// </summary>
        [TestMethod]
        public void OldAreasStillFallOneAtATime()
        {
            var alt = RevealAreas.Parse(Alt);

            Assert.AreEqual(3, alt.Count, "Die Altangabe liess sich nicht lesen.");
            Assert.IsTrue(alt.All(f => f.Step == null),
                "Eine gelesene Altflaeche traegt eine Schrittnummer - dann ist der Altbestand von "
                + "neuen Daten nicht mehr zu unterscheiden.");

            var verlauf = Enumerable.Range(0, 5)
                .Select(i => RevealAreas.NochVerdeckt(alt, i).Count())
                .ToArray();

            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0, 0 }, verlauf,
                "Die Flaechen fallen nicht mehr eine nach der anderen: "
                + string.Join(", ", verlauf));

            Assert.AreSame(alt[2], RevealAreas.NochVerdeckt(alt, 2).Single(),
                "Es faellt nicht die ERSTE Flaeche zuerst - dann deckt das Spiel in einer anderen "
                + "Reihenfolge auf, als der Spielleiter gezeichnet hat.");
        }

        /// <summary>
        /// Und der Rundlauf ist <b>zeichengleich</b>. Nur deshalb ändert das bloße Öffnen und
        /// Übernehmen einer Bestandsfrage die gespeicherte Angabe nicht.
        /// </summary>
        [TestMethod]
        public void AnUntouchedOldEntryComesBackCharacterForCharacter()
        {
            Assert.AreEqual(Alt, RevealAreas.ToJson(RevealAreas.Parse(Alt)),
                "Die zurueckgeschriebene Altangabe unterscheidet sich - dann aendert schon das "
                + "Oeffnen einer Bestandsfrage ihre Daten.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung nach oben:</b> greift die Erkennung zu weit und beachtet
        /// <c>Step</c> gar nicht, bleibt die Zusicherung oben grün - sie prüft dann nur eine
        /// Abwesenheit. Zwei Flächen in <i>einem</i> Schritt müssen zusammen fallen.
        /// </summary>
        [TestMethod]
        public void TwoAreasInOneStepFallTogether()
        {
            var beide = RevealAreas.Parse(
                "[{\"X\":0,\"Y\":0,\"W\":0.5,\"H\":0.5,\"Step\":0},"
                + "{\"X\":0.5,\"Y\":0,\"W\":0.5,\"H\":0.5,\"Step\":0}]");

            Assert.AreEqual(2, beide.Count);
            Assert.AreEqual(0, beide[0].Step,
                "Eine ausgeschriebene 0 kommt nicht als 0 an - dann ist sie von einer fehlenden "
                + "Angabe nicht zu unterscheiden.");

            Assert.AreEqual(2, RevealAreas.NochVerdeckt(beide, 0).Count());

            Assert.AreEqual(0, RevealAreas.NochVerdeckt(beide, 1).Count(),
                "Nach einem Schritt liegt noch eine Flaeche - die beiden gehoeren zusammen.");

            Assert.AreEqual(1, RevealAreas.Schrittzahl(beide),
                "Zwei Flaechen in einem Schritt ergeben nicht einen Schritt.");
        }

        /// <summary>
        /// <b>Ein alter Stand zeigt nie mehr als gewollt, höchstens weniger.</b>
        /// <para>
        /// Der Beweis hat drei Voraussetzungen - Schritte lückenlos ab 0, nach Schritt sortiert,
        /// mindestens eine Fläche je Schritt. <c>Normalisiert</c> erzwingt sie; diese Zusicherung
        /// misst das Ergebnis: auf jedem Schritt muss die <i>alte</i> Auslegung
        /// (<c>Skip</c> nach Position) eine Obermenge der neuen decken.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AnOlderBuildAlwaysCoversAtLeastAsMuch()
        {
            var neu = RevealAreas.Normalisiert(
            [
                new(0.0, 0.0, 0.3, 0.3, Step: 1),
                new(0.5, 0.0, 0.3, 0.3, Step: 0),
                new(0.0, 0.5, 0.3, 0.3, Step: 1),
                new(0.5, 0.5, 0.3, 0.3, Step: 0),
                new(0.2, 0.2, 0.3, 0.3, Step: 2),
            ]);

            for (var i = 0; i < neu.Count; i++)
            {
                Assert.IsTrue(RevealAreas.SchrittVon(neu[i], i) <= i,
                    $"Flaeche {i} traegt Schritt {neu[i].Step} - groesser als ihre Position. Damit "
                    + "deckt ein aelterer Stand WENIGER ab als gewollt, und die Frage verraet ein "
                    + "Stueck Bild.");
            }

            for (var schritt = 0; schritt <= RevealAreas.Schrittzahl(neu); schritt++)
            {
                var neueDeckung = RevealAreas.NochVerdeckt(neu, schritt).ToList();
                var alteDeckung = neu.Skip(schritt).ToList();

                foreach (var flaeche in neueDeckung)
                {
                    Assert.IsTrue(alteDeckung.Any(a => ReferenceEquals(a, flaeche)),
                        $"Auf Schritt {schritt} deckt die neue Auslegung eine Flaeche ab, die ein "
                        + "aelterer Stand schon weggenommen haette.");
                }
            }
        }

        /// <summary>
        /// <c>Normalisiert</c> schließt Lücken. Eine Frage, bei der ein ganzer Schritt gelöscht
        /// wurde, hätte sonst einen Bildschirm, auf dem sichtbar nichts passiert.
        /// </summary>
        [TestMethod]
        public void NormalisingClosesGapsBetweenSteps()
        {
            var mitLuecke = RevealAreas.Normalisiert(
            [
                new(0, 0, 0.3, 0.3, Step: 0),
                new(0.5, 0, 0.3, 0.3, Step: 7),
            ]);

            CollectionAssert.AreEqual(new[] { 0, 1 }, mitLuecke.Select(f => f.Step).ToArray(),
                "Die Luecke zwischen den Schritten steht noch - dann gibt es Bildschirme, auf "
                + "denen sichtbar nichts geschieht.");
        }

        /// <summary>
        /// <b>Die Hülle wird nachgezogen.</b> Sie ist der Notnagel für jeden Stand, der die Ecken
        /// nicht kennt; ist sie kleiner als das Vieleck, verrät die Frage ein Stück Bild - der
        /// einzige Weg, auf dem diese Fassung zu viel zeigen kann.
        /// </summary>
        [TestMethod]
        public void TheHullIsRecomputedFromTheCorners()
        {
            // Absichtlich falsch gesetzte Huelle: viel zu klein fuer das Dreieck.
            var falsch = new RevealArea(0.4, 0.4, 0.01, 0.01,
                Corners: [0.1, 0.1, 0.9, 0.2, 0.5, 0.8], Step: 0);

            var richtig = RevealAreas.Normalisiert([falsch]).Single();

            Assert.AreEqual(0.1, richtig.X, 1e-9, "Der linke Rand der Huelle stimmt nicht.");
            Assert.AreEqual(0.1, richtig.Y, 1e-9, "Der obere Rand der Huelle stimmt nicht.");
            Assert.AreEqual(0.8, richtig.W, 1e-9, "Die Huelle ist schmaler als das Vieleck.");
            Assert.AreEqual(0.7, richtig.H, 1e-9, "Die Huelle ist flacher als das Vieleck.");
        }

        /// <summary>
        /// Unbrauchbare Ecken werden übergangen, statt die Hülle zu verderben - und eine
        /// unlesbare Angabe kostet nach wie vor keinen Spielabend.
        /// </summary>
        [TestMethod]
        public void UnusableCornersLeaveTheHullAlone()
        {
            foreach (var ecken in new[] { "[0.1,0.2,0.3]", "[]" })
            {
                var gelesen = RevealAreas.Parse(
                    "[{\"X\":0.2,\"Y\":0.3,\"W\":0.4,\"H\":0.5,\"Corners\":" + ecken + "}]");

                var normal = RevealAreas.Normalisiert(gelesen).Single();

                Assert.AreEqual(0.2, normal.X, 1e-9,
                    $"Unbrauchbare Ecken {ecken} haben die Huelle verdorben.");
                Assert.AreEqual(0.4, normal.W, 1e-9);
            }

            Assert.AreEqual(0, RevealAreas.Parse("das ist kein JSON").Count);
        }

        /// <summary>
        /// <c>MitSchritt</c> nagelt die Taktung fest, <b>bevor</b> jemand eine Fläche entfernt.
        /// Ohne das verschiebt das Löschen der dritten von fünf Flächen lautlos den Schritt aller
        /// folgenden.
        /// </summary>
        [TestMethod]
        public void RemovingAnAreaDoesNotShiftTheOthers()
        {
            var geladen = RevealAreas.MitSchritt(RevealAreas.Parse(Alt));

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, geladen.Select(f => f.Step).ToArray(),
                "Beim Laden wurde die Taktung nicht festgeschrieben.");

            geladen.RemoveAt(1);

            CollectionAssert.AreEqual(new[] { 0, 2 }, geladen.Select(f => f.Step).ToArray(),
                "Das Entfernen hat den Schritt der uebrigen Flaechen verschoben.");
        }
    }
}
