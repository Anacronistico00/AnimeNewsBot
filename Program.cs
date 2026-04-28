using Telegram.Bot;
using DotNetEnv;

Env.Load();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
var chatId = long.Parse(Environment.GetEnvironmentVariable("CHAT_ID")!);

var bot = new TelegramBotClient(token);
var newsService = new NewsService();

Console.WriteLine("Bot avviato...");

while (true)
{
    var news = await newsService.GetNewItemsAsync();

    foreach (var item in news)
    {
        var message = $"📰 {item.Title}\n{item.Link}";
        await bot.SendMessage(chatId, message);

        Console.WriteLine($"Inviato: {item.Title}");
    }

    Console.WriteLine("Attendo 10 minuti...");
    await Task.Delay(TimeSpan.FromMinutes(1));
}
