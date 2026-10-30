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

## UI 约定：首页卡片阴影

- `HomePage.xaml` 的 `HomeCardStyle` 阴影由单个 `ShadowLayer`（与卡片同圆角）+ `DropShadowEffect` 生成，
  **不要再改回多层纯色 Border 叠加**——那种做法在底部会露出方角硬边。
- 阴影层/遮罩层（`ShadowLayer`、`MaskBorder`）的背景绑定到 `CardBorder.Background`，
  这样 hover 卡片变蓝时边缘不会透白；改卡片配色时无需再同步这两层。

## UI 约定：首页卡片阴影与 hover 缩放

- `HomePage.xaml` 的 `HomeCardStyle` 阴影由单个 `ShadowLayer` 生成：**外扩的同心圆角**（`Margin="-5,0,-5,-9"`、`CornerRadius=20` = 卡片 15 + 外扩 5）
  + `BlurEffect`。**不要改回多层纯色 Border 叠加**（底部会露方角），也不要用 `DropShadowEffect`（偏移后圆角外侧会露方块）。
- hover 放大**必须用 `LayoutTransform`**（`CardScale` 已命名，未命名会被冻结导致无法动画）。
  **`RenderTransform` 缩放在 150% DPI 下会让卡片底部两个圆角渲染丢失**，顶部正常，极易误判成阴影问题。
- `DecorCanvas` 的圆角裁剪走代码 `Clip`（`DecorCanvas_Loaded/SizeChanged`），**不要用 `OpacityMask` + `VisualBrush`**（缩放时可能失效，气泡溢出圆角）。

## 踩过的坑（别再试）

- XAML 里的 `Setter` 优先级低于**控件模板里的触发器**（ValueSource 会显示 `TemplateTrigger`），对默认模板的 CheckBox 无效；
  页面自定义 `x:Key` 样式也不继承隐式样式；`Style="{x:Null}"` 会主动屏蔽隐式样式。**只有本地值最稳**。
- `EventManager.RegisterClassHandler(typeof(Control), LoadedEvent, …)` 想统一兜底：实测只命中少量元素，且 Loaded 时刻样式往往还没应用，不可用。
