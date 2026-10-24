# 项目长期记忆 — CC 实施工具箱（WpfApp1 / CCToolbox）

## UI 约定：焦点虚线框（FocusVisualStyle）

WPF 默认那圈黑色点线焦点框在本项目里已全局关闭，规则如下，改 UI 时别破坏：

- **可聚焦控件**（Button/CheckBox/TextBox/ComboBox/DataGrid/ScrollViewer…）：新建 `x:Key` 样式时**必须**自带
  `<Setter Property="FocusVisualStyle" Value="{x:Null}"/>`，否则框会回来。
  共享类型已在 `Themes/CommonStyles.xaml` 里用隐式样式统一置空。
- **`Page` 不是 Control**，隐式样式对它无效，且 .NET 10 里 `FrameworkElement.FocusVisualStyle` 的**默认值不再是 null**。
  所以每个 `Views/*.xaml` 的 `<Page>` 根节点都要写 `FocusVisualStyle="{x:Null}"`（本地值）。新增页面照抄。
- `Frame`（`MainFrame`）也是可聚焦控件，已加隐式样式 + 本地值。
- 校验脚本：`artifacts/focus-visual-qa/`，`dotnet run` 后应为 PASS（会扫描 11 个页面 + Frame + 页面对象本身）。

## 踩过的坑（别再试）

- XAML 里的 `Setter` 优先级低于**控件模板里的触发器**（ValueSource 会显示 `TemplateTrigger`），对默认模板的 CheckBox 无效；
  页面自定义 `x:Key` 样式也不继承隐式样式；`Style="{x:Null}"` 会主动屏蔽隐式样式。**只有本地值最稳**。
- `EventManager.RegisterClassHandler(typeof(Control), LoadedEvent, …)` 想统一兜底：实测只命中少量元素，且 Loaded 时刻样式往往还没应用，不可用。
