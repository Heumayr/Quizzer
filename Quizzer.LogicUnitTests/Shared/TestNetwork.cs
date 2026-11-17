namespace Quizzer.TestShared
{
    /// <summary>
    /// Netz der Testserver, für alle Testprojekte in einer Datei (eingebunden per <c>Compile Include</c>).
    /// </summary>
    internal static class TestNetwork
    {
        /// <summary>
        /// Jeder Testserver bindet nur hier (V14). Auf allen Adressen fragt Windows je neuem Arbeitsbaum nach
        /// dem Netzzugriff, und der Testserver stünde im Heimnetz. Gehalten von <c>TestServerLoopbackUnitTests</c>.
        /// </summary>
        public const string Address = "127.0.0.1";
    }
}
