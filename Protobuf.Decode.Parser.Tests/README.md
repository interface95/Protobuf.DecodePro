# Protobuf.Decode.Parser 单元测试

这是 Protobuf.Decode.Parser 库的完整单元测试套件。

## 📊 测试覆盖

### ProtoParser 测试 (ProtoParserTests.cs)

#### ✅ 基本 Wire Types 测试
- **Varint**
  - 简单 Varint 解析
  - 多个 Varint 字段
  - 大型 Varint 值 (uint64.MaxValue)
  
- **Fixed32**
  - Fixed32 类型解析
  - 字节序验证 (Little-Endian)
  
- **Fixed64**
  - Fixed64 类型解析
  - 8 字节数据验证
  
- **LengthDelimited**
  - UTF-8 字符串解析
  - 空字符串
  - 中文字符串 ("你好世界")
  - 二进制数据 (非 UTF-8)

#### ✅ 嵌套消息测试
- 单层嵌套消息
- 深度嵌套 (3 层)
- 嵌套消息中的字段验证

#### ✅ 重复字段 (数组) 测试
- Unpacked 重复 Varint
- Packed 重复 Varint
- 重复嵌套消息

#### ✅ 复杂混合类型测试
- 包含所有 wire types 的消息
- 多字段复杂消息

#### ✅ 边界情况和错误处理
- 空数据
- 大字段号 (16384)
- 不完整的 Varint
- 不完整的 Fixed32
- 无效的 Wire Type (3, 4, 6, 7)

#### ✅ PrettyPrint 功能测试
- 简单消息格式化输出
- 嵌套消息缩进输出

---

### ProtoDisplayNode 测试 (ProtoDisplayNodeTests.cs)

#### ✅ FromNodes 转换测试
- 简单消息转换为显示节点
- 嵌套消息层次结构
- 重复字段分组 (数组模式)
- 混合重复和单字段

#### ✅ VarintToValue 转换测试
- 小值转换 (1)
- 大值转换 (300)
- 零值

#### ✅ 标签和摘要测试
- Varint 节点标签和摘要
- 字符串节点 UTF-8 显示
- 中文字符串显示
- 嵌套消息子节点计数
- Fixed32/Fixed64 十六进制显示

#### ✅ 路径和字段显示测试
- 简单字段号显示
- 重复字段索引显示 (如 `3[2]`)
- 数组组计数显示

#### ✅ 原始数据预览测试
- 小数据十六进制预览
- 空数据处理

#### ✅ 错误节点测试
- 创建和验证错误节点

#### ✅ 复杂场景测试
- 二进制数据 (含控制字符) 不显示为 UTF-8
- 混合类型数据分组

---

## 🚀 运行测试

```bash
# 运行所有测试
dotnet test

# 运行测试并显示详细输出
dotnet test --verbosity detailed

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~ProtoParserTests"
dotnet test --filter "FullyQualifiedName~ProtoDisplayNodeTests"
```

---

## 📈 测试统计

- **总测试数**: 43
- **通过**: 43 ✅
- **失败**: 0
- **跳过**: 0

---

## 🧪 测试覆盖的功能

### Wire Types
- ✅ Varint (0)
- ✅ Fixed64 (1)
- ✅ LengthDelimited (2)
- ✅ Fixed32 (5)

### 数据类型
- ✅ 整数 (int32, int64, uint32, uint64)
- ✅ 字符串 (UTF-8)
- ✅ 字节数组 (bytes)
- ✅ 嵌套消息
- ✅ 重复字段 (packed & unpacked)

### 边界情况
- ✅ 空消息
- ✅ 空字符串
- ✅ 最大 Varint (uint64.MaxValue)
- ✅ 大字段号 (16384)
- ✅ 中文等 Unicode 字符
- ✅ 二进制数据 (非 UTF-8)

### 错误处理
- ✅ 不完整的 Varint
- ✅ 不完整的 Fixed32/64
- ✅ 无效的 Wire Type
- ✅ Varint 溢出

---

## 🎯 测试原则

1. **全面性**: 覆盖所有 wire types 和边界情况
2. **准确性**: 验证字节级别的正确性
3. **可读性**: 清晰的测试名称和注释
4. **隔离性**: 每个测试独立运行
5. **可维护性**: 使用辅助方法简化测试代码

---

## 📝 测试辅助方法

- `HexToBytes(string)`: 十六进制字符串转字节数组
- `EncodeVarint(ulong)`: 生成 Varint 编码
- `EncodeKey(int, ProtoWireType)`: 生成字段 key

这些辅助方法使得测试代码更简洁、更易读。

