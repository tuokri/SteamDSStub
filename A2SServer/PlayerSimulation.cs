/*
 * Copyright (C) 2023-2024  Tuomo Kriikkula
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Timer = System.Timers.Timer;

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
    private Timer _playerUpdateTimer;
    private float _roundTime;
    private float _maxRoundTime;
    private Info _info;
    private Dictionary<string, string> _rules;
    private List<PlayerInfo> _simPlayers;

    private InfoUpdatedEventHandler? _onInfoUpdated;
    private RulesUpdatedEventHandler? _onRulesUpdated;
    private PlayersUpdatedEventHandler? _onPlayersUpdated;

    // TODO: having a constructor is cumbersome?
    // public PlayerSimulation(int startScoreMin, int startScoreMax, float startDurationMinSeconds, float startDurationMaxSeconds, Random rng, List<string> playerNames, byte numPlayers, WeightedRandomBag<int> scoreDeltas, TimeSpan playerUpdateInterval, Timer playerUpdateTimer, float roundTime, float maxRoundTime, Info info, Dictionary<string, string> rules, List<PlayerInfo> simPlayers, InfoUpdatedEventHandler? onInfoUpdated, RulesUpdatedEventHandler? onRulesUpdated, PlayersUpdatedEventHandler? onPlayersUpdated)
    // {
    //     _startScoreMin = startScoreMin;
    //     _startScoreMax = startScoreMax;
    //     _startDurationMinSeconds = startDurationMinSeconds;
    //     _startDurationMaxSeconds = startDurationMaxSeconds;
    //     _rng = rng;
    //     _playerNames = playerNames;
    //     _numPlayers = numPlayers;
    //     _scoreDeltas = scoreDeltas;
    //     _playerUpdateInterval = playerUpdateInterval;
    //     _playerUpdateTimer = playerUpdateTimer;
    //     _roundTime = roundTime;
    //     _maxRoundTime = maxRoundTime;
    //     _info = info;
    //     _rules = rules;
    //     _simPlayers = simPlayers;
    //     _onInfoUpdated = onInfoUpdated;
    //     _onRulesUpdated = onRulesUpdated;
    //     _onPlayersUpdated = onPlayersUpdated;
    // }

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