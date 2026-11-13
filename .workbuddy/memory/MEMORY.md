# 项目长期笔记

## 官网（GitHub Pages）
- 站点 `https://chenmo02.github.io/CCToolbox/`，仓库 `chenmo02/CCToolbox`，Pages 源为 `main / (root)`。
- 单文件 `index.html`，零外部依赖；程序图标 `img/app-icon.png`（由 `WpfApp1/favicon.ico` 手工转出）；`.nojekyll` 必须保留。
- **视觉风格（用户明确偏好）**：Hand-Drawn Doodle（stylekit.top）——奶白纸 `#fffef5`、墨黑虚线、彩色实心偏移阴影、胶带/图钉/波浪线。2026-11-13 曾改为 Comic Style（粗墨线/网点/对话气泡），用户判定「不好看」，已回退，**不要再改风格**。
- 部署：`git push origin main` 后 Pages 自动重发。本机 `github.com:22` 握手超时，SSH 走 `ssh.github.com:443`（配置在 `~/.ssh/config`）。

## 项目约定
- README 与官网文案使用简体中文，能力描述须与 `docs/` 事实一致，不编造功能。
- 版本号唯一真源：`WpfApp1/WpfApp1.csproj` 的 `<Version>` = `2.3.8`（用户 2026-11-13 明确指定）；`AssemblyVersion` / `FileVersion` 同步 `2.3.8.0`，首页徽标运行时读程序集元数据、不再写死。官网下载区仍是 v2.3.8 静态快照，README 不写死版本号，历史文档里的 v2.3.3 / v2.3.5 属旧记录。

- **UI 偏好（用户 2026-11-13 看截图后明确）**：首页欢迎卡只放品牌、标题与版本徽标，次要操作不要塞在这里；检查更新等入口统一放「系统参数配置」。设置页不要用 CheckBox 对勾框，改用滑动开关；新增 ComboBox 必须带 `ItemContainerStyle="{DynamicResource ComboBoxItemStandard}"`，与「默认数据库类型」下拉保持一致；**不要放不可点击的占位项**（禁用 beta 之类），看得见点不了会被追问。
- 网络实测（2026-11-13）：`chenmo02.github.io/CCToolbox/index.html` 返回 200，但本机经代理访问 `/updates/stable/latest.json` 返回 503 / 502 / 超时；`raw.githubusercontent.com` 返回 200；`github.com` 直连超时。更新清单已于 `e12ee96` push 到 main，Pages 与 raw 两个路径提供同一份文件；客户端主源最多等 4 秒后自动回退 raw（`1627112`），端到端验证默认源返回 UpToDate。公网不通时也可用设置页「选择本地清单」或 UNC 路径。
- 构建坑：连续多次 `dotnet build` 可能报 BG1002 找不到 .baml（WPF 标记编译缓存与 MSBuild 节点复用竞态），用 `dotnet build -m:1 -nr:false` 单节点编译一次即可恢复，与代码无关。
- **发版约定（2026-11-13 起）**：统一用 `scripts\一键打包.bat`（主脚本 `scripts/Build-Release.ps1`），输入版本号即同步 csproj 四处版本号、发布、校验 exe FileVersion、更新 latest.json 与官网下载区快照、归档到 `artifacts/releases/`。发布目录 `artifacts/publish` 会被 pubxml 的 `DeleteExistingFiles` 清空，归档务必放发布目录之外。`publish/打包命令.txt` 里的旧 `dm\xx` 路径已失效，不要再用。
- **编码约定**：Windows PowerShell 5.1 读取无 BOM 的 `.ps1` 会按 ANSI 解码导致中文乱码，所有 `.ps1` 必须存为 UTF-8 **带 BOM**；`latest.json` 必须 UTF-8 **无 BOM**（HTTP 客户端解析）。`csproj` 带 BOM、`index.html` 无 BOM，脚本写回时按原文件自动保留。

## 更新功能（第 1 期，2026-11-13 已实施）
- 用户确认范围：版本号 v2.3.8；更新源 GitHub Pages + Releases；第 1 期只做「检查 + 提示 + 跳转下载」，不下载、不替换、不改发布形态。
- 清单 `updates/stable/latest.json` 由 Pages 托管；契约、发布流程、验收记录见 `docs/更新功能说明.md`；生成脚本 `scripts/publish-update-manifest.ps1`（本机执行策略禁止未签名 ps1，运行需 `-ExecutionPolicy Bypass`）。
- 更新设置独立存 `%LOCALAPPDATA%\CCToolbox\update-settings.json`，日志在 `logs\update.log`；**不要并入 `ImportSettings`**（Clone 手写逐字段会丢字段）。
- 第 2 期未实施：下载 + SHA256、独立 Updater 进程、备份回滚、代码签名、清单签名。选型见 `docs/在线更新方案.md`。
