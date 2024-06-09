namespace A2SServer;

public class InfoUpdatedEventArgs(Info info) : EventArgs
{
    public Info Info { get; set; } = info;
}

public class RulesUpdatedEventArgs(
    Dictionary<string, string> modifiedRules,
    List<string> removedRules)
    : EventArgs
{
    public Dictionary<string, string> ModifiedRules { get; set; } = modifiedRules;
    public List<string> RemovedRules { get; set; } = removedRules;
}

public class PlayersUpdatedEventArgs(List<PlayerInfo> players) : EventArgs
{
    public List<PlayerInfo> Players { get; set; } = players;
}

public delegate void InfoUpdatedEventHandler(object sender, InfoUpdatedEventArgs args);

public delegate void RulesUpdatedEventHandler(object sender, RulesUpdatedEventArgs args);

public delegate void PlayersUpdatedEventHandler(object sender, PlayersUpdatedEventArgs args);

public class PlayerSimulation
{
    public enum Strategy
    {
        Static,
        Dynamic,
    }

    public enum Game
    {
        Generic,
        RisingStorm2,
    }

    private int _startScoreMin;
    private int _startScoreMax;
    private float _startDurationMinSeconds;
    private float _startDurationMaxSeconds;
    private Random _rng;
    private List<string> _playerNames;
    private byte _numPlayers;
    private WeightedRandomBag<int> _scoreDeltas;
    private TimeSpan _playerUpdateInterval;
    private System.Timers.Timer _playerUpdateTimer;
    private float _roundTime;
    private float _maxRoundTime;
    private Info _info;
    private Dictionary<string, string> _rules;
    private List<PlayerInfo> _simPlayers;

    private InfoUpdatedEventHandler? _onInfoUpdated;
    private RulesUpdatedEventHandler? _onRulesUpdated;
    private PlayersUpdatedEventHandler? _onPlayersUpdated;

    public event InfoUpdatedEventHandler InfoUpdated
    {
        add => _onInfoUpdated += value;
        remove => _onInfoUpdated -= value;
    }

    public event RulesUpdatedEventHandler RulesUpdated
    {
        add => _onRulesUpdated += value;
        remove => _onRulesUpdated -= value;
    }

    public event PlayersUpdatedEventHandler PlayersUpdated
    {
        add => _onPlayersUpdated += value;
        remove => _onPlayersUpdated -= value;
    }

    public void Start()
    {
        _playerUpdateTimer.Enabled = true;
    }

    private void OnPlayerUpdateTimerElapsed()
    {
        var intervalSecs = (float)_playerUpdateInterval.TotalSeconds;
        _roundTime += intervalSecs;

        if (_roundTime > _maxRoundTime)
        {
            // Console.WriteLine("resetting player simulation round");
            ResetPlayers();
        }
        else
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < _simPlayers.Count; ++i)
            {
                _simPlayers[i].Score += _scoreDeltas.GetRandom();
                _simPlayers[i].Duration += intervalSecs;
            }
        }

        // TODO: raise events.
    }

    private void ResetPlayers()
    {
        _roundTime = 0;
        _simPlayers.Clear();

        foreach (var name in RandomPlayerNames())
        {
            _simPlayers.Add(new PlayerInfo
            {
                Name = name,
                Score = _rng.Next(_startScoreMin, _startScoreMax),
                Duration = RandFloat(_rng, _startDurationMinSeconds, _startDurationMaxSeconds),
            });
        }
    }

    private IEnumerable<string> RandomPlayerNames()
    {
        var names = new List<string>(_playerNames);
        var rng = new Random();
        var i = 0;
        while (i < _numPlayers && names.Count > 0)
        {
            var idx = rng.Next(names.Count);
            yield return names[idx];
            names.RemoveAt(idx);
            ++i;
        }
    }

    private static float RandFloat(Random rng, float min, float max)
    {
        return (float)rng.NextDouble() * (max - min) + min;
    }
}