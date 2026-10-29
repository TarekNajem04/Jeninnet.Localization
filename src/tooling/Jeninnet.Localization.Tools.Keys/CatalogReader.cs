//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// One served catalog pair with its file/line location.
/// </summary>
internal sealed class CatalogEntry {
    /// <summary>
    /// Gets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the served template.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    public int Line { get; set; } = 1;
}

/// <summary>
/// One scanned catalog file: served pairs or a parse failure.
/// </summary>
internal sealed class CatalogFile {
    /// <summary>
    /// Gets the path relative to the working directory (forward slashes).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the format (<c>json</c>, <c>xml</c>, <c>resx</c>, <c>po</c>).
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Gets the served culture.
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the served pairs in file order.
    /// </summary>
    public List<CatalogEntry> Entries { get; } = [];

    /// <summary>
    /// Gets the parse failure, or <see langword="null"/> when the file parsed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets the 1-based error line.
    /// </summary>
    public int ErrorLine { get; set; } = 1;
}

/// <summary>
/// The catalog scan outcome: parsed catalogs plus skipped non-catalog files.
/// </summary>
internal sealed class CatalogScan {
    /// <summary>
    /// Gets the parsed catalogs in ordinal path order.
    /// </summary>
    public List<CatalogFile> Catalogs { get; } = [];

    /// <summary>
    /// Gets the skipped non-catalog files in ordinal path order.
    /// </summary>
    public List<string> IgnoredFiles { get; } = [];
}

/// <summary>
/// Reads the four parity formats with runtime-mirroring semantics:
/// JSON flat maps (duplicate keys fail, like the providers' dictionary load);
/// XML element maps keyed by local name; RESX <c>data name/value</c> pairs with the
/// neutral file serving the default culture; PO (Portable Object) pairs mirroring the provider
/// (fuzzy/header/empty skipped, singular plus plural pair mapping, EOT (End of Transmission) contexts).
/// Unparseable files become <c>catalog-error</c> findings with file/line precision.
/// </summary>
internal static class CatalogReader {
    /// <summary>
    /// The gettext-canonical context separator, mirroring the PO provider.
    /// </summary>
    internal const char CONTEXT_SEPARATOR = '\u0004';

    /// <summary>
    /// Scans catalog roots recursively for the four formats.
    /// </summary>
    /// <param name="catalogRoots">The catalog roots.</param>
    /// <param name="defaultCulture">The default culture served by neutral RESX files.</param>
    /// <returns>The scan outcome.</returns>
    public static CatalogScan Read(IReadOnlyList<string> catalogRoots, string defaultCulture) {
        ArgumentNullException.ThrowIfNull(catalogRoots);
        ArgumentNullException.ThrowIfNull(defaultCulture);

        var scan = new CatalogScan();
        var files = new SortedSet<string>(StringComparer.Ordinal);

        foreach(var root in catalogRoots) {
            foreach(var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if(extension is ".json" or ".xml" or ".resx" or ".po") {
                    files.Add(Path.GetFullPath(file));
                }
            }
        }

        foreach(var file in files) {
            var format = Path.GetExtension(file).ToLowerInvariant().TrimStart('.');
            var culture = ResolveCulture(file, format, defaultCulture);
            if(culture is null) {
                scan.IgnoredFiles.Add(RelativePath(file));
                continue;
            }

            scan.Catalogs.Add(ReadFile(file, format, culture));
        }

        return scan;
    }

    /// <summary>
    /// Reads file.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="format">The format.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The file result.</returns>
    private static CatalogFile ReadFile(string fullPath, string format, string culture) {
        var catalog = new CatalogFile {
            Path = RelativePath(fullPath),
            Format = format,
            Culture = culture,
        };

        try {
            switch(format) {
                case "json":
                    ReadJson(fullPath, catalog);
                    break;
                case "xml":
                    ReadXml(fullPath, catalog);
                    break;
                case "resx":
                    ReadResx(fullPath, catalog);
                    break;
                default:
                    ReadPo(fullPath, catalog);
                    break;
            }
        }
        catch(CatalogParseException exception) {
            catalog.Error = exception.Message;
            catalog.ErrorLine = exception.Line;
            catalog.Entries.Clear();
        }

        return catalog;
    }

    /// <summary>
    /// Resolves culture.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="format">The format.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The culture result.</returns>
    private static string? ResolveCulture(string fullPath, string format, string defaultCulture) {
        var name = Path.GetFileName(fullPath);
        if(format == "resx") {
            var stem = name[..^".resx".Length];
            var dot = stem.IndexOf('.');
            if(dot < 0) {
                return defaultCulture;
            }

            var culture = stem[(dot + 1)..];
            return IsCulture(culture) ? culture : null;
        }

        var cultureName = Path.GetFileNameWithoutExtension(fullPath);
        return IsCulture(cultureName) ? cultureName : null;
    }

    /// <summary>
    /// Determines whether the value is culture.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsCulture(string candidate) {
        if(string.IsNullOrWhiteSpace(candidate)) {
            return false;
        }

        try {
            _ = CultureInfo.GetCultureInfo(candidate);
            return true;
        }
        catch(ArgumentException) {
            return false;
        }
    }

    /// <summary>
    /// Reads json.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="catalog">The catalog.</param>
    private static void ReadJson(string fullPath, CatalogFile catalog) {
        var text = File.ReadAllText(fullPath);
        JsonDocument document;
        try {
            document = JsonDocument.Parse(text);
        }
        catch(JsonException exception) {
            throw new CatalogParseException($"Invalid JSON: {exception.Message}", (int)(exception.LineNumber ?? 1));
        }

        using(document) {
            if(document.RootElement.ValueKind != JsonValueKind.Object) {
                throw new CatalogParseException("Invalid JSON: the catalog root must be an object.", 1);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var finder = new JsonLineFinder(text);
            foreach(var property in document.RootElement.EnumerateObject()) {
                if(property.Value.ValueKind != JsonValueKind.String) {
                    throw new CatalogParseException(
                        $"Entry '{property.Name}' must map to a string template.",
                        finder.LineOf(property.Name, seen.Contains(property.Name) ? 1 : 0));
                }

                if(!seen.Add(property.Name)) {
                    throw new CatalogParseException(
                        $"Duplicate key '{property.Name}'.",
                        finder.LineOf(property.Name, occurrence: 1));
                }

                catalog.Entries.Add(new CatalogEntry {
                    Key = property.Name,
                    Template = property.Value.GetString() ?? string.Empty,
                    Line = finder.LineOf(property.Name, occurrence: 0),
                });
            }
        }
    }

    /// <summary>
    /// Reads xml.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="catalog">The catalog.</param>
    private static void ReadXml(string fullPath, CatalogFile catalog) {
        XDocument document;
        try {
            document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
        }
        catch(XmlException exception) {
            throw new CatalogParseException($"Invalid XML: {exception.Message}", exception.LineNumber);
        }

        if(document.Root is null) {
            throw new CatalogParseException("Invalid XML: the catalog has no root element.", 1);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var element in document.Root.Elements()) {
            var key = element.Name.LocalName;
            var line = element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 1;
            if(!seen.Add(key)) {
                throw new CatalogParseException($"Duplicate key '{key}'.", line);
            }

            catalog.Entries.Add(new CatalogEntry { Key = key, Template = element.Value, Line = line });
        }
    }

    /// <summary>
    /// Reads resx.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="catalog">The catalog.</param>
    private static void ReadResx(string fullPath, CatalogFile catalog) {
        XDocument document;
        try {
            document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
        }
        catch(XmlException exception) {
            throw new CatalogParseException($"Invalid RESX: {exception.Message}", exception.LineNumber);
        }

        if(document.Root is null) {
            throw new CatalogParseException("Invalid RESX: the file has no root element.", 1);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var element in document.Root.Elements("data")) {
            var name = element.Attribute("name")?.Value;
            var line = element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 1;
            if(name is null) {
                throw new CatalogParseException("Invalid RESX: a data element is missing its name attribute.", line);
            }

            if(!seen.Add(name)) {
                throw new CatalogParseException($"Duplicate key '{name}'.", line);
            }

            catalog.Entries.Add(new CatalogEntry {
                Key = name,
                Template = element.Element("value")?.Value ?? string.Empty,
                Line = line,
            });
        }
    }

    /// <summary>
    /// Reads po.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="catalog">The catalog.</param>
    private static void ReadPo(string fullPath, CatalogFile catalog) {
        foreach(var pair in PoEntryReader.Read(fullPath)) {
            catalog.Entries.Add(new CatalogEntry { Key = pair.Key, Template = pair.Template, Line = pair.Line });
        }
    }

    /// <summary>
    /// Performs the relative path operation.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>The relative path text.</returns>
    private static string RelativePath(string fullPath) {
        var current = Path.GetFullPath(Environment.CurrentDirectory);
        if(fullPath.StartsWith(current, StringComparison.OrdinalIgnoreCase)) {
            var relative = fullPath[current.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        return fullPath.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Locates the 1-based line of a JSON property name's occurrence, understanding
    /// string escapes so escaped keys still resolve.
    /// </summary>
    private sealed class JsonLineFinder {
        private readonly List<(string Key, int Line)> _properties = [];

        /// <summary>
        /// Initializes a new instance of the JsonLineFinder class.
        /// </summary>
        /// <param name="text">The text.</param>
        public JsonLineFinder(string text) {
            ArgumentNullException.ThrowIfNull(text);

            var line = 1;
            var position = 0;
            while(position < text.Length) {
                var current = text[position];
                if(current == '\n') {
                    line++;
                    position++;
                } else if(current == '"') {
                    var startLine = line;
                    var token = ReadJsonString(text, ref position, ref line);
                    if(token is not null) {
                        _properties.Add((token, startLine));
                    }
                } else {
                    position++;
                }
            }
        }

        /// <summary>
        /// Reads json string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="position">The position.</param>
        /// <param name="line">The line.</param>
        /// <returns>The json string result.</returns>
        private static string? ReadJsonString(string text, ref int position, ref int line) {
            // position is the opening quote; returns the unescaped value or null when unterminated.
            position++;
            var builder = new System.Text.StringBuilder();
            while(position < text.Length) {
                var inner = text[position];
                if(inner == '\n') {
                    line++;
                }

                if(inner == '"') {
                    position++;
                    return builder.ToString();
                }

                if(inner != '\\' || position + 1 >= text.Length) {
                    builder.Append(inner);
                    position++;
                    continue;
                }

                var escape = text[position + 1];
                switch(escape) {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u' when position + 5 < text.Length && IsHex(text, position + 2):
                        builder.Append((char)Convert.ToInt32(text.Substring(position + 2, 4), 16));
                        position += 6;
                        continue;
                    default:
                        builder.Append(escape);
                        break;
                }

                position += 2;
            }

            return null;
        }

        /// <summary>
        /// Determines whether the value is hex.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="start">The start.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        private static bool IsHex(string text, int start) {
            if(start + 4 > text.Length) {
                return false;
            }

            for(var index = 0; index < 4; index++) {
                var digit = text[start + index];
                if(digit is not ((>= '0' and <= '9') or
                     (>= 'a' and <= 'f') or
                     (>= 'A' and <= 'F'))) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Performs the line of operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="occurrence">The occurrence.</param>
        /// <returns>The line of value.</returns>
        public int LineOf(string key, int occurrence) {
            var seen = 0;
            foreach(var (candidate, line) in _properties) {
                if(candidate.Equals(key, StringComparison.Ordinal)) {
                    if(seen == occurrence) {
                        return line;
                    }

                    seen++;
                }
            }

            return 1;
        }
    }
}

/// <summary>
/// A structural catalog failure with a 1-based line number. Public so CLI hosts
/// embedding the tool as a library can catch it by type.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CatalogParseException"/> class.
/// </remarks>
/// <param name="message">The failure message.</param>
/// <param name="line">The 1-based line number.</param>
public sealed class CatalogParseException(string message, int line) : Exception(message) {

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    public int Line { get; } = line < 1 ? 1 : line;
}
