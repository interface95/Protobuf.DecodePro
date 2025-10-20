using Xunit;
using Xunit.Abstractions;
using Protobuf.Decode.Parser;
using System.IO.Compression;

namespace Protobuf.Decode.Parser.Tests;

/// <summary>
/// 使用真实世界的 Protobuf 数据进行测试
/// </summary>
public class RealWorldDataTests
{
    private readonly ITestOutputHelper _output;

    public RealWorldDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Parse_RealWorldGzipData_Collect36131_Success()
    {
        // Arrange: 读取真实的 gzip 压缩 protobuf 数据
        var gzipFilePath = Path.Combine("log", "collect36131");
        
        // 检查文件是否存在
        if (!File.Exists(gzipFilePath))
        {
            _output.WriteLine($"文件不存在: {gzipFilePath}");
            _output.WriteLine($"当前目录: {Directory.GetCurrentDirectory()}");
            throw new FileNotFoundException($"测试数据文件不存在: {gzipFilePath}");
        }

        // 解压 gzip 数据
        byte[] protobufData;
        using (var fileStream = File.OpenRead(gzipFilePath))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (var memoryStream = new MemoryStream())
        {
            gzipStream.CopyTo(memoryStream);
            protobufData = memoryStream.ToArray();
        }

        _output.WriteLine($"解压后数据大小: {protobufData.Length} bytes");
        _output.WriteLine($"解压后数据大小: {Convert.ToHexStringLower(protobufData)} bytes");
        _output.WriteLine($"前32字节 (hex): {BitConverter.ToString(protobufData.Take(32).ToArray())}");

        // Act: 解析 Protobuf 数据
        var nodes = ProtoParser.Parse(protobufData);

        // Assert: 验证解析结果
        Assert.NotNull(nodes);
        Assert.NotEmpty(nodes);

        _output.WriteLine($"解析出 {nodes.Count} 个顶层节点");

        // 输出节点统计信息
        var stats = AnalyzeNodes(nodes);
        _output.WriteLine($"\n节点统计:");
        _output.WriteLine($"  总节点数: {stats.TotalNodes}");
        _output.WriteLine($"  嵌套节点数: {stats.NestedNodes}");
        _output.WriteLine($"  最大嵌套深度: {stats.MaxDepth}");
        _output.WriteLine($"  字段号范围: {stats.MinFieldNumber} - {stats.MaxFieldNumber}");
        _output.WriteLine($"\nWire Type 分布:");
        foreach (var (wireType, count) in stats.WireTypeCounts.OrderBy(x => x.Key))
        {
            _output.WriteLine($"  {wireType}: {count}");
        }

        // 输出前几个节点的详细信息
        _output.WriteLine($"\n前5个顶层节点:");
        foreach (var node in nodes.Take(5))
        {
            PrintNode(node, 0);
        }

        // 基本断言
        Assert.True(stats.TotalNodes > 0);
        Assert.True(stats.MaxDepth > 0);
    }

    [Fact]
    public void Parse_RealWorldData_VerifyStructure()
    {
        // Arrange
        var gzipFilePath = Path.Combine("log", "collect36131");
        if (!File.Exists(gzipFilePath))
        {
            throw new FileNotFoundException($"测试数据文件不存在: {gzipFilePath}");
        }

        byte[] protobufData;
        using (var fileStream = File.OpenRead(gzipFilePath))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (var memoryStream = new MemoryStream())
        {
            gzipStream.CopyTo(memoryStream);
            protobufData = memoryStream.ToArray();
        }

        // Act
        var nodes = ProtoParser.Parse(protobufData);

        // Assert: 验证数据结构
        Assert.NotEmpty(nodes);

        // 检查是否包含嵌套消息
        var hasNestedMessages = nodes.Any(n => n.Children != null && n.Children.Count > 0);
        Assert.True(hasNestedMessages, "应该包含嵌套消息");

        // 检查是否包含字符串数据 (LengthDelimited)
        var hasStrings = ContainsWireType(nodes, ProtoWireType.LengthDelimited);
        Assert.True(hasStrings, "应该包含 LengthDelimited 类型数据");

        // 检查是否包含 Varint 数据
        var hasVarints = ContainsWireType(nodes, ProtoWireType.Varint);
        Assert.True(hasVarints, "应该包含 Varint 类型数据");

        _output.WriteLine("✅ 真实世界数据结构验证通过");
    }

    [Fact]
    public void DisplayNode_RealWorldData_CanConvert()
    {
        // Arrange
        var gzipFilePath = Path.Combine("log", "collect36131");
        if (!File.Exists(gzipFilePath))
        {
            throw new FileNotFoundException($"测试数据文件不存在: {gzipFilePath}");
        }

        byte[] protobufData;
        using (var fileStream = File.OpenRead(gzipFilePath))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (var memoryStream = new MemoryStream())
        {
            gzipStream.CopyTo(memoryStream);
            protobufData = memoryStream.ToArray();
        }

        var nodes = ProtoParser.Parse(protobufData);

        // Act: 转换为 DisplayNode
        var displayNodes = ProtoDisplayNode.FromNodes(nodes);

        // Assert
        Assert.NotEmpty(displayNodes);
        
        _output.WriteLine($"转换为 {displayNodes.Count} 个 DisplayNode");
        
        // 验证每个 DisplayNode 都有正确的属性
        foreach (var displayNode in displayNodes.Take(10))
        {
            Assert.NotNull(displayNode.Label);
            Assert.NotNull(displayNode.Summary);
            Assert.True(displayNode.FieldNumber >= 0);
            
            _output.WriteLine($"  Field #{displayNode.FieldNumber}: {displayNode.Summary}");
        }

        _output.WriteLine("✅ DisplayNode 转换成功");
    }

    [Fact]
    public void Performance_ParseLargeGzip_MeasureThroughput()
    {
        var gzipFilePath = Path.Combine("log", "collect36131");
        if (!File.Exists(gzipFilePath))
        {
            throw new FileNotFoundException($"测试数据文件不存在: {gzipFilePath}");
        }

        // 解压到内存一次，复用相同数据做多次测量，避免 IO 干扰
        byte[] protobufData;
        using (var fileStream = File.OpenRead(gzipFilePath))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (var memoryStream = new MemoryStream())
        {
            gzipStream.CopyTo(memoryStream);
            protobufData = memoryStream.ToArray();
        }

        var iterations = 5;
        var times = new List<double>(iterations);
        int lastTotalNodes = 0;

        for (var i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var nodes = ProtoParser.Parse(protobufData);
            sw.Stop();

            var stats = AnalyzeNodes(nodes);
            lastTotalNodes = stats.TotalNodes;

            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        var dataMB = protobufData.Length / (1024.0 * 1024.0);
        var avgMs = times.Average();
        var p95Ms = times.OrderBy(t => t).ElementAt((int)Math.Ceiling(iterations * 0.95) - 1);
        var bestMs = times.Min();
        var worstMs = times.Max();

        var mbPerSec = dataMB / (avgMs / 1000.0);
        var nodesPerSec = lastTotalNodes / (avgMs / 1000.0);

        _output.WriteLine($"数据大小: {dataMB:F2} MB");
        _output.WriteLine($"迭代次数: {iterations}");
        _output.WriteLine($"平均耗时: {avgMs:F2} ms");
        _output.WriteLine($"P95 耗时: {p95Ms:F2} ms");
        _output.WriteLine($"最佳耗时: {bestMs:F2} ms");
        _output.WriteLine($"最差耗时: {worstMs:F2} ms");
        _output.WriteLine($"吞吐: {mbPerSec:F2} MB/s, {nodesPerSec:F0} nodes/s (总节点 {lastTotalNodes})");

        // 给一个保守阈值（本地 ARM Mac，~400KB 数据，应 < 200ms 平均）
        Assert.True(avgMs < 2000, $"解析过慢: 平均 {avgMs:F2} ms");
    }

    #region Helper Methods

    private NodeStats AnalyzeNodes(IReadOnlyList<ProtoNode> nodes)
    {
        var stats = new NodeStats();
        AnalyzeNodesRecursive(nodes, 1, stats);
        return stats;
    }

    private void AnalyzeNodesRecursive(IReadOnlyList<ProtoNode> nodes, int depth, NodeStats stats)
    {
        foreach (var node in nodes)
        {
            stats.TotalNodes++;
            stats.MaxDepth = Math.Max(stats.MaxDepth, depth);
            stats.MinFieldNumber = Math.Min(stats.MinFieldNumber, node.FieldNumber);
            stats.MaxFieldNumber = Math.Max(stats.MaxFieldNumber, node.FieldNumber);

            if (!stats.WireTypeCounts.ContainsKey(node.WireType))
            {
                stats.WireTypeCounts[node.WireType] = 0;
            }
            stats.WireTypeCounts[node.WireType]++;

            if (node.Children != null && node.Children.Count > 0)
            {
                stats.NestedNodes++;
                AnalyzeNodesRecursive(node.Children, depth + 1, stats);
            }
        }
    }

    private bool ContainsWireType(IReadOnlyList<ProtoNode> nodes, ProtoWireType wireType)
    {
        foreach (var node in nodes)
        {
            if (node.WireType == wireType)
                return true;

            if (node.Children != null && ContainsWireType(node.Children, wireType))
                return true;
        }
        return false;
    }

    private void PrintNode(ProtoNode node, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        var preview = GetNodePreview(node);
        _output.WriteLine($"{indentStr}Field #{node.FieldNumber} [{node.WireType}] {preview}");

        if (node.Children != null && node.Children.Count > 0)
        {
            _output.WriteLine($"{indentStr}  ↳ {node.Children.Count} children");
            // 只打印前3个子节点，避免输出过多
            foreach (var child in node.Children.Take(3))
            {
                PrintNode(child, indent + 1);
            }
            if (node.Children.Count > 3)
            {
                _output.WriteLine($"{indentStr}    ... and {node.Children.Count - 3} more");
            }
        }
    }

    private string GetNodePreview(ProtoNode node)
    {
        if (node.Children != null && node.Children.Count > 0)
        {
            return $"→ {node.Children.Count} nested nodes";
        }

        return node.WireType switch
        {
            ProtoWireType.Varint => $"→ varint ({node.RawValue.Length} bytes)",
            ProtoWireType.LengthDelimited => GetLengthDelimitedPreview(node.RawValue),
            ProtoWireType.Fixed32 => "→ fixed32",
            ProtoWireType.Fixed64 => "→ fixed64",
            _ => $"→ {node.RawValue.Length} bytes"
        };
    }

    private string GetLengthDelimitedPreview(ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0)
            return "→ empty";

        // 尝试解析为 UTF-8 字符串
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(data.Span);
            if (text.All(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t'))
            {
                var preview = text.Length > 30 ? text.Substring(0, 30) + "..." : text;
                return $"→ \"{preview}\" ({data.Length} bytes)";
            }
        }
        catch { }

        return $"→ {data.Length} bytes";
    }

    #endregion

    #region Helper Classes

    private class NodeStats
    {
        public int TotalNodes { get; set; }
        public int NestedNodes { get; set; }
        public int MaxDepth { get; set; }
        public int MinFieldNumber { get; set; } = int.MaxValue;
        public int MaxFieldNumber { get; set; } = int.MinValue;
        public Dictionary<ProtoWireType, int> WireTypeCounts { get; } = new();
    }

    #endregion
}

