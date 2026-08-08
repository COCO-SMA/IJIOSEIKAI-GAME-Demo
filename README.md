# 鲲城异闻录 · IJIOSEIKAI-GAME-Demo

> 一个以"鲲城"（深圳的奇幻镜像）为舞台的像素风回合制 JRPG Demo。

## 在线试玩

https://coco-sma.github.io/IJIOSEIKAI-GAME-Demo/

## 本地运行

```bash
npm install
start.bat          # 正常启动
start.bat --dev    # 带 DevTools 启动
```

需要 Node.js + Electron 环境。

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
├── tools/                  # 地图/tileset 生成脚本
├── docs/                   # GDD 设计文档
└── .github/workflows/      # GitHub Pages 自动部署
```
