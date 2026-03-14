using System;
using System.Text.Json;

namespace STS2Mobile.Steam;

public enum CompareResult
{
    CloudWins,
    LocalWins,
    Equal,
}

// Compares two versions of a save file by in-game progress rather than timestamp.
// Returns Equal for non-progress files so the caller can apply its own tie-breaking.
public static class SaveProgressComparer
{
    public static CompareResult Compare(string path, string localContent, string cloudContent)
    {
        try
        {
            var canonPath = path.Replace("user://", "").Replace("\\", "/").ToLowerInvariant();

            if (canonPath.Contains("progress") && canonPath.EndsWith(".save"))
                return CompareProgress(localContent, cloudContent);

            if (canonPath.Contains("current_run") && canonPath.EndsWith(".save"))
                return CompareCurrentRun(localContent, cloudContent);

            // History files have unique filenames (no conflict); prefs have no progress concept.
            return CompareResult.Equal;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Cloud] Progress comparison failed for {path}: {ex.Message}");
            return CompareResult.Equal;
        }
    }

    // Cascades through progress indicators; first difference wins.
    private static CompareResult CompareProgress(string local, string cloud)
    {
        using var localDoc = JsonDocument.Parse(local);
        using var cloudDoc = JsonDocument.Parse(cloud);
        var localRoot = localDoc.RootElement;
        var cloudRoot = cloudDoc.RootElement;

        int localFloors = GetInt(localRoot, "floors_climbed");
        int cloudFloors = GetInt(cloudRoot, "floors_climbed");
        if (localFloors != cloudFloors)
            return localFloors > cloudFloors ? CompareResult.LocalWins : CompareResult.CloudWins;

        int localGames = SumCharacterGames(localRoot);
        int cloudGames = SumCharacterGames(cloudRoot);
        if (localGames != cloudGames)
            return localGames > cloudGames ? CompareResult.LocalWins : CompareResult.CloudWins;

        int localDiscovered = CountDiscovered(localRoot);
        int cloudDiscovered = CountDiscovered(cloudRoot);
        if (localDiscovered != cloudDiscovered)
            return localDiscovered > cloudDiscovered
                ? CompareResult.LocalWins
                : CompareResult.CloudWins;

        int localPlaytime = GetInt(localRoot, "total_playtime");
        int cloudPlaytime = GetInt(cloudRoot, "total_playtime");
        if (localPlaytime != cloudPlaytime)
            return localPlaytime > cloudPlaytime
                ? CompareResult.LocalWins
                : CompareResult.CloudWins;

        return CompareResult.Equal;
    }

    private static CompareResult CompareCurrentRun(string local, string cloud)
    {
        using var localDoc = JsonDocument.Parse(local);
        using var cloudDoc = JsonDocument.Parse(cloud);

        int localFloors = CountRunFloors(localDoc.RootElement);
        int cloudFloors = CountRunFloors(cloudDoc.RootElement);

        if (localFloors != cloudFloors)
            return localFloors > cloudFloors ? CompareResult.LocalWins : CompareResult.CloudWins;

        return CompareResult.Equal;
    }

    private static int CountRunFloors(JsonElement root)
    {
        int count = 0;
        if (
            root.TryGetProperty("map_point_history", out var history)
            && history.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var act in history.EnumerateArray())
            {
                if (act.ValueKind == JsonValueKind.Array)
                    count += act.GetArrayLength();
            }
        }
        else if (root.TryGetProperty("acts", out var acts) && acts.ValueKind == JsonValueKind.Array)
        {
            // Alternate save format
            count = acts.GetArrayLength();
        }

        return count;
    }

    private static int SumCharacterGames(JsonElement root)
    {
        int total = 0;
        if (
            root.TryGetProperty("character_stats", out var stats)
            && stats.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var character in stats.EnumerateArray())
            {
                total += GetInt(character, "total_wins");
                total += GetInt(character, "total_losses");
            }
        }
        return total;
    }

    private static int CountDiscovered(JsonElement root)
    {
        int count = 0;
        count += GetArrayLength(root, "discovered_cards");
        count += GetArrayLength(root, "discovered_relics");
        count += GetArrayLength(root, "discovered_potions");
        count += GetArrayLength(root, "discovered_events");
        count += GetArrayLength(root, "discovered_acts");
        return count;
    }

    private static int GetInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static int GetArrayLength(JsonElement element, string property)
    {
        return
            element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;
    }
}
