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

        if (!_adminIds.Contains(userId))
        {
            await _bot.SendMessage(chatId, "⛔ Non sei autorizzato ad usare i comandi.", cancellationToken: ct);
            return;
        }

        var parts = text.Trim().Split(' ', 2);
        var command = parts[0].Split('@')[0].ToLower();
        var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        await (command switch
        {
            "/start" => HandleStart(chatId, ct),
            "/stop" => HandleStop(chatId, ct),
            "/status" => HandleStatus(chatId, ct),
            "/interval" => HandleInterval(chatId, arg, ct),
            "/clear" => HandleClear(chatId, ct),
            "/latest" => HandleLatest(chatId, ct),
            _ => Task.CompletedTask
        });
    }

    private async Task HandleStart(long chatId, CancellationToken ct)
    {
        if (_state.IsRunning)
        {
            await _bot.SendMessage(chatId, "▶️ Il bot è già attivo.", cancellationToken: ct);
            return;
        }
        _state.IsRunning = true;
        await _bot.SendMessage(chatId, "▶️ Bot riavviato. Riprenderò a controllare le notizie.", cancellationToken: ct);
    }

    private async Task HandleStop(long chatId, CancellationToken ct)
    {
        if (!_state.IsRunning)
        {
            await _bot.SendMessage(chatId, "⏹️ Il bot è già in pausa.", cancellationToken: ct);
            return;
        }
        _state.IsRunning = false;
        await _bot.SendMessage(chatId, "⏹️ Bot messo in pausa. Usa /start per riavviarlo.", cancellationToken: ct);
    }

    private async Task HandleStatus(long chatId, CancellationToken ct)
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

        await _bot.SendMessage(chatId, msg, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private async Task HandleInterval(long chatId, string arg, CancellationToken ct)
    {
        if (!int.TryParse(arg, out var minutes) || minutes < 1 || minutes > 1440)
        {
            await _bot.SendMessage(chatId, "⚠️ Usa: /interval &lt;minuti&gt; (tra 1 e 1440)", parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }
        _state.IntervalMinutes = minutes;
        await _bot.SendMessage(chatId, $"⏱️ Intervallo aggiornato a {minutes} minuti.", cancellationToken: ct);
    }

    private async Task HandleClear(long chatId, CancellationToken ct)
    {
        await _storage.ClearAllLinksAsync();
        await _bot.SendMessage(chatId, "🗑️ Cache svuotata. Al prossimo ciclo verranno reinviate le ultime notizie.", cancellationToken: ct);
    }

    private async Task HandleLatest(long chatId, CancellationToken ct)
    {
        var items = await _newsService.PeekLatestAsync(5);
        if (items.Count == 0)
        {
            await _bot.SendMessage(chatId, "📭 Nessuna notizia disponibile.", cancellationToken: ct);
            return;
        }

        var lines = items.Select((item, i) => $"{i + 1}. <a href=\"{item.Link}\">{HtmlEncode(item.Title)}</a>");
        var msg = "📰 <b>Ultime notizie dal feed:</b>\n\n" + string.Join("\n", lines);
        await _bot.SendMessage(chatId, msg, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private static string HtmlEncode(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}