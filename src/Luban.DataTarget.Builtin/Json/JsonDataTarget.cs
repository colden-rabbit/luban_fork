using System.Text.Json;
using Luban.DataTarget;
using Luban.Defs;
using Luban.Utils;

namespace Luban.DataExporter.Builtin.Json;

[DataTarget("json")]
public class JsonDataTarget : DataTargetBase
{
    protected override string DefaultOutputFileExt => "json";

    public static bool UseCompactJson => EnvManager.Current.GetBoolOptionOrDefault("json", "compact", true, false);

    protected virtual JsonDataVisitor ImplJsonDataVisitor => JsonDataVisitor.Ins;

    public void WriteAsArray(List<Record> datas, Utf8JsonWriter x, JsonDataVisitor jsonDataVisitor)
    {
        x.WriteStartArray();
        foreach (var d in datas)
        {
            d.Data.Apply(jsonDataVisitor, x);
        }

        x.WriteEndArray();
    }

    public override OutputFile ExportTable(DefTable table, List<Record> records)
    {
        // 这里有内存溢出的风险，当怪物表内容过大时会溢出，现在是不导出json了，反正目前也不需要
        var ss = new MemoryStream();
        var jsonWriter = new Utf8JsonWriter(ss,
            new JsonWriterOptions() { Indented = !UseCompactJson, SkipValidation = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, });
        WriteAsArray(records, jsonWriter, ImplJsonDataVisitor);
        jsonWriter.Flush();
        return new OutputFile() { File = $"{table.OutputDataFile}.{OutputFileExt}", Content = DataUtil.StreamToBytes(ss), };
    }

    public override List<OutputFile> ExportTables(DefTable table, List<Record> records)
    {
        var groupedRecords = records.GroupBy(r => r.Source);
        var outputFiles    = new List<OutputFile>();
        var fileIndex      = 1; // 文件索引从1开始

        foreach (var group in groupedRecords)
        {
            var ss = new MemoryStream();
            var jsonWriter = new Utf8JsonWriter(ss,
                new JsonWriterOptions() { Indented = !UseCompactJson, SkipValidation = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, });
            WriteAsArray(group.ToList(), jsonWriter, ImplJsonDataVisitor);
            jsonWriter.Flush();
            outputFiles.Add(new OutputFile() { File = $"{table.OutputDataFile}_{fileIndex++}.{OutputFileExt}", Content = DataUtil.StreamToBytes(ss) });
        }

        return outputFiles;
    }
}
