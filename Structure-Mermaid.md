# MSpeaker 项目架构图 (Mermaid 格式)

## 系统架构图

```mermaid
graph TB
    subgraph Editor["编辑器层"]
        Importer[MspDialogueImporter<br/>.msp文件导入器]
        ImporterEditor[MspDialogueImporterEditor<br/>编辑器界面]
        CreateTool[CreateMspDialogue<br/>创建工具]
    end

    subgraph Runtime["运行时核心层"]
        subgraph Interfaces["接口层"]
            IEngine[IMspDialogueEngine]
            IView[IMspDialogueView]
            IPlugin[IMspEnginePlugin]
        end

        subgraph Parser["解析层"]
            ParserCore[MspDialogueParser<br/>解析器]
            ArgParser[MspArgumentParser<br/>参数解析器]
            Models[MspDialogueModels<br/>数据模型]
        end

        subgraph Engine["引擎层"]
            EngineBase[MspDialogueEngineBase<br/>引擎基类]
            SimpleEngine[MspSimpleDialogueEngine<br/>简单引擎]
        end

        subgraph Services["服务层"]
            ConditionEval[MspConditionEvaluator<br/>条件评估]
            VarService[MspVariableService<br/>变量服务]
            FuncInvoker[MspFunctionInvoker<br/>函数调用]
        end

        subgraph Views["视图层"]
            ViewBase[MspDialogueViewBase<br/>视图基类]
            TypewriterView[MspTypewriterDialogueView<br/>打字机视图]
            ChoiceButton[MspChoiceButton<br/>选择按钮]
        end

        subgraph Plugins["插件层"]
            PluginBase[MspEnginePlugin<br/>插件基类]
            PluginContext[MspPluginContext<br/>插件上下文]
            PortraitPlugin[MspPortraitImagePlugin<br/>头像插件]
        end

        subgraph Input["输入层"]
            InputHandler[MspDialogueInput<br/>输入处理]
        end

        subgraph Utils["工具层"]
            Globals[MspDialogueGlobals<br/>全局变量]
            Logger[MspDialogueLogger<br/>日志系统]
            FunctionAttr[MspDialogueFunctionAttribute<br/>函数属性]
        end
    end

    Asset[MspDialogueAsset<br/>对话资源]

    Importer --> Asset
    Asset --> ParserCore
    ParserCore --> Models
    ParserCore --> ArgParser
    Models --> EngineBase
    EngineBase --> SimpleEngine
    EngineBase -.-> IEngine
    EngineBase --> Services
    EngineBase --> ViewBase
    ViewBase -.-> IView
    ViewBase --> TypewriterView
    ViewBase --> ChoiceButton
    EngineBase --> PluginBase
    PluginBase -.-> IPlugin
    PluginBase --> PluginContext
    PluginBase --> PortraitPlugin
    InputHandler --> EngineBase
    Services --> Globals

    style Editor fill:#e1f5ff
    style Runtime fill:#fff4e1
    style Interfaces fill:#e8f5e9
    style Parser fill:#f0f0f0
    style Engine fill:#e8f5e9
    style Services fill:#fff9c4
    style Views fill:#fce4ec
    style Plugins fill:#f3e5f5
    style Input fill:#fff9c4
    style Utils fill:#e0f2f1
```

## 数据流图

```mermaid
flowchart LR
    A[.msp文件] --> B[MspDialogueImporter]
    B --> C[MspDialogueAsset]
    C --> D[MspDialogueParser]
    D --> E[List&lt;MspConversation&gt;]
    E --> F[MspDialogueEngineBase]
    F --> G[MspDialogueViewBase]
    G --> H[UI显示]
    F --> I[MspEnginePlugin]
    F --> J[Services]
    K[MspDialogueInput] --> F

    style A fill:#ffebee
    style C fill:#e3f2fd
    style E fill:#f1f8e9
    style H fill:#fff3e0
    style J fill:#fff9c4
```

## 类关系图

```mermaid
classDiagram
    class IMspDialogueEngine {
        <<interface>>
        +ParsedConversations List~MspConversation~
        +View IMspDialogueView
        +StartConversation()
        +SwitchConversation()
        +StopConversation()
        +PauseConversation()
        +ResumeConversation()
    }

    class IMspDialogueView {
        <<interface>>
        +SetView()
        +ClearView()
        +DisplayChoices()
        +IsStillDisplaying()
    }

    class IMspEnginePlugin {
        <<interface>>
        +Priority int
        +IsComplete bool
        +OnConversationStart()
        +OnLineDisplay()
        +OnLineComplete()
        +OnClear()
    }

    class IMspPluginContext {
        <<interface>>
        +CurrentConversation
        +CurrentLine
        +CurrentLineIndex
        +GetMetadata()
        +HasMetadata()
    }

    class IMspConditionEvaluator {
        <<interface>>
        +Evaluate(expression) bool
        +EvaluateChoice(expression) bool
    }

    class IMspVariableService {
        <<interface>>
        +GetValue()
        +SetValue()
        +HasVariable()
    }

    class IMspFunctionInvoker {
        <<interface>>
        +Invoke()
        +ClearCache()
    }

    class MspDialogueEngineBase {
        #IMspVariableService _variableService
        #IMspConditionEvaluator _conditionEvaluator
        #IMspFunctionInvoker _functionInvoker
        #MspEnginePlugin[] _plugins
    }

    class MspDialogueViewBase {
        #TextMeshProUGUI nameText
        #TextMeshProUGUI sentenceText
    }

    class MspEnginePlugin {
        <<abstract>>
        +Priority int
        +IsComplete bool
    }

    MspDialogueEngineBase ..|> IMspDialogueEngine
    MspDialogueViewBase ..|> IMspDialogueView
    MspEnginePlugin ..|> IMspEnginePlugin
    MspDialogueEngineBase --> IMspConditionEvaluator
    MspDialogueEngineBase --> IMspVariableService
    MspDialogueEngineBase --> IMspFunctionInvoker
    MspDialogueEngineBase --> MspEnginePlugin
    MspEnginePlugin --> IMspPluginContext
```

## 插件生命周期

```mermaid
sequenceDiagram
    participant Engine as MspDialogueEngineBase
    participant Context as MspPluginContext
    participant Plugin as MspEnginePlugin

    Engine->>Context: Update(conversation, lineIndex)
    Engine->>Plugin: OnConversationStart(context)

    loop 每行对话
        Engine->>Plugin: OnBeforeLineDisplay(context, line)
        Engine->>Plugin: OnLineDisplay(context)
        alt 需要等待
            Plugin-->>Engine: WaitForCompletion
            Engine->>Engine: WaitUntil(plugin.IsComplete)
        end
        Engine->>Plugin: OnLineComplete(context)
    end

    opt 显示选项
        Engine->>Plugin: OnBeforeChoicesDisplay(context, choices)
        Note over Plugin: 用户选择
        Engine->>Plugin: OnChoiceSelected(context, choice)
    end

    Engine->>Plugin: OnConversationEnd(context)
    Engine->>Plugin: OnClear()
```

## 服务依赖关系

```mermaid
graph TD
    subgraph Engine["引擎"]
        EngineBase[MspDialogueEngineBase]
    end

    subgraph Services["服务层"]
        ConditionEval[MspConditionEvaluator]
        VarService[MspVariableService]
        FuncInvoker[MspFunctionInvoker]
    end

    subgraph Parser["解析层"]
        ArgParser[MspArgumentParser]
    end

    subgraph Utils["工具层"]
        Globals[MspDialogueGlobals]
    end

    EngineBase --> ConditionEval
    EngineBase --> VarService
    EngineBase --> FuncInvoker
    ConditionEval --> VarService
    ConditionEval --> ArgParser
    FuncInvoker --> VarService
    VarService -.-> Globals

    style Engine fill:#ffcdd2
    style Services fill:#fff9c4
    style Parser fill:#c8e6c9
    style Utils fill:#e1bee7
```

## 目录结构

```
Runtime/
├── Interfaces/
│   ├── IMspDialogueEngine.cs
│   ├── IMspDialogueView.cs
│   └── IMspEnginePlugin.cs
├── Parser/
│   ├── MspArgumentParser.cs
│   ├── MspDialogueModels.cs
│   └── MspDialogueParser.cs
├── Plugins/
│   ├── MspEnginePlugin.cs
│   ├── MspPluginContext.cs
│   └── MspPortraitImagePlugin.cs
├── Services/
│   ├── IMspConditionEvaluator.cs
│   ├── IMspFunctionInvoker.cs
│   ├── IMspVariableService.cs
│   ├── MspConditionEvaluator.cs
│   ├── MspFunctionInvoker.cs
│   └── MspVariableService.cs
├── Utils/
│   ├── MspDialogueFunctionAttribute.cs
│   ├── MspDialogueGlobals.cs
│   └── MspDialogueLogger.cs
├── Views/
│   ├── MspChoiceButton.cs
│   ├── MspDialogueViewBase.cs
│   └── MspTypewriterDialogueView.cs
├── MspDialogueAsset.cs
├── MspDialogueEngineBase.cs
├── MspDialogueInput.cs
└── MspSimpleDialogueEngine.cs
```
