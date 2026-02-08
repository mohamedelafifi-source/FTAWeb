using System.Text.Json;
using System.Text.RegularExpressions;

namespace FTAWeb.Services;

/// <summary>
/// Parses text lines (NAME: x; PARENTS: y; SPOUSES: ...; SIBLINGS: ...; CHILDREN: ...)
/// and generates tree JSON compatible with the existing viewer.
/// </summary>
public static class ImportTreeService
{
    private static readonly Regex LineRegex = new(
        @"(NAME|PARENTS|SPOUSES|SIBLINGS|CHILDREN)\s*:\s*([^;]*)",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses text content and returns JSON string for the tree, or an error message.
    /// </summary>
    public static (string? Json, string? Error) ParseAndGenerateJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, "Text file is empty.");

        var lines = text.Split('\n', '\r')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var people = new Dictionary<string, PersonDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = ParseLine(line);
            if (match == null) continue;

            var name = match.Value.name;
            if (string.IsNullOrWhiteSpace(name)) continue;

            name = name.Trim();
            var dto = match.Value.Item2;
            if (people.ContainsKey(name))
                people[name] = Merge(people[name], dto);
            else
                people[name] = dto;
        }

        if (people.Count == 0)
            return (null, "No valid NAME entries found in the text file.");

        // Only people with a dictionary line (NAME: ...) are included; placeholders are not added or displayed.
        var hasDictionaryLine = new HashSet<string>(people.Keys, StringComparer.OrdinalIgnoreCase);

        // Assign levels from parents only: roots (no parents) = 0, else 1 + max(parent level).
        // Use -1 for "not yet computed" so we don't treat missing parents as level 0.
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in people.Keys)
            levels[name] = -1;

        const int maxIterations = 100;
        for (var iter = 0; iter < maxIterations; iter++)
        {
            var changed = false;
            foreach (var name in people.Keys)
            {
                var p = people[name];
                var parentNames = p.Parents
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim())
                    .Where(hasDictionaryLine.Contains)
                    .ToList();
                int newLevel;
                if (parentNames.Count == 0)
                    newLevel = 0;
                else
                {
                    var parentLevels = parentNames
                        .Select(n => levels.TryGetValue(n, out var L) ? L : -1)
                        .Where(l => l >= 0)
                        .ToList();
                    if (parentLevels.Count == 0)
                        continue; // parents not yet computed this round, skip
                    newLevel = parentLevels.Max() + 1;
                }
                if (levels[name] != newLevel)
                {
                    levels[name] = newLevel;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        // Anyone still -1 (e.g. no parents and never updated) gets 0
        foreach (var name in people.Keys)
            if (levels[name] < 0) levels[name] = 0;

        // Siblings must be at level 1, not level 0 with parents: if you have no parents but have siblings, you're level 1.
        foreach (var name in people.Keys)
        {
            var p = people[name];
            if (levels[name] != 0) continue;
            var hasSibling = p.Siblings.Any(s => !string.IsNullOrWhiteSpace(s) && hasDictionaryLine.Contains(s.Trim()));
            if (hasSibling)
                levels[name] = 1;
        }
        // Sync levels again so all siblings in the same group get level 1 (spouse sync below will also keep spouses aligned)

        // Spouse sync: spouses must be on the same level (same row). Use max so they appear with the "deeper" generation.
        for (var iter = 0; iter < maxIterations; iter++)
        {
            var changed = false;
            foreach (var kv in people)
            {
                var p = kv.Value;
                var myLevel = levels[p.Name];
                foreach (var spouseName in p.Spouses.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()))
                {
                    if (!levels.TryGetValue(spouseName, out var spouseLevel)) continue;
                    var newLevel = Math.Max(myLevel, spouseLevel);
                    if (levels[p.Name] != newLevel) { levels[p.Name] = newLevel; changed = true; }
                    if (levels[spouseName] != newLevel) { levels[spouseName] = newLevel; changed = true; }
                }
            }
            if (!changed) break;
        }

        // Build output: only people with a dictionary line; relationship lists exclude placeholders
        static List<string> OnlyKnown(List<string> names, HashSet<string> known) =>
            (names ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n) && known.Contains(n.Trim()))
                .Select(n => n.Trim())
                .ToList();

        var output = new List<object>();
        foreach (var kv in people)
        {
            var p = kv.Value;
            output.Add(new
            {
                id = Guid.NewGuid().ToString("D").ToUpperInvariant(),
                name = p.Name,
                level = levels.TryGetValue(p.Name, out var l) ? l : 0,
                parents = OnlyKnown(p.Parents, hasDictionaryLine),
                spouses = OnlyKnown(p.Spouses, hasDictionaryLine),
                siblings = OnlyKnown(p.Siblings, hasDictionaryLine),
                children = OnlyKnown(p.Children, hasDictionaryLine),
                imageName = "",
                isImplicit = false
            });
        }

        try
        {
            var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = false });
            return (json, null);
        }
        catch (Exception ex)
        {
            return (null, "Failed to generate JSON: " + ex.Message);
        }
    }

    private static (string name, PersonDto)? ParseLine(string line)
    {
        var name = "";
        var parents = new List<string>();
        var spouses = new List<string>();
        var siblings = new List<string>();
        var children = new List<string>();

        foreach (Match m in LineRegex.Matches(line))
        {
            var key = m.Groups[1].Value.Trim().ToUpperInvariant();
            var value = m.Groups[2].Value.Trim();
            var parts = value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            switch (key)
            {
                case "NAME":
                    name = value;
                    break;
                case "PARENTS":
                    parents = parts;
                    break;
                case "SPOUSES":
                    spouses = parts;
                    break;
                case "SIBLINGS":
                    siblings = parts;
                    break;
                case "CHILDREN":
                    children = parts;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(name)) return null;

        return (name.Trim(), new PersonDto
        {
            Name = name.Trim(),
            Parents = parents,
            Spouses = spouses,
            Siblings = siblings,
            Children = children
        });
    }

    private static PersonDto Merge(PersonDto existing, PersonDto incoming)
    {
        return new PersonDto
        {
            Name = existing.Name,
            Parents = MergeLists(existing.Parents, incoming.Parents),
            Spouses = MergeLists(existing.Spouses, incoming.Spouses),
            Siblings = MergeLists(existing.Siblings, incoming.Siblings),
            Children = MergeLists(existing.Children, incoming.Children)
        };
    }

    private static List<string> MergeLists(List<string> a, List<string> b)
    {
        var set = new HashSet<string>(a ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var x in b ?? new List<string>())
            if (!string.IsNullOrWhiteSpace(x)) set.Add(x.Trim());
        return set.ToList();
    }

    private class PersonDto
    {
        public string Name { get; set; } = "";
        public List<string> Parents { get; set; } = new();
        public List<string> Spouses { get; set; } = new();
        public List<string> Siblings { get; set; } = new();
        public List<string> Children { get; set; } = new();
    }
}
