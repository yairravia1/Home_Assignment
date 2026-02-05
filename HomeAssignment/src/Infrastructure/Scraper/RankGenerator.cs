namespace HomeAssignment.Infrastructure.Scraper;

public class RankGenerator
{
    private readonly Queue<int> _availableRanks;

    public RankGenerator(HashSet<int> usedRanks, int maxRank)
    {
        var available = Enumerable.Range(1, maxRank)
            .Where(rank => !usedRanks.Contains(rank))
            .ToList();

        var random = new Random();
        for (var i = available.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (available[i], available[j]) = (available[j], available[i]);
        }

        _availableRanks = new Queue<int>(available);
    }

    public int? TryGetNextRank()
    {
        return _availableRanks.Count > 0 ? _availableRanks.Dequeue() : (int?)null;
    }
}