using System;
using System.Drawing;
using System.Windows.Forms;

namespace OmenSuperHub {
  public partial class FloatingForm : Form {
    readonly PictureBox displayPictureBox;
    const int OverlayMargin = 12;
    const int ContentPadding = 10;

    public FloatingForm(string text, int textSize, string loc) {
      this.FormBorderStyle = FormBorderStyle.None; // 去除边框
      this.BackColor = Color.Black; // 背景设置为一种特殊颜色
      this.TransparencyKey = this.BackColor; // 将该颜色设为透明

      this.TopMost = true; // 设置始终在最前
      this.ShowInTaskbar = false; // 不在任务栏中显示
      this.StartPosition = FormStartPosition.Manual;

      // 初始化 PictureBox
      displayPictureBox = new PictureBox();
      displayPictureBox.BackColor = Color.Transparent; // 背景色透明
      displayPictureBox.SizeMode = PictureBoxSizeMode.AutoSize; // 自适应大小

      ApplySupersampling(text, textSize); // 应用超采样

      if (loc == "left") {
        // 左上角
        SetPositionTopLeft();
      } else {
        // 右上角
        SetPositionTopRight();
      }

      this.Controls.Add(displayPictureBox);
      AdjustFormSize();
    }

    private void ApplySupersampling(string text, int textSize) {
      string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
      int lineCount = Math.Max(1, lines.Length);
      int effectiveTextSize = Math.Max(14, Math.Min(34, textSize));

      using (Font font = new Font("Segoe UI", effectiveTextSize, FontStyle.Bold, GraphicsUnit.Pixel))
      using (Bitmap measureBitmap = new Bitmap(1, 1))
      using (Graphics measureGraphics = Graphics.FromImage(measureBitmap)) {
        float maxLineWidth = 0f;
        foreach (string line in lines) {
          SizeF size = measureGraphics.MeasureString(line, font);
          if (size.Width > maxLineWidth)
            maxLineWidth = size.Width;
        }

        int lineHeight = (int)Math.Ceiling(font.GetHeight(measureGraphics) * 1.2f);
        int bitmapWidth = Math.Max(220, (int)Math.Ceiling(maxLineWidth) + ContentPadding * 2);
        int bitmapHeight = Math.Max(lineHeight + ContentPadding * 2, lineHeight * lineCount + ContentPadding * 2);
        Bitmap newBitmap = new Bitmap(bitmapWidth, bitmapHeight);

        using (Graphics graphics = Graphics.FromImage(newBitmap)) {
          graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
          graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
          graphics.Clear(Color.Transparent);

          for (int i = 0; i < lineCount; i++) {
            string line = lines[i];
            string[] parts = line.Split(':');
            string title = parts.Length > 1 ? parts[0].Trim() : line;
            using (Brush brush = new SolidBrush(GetColorForTitle(title))) {
              float y = ContentPadding + i * lineHeight;
              graphics.DrawString(line, font, brush, new PointF(ContentPadding, y));
            }
          }
        }

        // 释放旧的图片
        displayPictureBox.Image?.Dispose();
        displayPictureBox.Image = newBitmap;
        displayPictureBox.Size = newBitmap.Size;
      }
    }

    private Color GetColorForTitle(string title) {
      // 根据title或其他逻辑为其分配不同的颜色
      switch (title) {
        case "CPU":
          return Color.FromArgb(0, 128, 192);
        case "GPU":
          return Color.FromArgb(0, 128, 192);
        case "SYS":
          return Color.FromArgb(192, 96, 0);
        case "Fan":
        case "FAN":
          return Color.FromArgb(0, 128, 64);
        case "Battery":
          return Color.FromArgb(192, 96, 0);
        case "State":
        case "CTL":
          return Color.FromArgb(128, 64, 0);
        case "Feat":
          return Color.FromArgb(96, 64, 128);
        default:
          return Color.FromArgb(255, 128, 0);
      }
    }

    public void SetText(string text, int textSize, string loc) {
      if (InvokeRequired) {
        // 使用 BeginInvoke 以减少 UI 阻塞
        BeginInvoke(new Action(() => SetText(text, textSize, loc)));
        return;
      }
      ApplySupersampling(text, textSize);
      AdjustFormSize();
      if (loc == "left") {
        // 左上角
        SetPositionTopLeft();
      } else {
        // 右上角
        SetPositionTopRight();
      }
    }

    private void AdjustFormSize() {
      this.Size = new Size(displayPictureBox.Width, displayPictureBox.Height);
      displayPictureBox.Location = new Point(0, 0);
    }

    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    protected override CreateParams CreateParams {
      get {
        CreateParams cp = base.CreateParams;
        cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE; // 设置窗口为透明和不激活
        return cp;
      }
    }

    // 设置窗口位于左上角
    public void SetPositionTopLeft() {
      Rectangle area = Screen.PrimaryScreen.WorkingArea;
      this.Location = new Point(area.Left + OverlayMargin, area.Top + OverlayMargin);
    }

    // 设置窗口位于右上角
    public void SetPositionTopRight() {
      Rectangle area = Screen.PrimaryScreen.WorkingArea;
      this.Location = new Point(area.Right - this.Width - OverlayMargin, area.Top + OverlayMargin);
    }
  }
}
