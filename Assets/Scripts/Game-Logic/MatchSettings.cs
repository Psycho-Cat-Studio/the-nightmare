
/// <summary>Authors shared spawn cadence and the initial invasion-spawn delay.</summary>
[System.Serializable]
public class MatchSettings
{

    public float WaitForSpawn = 10f;

    public float SpawnIntervalMinimum = 1f;
    public float SpawnIntervalOnePlayer = 8f;
    public float SpawnIntervalTwoPlayers = 6f;
    public float SpawnIntervalThreePlayers = 4f;
    public float SpawnIntervalOtherPlayerCounts = 3f;

    /// <summary>Upper spawn-interval bound in seconds, preserving the original default branch for zero or four-plus players.</summary>
    public float SpawnIntervalMaximum(int activePlayers)
    {
        switch (activePlayers)
        {
            case 1: return SpawnIntervalOnePlayer;
            case 2: return SpawnIntervalTwoPlayers;
            case 3: return SpawnIntervalThreePlayers;
            default: return SpawnIntervalOtherPlayerCounts;
        }
    }
}
