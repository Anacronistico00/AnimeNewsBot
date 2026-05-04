using CodeHollow.FeedReader;

public record CategorizedFeedItem(FeedItem Item, int ThreadId);

public class NewsService
{
    private readonly SupabaseStorageService _storage;

    private static readonly string[] RssFeeds =
    [
        "https://www.animenewsnetwork.com/all/rss.xml?ann-edition=it",
        "https://animeworld02.webnode.it/rss/all.xml",
        "https://www.manganime.it/feed",
        "https://akibagamers.it/feed",
        "https://www.everyeye.it/feed/feed_news_rss.asp",
        "https://www.gamesource.it/feed/",
        "https://notizianime.altervista.org/feed/",
        "https://www.fumetti-anime-and-gadget.com/feed/",
    ];

    private const int MaxItemsPerCycle = 5;

    public NewsService(SupabaseStorageService storage)
    {
        _storage = storage;
    }

    public async Task<List<CategorizedFeedItem>> GetNewItemsAsync()
    {
        var sentLinks = await _storage.LoadSentLinksAsync();

        var feedTasks = RssFeeds.Select(FetchFeedSafeAsync);
        var allFeeds = await Task.WhenAll(feedTasks);

        var seenInThisCycle = new HashSet<string>();

        var newItems = allFeeds
            .SelectMany(items => items)
            .Where(i =>
            {
                if (string.IsNullOrWhiteSpace(i.Item.Link)) return false;
                if (sentLinks.Contains(i.Item.Link)) return false;
                return seenInThisCycle.Add(i.Item.Link);
            })
            .Reverse()
            .Take(MaxItemsPerCycle)
            .ToList();

        if (newItems.Count > 0)
            await _storage.SaveLinksAsync(newItems.Select(i => (i.Item.Link, i.ThreadId)));

        return newItems;
    }

    public async Task<List<CategorizedFeedItem>> PeekLatestAsync(int count)
    {
        var feedTasks = RssFeeds.Select(FetchFeedSafeAsync);
        var allFeeds = await Task.WhenAll(feedTasks);

        var seenInThisCycle = new HashSet<string>();

        return allFeeds
            .SelectMany(items => items)
            .Where(i => !string.IsNullOrWhiteSpace(i.Item.Link) && seenInThisCycle.Add(i.Item.Link))
            .Take(count)
            .ToList();
    }

    private static async Task<IEnumerable<CategorizedFeedItem>> FetchFeedSafeAsync(string url)
    {
        try
        {
            var feed = await FeedReader.ReadAsync(url);
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Feed OK: {url} ({feed.Items.Count} articoli)");

            return feed.Items.Select(item =>
            {
                var threadId = CategoryClassifier.Classify(item.Title, item.Description);
                return new CategorizedFeedItem(item, threadId);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Feed non raggiungibile: {url} — {ex.Message}");
            return Enumerable.Empty<CategorizedFeedItem>();
        }
    }
}