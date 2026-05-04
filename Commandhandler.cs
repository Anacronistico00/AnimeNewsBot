using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class CommandHandler
{
    private readonly TelegramBotClient _bot;
    private readonly BotState _state;
    private readonly SupabaseStorageService _storage;
    private readonly NewsService _newsService;
    private readonly HashSet<long> _adminIds;

    private static readonly Dictionary<int, string> ThreadNames = new()
    {
        { 2, "🎌 Anime" },
        { 3, "📚 Manga" },
        { 4, "🎮 Videogame & More" },
        { 157, "📰 News & Curiosità" },
    };

    public CommandHandler(
        TelegramBotClient bot,
        BotState state,
        SupabaseStorageService storage,
        NewsService newsService,
        string adminIdsRaw)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _newsService = newsService;

        _adminIds = adminIdsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => long.TryParse(id, out var parsed) ? parsed : 0L)
            .Where(id => id != 0)
            .ToHashSet();
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } text } message) return;

        var userId = message.From?.Id ?? 0;
        var chatId = message.Chat.Id;
        var threadId = message.MessageThreadId;

        if (!_adminIds.Contains(userId))
        {
            await _bot.SendMessage(chatId, "⛔ Non sei autorizzato ad usare i comandi.",
                messageThreadId: threadId, cancellationToken: ct);
            return;
        }

        var parts = text.Trim().Split(' ', 2);
        var command = parts[0].Split('@')[0].ToLower();
        var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        await (command switch
        {
            "/start" => HandleStart(chatId, threadId, ct),
            "/stop" => HandleStop(chatId, threadId, ct),
            "/status" => HandleStatus(chatId, threadId, ct),
            "/interval" => HandleInterval(chatId, threadId, arg, ct),
            "/clear" => HandleClear(chatId, threadId, ct),
            "/latest" => HandleLatest(chatId, threadId, ct),
            _ => Task.CompletedTask
        });
    }

    private async Task HandleStart(long chatId, int? threadId, CancellationToken ct)
    {
        if (_state.IsRunning)
        {
            await Reply(chatId, threadId, "▶️ Il bot è già attivo.", ct);
            return;
        }
        _state.IsRunning = true;
        await Reply(chatId, threadId, "▶️ Bot riavviato. Riprenderò a controllare le notizie.", ct);
    }

    private async Task HandleStop(long chatId, int? threadId, CancellationToken ct)
    {
        if (!_state.IsRunning)
        {
            await Reply(chatId, threadId, "⏹️ Il bot è già in pausa.", ct);
            return;
        }
        _state.IsRunning = false;
        await Reply(chatId, threadId, "⏹️ Bot messo in pausa. Usa /start per riavviarlo.", ct);
    }

    private async Task HandleStatus(long chatId, int? threadId, CancellationToken ct)
    {
        var status = _state.IsRunning ? "▶️ Attivo" : "⏹️ In pausa";
        var lastCheck = _state.LastCheckUtc == DateTime.MinValue
            ? "Mai"
            : _state.LastCheckUtc.ToString("HH:mm:ss") + " UTC";

        var msg = $"📊 <b>Stato Bot</b>\n\n" +
                  $"Stato: {status}\n" +
                  $"Intervallo polling: ogni {_state.IntervalMinutes} min\n" +
                  $"Ultimo check: {lastCheck}\n" +
                  $"Notizie inviate (sessione): {_state.TotalSent}";

        await Reply(chatId, threadId, msg, ct, ParseMode.Html);
    }

    private async Task HandleInterval(long chatId, int? threadId, string arg, CancellationToken ct)
    {
        if (!int.TryParse(arg, out var minutes) || minutes < 1 || minutes > 1440)
        {
            await Reply(chatId, threadId, "⚠️ Usa: /interval &lt;minuti&gt; (tra 1 e 1440)", ct, ParseMode.Html);
            return;
        }
        _state.IntervalMinutes = minutes;
        await Reply(chatId, threadId, $"⏱️ Intervallo aggiornato a {minutes} minuti.", ct);
    }

    private async Task HandleClear(long chatId, int? threadId, CancellationToken ct)
    {
        await _storage.ClearAllLinksAsync();
        await Reply(chatId, threadId, "🗑️ Cache svuotata. Al prossimo ciclo verranno reinviate le ultime notizie.", ct);
    }

    private async Task HandleLatest(long chatId, int? threadId, CancellationToken ct)
    {
        var items = await _newsService.PeekLatestAsync(8);
        if (items.Count == 0)
        {
            await Reply(chatId, threadId, "📭 Nessuna notizia disponibile.", ct);
            return;
        }

        // Raggruppa per topic per una risposta più leggibile
        var grouped = items.GroupBy(i => i.ThreadId);
        var sb = new System.Text.StringBuilder("📰 <b>Ultime notizie per categoria:</b>\n");

        foreach (var group in grouped)
        {
            var topicName = ThreadNames.GetValueOrDefault(group.Key, $"Topic {group.Key}");
            sb.AppendLine($"\n<b>{topicName}</b>");
            foreach (var item in group)
                sb.AppendLine($"• <a href=\"{item.Item.Link}\">{HtmlEncode(item.Item.Title)}</a>");
        }

        await Reply(chatId, threadId, sb.ToString(), ct, ParseMode.Html);
    }

    private Task Reply(long chatId, int? threadId, string text, CancellationToken ct, ParseMode parseMode = ParseMode.Html)
        => _bot.SendMessage(chatId, text, parseMode: parseMode, messageThreadId: threadId, cancellationToken: ct);

    private static string HtmlEncode(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}