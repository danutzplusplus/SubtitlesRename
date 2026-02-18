using System.Text.RegularExpressions;

if (args.Length < 1)
{
    Console.WriteLine("Usage: SubtitleRenamer <directory> [--dry-run] [--reverse]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --dry-run   Preview renames without making changes.");
    Console.WriteLine("  --reverse   Rename video files to match subtitle filenames.");
    return 1;
}

string directory = args[0];
bool dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
bool reverse = args.Contains("--reverse", StringComparer.OrdinalIgnoreCase);

if (!Directory.Exists(directory))
{
    Console.WriteLine($"Error: Directory not found: {directory}");
    return 1;
}

Console.WriteLine(reverse
    ? "Mode: Rename video files to match subtitle filenames."
    : "Mode: Rename subtitle files to match video filenames.");
Console.WriteLine();

// Supported video extensions
HashSet<string> videoExtensions = new([".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v"], StringComparer.OrdinalIgnoreCase);

Regex[] episodePatterns =
[
    new Regex(@"S(\d{1,2})E(\d{1,3})(?:E\d{1,3})*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    new Regex(@"(\d{1,2})x(\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    new Regex(@"Season[\.\s_-]*(\d{1,2})[\.\s_-]*Episode[\.\s_-]*(\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
];

string? ExtractEpisodeKey(string filename)
{
    foreach (var pattern in episodePatterns)
    {
        var match = pattern.Match(filename);
        if (match.Success)
        {
            string season = match.Groups[1].Value.PadLeft(2, '0');
            string episode = match.Groups[2].Value.PadLeft(2, '0');
            return $"S{season}E{episode}";
        }
    }
    return null;
}

var allFiles = Directory.GetFiles(directory);

Dictionary<string, List<string>> videosByKey = new(StringComparer.OrdinalIgnoreCase);
foreach (string file in allFiles)
{
    string ext = Path.GetExtension(file);
    if (!videoExtensions.Contains(ext))
        continue;

    string key = ExtractEpisodeKey(Path.GetFileName(file)) ?? string.Empty;
    if (string.IsNullOrEmpty(key))
    {
        Console.WriteLine($"  SKIP (no pattern): {Path.GetFileName(file)}");
        continue;
    }

    if (!videosByKey.TryGetValue(key, out var list))
    {
        list = [];
        videosByKey[key] = list;
    }
    list.Add(file);
}

Dictionary<string, List<string>> subtitlesByKey = new(StringComparer.OrdinalIgnoreCase);
foreach (string file in allFiles)
{
    if (!Path.GetExtension(file).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        continue;

    string key = ExtractEpisodeKey(Path.GetFileName(file)) ?? string.Empty;
    if (string.IsNullOrEmpty(key))
    {
        Console.WriteLine($"  SKIP (no pattern): {Path.GetFileName(file)}");
        continue;
    }

    if (!subtitlesByKey.TryGetValue(key, out var list))
    {
        list = [];
        subtitlesByKey[key] = list;
    }
    list.Add(file);
}

int renamed = 0;
int skipped = 0;

foreach (var (key, subtitles) in subtitlesByKey)
{
    if (!videosByKey.TryGetValue(key, out var videos))
    {
        Console.WriteLine($"  SKIP [{key}]: No matching video found for {string.Join(", ", subtitles.Select(Path.GetFileName))}");
        skipped += subtitles.Count;
        continue;
    }

    if (videos.Count > 1)
    {
        Console.WriteLine($"  SKIP [{key}]: Ambiguous — multiple videos match: {string.Join(", ", videos.Select(Path.GetFileName))}");
        skipped += subtitles.Count;
        continue;
    }

    if (subtitles.Count > 1)
    {
        Console.WriteLine($"  SKIP [{key}]: Ambiguous — multiple subtitles match: {string.Join(", ", subtitles.Select(Path.GetFileName))}");
        skipped += subtitles.Count;
        continue;
    }

    string videoFile = videos[0];
    string subtitleFile = subtitles[0];

    string sourceFile = reverse ? subtitleFile : videoFile;
    string targetFile = reverse ? videoFile : subtitleFile;

    string sourceNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFile);
    string targetExt = Path.GetExtension(targetFile);
    string newTargetPath = Path.Combine(directory, sourceNameWithoutExt + targetExt);

    if (string.Equals(targetFile, newTargetPath, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  OK   [{key}]: Already named correctly — {Path.GetFileName(targetFile)}");
        continue;
    }

    if (File.Exists(newTargetPath))
    {
        Console.WriteLine($"  SKIP [{key}]: Target already exists — {Path.GetFileName(newTargetPath)}");
        skipped++;
        continue;
    }

    if (dryRun)
    {
        Console.WriteLine($"  PREVIEW [{key}]: {Path.GetFileName(targetFile)} → {Path.GetFileName(newTargetPath)}");
    }
    else
    {
        File.Move(targetFile, newTargetPath);
        Console.WriteLine($"  RENAMED [{key}]: {Path.GetFileName(targetFile)} → {Path.GetFileName(newTargetPath)}");
    }
    renamed++;
}

Console.WriteLine();
Console.WriteLine($"Done. {(dryRun ? "Would rename" : "Renamed")}: {renamed}, Skipped: {skipped}");
return 0;