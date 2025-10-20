# 嵌套消息中包含数组的测试示例

本文档展示了如何测试嵌套消息中包含数组的场景。

---

## 📦 示例 1: 嵌套消息内包含数组

### 数据结构

```protobuf
message Outer {
  Inner inner_field = 10;  // 嵌套消息
}

message Inner {
  int32 id = 1;              // 单个字段
  repeated int32 values = 2; // 数组字段
  string name = 3;           // 字符串字段
}
```

### 实际数据

```
Outer {
  inner_field: Inner {
    id: 100
    values: [1, 2, 3]
    name: "test"
  }
}
```

### 二进制编码结构

```
外层消息:
  └─ field 10 (LengthDelimited) → 嵌套消息
      ├─ field 1 (Varint) → 100
      ├─ field 2 (Varint) → 1
      ├─ field 2 (Varint) → 2
      ├─ field 2 (Varint) → 3
      └─ field 3 (LengthDelimited) → "test"
```

### 解析结果

```csharp
nodes.Count = 1 (外层只有一个字段)
nodes[0] {
    FieldNumber = 10
    WireType = LengthDelimited
    Children.Count = 5 (1个id + 3个values + 1个name)
    Children[0] { FieldNumber = 1, Value = 100 }
    Children[1] { FieldNumber = 2, Value = 1 }
    Children[2] { FieldNumber = 2, Value = 2 }
    Children[3] { FieldNumber = 2, Value = 3 }
    Children[4] { FieldNumber = 3, Value = "test" }
}
```

### 字节编码详解

```
外层消息 field 10:
  Key: 0x52 (10 << 3 | 2 = 82 = 0x52)
  Length: 嵌套消息的总长度
  
嵌套消息内容:
  field 1 (id=100):
    Key: 0x08 (1 << 3 | 0 = 8)
    Value: 0x64 (100的varint编码)
  
  field 2 (values[0]=1):
    Key: 0x10 (2 << 3 | 0 = 16)
    Value: 0x01
  
  field 2 (values[1]=2):
    Key: 0x10
    Value: 0x02
  
  field 2 (values[2]=3):
    Key: 0x10
    Value: 0x03
  
  field 3 (name="test"):
    Key: 0x1A (3 << 3 | 2 = 26)
    Length: 0x04
    Value: 74 65 73 74 ("test" 的 UTF-8)
```

---

## 🌲 示例 2: 多层嵌套，每层都包含数组

### 数据结构

```protobuf
message Level1 {
  repeated int32 outer_values = 1;  // 外层数组
  Level2 inner = 2;                 // 嵌套消息
}

message Level2 {
  repeated int32 inner_values = 1;  // 内层数组
  string description = 2;           // 字符串
}
```

### 实际数据

```
Level1 {
  outer_values: [1, 2]
  inner: Level2 {
    inner_values: [10, 20, 30]
    description: "inner"
  }
}
```

### 树形结构

```
根消息
├─ field 1 (Varint) → 1         ┐
├─ field 1 (Varint) → 2         ┘ 外层数组
└─ field 2 (LengthDelimited) → 嵌套消息
    ├─ field 1 (Varint) → 10    ┐
    ├─ field 1 (Varint) → 20    │ 内层数组
    ├─ field 1 (Varint) → 30    ┘
    └─ field 2 (LengthDelimited) → "inner"
```

### 解析结果

```csharp
外层 nodes.Count = 3
nodes[0] { FieldNumber = 1, Value = 1 }
nodes[1] { FieldNumber = 1, Value = 2 }
nodes[2] { 
    FieldNumber = 2
    WireType = LengthDelimited
    Children.Count = 4
    Children[0] { FieldNumber = 1, Value = 10 }
    Children[1] { FieldNumber = 1, Value = 20 }
    Children[2] { FieldNumber = 1, Value = 30 }
    Children[3] { FieldNumber = 2, Value = "inner" }
}
```

### 可视化展示

```
                    ┌─────────────┐
                    │  根消息      │
                    └──────┬──────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
    ┌─────▼─────┐    ┌─────▼─────┐    ┌────▼────┐
    │  field 1  │    │  field 1  │    │ field 2 │
    │  value: 1 │    │  value: 2 │    │ (嵌套)  │
    └───────────┘    └───────────┘    └────┬────┘
                                            │
                          ┌─────────────────┼─────────────────┐
                          │                 │                 │
                    ┌─────▼─────┐    ┌─────▼─────┐    ┌─────▼─────┐    ┌──────────┐
                    │  field 1  │    │  field 1  │    │  field 1  │    │ field 2  │
                    │ value: 10 │    │ value: 20 │    │ value: 30 │    │"inner"   │
                    └───────────┘    └───────────┘    └───────────┘    └──────────┘
```

---

## 🎯 测试要点

### 1. **嵌套消息的识别**
- 解析器能正确识别 `LengthDelimited` wire type
- 自动递归解析嵌套的消息内容

### 2. **数组字段的处理**
- 重复的字段号被正确解析为多个独立节点
- 每个数组元素保持其原始顺序

### 3. **混合字段类型**
- 在嵌套消息中同时包含:
  - 单个值字段 (id, description)
  - 数组字段 (values, outer_values, inner_values)
  - 字符串字段 (name, description)

### 4. **多层嵌套验证**
- 外层和内层的数组都能正确解析
- 层次关系保持完整

---

## 💡 实际应用场景

### 场景 1: 用户信息 + 历史记录
```protobuf
message UserProfile {
  int32 user_id = 1;
  repeated string tags = 2;        // 用户标签数组
  LoginHistory history = 3;        // 嵌套的登录历史
}

message LoginHistory {
  repeated int64 timestamps = 1;   // 登录时间戳数组
  string last_ip = 2;
}
```

### 场景 2: 订单 + 商品列表
```protobuf
message Order {
  string order_id = 1;
  repeated OrderItem items = 2;    // 商品列表
}

message OrderItem {
  int32 product_id = 1;
  int32 quantity = 2;
  repeated string options = 3;     // 商品选项数组
}
```

### 场景 3: 配置树
```protobuf
message ConfigNode {
  string name = 1;
  repeated string values = 2;      // 配置值数组
  repeated ConfigNode children = 3; // 子节点（递归嵌套）
}
```

---

## 🧪 测试代码位置

这些测试位于:
- **文件**: `ProtoParserTests.cs`
- **测试方法**:
  - `Parse_NestedMessageWithArray_Success()`
  - `Parse_MultiLevelNestedWithArrays_Success()`

运行测试:
```bash
dotnet test --filter "FullyQualifiedName~NestedMessageWithArray"
```

---

## 📊 测试覆盖率增加

| 测试项 | 原有 | 新增 | 总计 |
|--------|------|------|------|
| 嵌套消息 | ✅ | ✅ | ✅✅ |
| 重复字段 | ✅ | ✅ | ✅✅ |
| 嵌套+数组组合 | ❌ | ✅✅ | ✅✅ |
| **总测试数** | 43 | +2 | **45** |

---

## ✅ 验证结果

```bash
Passed!  - Failed:     0, Passed:    45, Skipped:     0, Total:    45
```

所有测试全部通过！🎉

