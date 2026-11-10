# CC 实施工具箱（CCToolbox）

> 把表格转 SQL、数据校验、差异对比和 JSON 处理集中到一个桌面工具里，让实施现场少一些重复操作，多一些可复核的结果。

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B%20x64-blue)](https://www.microsoft.com/windows)

## 一、这是什么

CC 实施工具箱是一个**本地单机运行的 Windows 桌面程序**，围绕项目现场高频的四类工作组织功能：数据准备（表格 → 建表与 INSERT 脚本）、数据检查（按目标表结构逐行校验）、差异复核（CSV / JSON 对比），以及实施过程中的辅助办公（发票打印、画板、便签）。

使用前有三条边界需要先明确：

- **不直连数据库**：SQL 只负责生成，不执行；字段校验也基于文本与文件完成。脚本执行由用户在数据库客户端自行完成。
- **数据不出本机**：除「在线绘图工具」通过 WebView2 加载 Excalidraw 需要联网外，其余功能全部在本机内存完成，不上传文件、不依赖服务端。
- **绿色单文件分发**：发布产物是一个自包含 exe（约 70 MB），免安装、无需预装 .NET 运行时，拷贝到任意目录即可运行。

> 版本提示：`WpfApp1.csproj` 的 `<Version>`、首页写死的版本徽标与部分历史文档中的版本号目前尚未统一，请以发布包的实际版本为准。

## 二、快速开始

### 直接使用

1. 获取已发布的 `CCToolbox.exe` 单文件包；若没有，按[第七节](#七开发与构建)的命令自行发布。
2. 将 exe 放到**当前用户可写**的目录后双击运行。`settings.json` 等配置文件写在程序所在目录，放在 `Program Files` 等只读位置会导致设置保存失败。
3. 左侧导航选择功能，按页面提示导入文件或粘贴文本即可。

### 系统要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10 19041（2004）及以上，x64 |
| .NET 运行时 | 不需要（自包含发布）；**仅源码调试**需要 .NET 10 SDK |
| WebView2 Runtime | 仅「在线绘图工具」需要，缺失时页面会给出提示 |
| 网络 | 仅「在线绘图工具」需要；其余功能可完全离线使用 |

## 三、功能一览

左侧导航分四组，共 10 个入口：

| 分组 | 功能 | 输入 | 主要输出 |
|---|---|---|---|
| 数据工具 | 首页数据概览 | — | 快捷入口卡片、版本与构建日期 |
| 数据工具 | 临时数据建表 | Excel / CSV / DBF | 可编辑预览、CREATE TABLE + 批量 INSERT、SQL / CSV / JSON 导出 |
| 数据工具 | CSV 文件预览 | CSV / TSV / TXT / DBF / BDF | 只读表格、编码与分隔符识别、全表搜索定位 |
| 数据工具 | 快速建数据表 | INSERT 语句 | 推断字段类型并手工调整、四类数据库 CREATE TABLE |
| 数据工具 | 数据验证排查 | DDL 或结构 Excel + INSERT 或数据 Excel | 四步向导、字段映射、错误明细、三 Sheet Excel 报告 |
| 开发工具 | JSON 数据解析 | JSON 文本 / 文件 | 格式化、压缩、校验、嵌套表格浏览与编辑回写、双搜索、导出 |
| 开发工具 | JSON 内容比对 | 两段 JSON | 新增 / 删除 / 修改 / 类型变化差异、双侧行号定位、报告导出 |
| 开发工具 | 在线绘图工具 | Excalidraw 页面 | 打开与保存 `.excalidraw`、导出 PNG / SVG |
| 办公工具 | 发票打印操作 | PDF / 图片（OFD 仅登记） | 排版预览、模板管理、打印机预览与打印 |
| 系统 | 系统参数配置 | 表单 | 默认数据库 / 表名 / 批量行数 / 导出路径等 |

另有**隐藏入口**：「CSV 对比」不在导航栏中，需从「系统参数配置 → 实用小工具 → CSV 对比」进入。它支持按行号或复合主键匹配，字段差异单独展示，并会先检查重复主键；结果可筛选、复制报告、导出 CSV。

右下角悬浮球是**随手记**，点击打开独立便签窗口，关闭时自动保存。

### 数据库能力矩阵

| 能力 | PostgreSQL | SQL Server | MySQL | Oracle |
|---|:---:|:---:|:---:|:---:|
| 建表 / INSERT 脚本生成 | ✓ | ✓ | ✓ | ✓ |
| 快速建数据表（CREATE TABLE） | ✓ | ✓ | ✓ | ✓ |
| 数据验证排查 | ✓ | ✓ | — | — |

不同数据库的标识符引用、DROP 写法、临时表语法与批量 INSERT 形式由程序自动适配；SQL Server 批量 INSERT 强制按 1,000 行分组。

### 文件格式支持

| 场景 | 格式 |
|---|---|
| 表格导入 | `.xlsx` / `.xls` / `.csv` / `.dbf`（DBF 可切换 8 种编码） |
| 分隔文本预览与对比 | `.csv` / `.tsv` / `.txt` / `.dbf` / `.bdf`（自动识别 BOM、UTF-8 / GBK 回退与分隔符） |
| 发票 | PDF、JPG / PNG / BMP / TIF；**OFD 目前只能选择登记，尚未实现预览与打印** |
| 画板 | `.excalidraw`，导出 PNG / SVG |

## 四、典型工作流

1. **项目初始数据准备**：导入业务表格 → 预览并修正内容 → 选择数据库与表名 → 生成建表与插入脚本 → 复制或保存 `.sql`。
2. **导入失败排查**：输入目标表结构（DDL / 结构 Excel）与待导入数据（INSERT / 数据 Excel）→ 自动匹配字段并人工确认 → 逐行校验 → 导出带主键定位信息的问题报告 → 修正后复核。
3. **数据迁移复核**：导出迁移前后的两份 CSV → 按复合主键对比 → 处理新增、缺失、变更记录与重复键 → 导出差异清单。
4. **接口与配置调试**：格式化 JSON → 在嵌套表格中定位并编辑回写 → 对两次响应或两个版本配置做差异对比。

## 五、能力边界与注意事项

- **校验范围**：数据验证排查当前仅支持 SQL Server 与 PostgreSQL；覆盖字段类型、格式及部分结构约束，**不覆盖**业务语义、跨字段关系、外键存在性与 JSON Schema。完整规则见 [data-validation-rules.md](docs/data-validation-rules.md)。
- **字段映射**：UUID 类字段永不自动确认，必须人工核对；字段采用五级自动匹配并给出置信度。
- **类型处理**：「临时数据建表」的所有列均按字符串生成（日期、数字也带引号，依赖目标库隐式转换）。需要精确字段类型请使用「快速建数据表」。
- **大文件阈值**：超过 100,000 行生成 SQL 前会确认；SQL 超过 5,000 行时编辑框只预览前 1,000 行，但复制与保存仍是完整内容；校验问题明细上限 10,000 条。
- **状态持久化**：CSV 对比的页面状态只保存在进程内存，退出程序即丢失。
- **画板依赖**：在线绘图依赖 Excalidraw 页面与 WebView2 Runtime，导出 PNG / SVG 通过注入 JS 实现，站点前端结构变化可能导致导出失效。
- **配置文件位置**：设置与发票数据写在程序目录，只读安装目录会保存失败。

## 六、数据与配置

核心文件处理全部在本地完成，不上传任何业务数据。运行时涉及的文件：

| 路径 | 内容 |
|---|---|
| `<程序目录>/settings.json` | 默认数据库、表名、批量行数、导出路径、快速建表前缀等 |
| `<程序目录>/invoice_templates.json` | 发票打印模板 |
| `<程序目录>/invoice_print_history.json` | 打印历史（最近 500 条） |
| `%LOCALAPPDATA%\CCToolbox\quick-notes.txt` | 随手记正文（旧版 JSON 会自动迁移） |
| `%LOCALAPPDATA%\CCToolbox\WebView2\` | 画板用户数据缓存 |

导出的 SQL / CSV / JSON 统一使用带 BOM 的 UTF-8，便于 Excel 正确识别中文。

## 七、开发与构建

### 技术栈

| 项 | 取值 |
|---|---|
| 框架 | .NET 10，`net10.0-windows10.0.19041.0`，WPF（XAML + code-behind，无 MVVM 框架） |
| 程序集 / 根命名空间 | `CCToolbox` / `WpfApp1`（历史命名，与产品名不一致） |
| 第三方依赖 | CsvHelper 33.1.0、EPPlus 8.5.1、ExcelDataReader 3.8.0、Microsoft.Web.WebView2 1.0.3124.44 |
| 平台能力 | Windows.Data.Pdf（WinRT）、System.Printing / XPS、WebView2、Win32 剪贴板 |
| 发布形态 | 自包含 + 单文件 + 压缩，win-x64，约 70 MB |
| 解决方案 | `WpfApp1.slnx`（仅含一个项目） |

### 命令

在仓库根目录执行（PowerShell）：

```powershell
# 还原依赖（源码调试需先安装 .NET 10 SDK）
dotnet restore .\WpfApp1\WpfApp1.csproj --configfile .\NuGet.Config

# 启动调试
dotnet run --project .\WpfApp1\WpfApp1.csproj --launch-profile CCToolbox-DebugLocal

# 发布 win-x64 自包含单文件
dotnet publish .\WpfApp1\WpfApp1.csproj -c Release -p:PublishProfile=SingleFile-win-x64
```

发布输出为 `artifacts/publish/win-x64-single/CCToolbox.exe`，该目录已被 `.gitignore` 忽略。

### 目录结构

```text
xx/
├── WpfApp1.slnx              解决方案
├── NuGet.Config              仅保留 nuget.org 源
├── WpfApp1/
│   ├── App.xaml / .cs        应用入口、主题字典合并、全局异常兜底
│   ├── MainWindow.xaml/.cs   主窗口、侧边导航、随手记悬浮球
│   ├── Views/                11 个 Page + 3 个窗口 / 对话框
│   ├── Services/             20+ 个解析、生成、校验、对比、打印服务
│   ├── Models/ Controls/ Converters/ Behaviors/
│   ├── Themes/               浅色 / 深色颜色 Token 与共享控件样式
│   └── Properties/           启动配置、单文件发布配置
├── docs/                     技术与规则文档
├── img/                      首页卡片配图（作为资源打包）
├── tests/                    导入性能与 UI 响应回归脚本（未加入解决方案）
├── publish/                  打包命令备忘
└── artifacts/                发布输出（git 忽略）
```

界面层直接调用静态服务类，服务层不反向依赖界面，无依赖注入容器。

## 八、文档索引

| 文档 | 内容 |
|---|---|
| [docs/程序说明.md](docs/程序说明.md) | 面向开发维护者的完整技术说明：分层与启动链路、服务清单、解析与生成规则、校验引擎、性能阈值、已知问题、扩展指引 |
| [docs/data-validation-rules.md](docs/data-validation-rules.md) | 数据验证排查的校验规则、前置条件与统计口径 |
| [docs/在线更新方案.md](docs/在线更新方案.md) | 检查更新 / 在线更新的选型分析与设计稿（**方案，尚未实施**） |
| [WpfApp1/页面功能映射.md](WpfApp1/页面功能映射.md) | 页面、服务、样式资源对照表 |
| [publish/打包命令.txt](publish/打包命令.txt) | 编译与发布命令备忘 |

---

**by 悲伤番茄** · 持续迭代中。
