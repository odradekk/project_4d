using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.UI;

/// <summary>Shared text feedback and notebook. Decision note: 2026-10-26-prologue-greybox.</summary>
public partial class GameHud : CanvasLayer
{
    private static readonly string[] Pages = [
        "一只眼睛。\n另一页的教室。", "印字反过来的橡皮。\n翻过来，字就反了。",
        "一支铅笔刺穿一摞纸。\n竖起，放倒。", "发光的讲台。\n别的页也有它。",
        "缝衣针穿过一摞纸。\n猫能看见缝。", "这一页的把手，那一页的毛线球。\n中间用虚线连着。",
        "凳子只剩四个小方块；两页里各有一个皮球；\n螺母在那一页变成大圆圈。", "立方体和问号。\n体动成什么？"
    ];
    public NotebookState Notebook { get; set; } = new();
    private readonly Queue<(string Speaker, string Text, double Seconds)> lines = new();
    private Label subtitle = null!, hint = null!, page = null!, pageNumber = null!, crosshair = null!;
    private Control notebook = null!;
    private double remaining;
    private double noticeRemaining;
    private string prompt = "";
    private int currentPage;
    public bool NotebookAvailable { get => Notebook.Available; set => Notebook.Available = value; }
    public bool NotebookOpen => notebook.Visible;
    public int CurrentPage => currentPage;
    public IReadOnlyCollection<int> UnlockedPages => Notebook.Pages;
    public string SubtitleText => subtitle.Text;
    public string HintText => hint.Text;
    internal bool SubtitleVisible => subtitle.Visible;
    internal bool CrosshairVisible => crosshair.Visible;

    public override void _Ready()
    {
        Layer = 10;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        crosshair = MakeLabel("+", 24); root.AddChild(crosshair);
        crosshair.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        crosshair.OffsetLeft = -12; crosshair.OffsetTop = -17; crosshair.OffsetRight = 12; crosshair.OffsetBottom = 17;
        hint = MakeLabel("", 20); root.AddChild(hint);
        hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        hint.OffsetLeft = 20; hint.OffsetRight = -20; hint.OffsetTop = 8; hint.OffsetBottom = 68;
        subtitle = MakeLabel("", 24); root.AddChild(subtitle);
        subtitle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        subtitle.AnchorTop = .25f;
        subtitle.OffsetLeft = 28; subtitle.OffsetRight = -28; subtitle.OffsetTop = 0; subtitle.OffsetBottom = -18;
        subtitle.VerticalAlignment = VerticalAlignment.Bottom;
        notebook = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        notebook.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.05f, .06f, .08f, 1f) });
        root.AddChild(notebook); notebook.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var layout = new VBoxContainer(); notebook.AddChild(layout);
        layout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layout.OffsetLeft = 32; layout.OffsetRight = -32; layout.OffsetTop = 24; layout.OffsetBottom = -24;
        pageNumber = MakeLabel("", 24); layout.AddChild(pageNumber);
        page = MakeLabel("", 28); page.SizeFlagsVertical = Control.SizeFlags.ExpandFill; layout.AddChild(page);
        layout.AddChild(MakeLabel("← → 翻页    N / E / Esc 关闭", 20));
    }
    private static Label MakeLabel(string text, int size)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2); label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }
    /// <summary>Queues a line for the requested duration; notices pause its timer.</summary>
    public void Say(string speaker, string text, double seconds)
    {
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        lines.Enqueue((speaker, text, seconds));
        if (remaining <= 0) NextLine();
    }
    public void ClearText() { lines.Clear(); remaining = noticeRemaining = 0; subtitle.Text = hint.Text = prompt = ""; RefreshVisibility(); }
    public void SetHint(string text) { prompt = text; if (noticeRemaining <= 0) hint.Text = prompt; }
    public void ShowNotice(string text, double seconds)
    {
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        hint.Text = text; noticeRemaining = seconds; RefreshVisibility();
    }
    public override void _Process(double delta)
    {
        if (noticeRemaining > 0)
        {
            var elapsed = Math.Min(delta, noticeRemaining);
            noticeRemaining -= elapsed; delta -= elapsed;
            if (noticeRemaining <= 0) { hint.Text = prompt; RefreshVisibility(); }
        }
        if (remaining <= 0) return;
        remaining -= delta;
        while (remaining <= 0 && lines.Count > 0) NextLine();
        if (remaining <= 0) { remaining = 0; subtitle.Text = ""; RefreshVisibility(); }
    }
    private void NextLine()
    {
        var line = lines.Dequeue();
        subtitle.Text = string.IsNullOrEmpty(line.Speaker) ? line.Text : $"{line.Speaker}：{line.Text}";
        remaining += line.Seconds;
        RefreshVisibility();
    }
    private void RefreshVisibility()
    {
        subtitle.Visible = noticeRemaining <= 0;
        crosshair.Visible = !NotebookOpen && (noticeRemaining > 0 || remaining <= 0);
    }
    public void UnlockPage(int number)
    {
        Notebook.Unlock(number);
    }
    public bool OpenNotebook()
    {
        if (!NotebookAvailable || Notebook.Pages.Count == 0) return false;
        currentPage = Notebook.Pages.Min(); notebook.Visible = true; crosshair.Visible = false; RefreshPage(); return true;
    }
    public void CloseNotebook() { notebook.Visible = false; RefreshVisibility(); }
    public void TurnPage(int direction)
    {
        var pages = Notebook.Pages.ToArray();
        var index = Array.IndexOf(pages, currentPage);
        currentPage = pages[Math.Clamp(index + Math.Sign(direction), 0, pages.Length - 1)];
        RefreshPage();
    }
    private void RefreshPage() { pageNumber.Text = $"数学笔记本 · {currentPage} / 8"; page.Text = Pages[currentPage - 1]; }
}
