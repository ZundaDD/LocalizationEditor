# LocalizationEditor（本地化编辑器）

基于 **.NET 10 + Photino.NET** 的桌面本地化编辑器：用一个轻量 UI 统一管理多语种 Hjson 资源，支持树状结构编辑、主语言驱动的结构合并。

## 特性

- **Hjson 支持**：读取/保存 `.hjson`
- **多语种编辑**：同一 key 在多个语言间切换编辑
- **主语言驱动树结构**：树展示以主语言结构为准（其它语种视作补丁）

## 快速开始

### 环境要求

- **.NET SDK 10**
- Windows / macOS / Linux（Photino 支持的平台）

### 运行

在项目根目录执行：

```bash
dotnet build
dotnet run
```

或直接获取Release

## 使用指南

### 1）创建/打开项目

菜单：`文件 - 新建项目` 或 `文件 - 打开项目`

项目文件为一个 `.json` 配置

### 2）添加语种

菜单：`语言 → 添加语种文件...`

### 3）编辑语种

菜单：`语言 → 编辑语种...`

- 选择一个语种
- 选择该语种的 `.hjson` 文件路径
- 可选择“设为主语言”
- 点击“确定”保存到项目配置并刷新

### 4）编辑翻译内容

1. 左侧树选择节点
2. 右侧切换语种并修改内容
3. 点击“暂存更改”
4. 菜单 `文件 - 保存项目` 或 `Ctrl + S` 将脏语种写回磁盘

## 项目结构

```
LocalizationEditor/
├─ Program.cs                 # Photino 窗口与消息分发
├─ ProjectManager.cs          # 项目管理逻辑
├─ wwwroot/                   # 前端 UI
│  ├─ index.html
│  ├─ style.css
│  ├─ app.js
│  └─ tutorial.md
└─ Hjson/                     # 魔改 Hjson
```

## 引用

- 应用素材图标来自[flaticon}(https://www.flaticon.com/)

## 贡献

欢迎提 Issue / PR。
