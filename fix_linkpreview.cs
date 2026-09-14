using System.IO;

var content = File.ReadAllText("SRNSMudApp/Services/LinkPreviewService.cs");
var newMethod = @"    private async Task<LinkPreviewData> GetUserPreviewAsync(string userId, string originalUrl)
    {
        if (_cache.TryGetValue(originalUrl, out var cachedData))
        {
            return cachedData;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return new LinkPreviewData { Url = originalUrl, IsSuccess = false };
        }

        var preview = new LinkPreviewData
        {
            Url = originalUrl,
            Title = $""User: {user.UserName}"",
            Description = $""@{user.UserName}"",
            SiteName = ""SRNSMudApp"",
            IsSuccess = true
        };

        _cache[originalUrl] = preview;
        return preview;
    }
";

content = content.Replace("// Temp marker, will rewrite using perl", newMethod);
File.WriteAllText("SRNSMudApp/Services/LinkPreviewService.cs", content);
