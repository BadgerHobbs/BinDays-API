#r "nuget: System.Text.Json, 8.0.3"

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;

if (args.Length != 2)
{
    Console.WriteLine("Usage: dotnet script FilterHar.csx <input.har> <output.har>");
    return;
}

var inputFile = args[0];
var outputFile = args[1];

var allowedMimeTypes = new HashSet<string>
{
    "text/html",
    "application/json",
    "application/javascript",
    "text/javascript",
    "application/x-www-form-urlencoded"
};

try
{
    var harContent = File.ReadAllText(inputFile);
    var harJson = JsonNode.Parse(harContent);

    if (harJson?["log"]?["entries"] is not JsonArray entries)
    {
        Console.WriteLine("Error: Could not find 'log.entries' array in the HAR file.");
        return;
    }

    var filteredEntries = new JsonArray();
    foreach (var entry in entries)
    {
        var mimeType = entry?["response"]?["content"]?["mimeType"]?.GetValue<string>();
        if (mimeType != null && allowedMimeTypes.Any(allowed => mimeType.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)))
        {
            filteredEntries.Add(entry.DeepClone());
        }
    }

    harJson["log"]["entries"] = filteredEntries;

    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(outputFile, harJson.ToJsonString(options));

    Console.WriteLine($"Successfully filtered {entries.Count} entries down to {filteredEntries.Count} and saved to {outputFile}");
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
}
