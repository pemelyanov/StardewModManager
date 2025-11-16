namespace StardewModManager.Core.Utils;

using System.Text;

public static class SteamVdfParser
{
    public static string EscapeVdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Basic VDF escaping - you might need to extend this
        return input.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    public static string UnescapeVdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Basic VDF unescaping
        return input.Replace("\\\\", "\\")
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\t", "\t");
    }
    
    public static OrderedDictionary<string, object> Parse(string content)
    {
        using var reader = new StringReader(content);
        return ParseObject(reader);
    }
    
    public static string ToString(IDictionary<string, object> dict)
    {
        var builder = new StringBuilder();
        ToString(builder, dict);
        return builder.ToString();
    }


    private static OrderedDictionary<string, object> ParseObject(StringReader reader)
    {
        var result = new OrderedDictionary<string, object>();
        string? line;

        while ((line = ReadAndCleanLine(reader)) != null)
        {
            if (IsEndOfObject(line)) break;
            
            var (key, value, hasValue) = ParseKeyValue(line);
            
            if (!hasValue && IsNextLineStartOfObject(reader))
            {
                var nestedObject = ParseObject(reader);
                result[key] = nestedObject;
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? ReadAndCleanLine(StringReader reader)
    {
        return reader.ReadLine()?.Trim();
    }

    private static bool IsEndOfObject(string line) => line == "}";

    private static bool IsNextLineStartOfObject(StringReader reader)
    {
        var nextLine = ReadAndCleanLine(reader);
        return nextLine == "{";
    }

    private static (string key, string value, bool hasValue) ParseKeyValue(string line)
    {
        if (string.IsNullOrEmpty(line)) 
            return (string.Empty, string.Empty, false);
        
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return (string.Empty, string.Empty, false);
        
        var key = parts[0].Trim('\"');
        
        if (parts.Length >= 2)
            return (key, parts[^1].Trim(), true);
        
        return (key, string.Empty, false);
    }
    
    private static void ToString(StringBuilder builder, IDictionary<string, object> dict, int indent = 0)
    {
        const char indentChar = '\t';
        string indentStr = new string(indentChar, indent);

        foreach (var kvp in dict)
        {
            builder.Append($"{indentStr}\"{kvp.Key}\"");
            
            if (kvp.Value is IDictionary<string, object> nestedDict)
            {
                builder.AppendLine();
                builder.AppendLine($"{indentStr}{{");
                ToString(builder, nestedDict, indent + 1);
                builder.AppendLine($"{indentStr}}}");
            }
            else
            {
                builder.AppendLine($"{indentChar}{indentChar}{kvp.Value}");
            }
        }
    }
}