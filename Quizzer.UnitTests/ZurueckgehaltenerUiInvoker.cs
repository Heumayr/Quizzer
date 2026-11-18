using Quizzer.Base;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Wie <see cref="InlineUiInvoker"/>, nur lässt sich die Warteschlange des Oberflächen-Threads nachbilden (P11-3): solange
    /// <see cref="Halte"/> gilt, warten <c>RunOnUiAsync</c>-Aufträge, bis <see cref="GibFreiAsync"/> sie in ihrer Reihenfolge
    /// ausführt. So kommt deterministisch etwas zwischen den Hub-Faden, der auflöst, und die Oberfläche, die es liest.
    /// </summary>
    internal sealed class ZurueckgehaltenerUiInvoker : IUiInvoker
    {
        private readonly object sperre = new();
        private readonly Queue<(Func<Task> Auftrag, TaskCompletionSource Fertig)> warteschlange = new();

        /// <summary>Ob <c>RunOnUiAsync</c> gerade wartet, statt sofort zu laufen.</summary>
        public bool Halte { get; set; }

        /// <summary>Wie viele Aufträge warten.</summary>
        public int Wartend
        {
            get { lock (sperre) return warteschlange.Count; }
        }

        public void Run(Action action) => action();

        public Task RunAsync(Action action) => RunAsync(() => { action(); return Task.CompletedTask; });

        public Task RunAsync(Func<Task> action)
        {
            lock (sperre)
            {
                if (Halte)
                {
                    var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    warteschlange.Enqueue((action, fertig));
                    return fertig.Task;
                }
            }

            return action();
        }

        /// <summary>Hebt das Halten auf und führt die wartenden Aufträge nacheinander aus.</summary>
        public async Task GibFreiAsync()
        {
            while (true)
            {
                (Func<Task> Auftrag, TaskCompletionSource Fertig) naechster;

                lock (sperre)
                {
                    Halte = false;

                    if (warteschlange.Count == 0)
                        return;

                    naechster = warteschlange.Dequeue();
                }

                try
                {
                    await naechster.Auftrag();
                    naechster.Fertig.SetResult();
                }
                catch (Exception ex)
                {
                    naechster.Fertig.SetException(ex);
                }
            }
        }
    }
}
