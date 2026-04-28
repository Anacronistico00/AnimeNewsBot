using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class SupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private const string TableName = "sent_links";

    public SupabaseStorageService(string supabaseUrl, string supabaseKey)
    {
        _baseUrl = supabaseUrl.TrimEnd('/');
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", supabaseKey);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
    }

    public async Task<HashSet<string>> LoadSentLinksAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/rest/v1/{TableName}?select=link");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<List<SentLinkRow>>(json) ?? new();
            return rows.Select(r => r.link).ToHashSet();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Errore caricamento link da Supabase: {ex.Message}");
            return new HashSet<string>();
        }
    }

    public async Task SaveLinksAsync(IEnumerable<string> links)
    {
        try
        {
            var rows = links.Select(l => new SentLinkRow { link = l }).ToList();
            var json = JsonSerializer.Serialize(rows);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/rest/v1/{TableName}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("Prefer", "resolution=ignore-duplicates");
            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Errore salvataggio link su Supabase: {ex.Message}");
        }
    }

    public async Task ClearAllLinksAsync()
    {
        try
        {
            // DELETE senza filtri — cancella tutto
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/rest/v1/{TableName}?link=neq.null");
            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Console.WriteLine("[INFO] Cache Supabase svuotata.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Errore durante clear su Supabase: {ex.Message}");
        }
    }

    private class SentLinkRow
    {
        public string link { get; set; } = string.Empty;
    }
}