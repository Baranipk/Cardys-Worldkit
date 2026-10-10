using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Paletteki ikonlar. Hepsi kodla çizilir (dosya veya Unity'nin dahili ikonları gerekmez), temaya göre
    /// açık/koyu renk alır ve önbelleğe alınır. Kendi ikonunuzu çizmek için <see cref="Create"/> kullanın.
    /// </summary>
    public static class WorldKitIcons
    {
        const int Size = 64;

        static readonly Dictionary<string, Texture2D> s_Cache = new Dictionary<string, Texture2D>();

        /// <summary>Temaya uygun ana ikon rengi.</summary>
        public static Color Foreground => EditorGUIUtility.isProSkin ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.22f, 0.22f, 0.22f);

        public static readonly Color Accent = new Color(0.3f, 0.65f, 1f);
        public static readonly Color Danger = new Color(0.95f, 0.35f, 0.35f);
        public static readonly Color Ruler = new Color(1f, 0.8f, 0.25f);

        // ---------------- Fırça modları ----------------
        public static Texture2D Single => Get("single", c => c.Rect(20, 20, 44, 44));
        public static Texture2D Line => Get("line", c => { c.Rect(4, 26, 18, 40); c.Rect(25, 26, 39, 40); c.Rect(46, 26, 60, 40); });
        public static Texture2D Area => Get("area", c =>
        {
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                c.Rect(8 + x * 17, 8 + y * 17, 21 + x * 17, 21 + y * 17);
        });
        public static Texture2D Outline => Get("outline", c => c.RectStroke(10, 10, 54, 54, 6));
        public static Texture2D Scatter => Get("scatter", c =>
        {
            c.Circle(16, 18, 6); c.Circle(42, 12, 5); c.Circle(30, 34, 7); c.Circle(52, 40, 5); c.Circle(14, 50, 5); c.Circle(40, 54, 4);
        });
        public static Texture2D Erase => Get("erase", c => { c.Line(14, 14, 50, 50, 7); c.Line(50, 14, 14, 50, 7); }, Danger);
        public static Texture2D RulerTool => Get("ruler", c =>
        {
            c.RectStroke(4, 20, 60, 44, 4);
            for (int x = 12; x <= 52; x += 8)
                c.Rect(x - 1, 20, x + 1, x % 16 == 12 ? 34 : 29);
        }, Ruler);

        // ---------------- Kategoriler ----------------
        public static Texture2D Floor => Get("floor", c =>
        {
            c.Rect(6, 6, 30, 30); c.Rect(34, 6, 58, 30); c.Rect(6, 34, 30, 58); c.Rect(34, 34, 58, 58);
        });
        public static Texture2D Wall => Get("wall", c =>
        {
            for (int row = 0; row < 4; row++)
            {
                int y0 = 8 + row * 13, offset = row % 2 == 0 ? 0 : 12;
                for (int x = -12 + offset; x < 60; x += 24)
                    c.Rect(Math.Max(6, x + 1), y0, Math.Min(58, x + 22), y0 + 10);
            }
        });
        public static Texture2D House => Get("house", c =>
        {
            c.Triangle(32, 6, 4, 30, 60, 30);
            c.Rect(12, 30, 52, 58);
            c.Clear(26, 40, 38, 58);
        });
        public static Texture2D Tree => Get("tree", c => { c.Circle(32, 24, 18); c.Rect(28, 38, 36, 58); });
        public static Texture2D Stamp => Get("stamp", c => { c.RectStroke(6, 6, 42, 42, 5); c.Rect(22, 22, 58, 58); });
        public static Texture2D Person => Get("person", c => { c.Circle(32, 18, 11); c.RoundRect(16, 32, 48, 58, 8); });
        public static Texture2D Reference => Get("reference", c =>
        {
            c.Circle(26, 18, 9); c.RoundRect(14, 30, 38, 58, 6);
            c.Rect(48, 6, 52, 58); c.Rect(44, 6, 56, 9); c.Rect(44, 55, 56, 58);
        });
        public static Texture2D Folder => Get("folder", c => { c.Rect(6, 14, 28, 22); c.Rect(6, 20, 58, 52); });

        // ---------------- Bölümler ----------------
        public static Texture2D Build => Get("build", c => { c.Rect(6, 34, 58, 58); c.Rect(14, 18, 50, 32); c.Rect(22, 6, 42, 16); });
        public static Texture2D Stats => Get("stats", c => { c.Rect(8, 36, 18, 58); c.Rect(27, 20, 37, 58); c.Rect(46, 8, 56, 58); });
        public static Texture2D Output => Get("output", c =>
        {
            c.RectStroke(8, 26, 56, 58, 5);
            c.Line(32, 6, 32, 40, 6); c.Line(32, 6, 20, 18, 6); c.Line(32, 6, 44, 18, 6);
        });

        // ---------------- Alt çubuk ve başlık ----------------
        public static Texture2D Camera => Get("camera", c =>
        {
            c.RoundRect(6, 18, 58, 52, 6); c.Rect(22, 12, 42, 20);
            c.ClearCircle(32, 35, 11); c.Circle(32, 35, 6);
        });
        public static Texture2D Layers => Get("layers", c =>
        {
            c.Rect(8, 40, 56, 50); c.Rect(8, 25, 56, 35);
            c.RectStroke(8, 10, 56, 20, 2);
        });
        public static Texture2D AddPlayer => Get("addplayer", c =>
        {
            c.Circle(24, 18, 10); c.RoundRect(8, 32, 40, 58, 7);
            c.Rect(46, 30, 52, 54); c.Rect(37, 39, 61, 45);
        });
        public static Texture2D SaveStamp => Get("savestamp", c =>
        {
            c.RectStroke(6, 6, 38, 38, 5); c.Rect(18, 18, 46, 46);
            c.Rect(48, 36, 54, 60); c.Rect(39, 45, 63, 51);
        });
        public static Texture2D Settings => Get("settings", c =>
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Line(32, 32, 32 + Mathf.Cos(a) * 26, 32 + Mathf.Sin(a) * 26, 9);
            }
            c.Circle(32, 32, 19); c.ClearCircle(32, 32, 8);
        });
        public static Texture2D Help => Get("help", c =>
        {
            c.Ring(32, 32, 26, 5); c.Circle(32, 19, 4); c.Rect(29, 28, 35, 48);
        });
        public static Texture2D Refresh => Get("refresh", c =>
        {
            c.Ring(32, 32, 20, 6); c.Clear(32, 0, 64, 30);
            c.Triangle(52, 18, 40, 30, 62, 32);
        });

        // ---------------- Zemin işlemleri ----------------
        public static Texture2D Paint => Get("paint", c =>
        {
            c.RoundRect(28, 4, 36, 30, 3);       // sap
            c.Rect(20, 30, 44, 38);              // bilezik
            c.RoundRect(18, 38, 46, 58, 5);      // kıllar
            c.Clear(25, 50, 27, 58); c.Clear(31, 50, 33, 58); c.Clear(37, 50, 39, 58);
        });
        public static Texture2D Recolor => Get("recolor", c => { c.Triangle(32, 6, 14, 36, 50, 36); c.Circle(32, 40, 18); });
        public static Texture2D Raise => Get("raise", c => { c.Rect(8, 48, 56, 58); c.Triangle(32, 6, 12, 30, 52, 30); c.Rect(26, 28, 38, 44); });
        public static Texture2D Lower => Get("lower", c => { c.Rect(8, 48, 56, 58); c.Triangle(32, 44, 12, 20, 52, 20); c.Rect(26, 4, 38, 22); });
        public static Texture2D Ramp => Get("ramp", c => { c.Triangle(4, 56, 60, 56, 60, 14); c.Rect(4, 52, 60, 58); });
        public static Texture2D Reach => Get("reach", c =>
        {
            c.Ring(14, 50, 9, 5);
            c.Line(21, 43, 31, 33, 5); c.Line(31, 33, 41, 33, 5); c.Line(41, 33, 46, 22, 5);
            c.Rect(46, 6, 50, 30); c.Triangle(50, 6, 62, 12, 50, 18);
        }, new Color(0.35f, 0.85f, 0.45f));
        public static Texture2D NavMesh => Get("navmesh", c =>
        {
            c.Triangle(6, 54, 30, 54, 18, 30); c.Triangle(32, 54, 58, 54, 45, 30);
            c.Triangle(20, 28, 44, 28, 32, 6);
        }, new Color(0.3f, 0.75f, 1f));
        // Boya fırçası: sap + uç (Halka modunun dairesiyle karışmasın)
        public static Texture2D RoundBrush => Get("roundbrush2", c =>
        {
            c.Line(56, 6, 34, 28, 6);
            c.Line(34, 28, 27, 35, 11);
            c.Circle(18, 45, 12);
            c.Triangle(6, 58, 8, 44, 20, 56);
        });
        public static Texture2D StackUp => Get("stackup", c =>
        {
            c.Rect(12, 40, 52, 58); c.RectStroke(12, 18, 52, 36, 4);
            c.Triangle(32, 2, 22, 14, 42, 14);
        });
        // Konum iğnesi (Çerçeve modunun karesiyle karışmasın)
        public static Texture2D Zone => Get("zone2", c =>
        {
            c.Circle(32, 24, 18);
            c.Triangle(17, 32, 47, 32, 32, 60);
            c.ClearCircle(32, 24, 7);
        });
        public static Texture2D Replace => Get("replace", c =>
        {
            c.RectStroke(4, 20, 26, 42, 4); c.Rect(38, 20, 60, 42);
            c.Line(27, 31, 35, 31, 4); c.Triangle(37, 31, 31, 25, 31, 37);
        });
        public static Texture2D PathTool => Get("pathtool", c =>
        {
            c.Line(6, 52, 22, 36, 3); c.Line(22, 36, 40, 34, 3); c.Line(40, 34, 58, 12, 3);
            c.Circle(10, 46, 6); c.Circle(30, 30, 6); c.Circle(52, 22, 6);
        });
        public static Texture2D CircleTool => Get("circletool", c =>
        {
            c.Ring(32, 32, 20, 3);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                c.Circle(32 + Mathf.Cos(a) * 20, 32 + Mathf.Sin(a) * 20, 6);
            }
        });
        public static Texture2D DropDown => Get("dropdown", c =>
        {
            c.Rect(20, 6, 44, 26); c.Rect(29, 26, 35, 40); c.Triangle(32, 50, 22, 38, 42, 38); c.Rect(6, 54, 58, 60);
        });
        public static Texture2D Shuffle => Get("shuffle", c =>
        {
            c.RoundRect(6, 30, 26, 50, 3); c.RoundRect(36, 12, 58, 34, 3); c.Circle(48, 52, 8);
        });
        public static Texture2D EdgeDecor => Get("edgedecor", c => { c.Rect(4, 30, 60, 60); c.Circle(14, 24, 7); c.Circle(32, 22, 9); c.Circle(50, 25, 6); });

        /// <summary>Temaya göre önbelleğe alınmış bir ikon. draw, 64×64 bir tuval üzerinde (sol üst = 0,0) çizer.</summary>
        public static Texture2D Get(string key, Action<Canvas> draw, Color? color = null)
        {
            var cacheKey = key + (EditorGUIUtility.isProSkin ? "_d" : "_l");
            if (s_Cache.TryGetValue(cacheKey, out var texture) && texture != null)
                return texture;

            texture = Create(draw, color ?? Foreground);
            s_Cache[cacheKey] = texture;
            return texture;
        }

        /// <summary>Önbelleğe almadan yeni bir ikon çizer.</summary>
        public static Texture2D Create(Action<Canvas> draw, Color color)
        {
            var canvas = new Canvas();
            draw(canvas);
            return canvas.ToTexture(color);
        }

        /// <summary>
        /// Basit, kenar yumuşatmalı 64×64 tuval. Şekiller kapsama (alfa) olarak birikir, sonunda tek renge boyanır.
        /// Koordinatlar sol üstten başlar.
        /// </summary>
        public sealed class Canvas
        {
            readonly float[] m_Alpha = new float[Size * Size];

            delegate float Sdf(float x, float y);

            void Apply(Sdf distance, bool erase = false)
            {
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float coverage = Mathf.Clamp01(0.5f - distance(x + 0.5f, y + 0.5f));
                    int i = y * Size + x;
                    m_Alpha[i] = erase ? Mathf.Min(m_Alpha[i], 1f - coverage) : Mathf.Max(m_Alpha[i], coverage);
                }
            }

            static float BoxSdf(float px, float py, float x0, float y0, float x1, float y1)
            {
                float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
                float dx = Mathf.Abs(px - cx) - (x1 - x0) * 0.5f;
                float dy = Mathf.Abs(py - cy) - (y1 - y0) * 0.5f;
                return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f);
            }

            static float SegmentSdf(float px, float py, float ax, float ay, float bx, float by)
            {
                var p = new Vector2(px - ax, py - ay);
                var ab = new Vector2(bx - ax, by - ay);
                float t = Mathf.Clamp01(Vector2.Dot(p, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                return (p - ab * t).magnitude;
            }

            public void Rect(float x0, float y0, float x1, float y1) => Apply((x, y) => BoxSdf(x, y, x0, y0, x1, y1));
            public void Clear(float x0, float y0, float x1, float y1) => Apply((x, y) => BoxSdf(x, y, x0, y0, x1, y1), true);
            public void RoundRect(float x0, float y0, float x1, float y1, float r) =>
                Apply((x, y) => BoxSdf(x, y, x0 + r, y0 + r, x1 - r, y1 - r) - r);
            public void RectStroke(float x0, float y0, float x1, float y1, float w) =>
                Apply((x, y) => Mathf.Abs(BoxSdf(x, y, x0 + w * 0.5f, y0 + w * 0.5f, x1 - w * 0.5f, y1 - w * 0.5f)) - w * 0.5f);
            public void Circle(float cx, float cy, float r) => Apply((x, y) => new Vector2(x - cx, y - cy).magnitude - r);
            public void ClearCircle(float cx, float cy, float r) => Apply((x, y) => new Vector2(x - cx, y - cy).magnitude - r, true);
            public void Ring(float cx, float cy, float r, float w) => Apply((x, y) => Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - r) - w * 0.5f);
            public void Line(float ax, float ay, float bx, float by, float w) => Apply((x, y) => SegmentSdf(x, y, ax, ay, bx, by) - w * 0.5f);

            public void Triangle(float ax, float ay, float bx, float by, float cx, float cy)
            {
                Apply((x, y) =>
                {
                    float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
                    float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
                    float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
                    bool inside = !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
                    float edge = Mathf.Min(SegmentSdf(x, y, ax, ay, bx, by), Mathf.Min(SegmentSdf(x, y, bx, by, cx, cy), SegmentSdf(x, y, cx, cy, ax, ay)));
                    return inside ? -edge : edge;
                });
            }

            public Texture2D ToTexture(Color color)
            {
                var pixels = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float a = m_Alpha[y * Size + x];
                    // Texture'da y aşağıdan yukarı: satırları çevir.
                    pixels[(Size - 1 - y) * Size + x] = new Color(color.r, color.g, color.b, color.a * a);
                }

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture;
            }
        }
    }
}
