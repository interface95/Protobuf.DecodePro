using Xunit;
using Protobuf.Decode.Parser;

namespace Protobuf.Decode.Parser.Tests;

/// <summary>
/// 测试 ProtoDisplayNode 的显示和转换功能
/// </summary>
public class ProtoDisplayNodeTests
{
    #region FromNodes Tests
    
    [Fact]
    public void FromNodes_SimpleMessage_CreatesDisplayNodes()
    {
        // Arrange
        var nodes = new List<ProtoNode>
        {
            new ProtoNode
            {
                FieldNumber = 1,
                WireType = ProtoWireType.Varint,
                RawValue = new byte[] { 0x96, 0x01 } // varint 150
            }
        };
        
        // Act
        var displayNodes = ProtoDisplayNode.FromNodes(nodes);
        
        // Assert
        Assert.Single(displayNodes);
        var displayNode = displayNodes[0];
        Assert.Equal(1, displayNode.FieldNumber);
        Assert.Equal(ProtoWireType.Varint, displayNode.WireType);
        Assert.NotEmpty(displayNode.Label);
        Assert.NotEmpty(displayNode.Summary);
    }
    
    [Fact]
    public void FromNodes_NestedMessage_CreatesHierarchy()
    {
        // Arrange
        var childNode = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x2A } // varint 42
        };
        
        var parentNode = new ProtoNode
        {
            FieldNumber = 2,
            WireType = ProtoWireType.LengthDelimited,
            RawValue = new byte[] { 0x08, 0x2A },
            Children = new List<ProtoNode> { childNode }
        };
        
        var nodes = new List<ProtoNode> { parentNode };
        
        // Act
        var displayNodes = ProtoDisplayNode.FromNodes(nodes);
        
        // Assert
        Assert.Single(displayNodes);
        var parent = displayNodes[0];
        Assert.Single(parent.Children);
        Assert.Equal(1, parent.Children[0].FieldNumber);
    }
    
    [Fact]
    public void FromNodes_RepeatedFields_GroupsCorrectly()
    {
        // Arrange: 同一字段号重复 3 次
        var nodes = new List<ProtoNode>
        {
            new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x01 } },
            new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x02 } },
            new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x03 } }
        };
        
        // Act
        var displayNodes = ProtoDisplayNode.FromNodes(nodes);
        
        // Assert
        Assert.Single(displayNodes); // 应该分组成一个数组节点
        var arrayNode = displayNodes[0];
        Assert.True(arrayNode.IsArrayGroup);
        Assert.Equal(3, arrayNode.Children.Count);
    }
    
    #endregion

    #region VarintToValue Tests
    
    [Fact]
    public void VarintToValue_SmallValue_ReturnsCorrectValue()
    {
        // Arrange
        var bytes = new byte[] { 0x01 }; // varint 1
        
        // Act
        var value = ProtoDisplayNode.VarintToValue(bytes);
        
        // Assert
        Assert.Equal(1, value);
    }
    
    [Fact]
    public void VarintToValue_LargeValue_ReturnsCorrectValue()
    {
        // Arrange: varint 300 = 0xAC 0x02
        var bytes = new byte[] { 0xAC, 0x02 };
        
        // Act
        var value = ProtoDisplayNode.VarintToValue(bytes);
        
        // Assert
        Assert.Equal(300, value);
    }
    
    [Fact]
    public void VarintToValue_ZeroValue_ReturnsZero()
    {
        // Arrange
        var bytes = new byte[] { 0x00 };
        
        // Act
        var value = ProtoDisplayNode.VarintToValue(bytes);
        
        // Assert
        Assert.Equal(0, value);
    }
    
    #endregion

    #region Label and Summary Tests
    
    [Fact]
    public void Label_VarintNode_ContainsFieldNumberAndValue()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 5,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x2A } // 42
        };
        var displayNode = new ProtoDisplayNode(node, "5");
        
        // Act
        var label = displayNode.Label;
        
        // Assert
        Assert.Contains("#5", label);
        Assert.Contains("Varint", label);
    }
    
    [Fact]
    public void Summary_VarintNode_ShowsValue()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x96, 0x01 } // 150
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.Contains("Varint", summary);
        Assert.Contains("150", summary);
    }
    
    [Fact]
    public void Summary_StringNode_ShowsUTF8Text()
    {
        // Arrange
        var stringBytes = System.Text.Encoding.UTF8.GetBytes("Hello");
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.LengthDelimited,
            RawValue = stringBytes
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.Contains("UTF8", summary);
        Assert.Contains("Hello", summary);
    }
    
    [Fact]
    public void Summary_ChineseString_ShowsCorrectly()
    {
        // Arrange
        var stringBytes = System.Text.Encoding.UTF8.GetBytes("你好");
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.LengthDelimited,
            RawValue = stringBytes
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.Contains("UTF8", summary);
        Assert.Contains("你好", summary);
    }
    
    [Fact]
    public void Summary_NestedMessage_ShowsChildCount()
    {
        // Arrange
        var childNode = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x01 }
        };
        var parentNode = new ProtoNode
        {
            FieldNumber = 2,
            WireType = ProtoWireType.LengthDelimited,
            RawValue = new byte[] { 0x08, 0x01 },
            Children = new List<ProtoNode> { childNode }
        };
        var displayNode = new ProtoDisplayNode(parentNode, "2");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.Contains("嵌套类", summary);
        Assert.Contains("1", summary);
        Assert.Contains("子节点", summary);
    }
    
    [Fact]
    public void Summary_Fixed32_ShowsHexValue()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.Fixed32,
            RawValue = new byte[] { 0x12, 0x34, 0x56, 0x78 }
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.Contains("Fixed32", summary);
        Assert.Contains("0x", summary);
    }
    
    [Fact]
    public void Summary_Fixed64_ShowsHexValue()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.Fixed64,
            RawValue = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.Contains("Fixed64", summary);
        Assert.Contains("0x", summary);
    }
    
    #endregion

    #region Path and Field Display Tests
    
    [Fact]
    public void FieldDisplay_SimpleField_ShowsFieldNumber()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 5,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x01 }
        };
        var displayNode = new ProtoDisplayNode(node, "5");
        
        // Act
        var fieldDisplay = displayNode.FieldDisplay;
        
        // Assert
        Assert.Equal("5", fieldDisplay);
    }
    
    [Fact]
    public void FieldDisplay_RepeatedField_ShowsWithIndex()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 3,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x01 }
        };
        var displayNode = new ProtoDisplayNode(node, "3[2]"); // 第二个重复字段
        
        // Act
        var fieldDisplay = displayNode.FieldDisplay;
        var isRepeated = displayNode.IsRepeated;
        var occurrenceIndex = displayNode.OccurrenceIndex;
        
        // Assert
        Assert.True(isRepeated);
        Assert.Equal(2, occurrenceIndex);
        Assert.Contains("[2]", fieldDisplay);
    }
    
    [Fact]
    public void FieldDisplay_ArrayGroup_ShowsCount()
    {
        // Arrange: 创建一个数组组
        var children = new List<ProtoDisplayNode>
        {
            new ProtoDisplayNode(
                new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x01 } },
                "1[1]"
            ),
            new ProtoDisplayNode(
                new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x02 } },
                "1[2]"
            )
        };
        
        var arrayGroup = new ProtoDisplayNode(
            "#1 数组",
            null,
            "1",
            children,
            1,
            ProtoWireType.Varint,
            "数组 · 2 个元素",
            ""
        );
        
        // Act
        var fieldDisplay = arrayGroup.FieldDisplay;
        var isArrayGroup = arrayGroup.IsArrayGroup;
        
        // Assert
        Assert.True(isArrayGroup);
        Assert.Contains("[2]", fieldDisplay);
    }
    
    #endregion

    #region RawPreview Tests
    
    [Fact]
    public void RawPreview_SmallData_ShowsHexBytes()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.Varint,
            RawValue = new byte[] { 0x01, 0x02, 0x03 }
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var rawPreview = displayNode.RawPreview;
        
        // Assert
        Assert.NotEmpty(rawPreview);
        Assert.Contains("01", rawPreview);
        Assert.Contains("02", rawPreview);
        Assert.Contains("03", rawPreview);
    }
    
    [Fact]
    public void RawPreview_EmptyData_ReturnsEmpty()
    {
        // Arrange
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.LengthDelimited,
            RawValue = Array.Empty<byte>()
        };
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var rawPreview = displayNode.RawPreview;
        
        // Assert
        Assert.Empty(rawPreview);
    }
    
    #endregion

    #region Error Node Tests
    
    [Fact]
    public void CreateError_ReturnsErrorNode()
    {
        // Arrange & Act
        var errorNode = ProtoDisplayNode.CreateError("解析失败");
        
        // Assert
        Assert.True(errorNode.IsError);
        Assert.Equal("解析失败", errorNode.Label);
        Assert.Equal("解析失败", errorNode.Summary);
        Assert.Empty(errorNode.Children);
    }
    
    #endregion

    #region Complex Scenarios Tests
    
    [Fact]
    public void FromNodes_MixedRepeatedAndSingle_GroupsCorrectly()
    {
        // Arrange: field 1 (重复), field 2 (单个), field 1 (重复)
        var nodes = new List<ProtoNode>
        {
            new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x01 } },
            new ProtoNode { FieldNumber = 2, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x05 } },
            new ProtoNode { FieldNumber = 1, WireType = ProtoWireType.Varint, RawValue = new byte[] { 0x02 } }
        };
        
        // Act
        var displayNodes = ProtoDisplayNode.FromNodes(nodes);
        
        // Assert
        Assert.Equal(2, displayNodes.Count); // field 1 (array), field 2
        
        var field1 = displayNodes[0];
        Assert.Equal(1, field1.FieldNumber);
        Assert.True(field1.IsArrayGroup);
        Assert.Equal(2, field1.Children.Count);
        
        var field2 = displayNodes[1];
        Assert.Equal(2, field2.FieldNumber);
        Assert.False(field2.IsArrayGroup);
    }
    
    [Fact]
    public void FromNodes_BinaryDataWithControlCharacters_DoesNotShowAsUTF8()
    {
        // Arrange: 包含控制字符的二进制数据
        var binaryData = new byte[] { 0x00, 0x01, 0x02, 0xFF };
        var node = new ProtoNode
        {
            FieldNumber = 1,
            WireType = ProtoWireType.LengthDelimited,
            RawValue = binaryData
        };
        
        var displayNode = new ProtoDisplayNode(node, "1");
        
        // Act
        var summary = displayNode.Summary;
        
        // Assert
        Assert.DoesNotContain("UTF8", summary);
        Assert.Contains("Bytes", summary);
    }
    
    #endregion
}

