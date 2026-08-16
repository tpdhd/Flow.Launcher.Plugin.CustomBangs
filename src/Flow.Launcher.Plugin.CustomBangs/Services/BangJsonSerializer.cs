using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.CustomBangs.Models;

namespace Flow.Launcher.Plugin.CustomBangs.Services;

public static class BangJsonSerializer
{
    public const int CurrentVersion = 6;
    private const int MaxBangCount = 5_000;
    private const long MaxImportBytes = 5_000_000;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Export(IEnumerable<BangDefinition> bangs)
    {
        var envelope = new BangExportEnvelope
        {
            Version = CurrentVersion,
            Bangs = bangs.Select(static bang => bang.Clone()).ToList(),
        };

        var json = JsonSerializer.Serialize(envelope, WriteOptions);
        if (envelope.Bangs.Any(static bang => bang.IsGroup))
        {
            return json;
        }

        // Keep ordinary exports byte-shape compatible with the Chrome extension.
        // groupMembers is emitted only when the Flow-specific group feature is used.
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("The exported JSON could not be constructed.");
        foreach (var bang in root["bangs"]?.AsArray() ?? [])
        {
            bang?.AsObject().Remove("groupMembers");
        }

        return root.ToJsonString(WriteOptions);
    }

    public static ObservableCollection<BangDefinition> Import(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        if (!document.RootElement.TryGetProperty("version", out var versionElement) ||
            !versionElement.TryGetInt32(out var version))
        {
            throw new InvalidDataException("The JSON file has no numeric 'version' field.");
        }

        var imported = version switch
        {
            CurrentVersion => ImportVersion6(json),
            5 => ImportVersion5(document.RootElement),
            _ => throw new InvalidDataException($"Unsupported Custom Bang Search version: {version}. Supported versions are 5 and 6."),
        };

        if (imported.Count > MaxBangCount)
        {
            throw new InvalidDataException($"The import contains {imported.Count} bangs; the safety limit is {MaxBangCount}.");
        }

        return imported;
    }

    public static ObservableCollection<BangDefinition> ImportFile(string path)
    {
        var file = new FileInfo(path);
        if (file.Length > MaxImportBytes)
        {
            throw new InvalidDataException($"The import file is larger than {MaxImportBytes / 1_000_000} MB.");
        }

        return Import(File.ReadAllText(path));
    }

    private static ObservableCollection<BangDefinition> ImportVersion6(string json)
    {
        var envelope = JsonSerializer.Deserialize<BangExportEnvelope>(json, ReadOptions)
            ?? throw new InvalidDataException("The JSON file is empty or malformed.");

        if (envelope.Bangs is null)
        {
            throw new InvalidDataException("The JSON file has no 'bangs' array.");
        }

        foreach (var bang in envelope.Bangs)
        {
            Normalize(bang);
        }

        return new ObservableCollection<BangDefinition>(envelope.Bangs);
    }

    private static ObservableCollection<BangDefinition> ImportVersion5(JsonElement root)
    {
        if (!root.TryGetProperty("bangs", out var bangsElement) || bangsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The version 5 file has no 'bangs' array.");
        }

        var bangs = new ObservableCollection<BangDefinition>();
        foreach (var item in bangsElement.EnumerateArray())
        {
            var keyword = item.TryGetProperty("bang", out var keywordElement)
                ? keywordElement.GetString() ?? string.Empty
                : string.Empty;
            var urls = item.TryGetProperty("urls", out var urlsElement) && urlsElement.ValueKind == JsonValueKind.Array
                ? urlsElement.EnumerateArray().Select(static url => url.GetString() ?? string.Empty).ToList()
                : [];

            bangs.Add(new BangDefinition
            {
                Keyword = keyword,
                Urls = urls,
            });
        }

        return bangs;
    }

    private static void Normalize(BangDefinition bang)
    {
        bang.Keyword = bang.Keyword;
        bang.Alias = bang.Alias;
        bang.DefaultUrl = bang.DefaultUrl;
        bang.Urls = bang.Urls
            .Select(static url => url?.Trim() ?? string.Empty)
            .Where(static url => url.Length > 0)
            .ToList();
        bang.GroupMembers = bang.GroupMembers
            .Select(static member => member?.Trim() ?? string.Empty)
            .Where(static member => member.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class BangExportEnvelope
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("bangs")]
        public List<BangDefinition>? Bangs { get; set; }
    }
}
