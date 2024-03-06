namespace A2SServer;

public class PlayerSimulation
{
    enum Strategy
    {
        Static,
        Dynamic,
    }

    enum Game
    {
        Generic,
        RisingStorm2,
    }

    public static IEnumerable<string> RandomPlayerNames(byte players, List<string> playerNames)
    {
        var rng = new Random();
        var i = 0;
        while (i < players && playerNames.Count > 0)
        {
            var idx = rng.Next(playerNames.Count);
            yield return playerNames[idx];
            playerNames.RemoveAt(idx);
            ++i;
        }
    }
}
