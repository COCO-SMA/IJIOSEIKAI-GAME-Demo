# 鲲城异闻录 · IJIOSEIKAI-GAME-Demo

> 一个以"鲲城"（深圳的奇幻镜像）为舞台的像素风回合制 JRPG Demo。

## 在线试玩

https://coco-sma.github.io/IJIOSEIKAI-GAME-Demo/

## 本地运行

### 浏览器预览（推荐，改完刷新即可）

```bash
preview.bat
```

然后打开 http://localhost:5173/ 。端口被占用时会自动往上找（5174、5175…），
实际地址在控制台输出里。服务器只监听 127.0.0.1，不对局域网暴露。

### 桌面端（Electron 窗口）

```bash
start.bat          # 正常启动
start.bat --dev    # 带 DevTools 启动
```

### 启动自检

确认游戏能正常引导、标题场景有渲染（需要预览服务器在跑）：

```bash
node_modules\electron\dist\electron.exe tools\smoke-check.js
```

结果同时写到 `data/smoke-result.json`，`ok: true` 表示无渲染错误。

> 无需单独安装 Node：`preview.bat` 和自检脚本都复用 `node_modules` 里
> Electron 自带的 Node 运行时。仅在需要重装依赖时才用到 npm。

## 技术栈

- HTML5 Canvas + JavaScript ES6+
- Electron（桌面打包）
- 纯程序化生成的像素城市 tileset（Python + Pillow）

## 项目结构

```
├── index.html              # 游戏入口
├── electron/               # Electron 主进程
├── src/
│   ├── engine/             # 自研引擎（渲染/输入/场景/资源/存档）
│   ├── game/               # 游戏系统（生命周期/事件/战斗/对话/继承/地图）
│   ├── data/               # JSON 数据驱动（区/出身/道具/敌人/事件/对话）
│   └── main.js             # 游戏启动入口
├── assets/tilesets/        # 像素 tileset 资源
├── preview.bat             # 浏览器预览启动器
├── tools/
│   ├── dev-server.js       # 零依赖本地静态服务器
│   ├── smoke-check.js      # 启动自检（离屏加载 + 报错收集）
│   └── *.py                # 地图/tileset 生成脚本
├── docs/                   # GDD 设计文档
└── .github/workflows/      # GitHub Pages 自动部署
```
