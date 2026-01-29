# MSpeaker - 对话系统引擎

一个强大、灵活的Unity对话系统框架，为游戏开发者提供完整的对话管理、解析、执行和UI渲染解决方案。

## 主要特性

- **自定义对话脚本格式**: 使用.msp格式定义对话，支持直观的脚本语言
- **灵活的对话引擎**: 提供基础引擎(MspDialogueEngineBase)和简化引擎(MspSimpleDialogueEngine)两种实现
- **高级控制流**: 支持条件分支、循环、变量、函数调用等复杂逻辑
- **可扩展的UI视图系统**: 包含基础视图和打字机效果视图，支持自定义扩展
- **选择系统**: 完整的选择按钮管理和对话分支功能
- **全局变量和函数系统**: 支持动态变量存储和自定义函数注册调用
- **资源管理**: 通过MspDialogueAsset ScriptableObject自动管理对话资源
- **编辑器工具**: 包含编辑器模板和脚本导入器，支持.msp文件自动导入

## 项目结构

```
Assets/MSpeaker/
├── Runtime/                      # 运行时核心代码
│   ├── Interfaces/              # 核心接口定义
│   ├── Parser/                  # 对话脚本解析器
│   ├── Services/                # 服务层(变量、条件、函数)
│   ├── Views/                   # UI视图系统(对话框、选择按钮)
│   ├── Utils/                   # 工具类和扩展
│   ├── Plugins/                 # 插件系统
│   ├── MspDialogueEngineBase.cs # 抽象对话引擎基类
│   ├── MspSimpleDialogueEngine.cs # 简化实现
│   ├── MspDialogueAsset.cs      # 对话资源ScriptableObject
│   └── MspDialogueInput.cs      # 输入处理
│
├── Editor/                       # 编辑器工具
│   ├── Scripts/                 # 编辑器脚本和导入器
│   └── Templates/               # 编辑器模板
│
└── Samples/                      # 示例项目
    ├── SampleScene.unity        # 示例场景
    ├── ExampleMspFunctions.cs   # 自定义函数示例
    ├── StartMspDialogueOnPlay.cs # 启动脚本示例
    ├── Button.prefab            # UI预制体
    ├── Dialogue/                # 示例对话文件
    ├── Resources/               # 示例资源
    └── Font/                    # 字体资源
```

## 使用示例

### 基础用法

1. **创建对话脚本** (.msp文件)

```
[Speaker] 你好，欢迎来到我的世界！
[Player] 谢谢，很高兴认识你！

branch:playerName==null?
    [Speaker] 你叫什么名字？
    choice:
        -> 我叫Alice
        -> 我叫Bob
else
    [Speaker] 欢迎你，{playerName}！
```

2. **在Scene中使用**

- 添加一个GameObject
- 挂载MspSimpleDialogueEngine或自定义的对话引擎
- 配置UI视图(MspDialogueViewBase或MspTypewriterDialogueView)
- 通过代码启动对话：

```csharp
dialogueEngine.StartConversation("your-dialogue-asset");
```

3. **自定义函数**

在类中使用[MspDialogueFunction]特性标记方法：

```csharp
[MspDialogueFunction]
public static string GetPlayerName()
{
    return MspDialogueGlobals.GlobalVariables.GetValueOrDefault("playerName", "Unknown");
}

[MspDialogueFunction]
public static void AddScore(int points)
{
    // 实现游戏逻辑
}
```

## 关键类和接口

### 核心接口

- **IMspDialogueEngine**: 对话引擎接口，定义对话执行的合约
- **IMspDialogueView**: 对话视图接口，定义UI显示的合约
- **IMspVariableService**: 变量服务接口
- **IMspConditionEvaluator**: 条件评估接口
- **IMspFunctionInvoker**: 函数调用接口

### 重要属性和方法

**MspDialogueEngineBase**

- `ParsedConversations`: 解析后的对话列表
- `View`: 关联的对话视图
- `StartConversation(asset)`: 启动新的对话
- `ContinueLine()`: 推进对话
- `SelectChoice(index)`: 选择对话选项
- `PauseConversation() / ResumeConversation()`: 暂停/恢复

**MspDialogueViewBase**

- `DisplayLine(speaker, text)`: 显示对话行
- `DisplayChoices(choices)`: 显示选择项
- `Clear()`: 清空显示内容

## 事件系统

对话引擎提供以下事件：

- `OnConversationStart`: 对话开始时触发
- `OnConversationEnd`: 对话结束时触发
- `OnConversationPaused`: 对话暂停时触发
- `OnConversationResumed`: 对话恢复时触发
- `PersistentOnConversationStart`: 持久化的对话开始事件
- `PersistentOnConversationEnd`: 持久化的对话结束事件

可以在Inspector中或通过代码注册事件监听：

```csharp
dialogueEngine.OnConversationStart.AddListener(() => {
    Debug.Log("对话已开始");
});
```

## 脚本语言规范

MSpeaker使用自定义的.msp脚本格式，支持以下功能：

### 基础对话行

```
[Speaker] 对话内容
```

### 条件分支

```
branch:condition?
    对话内容1
else
    对话内容2
```

### 循环

```
loop:loopName,3
    第 {loopName} 次迭代
endloop
```

### 变量

```
{variableName}      # 获取变量值
set:key=value       # 设置变量
```

### 函数调用

```
call:FunctionName()
call:FunctionName(arg1, arg2)
```

### 选择项

```
choice:
    -> 选项1
    -> 选项2
    -> 选项3
```

## 扩展开发

### 自定义对话视图

继承 `MspDialogueViewBase`创建自定义UI：

```csharp
public class CustomDialogueView : MspDialogueViewBase
{
    public override void DisplayLine(string speaker, string text)
    {
        // 自定义实现
    }
  
    public override void DisplayChoices(List<string> choices)
    {
        // 自定义实现
    }
}
```

### 自定义对话引擎

继承 `MspDialogueEngineBase`实现特定逻辑：

```csharp
public class CustomDialogueEngine : MspDialogueEngineBase
{
    protected override void OnDialogueStart()
    {
        // 自定义初始化逻辑
    }
}
```

### 自定义函数

使用 `MspDialogueFunction`特性标记方法，使其可从.msp脚本调用：

```csharp
[MspDialogueFunction]
public static void CustomAction(string parameter)
{
    // 实现自定义逻辑
}
```

## 示例项目

项目包含完整的示例：

- **SampleScene.unity**: 演示场景，展示基础功能
- **ExampleMspFunctions.cs**: 自定义函数示例
- **Dialogue/**: 示例对话文件

运行示例场景可以快速了解系统功能。

## 许可证

本项目的许可证信息请参考项目根目录的许可证文件。

## 支持和贡献

如有问题或建议，欢迎提出Issue或Pull Request。
