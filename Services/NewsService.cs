using CodeHollow.FeedReader;

public class NewsService
{
    private readonly SupabaseStorageService _storage;

    private static readonly string[] RssFeeds =
    [
        "https://www.manganime.it/feed",
        "https://www.animenewsnetwork.com/all/rss.xml"
    ];

    private const int MaxItemsPerCycle = 10;

    public NewsService(SupabaseStorageService storage)
    {
        _storage = storage;
    }

    public async Task<List<FeedItem>> GetNewItemsAsync()
    {
        var sentLinks = await _storage.LoadSentLinksAsync();

        var feedTasks = RssFeeds.Select(url => FetchFeedSafeAsync(url));
        var allFeeds = await Task.WhenAll(feedTasks);

        var newItems = allFeeds
            .SelectMany(items => items)
            .Where(i => !string.IsNullOrWhiteSpace(i.Link) && !sentLinks.Contains(i.Link))
            .Take(MaxItemsPerCycle)
            .ToList();

        if (newItems.Count > 0)
            await _storage.SaveLinksAsync(newItems.Select(i => i.Link));

        return newItems;
    }

    public async Task<List<FeedItem>> PeekLatestAsync(int count)
    {
        var feedTasks = RssFeeds.Select(url => FetchFeedSafeAsync(url));
        var allFeeds = await Task.WhenAll(feedTasks);

        return allFeeds
            .SelectMany(items => items)
            .Take(count)
            .ToList();
    }

    private static async Task<IEnumerable<FeedItem>> FetchFeedSafeAsync(string url)
    {
        try
        {
            var feed = await FeedReader.ReadAsync(url);
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Feed OK: {url} ({feed.Items.Count} articoli)");
            return feed.Items;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Feed non raggiungibile: {url} — {ex.Message}");
            return Enumerable.Empty<FeedItem>();
        }
    }
}