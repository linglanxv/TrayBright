using System.Drawing.Drawing2D;

namespace TrayBright;

/// <summary>
/// 自绘平滑滑块（仿 Twinkle Tray 风格）。
/// 支持两种模式：填充模式（亮度类）与渐变模式（色温类：暖橙→冷白全轨道渐变，拖块标记当前位置）。
/// </summary>
public class SmoothSlider : Control
{
    private float _value = 50f; // 0-100
    public bool Dragging { get; private set; }
    public string Label = "";

    /// <summary>是否显示"已填充"进度（色温模式设为 false）</summary>
    public bool ShowFill { get; set; } = true;

    /// <summary>渐变模式轨道起点/终点颜色</summary>
    public Color TrackStart { get; set; } = Color.FromArgb(255, 190, 90);
    public Color TrackEnd { get; set; } = Color.FromArgb(255, 140, 70);

    public event Action<int>? ValueCommitted; // 拖动过程中持续触发

    public float Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0f, 100f); Invalidate(); }
    }

    public SmoothSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
        Height = 34;
    }

    private RectangleF TrackRect
    {
        get
        {
            float pad = 10f;
            return new RectangleF(pad, (Height - 8f) / 2f, Width - pad * 2, 8f);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = TrackRect;
        if (ShowFill)
        {
            using (var bg = new SolidBrush(Color.FromArgb(70, 70, 80)))
                g.FillPath(bg, RoundedRect(track, 4f));

            float fillW = Math.Max(track.Height, track.Width * _value / 100f);
            var fillRect = new RectangleF(track.X, track.Y, fillW, track.Height);
            using (var grad = new LinearGradientBrush(fillRect, TrackStart, TrackEnd, 0f))
                g.FillPath(grad, RoundedRect(fillRect, 4f));
        }
        else
        {
            using (var bg = new SolidBrush(Color.FromArgb(70, 70, 80)))
                g.FillPath(bg, RoundedRect(track, 4f));
            using (var grad = new LinearGradientBrush(track, TrackStart, TrackEnd, 0f))
                g.FillPath(grad, RoundedRect(track, 4f));
        }

        // 拖块
        float cx = track.X + track.Width * _value / 100f;
        var thumb = new RectangleF(cx - 9f, Height / 2f - 9f, 18f, 18f);
        using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            g.FillEllipse(shadow, new RectangleF(thumb.X + 1, thumb.Y + 2, thumb.Width, thumb.Height));
        using (var white = new SolidBrush(Dragging ? Color.FromArgb(255, 245, 230) : Color.White))
            g.FillEllipse(white, thumb);

        // 百分比/色温文字
        string text = Label;
        if (text.Length > 0)
        {
            using var font = new Font("Segoe UI", 9f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.FromArgb(200, 200, 210));
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, Width - size.Width - 2, (Height - size.Height) / 2f);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void UpdateFromMouse(int x)
    {
        var track = TrackRect;
        Value = (x - track.X) * 100f / track.Width;
        ValueCommitted?.Invoke((int)Math.Round(_value));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Dragging = true;
            UpdateFromMouse(e.X);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Dragging) UpdateFromMouse(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Dragging = false;
        Invalidate();
    }
}
