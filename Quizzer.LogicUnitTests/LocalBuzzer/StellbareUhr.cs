namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>Eine Serveruhr zum Vorstellen (M35 C4): monoton, steht still, bis der Test sie bewegt.</summary>
    internal sealed class StellbareUhr : TimeProvider
    {
        private long ticks = TimeSpan.FromHours(1).Ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref ticks);

        public void Vor(TimeSpan dauer) => Interlocked.Add(ref ticks, dauer.Ticks);
    }
}
