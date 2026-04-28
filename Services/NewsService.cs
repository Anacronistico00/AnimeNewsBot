using CodeHollow.FeedReader;
using System.Text.Json;

public class NewsService
{
    private readonly string _filePath = "Storage/sent.json";
    private readonly string _rssUrl = "https://www.animenewsnetwork.com/all/rss.xml";

    public async Task<List<FeedItem>> GetNewItemsAsync()
    {
        var feed = await FeedReader.ReadAsync(_rssUrl);

        var sentLinks = LoadSentLinks();

        var newItems = feed.Items
            .Where(i => !sentLinks.Contains(i.Link))
            .ToList();

        foreach (var item in newItems)
        {
            sentLinks.Add(item.Link);
        }

        SaveSentLinks(sentLinks);

        return newItems;
    }

    private HashSet<string> LoadSentLinks()
    {
        if (!File.Exists(_filePath))
            return new HashSet<string>();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
    }

    private void SaveSentLinks(HashSet<string> links)
    {
        var json = JsonSerializer.Serialize(links);
        File.WriteAllText(_filePath, json);
    }
}
