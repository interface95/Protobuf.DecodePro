using Xunit;
using Protobuf.Decode.Parser;
using Xunit.Abstractions;

namespace Protobuf.Decode.Parser.Tests;

/// <summary>
/// 测试 ProtoParser 的各种场景
/// </summary>
public class ProtoParserTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ProtoParserTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    #region Helper Methods
    
    /// <summary>
    /// 将十六进制字符串转换为字节数组
    /// </summary>
    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
    
    /// <summary>
    /// 创建 Varint 编码的字节
    /// </summary>
    private static byte[] EncodeVarint(ulong value)
    {
        var bytes = new List<byte>();
        while (value > 0x7F)
        {
            bytes.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        bytes.Add((byte)value);
        return bytes.ToArray();
    }
    
    /// <summary>
    /// 创建字段的 key (field_number << 3 | wire_type)
    /// </summary>
    private static byte[] EncodeKey(int fieldNumber, ProtoWireType wireType)
    {
        ulong key = (ulong)((fieldNumber << 3) | (int)wireType);
        return EncodeVarint(key);
    }
    
    #endregion

    #region Basic Wire Types Tests
    
    [Fact]
    public void Parse_SimpleVarint_Success()
    {
        // Arrange: field 1 = varint 150
        // Key: field=1, wire_type=0 -> (1 << 3 | 0) = 0x08
        // Value: 150 -> 0x96 0x01
        var data = HexToBytes("08 96 01");
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(1, node.FieldNumber);
        Assert.Equal(ProtoWireType.Varint, node.WireType);
        Assert.Equal(2, node.RawValue.Length); // varint 150 占 2 字节
    }
    
    [Fact]
    public void Parse_MultipleVarints_Success()
    {
        // Arrange: 
        // field 1 = varint 1
        // field 2 = varint 300
        var key1 = EncodeKey(1, ProtoWireType.Varint);
        var value1 = EncodeVarint(1);
        var key2 = EncodeKey(2, ProtoWireType.Varint);
        var value2 = EncodeVarint(300);
        
        var data = key1.Concat(value1).Concat(key2).Concat(value2).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Equal(2, nodes.Count);
        Assert.Equal(1, nodes[0].FieldNumber);
        Assert.Equal(2, nodes[1].FieldNumber);
        Assert.All(nodes, n => Assert.Equal(ProtoWireType.Varint, n.WireType));
    }
    
    [Fact]
    public void Parse_Fixed32_Success()
    {
        // Arrange: field 1 = fixed32 (0x12345678)
        // Key: field=1, wire_type=5 -> (1 << 3 | 5) = 0x0D
        // Value: 0x78563412 (little-endian)
        var data = HexToBytes("0D 78 56 34 12");
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(1, node.FieldNumber);
        Assert.Equal(ProtoWireType.Fixed32, node.WireType);
        Assert.Equal(4, node.RawValue.Length);
    }
    
    [Fact]
    public void Parse_Fixed64_Success()
    {
        // Arrange: field 1 = fixed64
        // Key: field=1, wire_type=1 -> (1 << 3 | 1) = 0x09
        // Value: 8 bytes
        var data = HexToBytes("09 01 02 03 04 05 06 07 08");
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(1, node.FieldNumber);
        Assert.Equal(ProtoWireType.Fixed64, node.WireType);
        Assert.Equal(8, node.RawValue.Length);
    }
    
    [Fact]
    public void Parse_LengthDelimited_String_Success()
    {
        // Arrange: field 1 = string "testing"
        // Key: field=1, wire_type=2 -> (1 << 3 | 2) = 0x0A
        // Length: 7
        // Value: "testing" in UTF-8
        var stringBytes = System.Text.Encoding.UTF8.GetBytes("testing");
        var key = EncodeKey(1, ProtoWireType.LengthDelimited);
        var length = EncodeVarint((ulong)stringBytes.Length);
        var data = key.Concat(length).Concat(stringBytes).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(1, node.FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, node.WireType);
        Assert.Equal(7, node.RawValue.Length);
        
        // 验证内容
        var actual = System.Text.Encoding.UTF8.GetString(node.RawValue.Span);
        Assert.Equal("testing", actual);
    }
    
    [Fact]
    public void Parse_EmptyLengthDelimited_Success()
    {
        // Arrange: field 1 = empty string
        var data = HexToBytes("0A 00"); // key=0x0A, length=0
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(1, node.FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, node.WireType);
        Assert.True(node.RawValue.IsEmpty);
    }
    
    #endregion

    #region Nested Message Tests
    
    [Fact]
    public void Parse_NestedMessage_Success()
    {
        // Arrange: field 3 包含嵌套消息
        // 嵌套消息: field 1 = varint 150
        var nestedKey = EncodeKey(1, ProtoWireType.Varint);
        var nestedValue = EncodeVarint(150);
        var nestedMessage = nestedKey.Concat(nestedValue).ToArray();
        
        // 外层消息: field 3 = length-delimited (嵌套消息)
        var outerKey = EncodeKey(3, ProtoWireType.LengthDelimited);
        var length = EncodeVarint((ulong)nestedMessage.Length);
        var data = outerKey.Concat(length).Concat(nestedMessage).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(3, node.FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, node.WireType);
        Assert.NotNull(node.Children);
        Assert.Single(node.Children!);
        
        // 验证嵌套节点
        var nestedNode = node.Children[0];
        Assert.Equal(1, nestedNode.FieldNumber);
        Assert.Equal(ProtoWireType.Varint, nestedNode.WireType);
    }
    
    [Fact]
    public void Parse_DeepNestedMessage_Success()
    {
        // Arrange: 三层嵌套
        // Level 3: field 1 = varint 42
        var level3Key = EncodeKey(1, ProtoWireType.Varint);
        var level3Value = EncodeVarint(42);
        var level3 = level3Key.Concat(level3Value).ToArray();
        
        // Level 2: field 2 = nested message (level3)
        var level2Key = EncodeKey(2, ProtoWireType.LengthDelimited);
        var level2Length = EncodeVarint((ulong)level3.Length);
        var level2 = level2Key.Concat(level2Length).Concat(level3).ToArray();
        
        // Level 1: field 3 = nested message (level2)
        var level1Key = EncodeKey(3, ProtoWireType.LengthDelimited);
        var level1Length = EncodeVarint((ulong)level2.Length);
        var data = level1Key.Concat(level1Length).Concat(level2).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        Assert.NotNull(nodes[0].Children);
        Assert.Single(nodes[0].Children!);
        Assert.NotNull(nodes[0].Children![0].Children);
        Assert.Single(nodes[0].Children![0].Children!);
        
        var deepestNode = nodes[0].Children![0].Children![0];
        Assert.Equal(1, deepestNode.FieldNumber);
        Assert.Equal(ProtoWireType.Varint, deepestNode.WireType);
    }
    
    #endregion

    #region Repeated Fields (Array) Tests
    
    [Fact]
    public void Parse_RepeatedVarint_Success()
    {
        // Arrange: field 4 重复 3 次
        // field 4 = 1
        // field 4 = 2
        // field 4 = 3
        var items = new List<byte>();
        for (int i = 1; i <= 3; i++)
        {
            items.AddRange(EncodeKey(4, ProtoWireType.Varint));
            items.AddRange(EncodeVarint((ulong)i));
        }
        var data = items.ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Equal(3, nodes.Count);
        Assert.All(nodes, n => Assert.Equal(4, n.FieldNumber));
        Assert.All(nodes, n => Assert.Equal(ProtoWireType.Varint, n.WireType));
    }
    
    [Fact]
    public void Parse_PackedRepeatedVarint_Success()
    {
        // Arrange: Packed repeated field
        // field 4 = [1, 2, 3] (packed)
        // Key: field=4, wire_type=2
        // Length: 3
        // Values: 0x01, 0x02, 0x03
        var packedValues = new byte[] { 0x01, 0x02, 0x03 };
        var key = EncodeKey(4, ProtoWireType.LengthDelimited);
        var length = EncodeVarint((ulong)packedValues.Length);
        var data = key.Concat(length).Concat(packedValues).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(4, node.FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, node.WireType);
        Assert.Equal(3, node.RawValue.Length);
    }
    
    [Fact]
    public void Parse_RepeatedNestedMessage_Success()
    {
        // Arrange: field 5 重复 2 次嵌套消息
        var items = new List<byte>();
        
        for (int i = 1; i <= 2; i++)
        {
            // 嵌套消息: field 1 = varint i
            var nestedKey = EncodeKey(1, ProtoWireType.Varint);
            var nestedValue = EncodeVarint((ulong)i);
            var nestedMessage = nestedKey.Concat(nestedValue).ToArray();
            
            // field 5 = nested message
            items.AddRange(EncodeKey(5, ProtoWireType.LengthDelimited));
            items.AddRange(EncodeVarint((ulong)nestedMessage.Length));
            items.AddRange(nestedMessage);
        }
        
        var data = items.ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Equal(2, nodes.Count);
        Assert.All(nodes, n => Assert.Equal(5, n.FieldNumber));
        Assert.All(nodes, n => Assert.NotNull(n.Children));
        Assert.All(nodes, n => Assert.Single(n.Children!));
    }
    
    [Fact]
    public void Parse_NestedMessageWithArray_Success()
    {
        // Arrange: 嵌套消息内包含数组
        // 外层消息: field 10 包含一个嵌套消息
        // 嵌套消息包含:
        //   - field 1 = varint 100 (单个字段)
        //   - field 2 = varint 1, 2, 3 (重复字段/数组)
        //   - field 3 = string "test"
        
        var nestedItems = new List<byte>();
        
        // field 1 = varint 100
        nestedItems.AddRange(EncodeKey(1, ProtoWireType.Varint));
        nestedItems.AddRange(EncodeVarint(100));
        
        // field 2 = 数组 [1, 2, 3]
        for (var i = 1; i <= 3; i++)
        {
            nestedItems.AddRange(EncodeKey(2, ProtoWireType.Varint));
            nestedItems.AddRange(EncodeVarint((ulong)i));
        }
        
        // field 3 = string "test"
        var stringBytes = System.Text.Encoding.UTF8.GetBytes("test");
        nestedItems.AddRange(EncodeKey(3, ProtoWireType.LengthDelimited));
        nestedItems.AddRange(EncodeVarint((ulong)stringBytes.Length));
        nestedItems.AddRange(stringBytes);
        
        var nestedMessage = nestedItems.ToArray();
        
        // 外层消息: field 10 = nested message
        var outerItems = new List<byte>();
        outerItems.AddRange(EncodeKey(10, ProtoWireType.LengthDelimited));
        outerItems.AddRange(EncodeVarint((ulong)nestedMessage.Length));
        outerItems.AddRange(nestedMessage);
        
        var data = outerItems.ToArray();
        _testOutputHelper.WriteLine(Convert.ToHexStringLower(data));
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes); // 只有一个外层字段
        var outerNode = nodes[0];
        Assert.Equal(10, outerNode.FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, outerNode.WireType);
        Assert.NotNull(outerNode.Children);
        
        // 验证嵌套消息内容: 应该有 5 个节点 (field 1 x1, field 2 x3, field 3 x1)
        var nestedNodes = outerNode.Children!;
        Assert.Equal(5, nestedNodes.Count);
        
        // field 1 = varint 100
        Assert.Equal(1, nestedNodes[0].FieldNumber);
        Assert.Equal(ProtoWireType.Varint, nestedNodes[0].WireType);
        
        // field 2 = 数组 [1, 2, 3]
        Assert.Equal(2, nestedNodes[1].FieldNumber);
        Assert.Equal(ProtoWireType.Varint, nestedNodes[1].WireType);
        Assert.Equal(2, nestedNodes[2].FieldNumber);
        Assert.Equal(ProtoWireType.Varint, nestedNodes[2].WireType);
        Assert.Equal(2, nestedNodes[3].FieldNumber);
        Assert.Equal(ProtoWireType.Varint, nestedNodes[3].WireType);
        
        // field 3 = string "test"
        Assert.Equal(3, nestedNodes[4].FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, nestedNodes[4].WireType);
        var actualString = System.Text.Encoding.UTF8.GetString(nestedNodes[4].RawValue.Span);
        Assert.Equal("test", actualString);
    }
    
    [Fact]
    public void Parse_MultiLevelNestedWithArrays_Success()
    {
        // Arrange: 多层嵌套，每层都包含数组
        // Level 1 (外层):
        //   - field 1 = varint 1, 2 (数组)
        //   - field 2 = nested message (包含数组)
        
        // Level 2 (嵌套层):
        //   - field 1 = varint 10, 20, 30 (数组)
        //   - field 2 = string "inner"
        
        // 构建 Level 2
        var level2Items = new List<byte>();
        
        // field 1 = 数组 [10, 20, 30]
        foreach (var value in new[] { 10, 20, 30 })
        {
            level2Items.AddRange(EncodeKey(1, ProtoWireType.Varint));
            level2Items.AddRange(EncodeVarint((ulong)value));
        }
        
        // field 2 = string "inner"
        var innerString = System.Text.Encoding.UTF8.GetBytes("inner");
        level2Items.AddRange(EncodeKey(2, ProtoWireType.LengthDelimited));
        level2Items.AddRange(EncodeVarint((ulong)innerString.Length));
        level2Items.AddRange(innerString);
        
        var level2Message = level2Items.ToArray();
        
        // 构建 Level 1
        var level1Items = new List<byte>();
        
        // field 1 = 数组 [1, 2]
        foreach (var value in new[] { 1, 2 })
        {
            level1Items.AddRange(EncodeKey(1, ProtoWireType.Varint));
            level1Items.AddRange(EncodeVarint((ulong)value));
        }
        
        // field 2 = nested message (level2)
        level1Items.AddRange(EncodeKey(2, ProtoWireType.LengthDelimited));
        level1Items.AddRange(EncodeVarint((ulong)level2Message.Length));
        level1Items.AddRange(level2Message);
        
        var data = level1Items.ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Equal(3, nodes.Count); // field 1 x2, field 2 x1
        
        // 验证外层数组
        Assert.Equal(1, nodes[0].FieldNumber);
        Assert.Equal(1, nodes[1].FieldNumber);
        
        // 验证嵌套消息
        var nestedNode = nodes[2];
        Assert.Equal(2, nestedNode.FieldNumber);
        Assert.NotNull(nestedNode.Children);
        
        // 验证嵌套消息内的数组和字符串
        var innerNodes = nestedNode.Children!;
        Assert.Equal(4, innerNodes.Count); // field 1 x3, field 2 x1
        
        // 验证内层数组
        Assert.Equal(1, innerNodes[0].FieldNumber);
        Assert.Equal(1, innerNodes[1].FieldNumber);
        Assert.Equal(1, innerNodes[2].FieldNumber);
        
        // 验证内层字符串
        Assert.Equal(2, innerNodes[3].FieldNumber);
        var str = System.Text.Encoding.UTF8.GetString(innerNodes[3].RawValue.Span);
        Assert.Equal("inner", str);
    }
    
    #endregion

    #region Complex Mixed Types Tests
    
    [Fact]
    public void Parse_ComplexMessage_AllTypes_Success()
    {
        // Arrange: 复杂消息包含所有类型
        var items = new List<byte>();
        
        // field 1 = varint 150
        items.AddRange(EncodeKey(1, ProtoWireType.Varint));
        items.AddRange(EncodeVarint(150));
        
        // field 2 = string "hello"
        var stringBytes = System.Text.Encoding.UTF8.GetBytes("hello");
        items.AddRange(EncodeKey(2, ProtoWireType.LengthDelimited));
        items.AddRange(EncodeVarint((ulong)stringBytes.Length));
        items.AddRange(stringBytes);
        
        // field 3 = fixed32
        items.AddRange(EncodeKey(3, ProtoWireType.Fixed32));
        items.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        
        // field 4 = fixed64
        items.AddRange(EncodeKey(4, ProtoWireType.Fixed64));
        items.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
        
        // field 5 = nested message (field 1 = varint 99)
        var nestedKey = EncodeKey(1, ProtoWireType.Varint);
        var nestedValue = EncodeVarint(99);
        var nestedMessage = nestedKey.Concat(nestedValue).ToArray();
        items.AddRange(EncodeKey(5, ProtoWireType.LengthDelimited));
        items.AddRange(EncodeVarint((ulong)nestedMessage.Length));
        items.AddRange(nestedMessage);
        
        var data = items.ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Equal(5, nodes.Count);
        
        Assert.Equal(1, nodes[0].FieldNumber);
        Assert.Equal(ProtoWireType.Varint, nodes[0].WireType);
        
        Assert.Equal(2, nodes[1].FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, nodes[1].WireType);
        
        Assert.Equal(3, nodes[2].FieldNumber);
        Assert.Equal(ProtoWireType.Fixed32, nodes[2].WireType);
        
        Assert.Equal(4, nodes[3].FieldNumber);
        Assert.Equal(ProtoWireType.Fixed64, nodes[3].WireType);
        
        Assert.Equal(5, nodes[4].FieldNumber);
        Assert.Equal(ProtoWireType.LengthDelimited, nodes[4].WireType);
        Assert.NotNull(nodes[4].Children);
    }
    
    #endregion

    #region Edge Cases and Error Handling Tests
    
    [Fact]
    public void Parse_EmptyData_ReturnsEmpty()
    {
        // Arrange
        var data = Array.Empty<byte>();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Empty(nodes);
    }
    
    [Fact]
    public void Parse_LargeFieldNumber_Success()
    {
        // Arrange: field 16384 = varint 1
        var key = EncodeKey(16384, ProtoWireType.Varint);
        var value = EncodeVarint(1);
        var data = key.Concat(value).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        Assert.Equal(16384, nodes[0].FieldNumber);
    }
    
    [Fact]
    public void Parse_LargeVarint_Success()
    {
        // Arrange: field 1 = varint (max uint64)
        var key = EncodeKey(1, ProtoWireType.Varint);
        var value = EncodeVarint(ulong.MaxValue);
        var data = key.Concat(value).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        Assert.Equal(1, nodes[0].FieldNumber);
        Assert.Equal(10, nodes[0].RawValue.Length); // max varint 占 10 字节
    }
    
    [Fact]
    public void Parse_ChineseString_Success()
    {
        // Arrange: field 1 = string "你好世界"
        var stringBytes = System.Text.Encoding.UTF8.GetBytes("你好世界");
        var key = EncodeKey(1, ProtoWireType.LengthDelimited);
        var length = EncodeVarint((ulong)stringBytes.Length);
        var data = key.Concat(length).Concat(stringBytes).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        var actual = System.Text.Encoding.UTF8.GetString(node.RawValue.Span);
        Assert.Equal("你好世界", actual);
    }
    
    [Fact]
    public void Parse_BinaryData_Success()
    {
        // Arrange: field 1 = bytes (非 UTF-8 数据)
        var binaryData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
        var key = EncodeKey(1, ProtoWireType.LengthDelimited);
        var length = EncodeVarint((ulong)binaryData.Length);
        var data = key.Concat(length).Concat(binaryData).ToArray();
        
        // Act
        var nodes = ProtoParser.Parse(data);
        
        // Assert
        Assert.Single(nodes);
        var node = nodes[0];
        Assert.Equal(4, node.RawValue.Length);
        Assert.True(binaryData.SequenceEqual(node.RawValue.ToArray()));
    }
    
    [Fact]
    public void Parse_IncompleteVarint_ThrowsException()
    {
        // Arrange: varint 不完整 (缺少终止字节)
        var data = new byte[] { 0x08, 0x96, 0x80 }; // 最后一个字节应该 < 0x80
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ProtoParser.Parse(data));
    }
    
    [Fact]
    public void Parse_IncompleteFixed32_ThrowsException()
    {
        // Arrange: fixed32 不完整 (只有 2 字节)
        var data = new byte[] { 0x0D, 0x01, 0x02 }; // 应该有 4 字节
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ProtoParser.Parse(data));
    }
    
    [Fact]
    public void Parse_InvalidWireType_ThrowsException()
    {
        // Arrange: 无效的 wire type (3, 4, 6, 7 都是无效的)
        var data = new byte[] { 0x0B }; // field=1, wire_type=3 (无效)
        
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => ProtoParser.Parse(data));
    }
    
    #endregion

    #region PrettyPrint Tests
    
    [Fact]
    public void PrettyPrint_SimpleMessage_ReturnsFormattedString()
    {
        // Arrange
        var data = HexToBytes("08 96 01"); // field 1 = varint 150
        var nodes = ProtoParser.Parse(data);
        
        // Act
        var output = ProtoParser.PrettyPrint(nodes);
        
        // Assert
        Assert.NotEmpty(output);
        Assert.Contains("Field 1", output);
        Assert.Contains("Varint", output);
    }
    
    [Fact]
    public void PrettyPrint_NestedMessage_ReturnsIndentedOutput()
    {
        // Arrange: 嵌套消息
        var nestedKey = EncodeKey(1, ProtoWireType.Varint);
        var nestedValue = EncodeVarint(150);
        var nestedMessage = nestedKey.Concat(nestedValue).ToArray();
        
        var outerKey = EncodeKey(3, ProtoWireType.LengthDelimited);
        var length = EncodeVarint((ulong)nestedMessage.Length);
        var data = outerKey.Concat(length).Concat(nestedMessage).ToArray();
        
        var nodes = ProtoParser.Parse(data);
        
        // Act
        var output = ProtoParser.PrettyPrint(nodes);
        
        // Assert
        Assert.Contains("{", output);
        Assert.Contains("}", output);
        Assert.Contains("Field 3", output);
        Assert.Contains("Field 1", output);
    }
    
    #endregion
}

