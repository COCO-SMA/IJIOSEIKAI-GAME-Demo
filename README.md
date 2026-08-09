# 鲲城异闻录 · IJIOSEIKAI-GAME-Demo

> 一个以"鲲城"为舞台的像素风回合制 JRPG。一局一生，最多八代，
> 目标是从"做客"变成"地头"。

鲲城是座虚构的超一线城市，表面上是科技、金融、效率的现代都市。
住得够久你会开始注意到一些"不对劲"——末班车后偶尔多出一班不在时刻表上的车，
某栋写字楼按 7 楼会到一间不该存在的办公室。大部分人不会注意到，
或者注意到了也选择忽略。你是那种会注意到的人。

奇幻不等于魔幻：这里没有龙，只有一座一直有点不对劲的城市，
而那些不对劲是**搞笑的，不是恐怖的**。

## 运行

需要 **Unity 2022.3.62f3c1**。克隆后用 Unity Hub 打开 `unity/`，
打开 `Assets/Scenes/TitleScene.unity` 按 Play 即可。

也可以直接出 Windows 包：

```bat
build-game.bat            :: 开发包，保留日志窗口
build-game.bat release    :: 发布包
play-game.bat             :: 启动已构建的包
```

脚本默认从 `E:\2022.3.62f3c1\Editor\Unity.exe` 找 Unity，
装在别处就先设环境变量 `UNITY_EXE` 指向你的 `Unity.exe`。

> **冷启动约 10 分钟**，日志几十 MB 写在 `%TEMP%\kuncheng_build.log`。
> 别以为它卡住了。

## 自动化验证

两套 batchmode 测试，各自一个入口，一次 Unity 启动跑完一套：

```powershell
$U = "E:\2022.3.62f3c1\Editor\Unity.exe"
& $U -batchmode -quit -projectPath unity -logFile "$env:TEMP\t.log" `
     -executeMethod KunchengRPG.EditorTools.GridCombatTests.RunAll
Select-String -Path "$env:TEMP\t.log" -Pattern "RESULT|error CS"
```

| 入口 | 覆盖 | 当前 |
|---|---|---|
| `GridCombatTests.RunAll` | 网格战斗：距离/占位/移动/位移/地形/天气/八条胜利规则/行动日志/队伍装配/敌人调值 | 129 通过 |
| `AnomalyTests.RunAll` | 异常展开：深度累积、升层、稀有度上限、安全阀、修正收集 | 58 通过 |

`SceneBuilder.BuildAll` 重新生成场景与 prefab，改了 UI 装配后要跑一次。
日志几十 MB，**永远用 `Select-String` 过滤，不要整读**。

## 技术栈

- Unity 2022.3.62f3c1，C#，URP 未启用（2D 内置管线）
- 数据驱动：`Resources/Data/` 下全是 JSON，加载走 `AssetLoader`
- 程序化生成的像素 tileset 与占位角色图，无外部美术依赖

## 项目结构

```
├── unity/Assets/
│   ├── Scripts/
│   │   ├── Core/          GameManager（单例总线）、SaveManager、InputManager、AssetLoader
│   │   ├── Data/          全部 [Serializable] 数据模型
│   │   ├── Game/          战斗（网格/规则/天气/日志）、异常、继承、结局、
│   │   │                  区亲和度、生命周期、事件、地图、玩家
│   │   ├── Scenes/        场景控制器
│   │   └── UI/            面板与中文字体处理
│   ├── Editor/            构建与测试工具（namespace KunchengRPG.EditorTools）
│   ├── Resources/Data/    区/出身/敌人/事件/对话/异常/道具/结局 JSON
│   ├── Art/               tileset 与占位精灵
│   └── Scenes/            TitleScene、ExploreScene（均由 SceneBuilder 生成）
├── tools/
│   ├── sync-github.ps1    每日同步（带编译验证与硬闸）
│   ├── gen-anomalies*.ps1 异常数据生成
│   ├── *effect*.ps1       效果原语分类与 effectId 写回
│   └── *.py               tileset/地图生成（Pillow；输出路径是旧的绝对路径，
│                          要用先改成 unity/Assets/Art/ 下）
├── build-game.bat
└── play-game.bat
```

## 现状

系统骨架基本成型，**内容是当前真正的瓶颈**——差一到两个数量级：

| | 现有 | v1.0 目标 |
|---|---|---|
| 区 | 2 | 2（已达标，完整 11 区是后续） |
| 出身 | 7 | 7（已达标） |
| 敌人 | 3 | 13-15 |
| 事件 | 1 个 demo | 80-120 |
| 手工地图 | 0 | 9 |
| 异常物品 | 30（逐层数值与文案全齐） | 30（已完成） |

已实现：六组件属性链、异常展开 1-9 层与六档稀有度、网格战斗（八条胜利规则、
三档天气、行动日志）、继承与投胎签、区亲和度跨代衰减、五种结局判定。

未实现：技能系统、体重/摸鱼状态、婚恋事件、NPC 跨代记忆数据结构、
追写（Retcon）机制本身（其行动日志前置已铺好）。

**美术目前全是占位图**——一张程序化生成的 tileset 加三张色块精灵。
美术方向尚未确定，这是与内容并列的第二个缺口。

## 历史实现

`main` 分支曾经是一套 HTML5 Canvas + Electron 的自研引擎实现，
已于 2026-08 全部移除，Unity 是唯一主干。旧代码在 git 历史里，
需要时从 `26b27a8` 之前的提交取。

原先挂在 GitHub Pages 的在线试玩跑的是那套 JS 实现，随之下线。
Unity 侧要恢复在线试玩需要出 WebGL 包，尚未做。
