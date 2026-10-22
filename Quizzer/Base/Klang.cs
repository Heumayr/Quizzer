using Quizzer.Logic.Spielablauf;
using System.IO;
using System.Media;

namespace Quizzer.Base
{
    /// <summary>
    /// Spielt die Töne aus <see cref="Toene"/> (V5, 2026-10-22) - auf dem Rechner, an dem der
    /// Beamer hängt. Abschaltbar in den Einstellungen.
    /// <para>
    /// <b>Wirft nie.</b> Ein Rechner ohne Tonausgabe (oder mit gesperrter) darf den Spielablauf
    /// nicht anhalten; dann ist es eben still.
    /// </para>
    /// </summary>
    public static class Klang
    {
        private static bool? aktiv;

        private static readonly Lazy<SoundPlayer> buzzer = new(() => Spieler(Toene.Buzzer()));
        private static readonly Lazy<SoundPlayer> tick = new(() => Spieler(Toene.Tick()));
        private static readonly Lazy<SoundPlayer> ende = new(() => Spieler(Toene.Ende()));

        public static bool Aktiv
        {
            get => aktiv ??= DataModels.UserSettings.Load().Toene;
            set => aktiv = value;
        }

        public static void Buzzer() => Spiele(buzzer);

        public static void Tick() => Spiele(tick);

        public static void Ende() => Spiele(ende);

        private static SoundPlayer Spieler(byte[] wav)
        {
            var spieler = new SoundPlayer(new MemoryStream(wav));
            spieler.Load();
            return spieler;
        }

        private static void Spiele(Lazy<SoundPlayer> spieler)
        {
            if (!Aktiv)
                return;

            try
            {
                // Play() spielt im Hintergrund und ersetzt einen noch laufenden Ton - zwei Buzzer
                // kurz hintereinander überlagern sich so nicht zu Brei.
                spieler.Value.Play();
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or TimeoutException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }
}
