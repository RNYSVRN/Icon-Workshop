using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IconWorkshop.Packs;

/// <summary>A start tag with its attributes, in document order. Enough for appfilter.xml and drawable.xml.</summary>
internal sealed record XmlTag(string Name, IReadOnlyDictionary<string, string> Attributes)
{
    public string? this[string attribute] => Attributes.TryGetValue(attribute, out var v) ? v : null;
}

/// <summary>
/// Reads start tags from text XML or Android binary XML (AXML). The text reader is deliberately
/// tolerant: icon packs often contain small XML errors (unescaped "&amp;", duplicate attributes ...).
/// </summary>
internal static partial class XmlElements
{
    public static bool IsBinary(byte[] data) => data.Length >= 8 && data[0] == 0x03 && data[1] == 0x00;

    public static List<XmlTag> Read(byte[] data, ArscTable? arsc = null) =>
        IsBinary(data) ? ReadBinary(data, arsc) : ReadText(DecodeText(data));

    private static string DecodeText(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Encoding.Unicode.GetString(data, 2, data.Length - 2);
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return Encoding.UTF8.GetString(data, 3, data.Length - 3);
        return Encoding.UTF8.GetString(data);
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<([A-Za-z_][\w.\-]*)((?:\s+[\w:.\-]+\s*=\s*(?:""[^""]*""|'[^']*'))*)\s*/?>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"([\w:.\-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();

    public static List<XmlTag> ReadText(string text)
    {
        text = CommentRegex().Replace(text, " ");
        var result = new List<XmlTag>();
        foreach (Match m in TagRegex().Matches(text))
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match a in AttributeRegex().Matches(m.Groups[2].Value))
            {
                string value = a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Value;
                attrs.TryAdd(LocalName(a.Groups[1].Value), WebUtility.HtmlDecode(value));
            }
            result.Add(new XmlTag(LocalName(m.Groups[1].Value), attrs));
        }
        return result;
    }

    private static string LocalName(string name)
    {
        int i = name.IndexOf(':');
        return i >= 0 ? name[(i + 1)..] : name;
    }

    // ---------- Binary XML ----------

    // Well-known framework attribute IDs, for attributes whose names are stripped by obfuscators.
    private static readonly Dictionary<uint, string> FrameworkAttributes = new()
    {
        [0x01010001] = "label",
        [0x01010002] = "icon",
        [0x0101021b] = "versionCode",
        [0x0101021c] = "versionName",
        [0x01010003] = "name",
    };

    public static List<XmlTag> ReadBinary(byte[] d, ArscTable? arsc)
    {
        var result = new List<XmlTag>();
        int end = Math.Min(d.Length, BitConverter.ToInt32(d, 4));
        int p = BitConverter.ToUInt16(d, 2);
        ResStringPool? strings = null;
        uint[] resourceMap = [];

        while (p + 8 <= end)
        {
            int type = BitConverter.ToUInt16(d, p);
            int headerSize = BitConverter.ToUInt16(d, p + 2);
            int size = BitConverter.ToInt32(d, p + 4);
            if (size <= 0) break;

            if (type == 0x0001) strings = new ResStringPool(d, p);
            else if (type == 0x0180)
            {
                int n = (size - headerSize) / 4;
                resourceMap = new uint[n];
                for (int i = 0; i < n; i++) resourceMap[i] = BitConverter.ToUInt32(d, p + headerSize + i * 4);
            }
            else if (type == 0x0102 && strings is not null)
            {
                int ext = p + headerSize;
                string name = strings.Get(BitConverter.ToUInt32(d, ext + 4)) ?? "";
                int attrStart = BitConverter.ToUInt16(d, ext + 8);
                int attrSize = BitConverter.ToUInt16(d, ext + 10);
                int attrCount = BitConverter.ToUInt16(d, ext + 12);
                var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < attrCount; i++)
                {
                    int a = ext + attrStart + i * attrSize;
                    uint nameIndex = BitConverter.ToUInt32(d, a + 4);
                    uint raw = BitConverter.ToUInt32(d, a + 8);
                    int dataType = d[a + 15];
                    uint data = BitConverter.ToUInt32(d, a + 16);

                    string attrName = strings.Get(nameIndex) ?? "";
                    if (attrName.Length == 0 && nameIndex < resourceMap.Length)
                        FrameworkAttributes.TryGetValue(resourceMap[nameIndex], out attrName!);
                    if (string.IsNullOrEmpty(attrName)) continue;

                    string? value = raw != 0xFFFFFFFF ? strings.Get(raw) : TypedValue(dataType, data, strings, arsc);
                    if (value is not null) attrs.TryAdd(attrName, value);
                }
                result.Add(new XmlTag(name, attrs));
            }
            p += size;
        }
        return result;
    }

    private static string? TypedValue(int type, uint data, ResStringPool strings, ArscTable? arsc) => type switch
    {
        0x03 => strings.Get(data),
        0x01 => ResolveReference(data, arsc),
        0x10 => ((int)data).ToString(System.Globalization.CultureInfo.InvariantCulture),
        0x11 => "0x" + data.ToString("x"),
        0x12 => data != 0 ? "true" : "false",
        0x04 => BitConverter.Int32BitsToSingle((int)data).ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary>"@drawable/name" style references are reduced to the bare name (as in text appfilters).</summary>
    private static string ResolveReference(uint id, ArscTable? arsc)
    {
        if (arsc?.NameOf(id) is { } n)
            return n.Type == "string" && arsc.StringValue(id) is { } s ? s : n.Name;
        return "@0x" + id.ToString("x8");
    }
}
