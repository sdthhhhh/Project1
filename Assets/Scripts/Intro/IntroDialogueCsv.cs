using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class IntroDialogueLine
{
    public int id;
    public string speaker;
    public string text;
}

/// <summary>
/// Loads intro narration from a CSV that Excel can open/edit.
/// Expected headers: id,speaker,text
/// Put commas inside double quotes. Use "" for a literal quote.
/// </summary>
public static class IntroDialogueCsv
{
    public static List<IntroDialogueLine> LoadFromTextAsset(TextAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        return Parse(asset.text);
    }

    public static List<IntroDialogueLine> LoadFromStreamingAssets(string relativePath)
    {
        string path = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Intro dialogue CSV not found.", path);
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    public static List<IntroDialogueLine> Parse(string csv)
    {
        var lines = new List<IntroDialogueLine>();
        if (string.IsNullOrWhiteSpace(csv)) return lines;

        using (var reader = new StringReader(csv))
        {
            string header = reader.ReadLine();
            if (header == null) return lines;

            string row;
            while ((row = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                if (!TryParseRow(row, out IntroDialogueLine line)) continue;
                if (string.IsNullOrWhiteSpace(line.text)) continue;
                lines.Add(line);
            }
        }

        lines.Sort((a, b) => a.id.CompareTo(b.id));
        return lines;
    }

    private static bool TryParseRow(string row, out IntroDialogueLine line)
    {
        line = null;
        List<string> cols = SplitCsvRow(row);
        if (cols.Count < 3) return false;
        if (!int.TryParse(cols[0].Trim(), out int id)) return false;

        line = new IntroDialogueLine
        {
            id = id,
            speaker = cols[1].Trim(),
            text = cols[2].Trim()
        };
        return true;
    }

    private static List<string> SplitCsvRow(string row)
    {
        var cols = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < row.Length && row[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    cols.Add(current.ToString());
                    current.Length = 0;
                }
                else current.Append(c);
            }
        }

        cols.Add(current.ToString());
        return cols;
    }
}
