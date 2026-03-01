using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace api.Services;

public class GeminiService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public GeminiService(IConfiguration config, HttpClient httpClient)
    {
        var configKey = config["GeminiApiKey"];
        _apiKey = string.IsNullOrEmpty(configKey)
            ? (Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "")
            : configKey;
        _httpClient = httpClient;
    }

    public async Task<string> GenerateInsight(string name, string type, string grade, double total, 
        double t, double f, double e, double o, int rvmCount, double evPct, double rePct)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "paste_your_key_here" || _apiKey == "your_gemini_api_key_here")
            return "Gemini API key not configured. Please add it to appsettings.json to see AI recommendations.";

        var prompt = $"""
            You are a sustainability advisor for The Coca-Cola Company. 
            Area: {name} ({type}). 
            Grade: {grade}. 
            Total emissions: {total} MTCO2e. 
            Breakdown: trucking {t}%, factory {f}%, energy {e}%, other {o}%. 
            Current RVMs deployed: {rvmCount}. 
            EV fleet: {evPct}%. 
            Renewable energy: {rePct}%. 
            Provide exactly 3 specific, actionable recommendations to reduce carbon footprint. 
            At least one must address RVM expansion and one must address electric vehicle fleet adoption. 
            Be concise and practical.
            """;

        return await CallGemini(prompt);
    }

    public async Task<string> QueryWithContext(string areaName, string grade, double totalEmissions, string userQuestion)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "paste_your_key_here" || _apiKey == "your_gemini_api_key_here")
            return "Gemini API key not configured. I'm unable to answer your question at this time.";

        var prompt = $"""
            Context: You are a sustainability advisor for The Coca-Cola Company. 
            The current focus is {areaName}, which has a sustainability grade of '{grade}' and total emissions of {totalEmissions} MTCO2e.
            
            User Question: {userQuestion}
            
            Provide a professional, helpful, and concise answer based on the context of Coca-Cola's sustainability goals.
            """;

        return await CallGemini(prompt);
    }

    private async Task<string> CallGemini(string prompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
        
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 2000
            }
        };

        try
        {
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            // Navigate to: candidates[0].content.parts[0].text
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? "No response generated.";
        }
        catch (Exception ex)
        {
            return $"Error calling Gemini: {ex.Message}";
        }
    }
}
