using System.Text;

namespace Kadari.Infrastructure.CareRegistry;

/// <summary>
/// A minimal RFC 4180 reader for the registry's research files: comma-separated, double-quoted fields that may hold
/// commas, quotes ("") and line breaks, an optional UTF-8 BOM. Rows are keyed by header and carry the line they
/// started on, so an import error can point at the spreadsheet row.
/// </summary>
public static class CsvTable
{
    public sealed record Row(int Line, IReadOnlyDictionary<string, string> Fields)
    {
        public string? this[string column] =>
            Fields.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    }

    public static IReadOnlyList<Row> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var records = ReadRecords(text.TrimStart('﻿'));
        if (records.Count == 0)
        {
            return [];
        }

        var header = records[0].Fields.Select(h => h.Trim()).ToArray();
        var rows = new List<Row>();
        foreach (var (line, fields) in records.Skip(1))
        {
            if (fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Length; i++)
            {
                map[header[i]] = i < fields.Count ? fields[i] : "";
            }

            rows.Add(new Row(line, map));
        }

        return rows;
    }

    private static List<(int Line, List<string> Fields)> ReadRecords(string text)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }

                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add((recordLine, fields));
                    fields = [];
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add((recordLine, fields));
        }

        return records;
    }
}
