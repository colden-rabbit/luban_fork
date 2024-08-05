using Luban.DataTarget;
using Luban.Defs;
using Luban.Serialization;

namespace Luban.DataExporter.Builtin.Binary;

[DataTarget("bin")]
public class BinaryDataTarget : DataTargetBase
{
    protected override string DefaultOutputFileExt => "bytes";

    private void WriteList(DefTable table, List<Record> datas, ByteBuf x)
    {
        x.WriteSize(datas.Count);
        foreach (var d in datas)
        {
            d.Data.Apply(BinaryDataVisitor.Ins, x);
        }
    }

    public override OutputFile ExportTable(DefTable table, List<Record> records)
    {
        var bytes = new ByteBuf();
        WriteList(table, records, bytes);
        return new OutputFile() { File = $"{table.OutputDataFile}.{OutputFileExt}", Content = bytes.CopyData(), };
    }

    public override List<OutputFile> ExportTables(DefTable table, List<Record> records)
    {
        var groupedRecords = records.GroupBy(r => r.Source);
        var outputFiles    = new List<OutputFile>();

        foreach (var group in groupedRecords)
        {
            // 文件索引
            var fileNameIndex = Path.GetFileNameWithoutExtension(group.Key);
            var bytes         = new ByteBuf();
            WriteList(table, group.ToList(), bytes);
            outputFiles.Add(new OutputFile() { File = $"{table.OutputDataFile}_{fileNameIndex}.{OutputFileExt}", Content = bytes.CopyData(), });
        }

        return outputFiles;
    }
}
