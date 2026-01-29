# MSpeaker

MSpeaker 是一个用于 Unity 的对话系统，支持通过文本文件编写复杂的对话内容。

## 系统要求

- Unity 6000.3.0f1 或更高版本
- Universal Render Pipeline (URP)
- TextMeshPro

## 核心功能

### 对话文件格式 (.msp)

MSpeaker 使用 `.msp` 文本文件来编写对话内容。文件支持以下语法：

- **注释**: `# 这是注释`
- **会话定义**: `{{DialogueName(会话名称)}}` 或 `{{ConversationName(会话名称)}}`
- **说话者**: `[说话者名称]`
- **对话文本**: 任意文本（可多行）
- **选择分支**: `- 选项文本 -> 目标会话名称 ## 元数据`
- **行内函数调用**: `{{函数名}}` 或 `{{函数名(参数1, 参数2)}}`
- **说话者头像**: `{{Image(Resources路径)}}`
- **条件判断**: `{{If(条件表达式)}}` / `{{Else}}` / `{{EndIf}}`
- **变量判断**: `{{IfVar(变量名, 值)}}`
- **标签和跳转**: `{{Label(标签名)}}` / `{{Goto(标签名)}}`
- **循环**: `{{Loop(次数或变量)}}`
- **行内元数据**: `## 键:值 标志`
- **变量引用**: `$变量名`

### 变量系统

支持全局变量和对话变量：

- 全局变量通过 `MspDialogueGlobals.GlobalVariables` 访问
- 对话变量通过变量服务管理
- 变量可在条件表达式中使用
- 支持字符串、整数、浮点数、布尔值类型

### 函数调用系统

通过 `[MspDialogueFunction]` 特性标记静态方法，可在对话中调用：

```csharp
[MspDialogueFunction]
public static string GetPlayerName()
{
    return "Player";
}
```

在对话中使用：`{{GetPlayerName()}}`

### 插件系统

通过继承 `MspEnginePlugin` 创建插件，支持以下生命周期：

- `OnConversationStart` - 对话开始
- `OnConversationEnd` - 对话结束
- `OnBeforeLineDisplay` - 显示行之前
- `OnLineDisplay` - 显示行时
- `OnLineComplete` - 行完成
- `OnPause` / `OnResume` - 暂停/恢复
- `OnBeforeChoicesDisplay` - 显示选择之前
- `OnChoiceSelected` - 选择被选中
- `OnClear` - 清除视图

内置插件：

- `MspBackgroundMusicPlugin` - 背景音乐
- `MspSoundEffectPlugin` - 音效
- `MspCharacterAnimationPlugin` - 角色动画
- `MspVisualEffectPlugin` - 视觉效果
- `MspPortraitImagePlugin` - 头像图片

### 视图系统

- `MspDialogueViewBase` - 基础视图类
- `MspTypewriterDialogueView` - 打字机效果视图

## 使用方法

### 1. 创建对话文件

在项目中创建 `.msp` 文件，Unity 会自动导入为 `MspDialogueAsset`。

### 2. 设置对话引擎

在场景中添加 `MspSimpleDialogueEngine` 组件：

- 设置 `Dialogue View` 引用
- 配置函数调用程序集（可选）
- 添加需要的插件组件

### 3. 创建视图

创建继承自 `MspDialogueViewBase` 的视图组件，或使用 `MspTypewriterDialogueView`。

### 4. 启动对话

```csharp
var engine = GetComponent<MspSimpleDialogueEngine>();
engine.StartConversation(dialogueAsset, startIndex: 0);
```

## 项目结构

```
Assets/MSpeaker/
├── Runtime/              # 运行时核心代码
│   ├── Interfaces/       # 接口定义
│   ├── Parser/           # 对话解析器
│   ├── Plugins/          # 插件系统
│   ├── Services/         # 服务（变量、条件、函数）
│   ├── Utils/            # 工具类
│   ├── Views/            # 视图系统
│   ├── MspDialogueAsset.cs
│   ├── MspDialogueEngineBase.cs
│   ├── MspSimpleDialogueEngine.cs
│   └── MspDialogueInput.cs
├── Editor/               # 编辑器工具
│   ├── Scripts/          # 编辑器脚本
│   │   ├── MspDialogueImporter.cs
│   │   ├── MspDialogueImporterEditor.cs
│   │   ├── MspDialogueValidator.cs
│   │   └── MspDialoguePreviewWindow.cs
│   └── Templates/        # 模板文件
└── Samples/              # 示例
    ├── Dialogue/         # 示例对话文件
    ├── ExampleMspFunctions.cs
    └── StartMspDialogueOnPlay.cs
```

## 示例

参考 `Assets/MSpeaker/Samples/` 目录中的示例：

- `ExampleDialogue.msp` - 完整的对话示例，展示各种功能
- `ExampleMspFunctions.cs` - 函数调用示例
- `SampleScene.unity` - 示例场景

## 许可证

请查看项目许可证文件。
