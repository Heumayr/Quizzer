namespace Quizzer.Logic.Context
{
    /// <summary>
    /// Meldet jedes erfolgreiche Speichern, egal ueber welchen Controller. Der Hintergrund-Abgleich
    /// des WPF (M9: „speicherungen werden nebenbei immer an den server uebermittelt") haengt sich
    /// hier ein - statt an jede der rund sechzig Stellen, die speichern.
    /// </summary>
    public static class DataChangeNotifier
    {
        /// <summary>Wird nach jedem Speichern mit mindestens einer geaenderten Zeile ausgeloest.</summary>
        public static event EventHandler? Saved;

        internal static void RaiseSaved(int count)
        {
            if (count > 0)
                Saved?.Invoke(null, EventArgs.Empty);
        }
    }
}
