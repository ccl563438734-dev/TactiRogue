using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TactiRogue
{
    public sealed class TactiRogueExcelCell
    {
        public TactiRogueExcelCell(string value, bool hasFormula = false)
        {
            Value = value ?? string.Empty;
            HasFormula = hasFormula;
        }

        public string Value { get; }
        public bool HasFormula { get; }
    }

    public sealed class TactiRogueExcelSheet
    {
        public TactiRogueExcelSheet(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
        public List<List<TactiRogueExcelCell>> Rows { get; } = new List<List<TactiRogueExcelCell>>();
        public bool HasMergedCells { get; set; }

        public void AddRow(IEnumerable<string> values)
        {
            Rows.Add((values ?? Array.Empty<string>()).Select(value => new TactiRogueExcelCell(value)).ToList());
        }
    }

    public sealed class TactiRogueExcelWorkbook
    {
        public List<TactiRogueExcelSheet> Sheets { get; } = new List<TactiRogueExcelSheet>();
    }

    public static class TactiRogueSimpleXlsx
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

        public static TactiRogueExcelWorkbook Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Workbook path is required.", nameof(path));
            }

            var workbook = new TactiRogueExcelWorkbook();
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var sharedStrings = LoadSharedStrings(archive);
                var workbookEntry = GetRequiredEntry(archive, "xl/workbook.xml");
                var workbookDocument = LoadDocument(workbookEntry);
                var relationshipMap = LoadWorkbookRelationships(archive);

                foreach (var sheetElement in workbookDocument
                             .Root?
                             .Element(SpreadsheetNamespace + "sheets")?
                             .Elements(SpreadsheetNamespace + "sheet") ?? Enumerable.Empty<XElement>())
                {
                    var name = (string)sheetElement.Attribute("name") ?? "Sheet";
                    var relationshipId = (string)sheetElement.Attribute(RelationshipsNamespace + "id");
                    if (string.IsNullOrWhiteSpace(relationshipId) || !relationshipMap.TryGetValue(relationshipId, out var target))
                    {
                        continue;
                    }

                    var worksheetEntry = GetRequiredEntry(archive, $"xl/{target.TrimStart('/')}");
                    var worksheetDocument = LoadDocument(worksheetEntry);
                    workbook.Sheets.Add(ParseSheet(name, worksheetDocument, sharedStrings));
                }
            }

            return workbook;
        }

        public static void Save(string path, TactiRogueExcelWorkbook workbook)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Workbook path is required.", nameof(path));
            }

            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteDocument(archive, "[Content_Types].xml", BuildContentTypesDocument(workbook));
                WriteDocument(archive, "_rels/.rels", BuildRootRelationshipsDocument());
                WriteDocument(archive, "docProps/core.xml", BuildCorePropertiesDocument());
                WriteDocument(archive, "docProps/app.xml", BuildAppPropertiesDocument(workbook));
                WriteDocument(archive, "xl/workbook.xml", BuildWorkbookDocument(workbook));
                WriteDocument(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsDocument(workbook));

                for (var index = 0; index < workbook.Sheets.Count; index++)
                {
                    WriteDocument(archive, $"xl/worksheets/sheet{index + 1}.xml", BuildWorksheetDocument(workbook.Sheets[index]));
                }
            }
        }

        private static TactiRogueExcelSheet ParseSheet(string name, XDocument worksheetDocument, IReadOnlyList<string> sharedStrings)
        {
            var sheet = new TactiRogueExcelSheet(name)
            {
                HasMergedCells = worksheetDocument.Root?.Element(SpreadsheetNamespace + "mergeCells")?.Elements(SpreadsheetNamespace + "mergeCell").Any() == true,
            };

            var sheetData = worksheetDocument.Root?.Element(SpreadsheetNamespace + "sheetData");
            if (sheetData == null)
            {
                return sheet;
            }

            foreach (var rowElement in sheetData.Elements(SpreadsheetNamespace + "row"))
            {
                var rowNumber = (int?)rowElement.Attribute("r") ?? sheet.Rows.Count + 1;
                while (sheet.Rows.Count < rowNumber - 1)
                {
                    sheet.Rows.Add(new List<TactiRogueExcelCell>());
                }

                var cellsByIndex = new Dictionary<int, TactiRogueExcelCell>();
                var maxColumnIndex = -1;
                foreach (var cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    var reference = (string)cellElement.Attribute("r");
                    var columnIndex = GetColumnIndex(reference);
                    maxColumnIndex = Math.Max(maxColumnIndex, columnIndex);
                    cellsByIndex[columnIndex] = new TactiRogueExcelCell(ReadCellValue(cellElement, sharedStrings), cellElement.Element(SpreadsheetNamespace + "f") != null);
                }

                var row = new List<TactiRogueExcelCell>();
                for (var columnIndex = 0; columnIndex <= maxColumnIndex; columnIndex++)
                {
                    row.Add(cellsByIndex.TryGetValue(columnIndex, out var cell) ? cell : new TactiRogueExcelCell(string.Empty));
                }

                sheet.Rows.Add(row);
            }

            return sheet;
        }

        private static string ReadCellValue(XElement cellElement, IReadOnlyList<string> sharedStrings)
        {
            var cellType = (string)cellElement.Attribute("t");
            switch (cellType)
            {
                case "inlineStr":
                    return ReadInlineString(cellElement);
                case "s":
                    var sharedStringIndex = ReadInt(cellElement.Element(SpreadsheetNamespace + "v")?.Value);
                    return sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count ? sharedStrings[sharedStringIndex] : string.Empty;
                case "b":
                    return cellElement.Element(SpreadsheetNamespace + "v")?.Value == "1" ? "true" : "false";
                default:
                    return cellElement.Element(SpreadsheetNamespace + "v")?.Value
                        ?? cellElement.Element(SpreadsheetNamespace + "is")?.Value
                        ?? string.Empty;
            }
        }

        private static string ReadInlineString(XElement cellElement)
        {
            var inlineString = cellElement.Element(SpreadsheetNamespace + "is");
            if (inlineString == null)
            {
                return string.Empty;
            }

            return string.Concat(inlineString.Descendants(SpreadsheetNamespace + "t").Select(element => element.Value));
        }

        private static IReadOnlyList<string> LoadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return Array.Empty<string>();
            }

            var document = LoadDocument(entry);
            return document.Root?
                .Elements(SpreadsheetNamespace + "si")
                .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(element => element.Value)))
                .ToArray() ?? Array.Empty<string>();
        }

        private static Dictionary<string, string> LoadWorkbookRelationships(ZipArchive archive)
        {
            var relationships = new Dictionary<string, string>(StringComparer.Ordinal);
            var entry = GetRequiredEntry(archive, "xl/_rels/workbook.xml.rels");
            var document = LoadDocument(entry);
            foreach (var relationship in document.Root?.Elements(PackageRelationshipsNamespace + "Relationship") ?? Enumerable.Empty<XElement>())
            {
                var id = (string)relationship.Attribute("Id");
                var target = (string)relationship.Attribute("Target");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(target))
                {
                    relationships[id] = target.Replace("\\", "/");
                }
            }

            return relationships;
        }

        private static XDocument BuildContentTypesDocument(TactiRogueExcelWorkbook workbook)
        {
            var root = new XElement(ContentTypesNamespace + "Types",
                new XElement(ContentTypesNamespace + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypesNamespace + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypesNamespace + "Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypesNamespace + "Override",
                    new XAttribute("PartName", "/docProps/core.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.core-properties+xml")),
                new XElement(ContentTypesNamespace + "Override",
                    new XAttribute("PartName", "/docProps/app.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.extended-properties+xml")));

            for (var index = 0; index < workbook.Sheets.Count; index++)
            {
                root.Add(new XElement(ContentTypesNamespace + "Override",
                    new XAttribute("PartName", $"/xl/worksheets/sheet{index + 1}.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        }

        private static XDocument BuildRootRelationshipsDocument()
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(PackageRelationshipsNamespace + "Relationships",
                    new XElement(PackageRelationshipsNamespace + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")),
                    new XElement(PackageRelationshipsNamespace + "Relationship",
                        new XAttribute("Id", "rId2"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"),
                        new XAttribute("Target", "docProps/core.xml")),
                    new XElement(PackageRelationshipsNamespace + "Relationship",
                        new XAttribute("Id", "rId3"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"),
                        new XAttribute("Target", "docProps/app.xml"))));
        }

        private static XDocument BuildCorePropertiesDocument()
        {
            XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
            XNamespace dc = "http://purl.org/dc/elements/1.1/";
            XNamespace dcterms = "http://purl.org/dc/terms/";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            var timestamp = DateTime.UtcNow.ToString("s") + "Z";
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(cp + "coreProperties",
                    new XAttribute(XNamespace.Xmlns + "dc", dc),
                    new XAttribute(XNamespace.Xmlns + "dcterms", dcterms),
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                    new XElement(dc + "creator", "Codex"),
                    new XElement(cp + "lastModifiedBy", "Codex"),
                    new XElement(dcterms + "created",
                        new XAttribute(xsi + "type", "dcterms:W3CDTF"),
                        timestamp),
                    new XElement(dcterms + "modified",
                        new XAttribute(xsi + "type", "dcterms:W3CDTF"),
                        timestamp)));
        }

        private static XDocument BuildAppPropertiesDocument(TactiRogueExcelWorkbook workbook)
        {
            XNamespace properties = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
            XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(properties + "Properties",
                    new XAttribute(XNamespace.Xmlns + "vt", vt),
                    new XElement(properties + "Application", "Codex"),
                    new XElement(properties + "DocSecurity", 0),
                    new XElement(properties + "ScaleCrop", "false"),
                    new XElement(properties + "HeadingPairs",
                        new XElement(vt + "vector",
                            new XAttribute("size", 2),
                            new XAttribute("baseType", "variant"),
                            new XElement(vt + "variant", new XElement(vt + "lpstr", "Worksheets")),
                            new XElement(vt + "variant", new XElement(vt + "i4", workbook.Sheets.Count)))),
                    new XElement(properties + "TitlesOfParts",
                        new XElement(vt + "vector",
                            new XAttribute("size", workbook.Sheets.Count),
                            new XAttribute("baseType", "lpstr"),
                            workbook.Sheets.Select(sheet => new XElement(vt + "lpstr", sheet.Name))))));
        }

        private static XDocument BuildWorkbookDocument(TactiRogueExcelWorkbook workbook)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(SpreadsheetNamespace + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", RelationshipsNamespace),
                    new XElement(SpreadsheetNamespace + "sheets",
                        workbook.Sheets.Select((sheet, index) =>
                            new XElement(SpreadsheetNamespace + "sheet",
                                new XAttribute("name", sheet.Name),
                                new XAttribute("sheetId", index + 1),
                                new XAttribute(RelationshipsNamespace + "id", $"rId{index + 1}"))))));
        }

        private static XDocument BuildWorkbookRelationshipsDocument(TactiRogueExcelWorkbook workbook)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(PackageRelationshipsNamespace + "Relationships",
                    workbook.Sheets.Select((sheet, index) =>
                        new XElement(PackageRelationshipsNamespace + "Relationship",
                            new XAttribute("Id", $"rId{index + 1}"),
                            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                            new XAttribute("Target", $"worksheets/sheet{index + 1}.xml")))));
        }

        private static XDocument BuildWorksheetDocument(TactiRogueExcelSheet sheet)
        {
            var rows = new List<XElement>();
            for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                var rowValues = sheet.Rows[rowIndex];
                var cells = new List<XElement>();
                for (var columnIndex = 0; columnIndex < rowValues.Count; columnIndex++)
                {
                    cells.Add(new XElement(SpreadsheetNamespace + "c",
                        new XAttribute("r", $"{GetColumnName(columnIndex)}{rowIndex + 1}"),
                        new XAttribute("t", "inlineStr"),
                        new XElement(SpreadsheetNamespace + "is",
                            new XElement(SpreadsheetNamespace + "t",
                                new XAttribute(XNamespace.Xml + "space", "preserve"),
                                rowValues[columnIndex]?.Value ?? string.Empty))));
                }

                rows.Add(new XElement(SpreadsheetNamespace + "row",
                    new XAttribute("r", rowIndex + 1),
                    cells));
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(SpreadsheetNamespace + "worksheet",
                    new XElement(SpreadsheetNamespace + "sheetData", rows)));
        }

        private static XDocument LoadDocument(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string entryPath)
        {
            var entry = archive.GetEntry(entryPath);
            if (entry == null)
            {
                throw new InvalidDataException($"Workbook entry '{entryPath}' is missing.");
            }

            return entry;
        }

        private static void WriteDocument(ZipArchive archive, string entryPath, XDocument document)
        {
            var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                document.Save(writer);
            }
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
            if (string.IsNullOrEmpty(letters))
            {
                return 0;
            }

            var value = 0;
            foreach (var letter in letters)
            {
                value = (value * 26) + (char.ToUpperInvariant(letter) - 'A' + 1);
            }

            return Math.Max(0, value - 1);
        }

        private static string GetColumnName(int columnIndex)
        {
            var builder = new StringBuilder();
            var value = columnIndex + 1;
            while (value > 0)
            {
                var remainder = (value - 1) % 26;
                builder.Insert(0, (char)('A' + remainder));
                value = (value - 1) / 26;
            }

            return builder.ToString();
        }

        private static int ReadInt(string rawValue)
        {
            return int.TryParse(rawValue, out var value) ? value : -1;
        }
    }
}
