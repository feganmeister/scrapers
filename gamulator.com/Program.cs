using Serilog;
using System.Text.RegularExpressions;

var platform = "nintendo-ds";
var totalPages = 112;

var rootRomDirectory = @$"C:\Users\fegan\Downloads\ROMs\{platform}";
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(rootRomDirectory, "log.txt"),
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();
var baseUrl = "https://www.gamulator.com";
var baseDownloadUrl = "https://downloads.gamulator.com";
var controller = $"/roms/{platform}";
var query = "?currentpage=";
var http = new HttpClient();
for (var i = 1; i <= totalPages; i++)
{
    Log.Verbose($"Starting page {i} of {totalPages}");
    var romList = await http.GetAsync($"{baseUrl}{controller}{query}{i}").Result.Content.ReadAsStringAsync();
    var body = Helpers.ExtractBody(romList);
    var links = Helpers.ExtractLinks(body, controller);
    Log.Information($"Found {links.Count} links on page {i}");
    foreach (var link in links)
    {
        Log.Verbose($"Processing page {link}");
        var downLoadPage = await http.GetAsync($"{baseUrl}{link}/download").Result.Content.ReadAsStringAsync();
        var downLoadBody = Helpers.ExtractBody(downLoadPage);
        var downLoadLinks = Helpers.ExtractLinks(downLoadBody, baseDownloadUrl);
        Log.Information($"Found {downLoadLinks.Count} download links on {link}");
        foreach (var downLoadLink in downLoadLinks)
        {
            Log.Verbose($"Processing download link {downLoadLink}");
            await DownloadFileAsync(downLoadLink, Path.GetFileName($"{downLoadLink.Split('/').Last()}"), rootRomDirectory, http);
        }
    }
    Console.WriteLine($"Page {i} done with {links.Count} links");
}

static async Task DownloadFileAsync(string url, string fileName, string rootRomDirectory, HttpClient http)
{
    Log.Information($"Downloading {url} to {fileName}");
    try {
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await using var ms = await response.Content.ReadAsStreamAsync();
        var filePath = Path.Combine(rootRomDirectory, fileName);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await ms.CopyToAsync(fs);
    }
    catch (Exception ex) {
        Log.Error(ex, $"Failed to download {url}");
    }
}

class Helpers
{

    public static string ExtractBody(string html)
    {
        var bodyRegex = new Regex("<body[^>]*>(.*?)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var match = bodyRegex.Match(html);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public static List<string> ExtractLinks(string html, string controller)
    {
        var linkRegex = new Regex("<a[^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = linkRegex.Matches(html);
        var links = new HashSet<string>(); // Use HashSet to ensure uniqueness
        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;
            if (!href.StartsWith($"{controller}/")) { continue; }
            links.Add(href);
        }
        return links.ToList();
    }

}