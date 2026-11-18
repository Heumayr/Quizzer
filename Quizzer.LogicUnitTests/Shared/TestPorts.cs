namespace Quizzer.TestShared
{
    /// <summary>
    /// Jeder feste Port eines Testservers, über alle drei Testprojekte in einer Liste (V10). Heute laufen die
    /// Projekte nacheinander; ein doppelter Port wäre ein Wettlauf, sobald sie es nicht mehr tun. Wer einen
    /// freien Port braucht und ihn nicht kennen muss, nimmt 0. Gehalten von <c>TestPortsUnitTests</c>: jeder
    /// Wert einmal, jeder Eintrag in genau einer Datei, kein fester Port daneben.
    /// </summary>
    internal static class TestPorts
    {
        // Quizzer.LogicUnitTests
        public const int JoinEndToEnd = 5397;
        public const int BuzzerEndToEnd = 5399;
        public const int BuzzerReconnect = 5401;
        public const int Zeitsperre = 5405;
        public const int ZockerHub = 5409;
        public const int MinispielHub = 5411;
        public const int SpielerwahlHub = 5415;
        public const int Antwortsperre = 5419;
        public const int GluecksradHub = 5425;
        public const int KopfrechnenHub = 5433;
        public const int MemoryHub = 5435;
        public const int BlitzrundeHub = 5437;
        public const int BuchstabensalatHub = 5439;
        public const int ErklaerbaerHub = 5441;
        public const int KartoffelHub = 5443;
        public const int JokerHub = 5447;
        public const int AngeboteneAdressen = 5449;
        public const int TeamHub = 5451;
        public const int FinaleHub = 5471;

        // Quizzer.UnitTests
        public const int BuzzerHandshake = 5403;
        public const int BuzzerPortBelegt = 5407;
        public const int ReaktionSpielleiter = 5413;
        public const int SonderregelSpielleiter = 5417;
        public const int SonderregelOhneVorphase = 5421;
        public const int SonderregelAuktion = 5423;
        public const int SonderregelExperte = 5427;
        public const int SonderregelAllianz = 5429;
        public const int SonderregelKopfgeld = 5431;
        public const int JokerRunde = 5445;
        public const int TeamBuzzer = 5453;
        public const int Teamberatung = 5455;
        public const int Finale = 5473;
        public const int FinaleTeam = 5475;
        public const int GleichGedacht = 5477;
        public const int AlleOderKeiner = 5479;
        public const int TeamAbstimmung = 5481;
        public const int Tauziehen = 5483;
        public const int Stellvertreter = 5485;
        public const int Staffel = 5487;
        public const int RundeZuruecksetzen = 5489;

        // Quizzer.Server.Tests: keine festen Ports (In-Memory-Server, sonst Port 0).
    }
}
