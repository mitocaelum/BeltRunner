using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework.Interfaces;

namespace BeltRunner.Core.TEST.Testing;

internal static class HumanReadableTestDocumentation {
    private static readonly Lazy<XDocument?> Documentation = new(LoadDocumentation);

    public static (string? Purpose, string? WhyThisMatters, string? Expected) GetFor(ITest test) {
        XDocument? document = Documentation.Value;

        if( document is null || string.IsNullOrWhiteSpace(test.ClassName) || string.IsNullOrWhiteSpace(test.MethodName) ) {
            return (null, null, null);
        }

        string memberName = $"M:{test.ClassName}.{test.MethodName}";
        XElement? member = document
            .Root?
            .Element("members")?
            .Elements("member")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), memberName, StringComparison.Ordinal));

        if( member is null ) {
            return (null, null, null);
        }

        XElement? remarks = member.Element("remarks");
        string? purpose = FindParaText(remarks, "Purpose:");
        string? whyThisMatters = FindParaText(remarks, "Why this matters:");
        string? expected = FindParaText(remarks, "Expected result:");
        string? summary = Normalize(member.Element("summary")?.Value);

        purpose ??= summary;
        expected ??= summary;

        return (purpose, whyThisMatters, expected);
    }

    private static XDocument? LoadDocumentation() {
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        if( string.IsNullOrWhiteSpace(assemblyPath) ) {
            return null;
        }

        string xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

        if( !File.Exists(xmlPath) ) {
            return null;
        }

        return XDocument.Load(xmlPath);
    }

    private static string? FindParaText(XElement? remarks, string prefix) {
        XElement? para = remarks?
            .Elements("para")
            .FirstOrDefault(x => RenderText(x).TrimStart().StartsWith(prefix, StringComparison.Ordinal));

        if( para is null ) {
            return null;
        }

        string value = RenderText(para).Trim();
        return value[prefix.Length..].Trim();
    }

    private static string? Normalize(string? value) {
        if( string.IsNullOrWhiteSpace(value) ) {
            return null;
        }

        return string.Join(" ", value
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()));
    }

    private static string RenderText(XElement element) {
        return Normalize(string.Concat(element.Nodes().Select(RenderNode))) ?? string.Empty;
    }

    private static string RenderNode(XNode node) {
        if( node is XText text ) {
            return text.Value;
        }

        if( node is not XElement element ) {
            return string.Empty;
        }

        if( string.Equals(element.Name.LocalName, "see", StringComparison.OrdinalIgnoreCase) ) {
            string? cref = (string?)element.Attribute("cref");

            if( !string.IsNullOrWhiteSpace(cref) ) {
                int separatorIndex = cref.IndexOf(':');
                string value = separatorIndex >= 0 ? cref[(separatorIndex + 1)..] : cref;
                int nameIndex = value.LastIndexOf('.');

                return nameIndex >= 0 ? value[(nameIndex + 1)..] : value;
            }

            string? langword = (string?)element.Attribute("langword");
            return langword ?? string.Empty;
        }

        if( string.Equals(element.Name.LocalName, "c", StringComparison.OrdinalIgnoreCase) ) {
            return element.Value;
        }

        return string.Concat(element.Nodes().Select(RenderNode));
    }
}
