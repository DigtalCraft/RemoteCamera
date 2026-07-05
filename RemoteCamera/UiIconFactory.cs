using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RemoteCamera
{
    /// <summary>
    /// アプリ用のアイコンとボタン画像を生成する。
    /// </summary>
    internal static class UiIconFactory
    {
        /// <summary>
        /// アプリケーション用のアイコンを作成する。
        /// </summary>
        /// <returns>フォームとトレイで使うアイコン。</returns>
        public static Icon CreateAppIcon()
        {
            using var bitmap = CreateAppBitmap(64);
            return BitmapToIcon(bitmap);
        }

        /// <summary>
        /// ヘッダーや大きめの表示用アイコンを作成する。
        /// </summary>
        /// <param name="size">画像サイズ。</param>
        /// <returns>生成したビットマップ。</returns>
        public static Bitmap CreateAppBitmap(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            DrawAppBadge(graphics, new Rectangle(0, 0, size, size));
            return bitmap;
        }

        /// <summary>
        /// ボタン用の画像を作成する。
        /// </summary>
        /// <param name="kind">ボタン種別。</param>
        /// <returns>ボタンに設定する画像。</returns>
        public static Bitmap CreateButtonBitmap(ButtonIconKind kind)
        {
            var bitmap = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            switch (kind)
            {
                case ButtonIconKind.Folder:
                    DrawFolderIcon(graphics);
                    break;
                case ButtonIconKind.Record:
                    DrawRecordIcon(graphics);
                    break;
                case ButtonIconKind.Stop:
                    DrawStopIcon(graphics);
                    break;
                case ButtonIconKind.Pause:
                    DrawPauseIcon(graphics);
                    break;
                case ButtonIconKind.Play:
                    DrawPlayIcon(graphics);
                    break;
                case ButtonIconKind.Exit:
                    DrawExitIcon(graphics);
                    break;
            }

            return bitmap;
        }

        /// <summary>
        /// アイコン画像を Windows Icon に変換する。
        /// </summary>
        private static Icon BitmapToIcon(Bitmap bitmap)
        {
            var iconHandle = bitmap.GetHicon();
            try
            {
                using var tempIcon = Icon.FromHandle(iconHandle);
                return (Icon)tempIcon.Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
        }

        /// <summary>
        /// アプリのメインバッジを描画する。
        /// </summary>
        private static void DrawAppBadge(Graphics graphics, Rectangle bounds)
        {
            var outerRect = RectangleF.Inflate(bounds, -4, -4);
            var innerRect = RectangleF.Inflate(bounds, -9, -9);
            var lensRect = new RectangleF(bounds.Width * 0.33f, bounds.Height * 0.31f, bounds.Width * 0.34f, bounds.Height * 0.34f);
            var bodyRect = new RectangleF(bounds.Width * 0.18f, bounds.Height * 0.47f, bounds.Width * 0.64f, bounds.Height * 0.23f);

            using var outerPath = CreateRoundedRectPath(outerRect, outerRect.Height * 0.28f);
            using var outerBrush = new LinearGradientBrush(outerRect, Color.FromArgb(24, 38, 64), Color.FromArgb(9, 14, 24), 45f);
            using var outerBorder = new Pen(Color.FromArgb(120, 117, 207, 255), 1.5f);
            graphics.FillPath(outerBrush, outerPath);
            graphics.DrawPath(outerBorder, outerPath);

            using var glowPath = new GraphicsPath();
            glowPath.AddEllipse(RectangleF.Inflate(innerRect, 6, 6));
            using var glowBrush = new PathGradientBrush(glowPath)
            {
                CenterColor = Color.FromArgb(100, 80, 170, 255),
                SurroundColors = new[] { Color.FromArgb(0, 80, 170, 255) },
                CenterPoint = new PointF(bounds.Width * 0.36f, bounds.Height * 0.28f)
            };
            graphics.FillEllipse(glowBrush, RectangleF.Inflate(innerRect, 2, 2));

            using var bodyPath = CreateRoundedRectPath(bodyRect, bodyRect.Height * 0.40f);
            using var bodyBrush = new LinearGradientBrush(bodyRect, Color.FromArgb(61, 82, 117), Color.FromArgb(23, 35, 54), 90f);
            using var bodyPen = new Pen(Color.FromArgb(180, 216, 236, 255), 1.1f);
            graphics.FillPath(bodyBrush, bodyPath);
            graphics.DrawPath(bodyPen, bodyPath);

            var lensOuter = RectangleF.Inflate(lensRect, 1.5f, 1.5f);
            using var lensOuterBrush = new SolidBrush(Color.FromArgb(255, 93, 214, 255));
            using var lensInnerBrush = new SolidBrush(Color.FromArgb(34, 49, 73));
            using var lensCoreBrush = new SolidBrush(Color.FromArgb(220, 14, 20, 36));
            graphics.FillEllipse(lensOuterBrush, lensOuter);
            graphics.FillEllipse(lensInnerBrush, lensRect);

            var lensCore = RectangleF.Inflate(lensRect, -4, -4);
            graphics.FillEllipse(lensCoreBrush, lensCore);

            using var highlightBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
            graphics.FillEllipse(highlightBrush, new RectangleF(bounds.Width * 0.39f, bounds.Height * 0.37f, bounds.Width * 0.07f, bounds.Height * 0.07f));

            using var recordDotBrush = new SolidBrush(Color.FromArgb(240, 255, 88, 104));
            graphics.FillEllipse(recordDotBrush, new RectangleF(bounds.Width * 0.69f, bounds.Height * 0.20f, bounds.Width * 0.11f, bounds.Height * 0.11f));
        }

        /// <summary>
        /// フォルダーアイコンを描画する。
        /// </summary>
        private static void DrawFolderIcon(Graphics graphics)
        {
            using var tabBrush = new SolidBrush(Color.FromArgb(255, 97, 169, 255));
            using var bodyBrush = new SolidBrush(Color.FromArgb(255, 64, 122, 214));
            using var borderPen = new Pen(Color.FromArgb(255, 152, 205, 255), 1f);

            var tab = new RectangleF(4, 6, 8, 5);
            var body = new RectangleF(3, 9, 18, 11);

            using var bodyPath = CreateRoundedRectPath(body, 4f);
            using var tabPath = CreateRoundedRectPath(tab, 2f);
            graphics.FillPath(bodyBrush, bodyPath);
            graphics.FillPath(tabBrush, tabPath);
            graphics.DrawPath(borderPen, bodyPath);
        }

        /// <summary>
        /// 録画アイコンを描画する。
        /// </summary>
        private static void DrawRecordIcon(Graphics graphics)
        {
            using var ringBrush = new SolidBrush(Color.FromArgb(255, 255, 116, 132));
            using var coreBrush = new SolidBrush(Color.FromArgb(255, 210, 36, 56));
            using var shineBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 216));

            graphics.FillEllipse(ringBrush, 3, 3, 18, 18);
            graphics.FillEllipse(coreBrush, 5, 5, 14, 14);
            graphics.FillEllipse(shineBrush, 8, 7, 4, 4);
        }

        /// <summary>
        /// 停止アイコンを描画する。
        /// </summary>
        private static void DrawStopIcon(Graphics graphics)
        {
            using var circleBrush = new SolidBrush(Color.FromArgb(255, 255, 196, 98));
            using var squareBrush = new SolidBrush(Color.FromArgb(255, 146, 92, 12));

            graphics.FillEllipse(circleBrush, 3, 3, 18, 18);
            graphics.FillRectangle(squareBrush, 7, 7, 10, 10);
        }

        /// <summary>
        /// 一時停止アイコンを描画する。
        /// </summary>
        private static void DrawPauseIcon(Graphics graphics)
        {
            using var circleBrush = new SolidBrush(Color.FromArgb(255, 122, 205, 255));
            using var barBrush = new SolidBrush(Color.FromArgb(255, 13, 79, 126));

            graphics.FillEllipse(circleBrush, 3, 3, 18, 18);
            graphics.FillRectangle(barBrush, 8, 6, 3, 12);
            graphics.FillRectangle(barBrush, 13, 6, 3, 12);
        }

        /// <summary>
        /// 再生アイコンを描画する。
        /// </summary>
        private static void DrawPlayIcon(Graphics graphics)
        {
            using var circleBrush = new SolidBrush(Color.FromArgb(255, 121, 224, 170));
            using var triangleBrush = new SolidBrush(Color.FromArgb(255, 15, 98, 68));

            graphics.FillEllipse(circleBrush, 3, 3, 18, 18);
            graphics.FillPolygon(triangleBrush, new[]
            {
                new Point(9, 6),
                new Point(17, 12),
                new Point(9, 18)
            });
        }

        /// <summary>
        /// 終了アイコンを描画する。
        /// </summary>
        private static void DrawExitIcon(Graphics graphics)
        {
            using var doorBrush = new SolidBrush(Color.FromArgb(255, 173, 185, 201));
            using var arrowBrush = new SolidBrush(Color.FromArgb(255, 93, 214, 255));

            graphics.FillRectangle(doorBrush, 4, 4, 8, 16);
            graphics.FillRectangle(arrowBrush, 13, 6, 7, 12);
            graphics.FillPolygon(arrowBrush, new[]
            {
                new Point(12, 12),
                new Point(17, 8),
                new Point(17, 16)
            });
        }

        /// <summary>
        /// 丸みのある角丸パスを作成する。
        /// </summary>
        private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2f;

            if (diameter <= 0f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            var arc = new RectangleF(rect.Location, new SizeF(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }

    /// <summary>
    /// ボタン用アイコンの種類。
    /// </summary>
    internal enum ButtonIconKind
    {
        Folder,
        Record,
        Stop,
        Pause,
        Play,
        Exit
    }

}
