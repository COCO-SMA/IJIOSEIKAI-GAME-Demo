# 鲲城 RPG — Unity 项目设置指南

## 已完成

C# 代码全部写完，共 16 个脚本文件：

### 核心引擎 (Core/)
- `GameManager.cs` — 单例，游戏状态管理，数据加载
- `InputManager.cs` — 键盘输入（WASD/方向键移动，Space交互，I摸鱼，E结束本年）
- `AssetLoader.cs` — JSON数据加载（使用Newtonsoft.Json）

### 数据模型 (Data/)
- `DataModels.cs` — 所有JSON数据的C#映射类

### 游戏系统 (Game/)
- `Player.cs` — 玩家状态 + 六组件身体系统 + 属性系统
- `PlayerController.cs` — 网格移动 + 精灵渲染
- `MapController.cs` — Tilemap加载 + 碰撞检测 + 邻近检测
- `LifecycleManager.cs` — AP/年龄/年度结算/死亡检查
- `EventSystem.cs` — POI事件触发 + 后果计算
- `CombatSystem.cs` — 回合制战斗（攻击/装没事/用东西/说话/跑）
- `DialogueSystem.cs` — NPC对话树导航

### UI (UI/)
- `HUDController.cs` — 状态栏 + 交互提示
- `EventPanel.cs` — 事件选项面板
- `DialoguePanel.cs` — 对话面板

### 场景 (Scenes/)
- `TitleSceneController.cs` — 标题→选区→选出身→输入名字
- `ExploreSceneController.cs` — 地图探索主循环

### 数据
- 22个JSON文件已复制到 `Assets/Resources/Data/`
- tileset PNG 已复制到 `Assets/Art/Tilesets/`

---

## 你需要做的步骤

### 目标引擎版本

本项目锁定 **Unity 2022.3.62f3c1**（Unity 中国版，2022 LTS），
已安装在 `E:\2022.3.62f3c1\`。

版本号写在 `ProjectSettings/ProjectVersion.txt`，请勿用其它版本打开，
否则 Hub 会提示升级，升级后 `.meta` 和场景文件可能不兼容回退。

### 第1步：打开项目

1. 打开 **Unity Hub** → **Projects** → **Add** → **Add project from disk**
2. 选择本仓库的 `unity/` 目录（**不是**仓库根目录）
3. 确认编辑器版本显示 `2022.3.62f3c1`，然后打开

首次打开会解析包并生成 `Library/`，需要几分钟。

### 第2步：确认依赖已就位

`Packages/manifest.json` 已按 2022 LTS 配好，打开时自动还原，无需手动装：

- `com.unity.feature.2d` — 2D 功能集（sprite / tilemap / tilemap.extras / pixel-perfect / animation）
- `com.unity.nuget.newtonsoft-json` — JSON 解析，代码依赖
- `com.unity.textmeshpro` — 中文字体渲染用
- `com.unity.ugui` — legacy UI

如果 Package Manager 报错，检查网络能否访问 Unity 中国版镜像源。

### 第5步：设置 Tileset

1. 在 Project 窗口找到 `Assets/Art/Tilesets/city_tileset.png`
2. 选中它，在 Inspector 中设置：
   - Texture Type: **Sprite (2D and UI)**
   - Sprite Mode: **Multiple**
   - Pixels Per Unit: **32**
   - Filter Mode: **Point (no filter)**
   - Compression: **None**
3. 点 **Sprite Editor** → **Slice** → Grid by Cell Size → 32x32 → Slice → Apply

### 第6步：创建场景

#### TitleScene
1. 创建新场景 `Assets/Scenes/TitleScene.unity`
2. 创建 Canvas（UI → Canvas）
3. 添加各个 Panel（TitlePanel, DistrictPanel, OriginPanel, NameInputPanel）
4. 添加 TitleSceneController 脚本到场景
5. 把 UI 元素拖到脚本的对应字段

#### ExploreScene
1. 创建新场景 `Assets/Scenes/ExploreScene.unity`
2. 创建 Grid → Tilemap（3层：Ground, Building, Decoration）
3. 设置 Tilemap cell size 为 32x32
4. 添加 Pixel Perfect Camera 组件
5. 创建 Player GameObject（SpriteRenderer + PlayerController）
6. 创建 Canvas（HUD + EventPanel + DialoguePanel）
7. 添加 ExploreSceneController 脚本并连接所有引用

### 第7步：配置 Tile 数组

在 MapController 的 Inspector 中：
- Tiles 数组大小设为 24
- 把 Sprite Editor 切好的 24 个 tile 拖到对应位置

### 第8步：测试运行

1. 打开 TitleScene
2. 按 Play
3. 应该看到标题画面 → Space → 选区 → 选出身 → 输入名字 → 进入地图探索

---

## 操作方式

| 按键 | 功能 | AP消耗 |
|------|------|--------|
| WASD/方向键 | 移动 | 免费 |
| Space | 与POI/NPC交互 | 1 AP |
| I | 摸鱼（19岁+） | 1 AP |
| E | 结束本年 | 跳过剩余 |
| Esc | 取消/返回 | - |

---

## 注意事项

- JSON 文件必须放在 `Assets/Resources/Data/` 下，代码用 `Resources.Load` 加载
- 场景文件（.unity）需要在 Unity 编辑器中手动创建和配置
- Tilemap 的 Tile 需要手动从切好的 Sprite 创建
- 如果编译报错，检查 Newtonsoft.Json 是否已安装
- 项目路径不要包含中文，否则可能有编译问题
