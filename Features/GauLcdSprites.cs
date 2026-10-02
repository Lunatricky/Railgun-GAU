using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript.Domain
{
    /// <summary>
    /// Copy-paste FS-style row bars. Layout only — caller supplies lines.
    /// </summary>
    class GauLcdSprites
    {
        readonly Color menuColor;
        readonly Color backgroundColor;
        readonly Color fontColor;
        readonly StringBuilder measure = new StringBuilder();
        readonly List<string> texts = new List<string>();
        readonly List<Color> backgroundColors = new List<Color>();
        readonly List<Color> fontColors = new List<Color>();

        public GauLcdSprites()
            : this(Color.White, Color.Black, Color.White)
        {
        }

        public GauLcdSprites(Color menuColor, Color backgroundColor, Color fontColor)
        {
            this.menuColor = menuColor;
            this.backgroundColor = backgroundColor;
            this.fontColor = fontColor;
        }

        public void Add(string s)
        {
            texts.Add(s);
            backgroundColors.Add(backgroundColor);
            fontColors.Add(fontColor);
        }

        public void AddB(string s, Color b)
        {
            texts.Add(s);
            backgroundColors.Add(b);
            fontColors.Add(fontColor);
        }

        public void DrawTo(List<IMyTextSurface> surfaces)
        {
            if (surfaces == null)
                return;

            for (int i = 0; i < surfaces.Count; i++)
                DrawInfoPanel(surfaces[i]);
        }

        public void DrawInfoPanel(IMyTextSurface panel)
        {
            List<MySprite> sprites = new List<MySprite>();

            if (texts != null && texts.Count > 0)
                sprites = BuildSprites(panel);

            using (var frame = panel.DrawFrame())
            {
                for (int i = 0; i < sprites.Count; i++)
                    frame.Add(sprites[i]);
            }
        }

        static MySprite MakeRectSprite(Vector2 center, Vector2 size, Color backgroundColor)
        {
            return new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = center,
                Size = size,
                Color = backgroundColor,
                Alignment = TextAlignment.LEFT
            };
        }

        static MySprite MakeTextSprite(Vector2 position, Color fontColor, string text, float scale)
        {
            return new MySprite
            {
                Type = SpriteType.TEXT,
                Data = text,
                Position = position,
                Color = fontColor,
                RotationOrScale = scale,
                FontId = "DEBUG",
                Alignment = TextAlignment.LEFT
            };
        }

        List<MySprite> BuildSprites(IMyTextSurface panel)
        {
            Vector2 surfaceSize = panel.SurfaceSize;
            Vector2 textureSize = panel.TextureSize;
            var sprites = new List<MySprite>();
            sprites.Add(MakeRectSprite(new Vector2(0, 0), 2 * textureSize, menuColor));

            int rows = texts.Count;
            float minDim = surfaceSize.X < surfaceSize.Y ? surfaceSize.X : surfaceSize.Y;
            float pad = Clamp(minDim * 0.01f, 2f, 6f);

            float originX = (textureSize.X - surfaceSize.X) * 0.5f;
            float originY = (textureSize.Y - surfaceSize.Y) * 0.5f;
            float colW = surfaceSize.X;
            float rowPitch = surfaceSize.Y / rows;
            float barH = rowPitch - pad;
            if (barH < 4f)
                barH = rowPitch * 0.85f;

            float barX = originX + pad;
            float barW = colW - 2f * pad;
            float textPad = pad * 1.5f;
            float textMaxW = barW - 2f * textPad;
            float textMaxH = barH * 0.7f;

            float scale = textMaxH / DebugLineHeight;
            if (scale > 1.3f)
                scale = 1.3f;
            if (scale < 0.35f)
                scale = 0.35f;

            for (int i = 0; i < rows; i++)
            {
                float centerY = originY + i * rowPitch + rowPitch * 0.5f;
                var barPos = new Vector2(barX, centerY);
                var barSize = new Vector2(barW, barH);

                sprites.Add(MakeRectSprite(barPos, barSize, backgroundColors[i]));

                string line = texts[i] ?? "";
                float scaleLine = FitWidth(panel, line, scale, textMaxW);
                float lineH = scaleLine * DebugLineHeight;
                float barTop = centerY - barH * 0.5f;
                float extra = barH - lineH;
                if (extra < 0f)
                    extra = 0f;
                float textX = barX + textPad;
                float textY = barTop + extra * 0.22f;

                sprites.Add(MakeTextSprite(new Vector2(textX, textY), fontColors[i], line, scaleLine));
            }

            return sprites;
        }

        const float DebugLineHeight = 28.8f;
        const float DebugCharWidth = 15f;

        float FitWidth(IMyTextSurface panel, string text, float scale, float maxW)
        {
            if (maxW <= 1f)
                return scale;

            Vector2 size = Measure(panel, text, scale);
            float w = size.X > 1f
                ? size.X
                : (text != null && text.Length > 0 ? text.Length : 1) * DebugCharWidth * scale;

            if (w > maxW)
                scale *= maxW / w;
            return scale;
        }

        Vector2 Measure(IMyTextSurface panel, string text, float scale)
        {
            measure.Clear();
            measure.Append(text ?? "");
            return panel.MeasureStringInPixels(measure, "DEBUG", scale);
        }

        static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }
    }
}
