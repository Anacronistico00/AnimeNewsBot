using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;

if (File.Exists(".env"))
    Env.Load();

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
var chatIdRaw = Environment.GetEnvironmentVariable("CHAT_ID");
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY");
var adminIdsRaw = Environment.GetEnvironmentVariable("ADMIN_IDS") ?? string.Empty;
var pollInterval = int.TryParse(Environment.GetEnvironmentVariable("POLL_INTERVAL_MINUTES"), out var pi) ? pi : 10;

if (string.IsNullOrWhiteSpace(token)) { Console.WriteLine("[ERRORE] BOT_TOKEN mancante."); return; }
if (string.IsNullOrWhiteSpace(chatIdRaw) || !long.TryParse(chatIdRaw, out var chatId)) { Console.WriteLine("[ERRORE] CHAT_ID mancante o non valido."); return; }
if (string.IsNullOrWhiteSpace(supabaseUrl)) { Console.WriteLine("[ERRORE] SUPABASE_URL mancante."); return; }
if (string.IsNullOrWhiteSpace(supabaseKey)) { Console.WriteLine("[ERRORE] SUPABASE_KEY mancante."); return; }
if (string.IsNullOrWhiteSpace(adminIdsRaw)) { Console.WriteLine("[WARN] ADMIN_IDS non configurato."); }

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

var bot = new TelegramBotClient(token);
var storage = new SupabaseStorageService(supabaseUrl, supabaseKey);
var newsService = new NewsService(storage);
var state = new BotState(pollInterval);
var cmdHandler = new CommandHandler(bot, state, storage, newsService, adminIdsRaw);

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// porta Render
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://0.0.0.0:{port}");

// endpoint minimo per Render
app.MapGet("/", () => "Bot attivo");

// avvia server in background
_ = app.RunAsync(cts.Token);


var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message],
    DropPendingUpdates = true
};

bot.StartReceiving(
    updateHandler: async (_, update, ct) =>
    {
        try { await cmdHandler.HandleUpdateAsync(update, ct); }
        catch (Exception ex) { Console.WriteLine($"[WARN] Errore gestione comando: {ex.Message}"); }
    },
    errorHandler: (_, ex, _, ct) =>
    {
        if (ex is not OperationCanceledException)
            Console.WriteLine($"[WARN] Errore polling Telegram: {ex.Message}");
        return Task.CompletedTask;
    },
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine($"Bot avviato. Polling RSS ogni {state.IntervalMinutes} min.");

while (!cts.Token.IsCancellationRequested)
{
    if (state.IsRunning)
    {
        try
        {
            state.LastCheckUtc = DateTime.UtcNow;
            var news = await newsService.GetNewItemsAsync();

            if (news.Count == 0)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Nessuna nuova notizia.");
            }

            foreach (var item in news)
            {
                if (cts.Token.IsCancellationRequested) break;

                try
                {
                    // Formato HTML: niente problemi di escape
                    var message = $"📰 <b>{HtmlEncode(item.Title)}</b>\n<a href=\"{item.Link}\">Leggi articolo</a>";
                    await bot.SendMessage(chatId, message, parseMode: ParseMode.Html);
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Inviato: {item.Title}");
                    state.IncrementSent(1);

                    await Task.Delay(500, cts.Token);
                }
                catch (ApiRequestException ex)
                {
                    Console.WriteLine($"[WARN] Errore Telegram per '{item.Title}': {ex.Message}");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Errore inaspettato durante invio: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRORE] Ciclo RSS: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Bot in pausa, skip ciclo RSS.");
    }

    try
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Attendo {state.IntervalMinutes} min...");
        await Task.Delay(TimeSpan.FromMinutes(state.IntervalMinutes), cts.Token);
    }
    catch (OperationCanceledException) { break; }
}

Console.WriteLine("Bot terminato.");

static string HtmlEncode(string text) =>
    text.Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");