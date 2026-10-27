using System.IO.Compression;
using System.Text.Json;
using Luban.Datas;
using Luban.Serialization;
using Luban.Tests.Helpers;
using Xunit;

namespace Luban.Tests.Integration;

[Collection("LubanPipeline")]
public class EtCompatibilityTests
{
    [Theory]
    [InlineData(1970, 1, 1, 0, -28800000L)]
    [InlineData(1970, 1, 1, 8, 0L)]
    [InlineData(2025, 1, 1, 8, 1735689600000L)]
    public void DateTime_UsesMillisecondsAndExportTimeZone(int year, int month, int day, int hour, long expected)
    {
        var date = new DDateTime(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified));
        Assert.Equal(expected, date.GetUnixTime(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    public void Size_OnlyAcceptsDeclaredLengths(int length, bool expectFailure)
    {
        using var ws = new TempWorkspace();
        string conf = WriteFixture(ws, """
            <module name="">
              <bean name="Row"><var name="id" type="int"/><var name="vals" type="(array#size=0|5),int"/></bean>
              <table name="TbRow" value="Row" input="*@row.json"/>
            </module>
            """);
        File.WriteAllText(Path.Combine(ws.Root, "row.json"), JsonSerializer.Serialize(new[]
        {
            new { id = 1, vals = Enumerable.Range(1, length).ToArray() },
        }));
        var result = LubanRunner.Run(conf, forceLoadTableDatas: true, outputDataDir: ws.DataDir);
        Assert.Equal(expectFailure, result.AnyValidatorFail);
    }

    [Fact]
    public void MoreOutput_PreservesSourceNamesAndBinJsonRecords()
    {
        using var ws = new TempWorkspace();
        string conf = WriteFixture(ws, """
            <module name="">
              <bean name="Row"><var name="id" type="int"/></bean>
              <table name="TbRow" value="Row" input="*@first.json,*@second.json" output="Rows" outputMode="more" interfaces="IExample"/>
            </module>
            """);
        File.WriteAllText(Path.Combine(ws.Root, "first.json"), "[{\"id\":1}]");
        File.WriteAllText(Path.Combine(ws.Root, "second.json"), "[{\"id\":2},{\"id\":3}]");
        var result = LubanRunner.Run(conf, dataTargets: new[] { "bin", "json2" }, outputDataDir: ws.DataDir,
            extraOptions: new Dictionary<string, string>
            {
                ["bin.outputDataDir"] = Path.Combine(ws.DataDir, "bin"),
                ["json2.outputDataDir"] = Path.Combine(ws.DataDir, "json"),
            });
        Assert.False(result.AnyValidatorFail);
        Assert.Equal(4, Directory.GetFiles(ws.DataDir, "*", SearchOption.AllDirectories).Length);
        AssertShard(ws.DataDir, "first", new[] { 1 });
        AssertShard(ws.DataDir, "second", new[] { 2, 3 });
    }

    [Theory]
    [InlineData("main@row.xlsx")]
    [InlineData("row.xlsx")]
    public void ExcelHeaderSchema_PreservesSheetAndAtSignInDirectory(string input)
    {
        using var ws = new TempWorkspace("luban-et@2");
        string conf = WriteFixture(ws, $"""
            <module name="">
              <table name="TbRow" value="Row" input="{input}" readSchemaFromFile="true"/>
            </module>
            """);
        using (var archive = ZipFile.Open(Path.Combine(ws.Root, "row.xlsx"), ZipArchiveMode.Create))
        {
            WriteEntry("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="main" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            WriteEntry("xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="inlineStr"><is><t>##var</t></is></c>
                      <c r="B1" t="inlineStr"><is><t>id</t></is></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="inlineStr"><is><t>##type</t></is></c>
                      <c r="B2" t="inlineStr"><is><t>int</t></is></c>
                    </row>
                    <row r="3"><c r="B3"><v>7</v></c></row>
                  </sheetData>
                </worksheet>
                """);

            void WriteEntry(string name, string content)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }
        var result = LubanRunner.Run(conf, dataTargets: new[] { "bin" }, outputDataDir: ws.DataDir);
        Assert.False(result.AnyValidatorFail);
        var bytes = new ByteBuf(File.ReadAllBytes(Directory.GetFiles(ws.DataDir, "*.bytes").Single()));
        Assert.Equal(1, bytes.ReadSize());
        Assert.Equal(7, bytes.ReadInt());
        Assert.Equal(0, bytes.Size);
    }

    private static void AssertShard(string directory, string sourceName, int[] expectedIds)
    {
        var bytes = new ByteBuf(File.ReadAllBytes(Path.Combine(directory, "bin", $"Rows_{sourceName}.bytes")));
        Assert.Equal(expectedIds.Length, bytes.ReadSize());
        foreach (int expectedId in expectedIds)
        {
            Assert.Equal(expectedId, bytes.ReadInt());
        }
        Assert.Equal(0, bytes.Size);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "json", $"Rows_{sourceName}.json")));
        Assert.Equal(expectedIds, json.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetInt32()).ToArray());
    }

    private static string WriteFixture(TempWorkspace workspace, string schema)
    {
        File.WriteAllText(Path.Combine(workspace.Root, "schema.xml"), schema);
        string conf = Path.Combine(workspace.Root, "luban.conf");
        File.WriteAllText(conf, """
            {
              "groups": [{"names":["s"],"default":true}],
              "schemaFiles": [{"fileName":"schema.xml","type":""}],
              "dataDir": ".",
              "targets": [{"name":"server","manager":"Tables","groups":["s"],"topModule":"cfg"}]
            }
            """);
        return conf;
    }
}
