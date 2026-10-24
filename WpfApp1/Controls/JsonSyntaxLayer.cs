using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1.Controls;

/// <summary>
/// 在可编辑 TextBox 之上绘制 JSON 语法着色：键名、字符串、数字、布尔、null、标点分别上色，
/// 光标、选区、输入法、滚动仍由原生 TextBox 负责。
/// </summary>
public sealed class JsonSyntaxLayer : FrameworkElement
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(TextBox), typeof(JsonSyntaxLayer), new PropertyMetadata(null, EditorChanged));

    public TextBox? Editor
    {
        get => (TextBox?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>超过该长度不再着色，避免超大文档逐键重扫造成卡顿。</summary>
    private const int MaxHighlightLength = 800_000;

    private static readonly Brush Plain = ColorBrush("#CBD5E1");
    private static readonly Brush Key = ColorBrush("#7DD3FC");
    private static readonly Brush String = ColorBrush("#86EFAC");
    private static readonly Brush Number = ColorBrush("#FDBA74");
    private static readonly Brush Bool = ColorBrush("#C4B5FD");
    private static readonly Brush Null = ColorBrush("#94A3B8");
    private static readonly Brush Punctuation = ColorBrush("#64748B");
    private static readonly Brush ComposingForeground = ColorBrush("#CBD5E1");

    private readonly List<Token> _tokens = [];
    private readonly Dictionary<int, (int Start, DrawingGroup Drawing)> _lines = [];
    private readonly Queue<int> _cachedLineOrder = new();
    private string _source = "";
    private (double Width, double Dpi, string FontFamily, FontStyle Style, FontWeight Weight, FontStretch Stretch, double FontSize) _layout;
    private Brush? _normalForeground;
    private bool _isComposing;
    private bool _compositionSession;
    private bool _plainFallback;

    private readonly record struct Token(int Start, int Length, Brush Brush, bool Italic)
    {
        public int End => Start + Length;
    }

    public JsonSyntaxLayer()
    {
        IsHitTestVisible = false;
        SizeChanged += (_, _) => InvalidateVisual();
    }

    private static void EditorChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        var layer = (JsonSyntaxLayer)source;

        if (args.OldValue is TextBox previous)
        {
            previous.TextChanged -= layer.TextChanged;
            previous.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(layer.ScrollChanged));
            previous.RemoveHandler(TextCompositionManager.TextInputStartEvent, new TextCompositionEventHandler(layer.CompositionStarted));
            previous.RemoveHandler(TextCompositionManager.TextInputUpdateEvent, new TextCompositionEventHandler(layer.CompositionUpdated));
            previous.RemoveHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(layer.CompositionCommitted));
            previous.LostKeyboardFocus -= layer.EditorLostFocus;
            previous.GotKeyboardFocus -= layer.EditorGotFocus;
            if (layer._normalForeground != null)
                previous.Foreground = layer._normalForeground;
        }

        if (args.NewValue is TextBox current)
        {
            layer._normalForeground = current.Foreground;
            current.TextChanged += layer.TextChanged;
            current.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(layer.ScrollChanged));
            current.AddHandler(TextCompositionManager.TextInputStartEvent, new TextCompositionEventHandler(layer.CompositionStarted));
            current.AddHandler(TextCompositionManager.TextInputUpdateEvent, new TextCompositionEventHandler(layer.CompositionUpdated));
            current.AddHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(layer.CompositionCommitted));
            current.LostKeyboardFocus += layer.EditorLostFocus;
            current.GotKeyboardFocus += layer.EditorGotFocus;
        }

        layer.UpdateTokens();
    }

    private void TextChanged(object sender, TextChangedEventArgs args) => UpdateTokens();

    private void ScrollChanged(object sender, ScrollChangedEventArgs args) => InvalidateVisual();

    private void CompositionStarted(object sender, TextCompositionEventArgs args)
    {
        // 输入法在控件获得焦点时也会触发 Start（组字内容为空），此时不能把整篇文字切成纯色
        _compositionSession = true;
        if (args.TextComposition is { CompositionText.Length: > 0 })
            SetComposing(true);
    }

    /// <summary>组字内容变化：有内容才交还原生前景色，清空（Esc 取消）则恢复着色。</summary>
    private void CompositionUpdated(object sender, TextCompositionEventArgs args)
    {
        if (!_compositionSession)
            return;

        SetComposing(args.TextComposition is { CompositionText.Length: > 0 });
    }

    /// <summary>输入法提交（或普通按键输入）时结束组字状态。</summary>
    private void CompositionCommitted(object sender, TextCompositionEventArgs args)
    {
        _compositionSession = false;
        SetComposing(false);
    }

    /// <summary>焦点离开时兜底复位，避免输入法状态卡住导致整篇变白。</summary>
    private void EditorLostFocus(object sender, KeyboardFocusChangedEventArgs args) => ResetComposition();

    /// <summary>重新获得焦点时也复位一次，防止上一次输入法会话残留。</summary>
    private void EditorGotFocus(object sender, KeyboardFocusChangedEventArgs args) => ResetComposition();

    private void ResetComposition()
    {
        _compositionSession = false;
        SetComposing(false);
    }

    /// <summary>
    /// 输入法组字期间、或文档过大不着色时，交还给原生 TextBox 显示，
    /// 否则透明前景色会让候选文字或整篇文字看不见。
    /// </summary>
    private void ApplyForeground()
    {
        if (Editor == null)
            return;

        Editor.Foreground = _isComposing || _plainFallback
            ? ComposingForeground
            : _normalForeground ?? Brushes.Transparent;
    }

    private void SetComposing(bool composing)
    {
        if (_isComposing == composing)
            return;

        _isComposing = composing;
        ApplyForeground();
        InvalidateVisual();
    }

    private void UpdateTokens()
    {
        _tokens.Clear();
        ClearLines();

        _source = Editor?.Text ?? "";
        _plainFallback = _source.Length > MaxHighlightLength;
        ApplyForeground();

        if (_source.Length == 0 || _plainFallback)
        {
            InvalidateVisual();
            return;
        }

        int index = 0;
        int length = _source.Length;
        while (index < length)
        {
            char current = _source[index];

            if (current == '"')
            {
                int start = index;
                index++;
                while (index < length)
                {
                    if (_source[index] == '\\')
                    {
                        index += 2;
                        continue;
                    }

                    if (_source[index] == '"')
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                if (index > length)
                    index = length;

                int lookahead = index;
                while (lookahead < length && char.IsWhiteSpace(_source[lookahead]))
                    lookahead++;

                bool isKey = lookahead < length && _source[lookahead] == ':';
                _tokens.Add(new Token(start, index - start, isKey ? Key : String, false));
                continue;
            }

            if (current == '-' || char.IsAsciiDigit(current))
            {
                int start = index;
                int cursor = current == '-' ? index + 1 : index;
                int digitsStart = cursor;
                while (cursor < length && char.IsAsciiDigit(_source[cursor]))
                    cursor++;

                if (cursor == digitsStart)
                {
                    index++;
                    continue;
                }

                if (cursor < length && _source[cursor] == '.')
                {
                    cursor++;
                    while (cursor < length && char.IsAsciiDigit(_source[cursor]))
                        cursor++;
                }

                if (cursor < length && (_source[cursor] == 'e' || _source[cursor] == 'E'))
                {
                    cursor++;
                    if (cursor < length && (_source[cursor] == '+' || _source[cursor] == '-'))
                        cursor++;
                    while (cursor < length && char.IsAsciiDigit(_source[cursor]))
                        cursor++;
                }

                _tokens.Add(new Token(start, cursor - start, Number, false));
                index = cursor;
                continue;
            }

            if (current == 't' && Matches("true", index))
            {
                _tokens.Add(new Token(index, 4, Bool, false));
                index += 4;
                continue;
            }

            if (current == 'f' && Matches("false", index))
            {
                _tokens.Add(new Token(index, 5, Bool, false));
                index += 5;
                continue;
            }

            if (current == 'n' && Matches("null", index))
            {
                _tokens.Add(new Token(index, 4, Null, true));
                index += 4;
                continue;
            }

            if (current is '{' or '}' or '[' or ']' or ',' or ':')
            {
                _tokens.Add(new Token(index, 1, Punctuation, false));
                index++;
                continue;
            }

            index++;
        }

        InvalidateVisual();
    }

    private bool Matches(string word, int index)
        => index + word.Length <= _source.Length &&
           string.CompareOrdinal(_source, index, word, 0, word.Length) == 0;

    protected override void OnRender(DrawingContext dc)
    {
        var box = Editor;
        if (box == null || !box.IsArrangeValid || box.LineCount == 0)
            return;

        // 兜底自愈：没有焦点却仍停留在组字状态时（输入法事件丢失）恢复着色
        if (_isComposing && !box.IsKeyboardFocusWithin)
        {
            _isComposing = false;
            _compositionSession = false;
            ApplyForeground();
        }

        if (_isComposing || _plainFallback)
            return;

        int first = box.GetFirstVisibleLineIndex();
        int last = Math.Min(box.GetLastVisibleLineIndex(), box.LineCount - 1);
        if (first < 0 || first > last)
            return;

        var scroll = box.Template.FindName("PART_ContentHost", box) as ScrollViewer;
        var bar = scroll?.Template.FindName("PART_VerticalScrollBar", scroll) as FrameworkElement;
        double right = ActualWidth - (scroll?.ComputedVerticalScrollBarVisibility == Visibility.Visible ? bar?.ActualWidth ?? 0 : 0);

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, Math.Max(0, right), ActualHeight)));
        var origin = box.TranslatePoint(new Point(), this);
        var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight, box.FontStretch);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var layout = (right, dpi, box.FontFamily.Source, box.FontStyle, box.FontWeight, box.FontStretch, box.FontSize);
        if (_layout != layout)
        {
            ClearLines();
            _layout = layout;
        }

        // 先取首行位置与行高，再按行复用缓存：滚动时只重绘可见行，避免整篇重排。
        var firstLine = GetLine(first, box, typeface, dpi);
        var rect = box.GetRectFromCharacterIndex(firstLine.Start);
        if (rect.IsEmpty)
        {
            dc.Pop();
            return;
        }

        double lineHeight = rect.Height;
        if (first < last)
        {
            var nextRect = box.GetRectFromCharacterIndex(GetLine(first + 1, box, typeface, dpi).Start);
            if (!nextRect.IsEmpty)
                lineHeight = nextRect.Y - rect.Y;
        }

        for (int line = first; line <= last; line++)
        {
            var cached = GetLine(line, box, typeface, dpi);
            dc.PushTransform(new TranslateTransform(origin.X + rect.X, origin.Y + rect.Y + (line - first) * lineHeight));
            dc.DrawDrawing(cached.Drawing);
            dc.Pop();
        }

        dc.Pop();
    }

    private (int Start, DrawingGroup Drawing) GetLine(int line, TextBox box, Typeface typeface, double dpi)
    {
        if (_lines.TryGetValue(line, out var cached))
            return cached;

        int start = box.GetCharacterIndexFromLineIndex(line);
        int lineLength = box.GetLineLength(line);
        if (start < 0 || lineLength <= 0)
            lineLength = 0;

        string source = lineLength > 0
            ? _source.Substring(start, lineLength).TrimEnd('\r', '\n')
            : "";

        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            if (source.Length > 0)
            {
                var text = new FormattedText(
                    source,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    box.FontSize,
                    Plain,
                    dpi);

                int low = 0;
                int high = _tokens.Count;
                while (low < high)
                {
                    int middle = (low + high) / 2;
                    if (_tokens[middle].End <= start)
                        low = middle + 1;
                    else
                        high = middle;
                }

                for (int i = low; i < _tokens.Count && _tokens[i].Start < start + source.Length; i++)
                {
                    var token = _tokens[i];
                    int from = Math.Max(0, token.Start - start);
                    int to = Math.Min(source.Length, token.End - start);
                    if (to <= from)
                        continue;

                    text.SetForegroundBrush(token.Brush, from, to - from);
                    if (token.Italic)
                        text.SetFontStyle(FontStyles.Italic, from, to - from);
                }

                dc.DrawText(text, new Point());
            }
        }

        drawing.Freeze();

        // 限制缓存行数，滚动大文件时内存不随文档增长。
        if (_lines.Count >= 256)
            _lines.Remove(_cachedLineOrder.Dequeue());
        _cachedLineOrder.Enqueue(line);

        cached = (start, drawing);
        _lines.Add(line, cached);
        return cached;
    }

    private void ClearLines()
    {
        _lines.Clear();
        _cachedLineOrder.Clear();
    }

    private static SolidColorBrush ColorBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }
}
