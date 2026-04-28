/// <summary>
/// Stato condiviso thread-safe tra il loop RSS e il gestore comandi.
/// </summary>
public class BotState
{
    private bool _isRunning = true;
    private int _intervalMinutes;
    private DateTime _lastCheckUtc = DateTime.MinValue;
    private int _totalSent = 0;

    public BotState(int intervalMinutes)
    {
        _intervalMinutes = intervalMinutes;
    }

    public bool IsRunning
    {
        get => Volatile.Read(ref _isRunning);
        set => Volatile.Write(ref _isRunning, value);
    }

    public int IntervalMinutes
    {
        get => Volatile.Read(ref _intervalMinutes);
        set => Volatile.Write(ref _intervalMinutes, value);
    }

    public DateTime LastCheckUtc
    {
        get { lock (this) return _lastCheckUtc; }
        set { lock (this) _lastCheckUtc = value; }
    }

    public int TotalSent
    {
        get => Volatile.Read(ref _totalSent);
    }

    public void IncrementSent(int count) => Interlocked.Add(ref _totalSent, count);
}