public static class CategoryClassifier
{
    public const int General = 157;
    public const int Anime = 2;
    public const int Manga = 3;
    public const int Videogame = 4;

    private static readonly string[] AnimeKeywords =
    [
        "anime", " ova ", "o.v.a", " ona ", "o.n.a", "oav",
        "episodio", "episodi", "stagione", "simulcast",
        "crunchyroll", "hideive", "funimation", "anime generation", "d anime",
        "doppiaggio", "doppiato", "sottotitoli", "sub ita", "dub ita",
        "adattamento animato", "serie animata", "serie tv animata",
        "character design", "key visual", "visual key",
        "trailer anime", " pv ", "promotional video",
        "opening anime", "ending anime", " op ", " ed ",
        "seiyuu", "voice actor", "doppiatore",
        "studio animazione", "studio di animazione",
        "mappa stagionale", "palinsesto anime",
        "stagione anime", "anime stagione",
        "prima stagione", "seconda stagione", "terza stagione",
        "annuncio anime", "anime annunciato", "anime confermato",
        "anime in uscita", "anime primavera", "anime estate",
        "anime autunno", "anime inverno",
        "bones ", "shaft ", "a-1 pictures", "ufotable",
        "wit studio", "cloverworks", "j.c.staff",
        "trigger studio", "kyoto animation", "gainax", "sunrise studio",
        "toei animation", "madhouse", "production i.g",
    ];

    private static readonly string[] MangaKeywords =
    [
        "manga", "manhwa", "manhua", "webtoon",
        "capitolo", "capitoli", "chapter",
        "volume", "volumi", "tankōbon", "tankobon",
        "one-shot", "oneshot", "one shot",
        "serializzazione", "serializzato",
        "shonen", "shōnen", "shojo", "shōjo", "seinen", "josei",
        "fumetto giapponese", "fumetto orientale",
        "weekly jump", "shonen jump", "weekly shonen", "monthly shonen",
        "sunday", "magazine jump", "jump sq", "ultra jump",
        "bao publishing", "jpop manga", "j-pop manga",
        "star comics", "panini manga", "planet manga",
        "dynit manga", "gp manga", "hikari", "rw lion",
        "manga annunciato", "manga in arrivo", "manga italia",
        "uscite manga", "nuovo manga", "fine manga",
        "autore manga", "mangaka", "disegnatore manga",
        "licenza manga", "licenziato",
    ];

    private static readonly string[] VideogameKeywords =
    [
        "videogioco", "videogiochi", "video gioco", "video giochi",
        " gioco ", " giochi ", " game ", " gaming ",
        "playstation", " ps5 ", " ps4 ", " ps3 ",
        "xbox series", "xbox one", "xbox 360",
        "nintendo switch", "nintendo 3ds", "nintendo ds",
        "pc gaming", " steam ", "epic games store",
        " dlc ", "downloadable content", "espansione gioco",
        "patch gioco", "aggiornamento gioco", "update gioco",
        "trailer gioco", "gameplay trailer", "gameplay video",
        "recensione gioco", "review gioco",
        "uscita gioco", "data uscita gioco", "launch gioco",
        " rpg ", " jrpg ", " action rpg ",
        "action game", "indie game", "indie dev",
        "bandai namco", "koei tecmo",
        "square enix game", "capcom game", "konami game",
        "sega game", "atlus", "nippon ichi", "nisa",
        "level-5", "falcom", "marvelous",
        "giochi ps", "giochi xbox", "giochi nintendo",
        "giochi pc", "giochi switch",
        "playstation plus", "xbox game pass", "nintendo online",
        "trofei", "achievement", "trophy",
        "open world", "soulslike", "souls-like",
        "fighting game", "picchiaduro", "visual novel game",
        "gacha game", "mobile game",
    ];

    /// <summary>
    /// Classifica una notizia in base al titolo e alla descrizione.
    /// Ordine di priorità: Manga > Anime > Videogame > News & Curiosità
    /// </summary>
    public static int Classify(string title, string? description = null)
    {
        var text = (title + " " + (description?[..Math.Min(description.Length, 300)] ?? string.Empty))
            .ToLowerInvariant();

        if (MatchesAny(text, MangaKeywords)) return Manga;
        if (MatchesAny(text, AnimeKeywords)) return Anime;
        if (MatchesAny(text, VideogameKeywords)) return Videogame;

        return General;
    }

    private static bool MatchesAny(string text, string[] keywords)
        => keywords.Any(k =>
        {
            if (k.Length <= 4)
                return System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{k.Trim()}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return text.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase);
        });
}