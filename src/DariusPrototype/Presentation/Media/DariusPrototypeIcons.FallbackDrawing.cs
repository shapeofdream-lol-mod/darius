using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public static partial class DariusPrototypeIcons
{
    private static Sprite CreateFallback(string key)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "DariusFallbackIcon_" + key;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color bg = new Color(0.16f, 0.025f, 0.025f, 1f);
        Color red = new Color(0.70f, 0.04f, 0.035f, 1f);
        Color gold = new Color(0.93f, 0.72f, 0.32f, 1f);
        Color dark = new Color(0.05f, 0.01f, 0.01f, 1f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (edge < 4) pixels[y * size + x] = dark;
        }

        if (key == "Q") DrawRing(pixels, size, 31.5f, 31.5f, 24f, 6f, gold);
        else if (key == "W")
        {
            DrawThickLine(pixels, size, 17, 48, 47, 16, 5, red);
            DrawThickLine(pixels, size, 20, 51, 50, 19, 2, gold);
        }
        else if (key == "E")
        {
            DrawThickLine(pixels, size, 16, 18, 32, 45, 5, red);
            DrawThickLine(pixels, size, 48, 18, 32, 45, 5, red);
            DrawThickLine(pixels, size, 31, 45, 31, 53, 4, gold);
        }
        else if (key == "R")
        {
            DrawThickLine(pixels, size, 31, 13, 31, 52, 4, gold);
            DrawThickLine(pixels, size, 18, 20, 45, 20, 6, red);
            DrawThickLine(pixels, size, 18, 20, 24, 29, 4, red);
        }
        else if (key == "FLASH")
        {
            DrawDisc(pixels, size, 31, 31, 11, gold);
            DrawThickLine(pixels, size, 12, 31, 51, 31, 3, new Color(1f, 0.92f, 0.60f, 1f));
            DrawThickLine(pixels, size, 31, 12, 31, 51, 3, new Color(1f, 0.92f, 0.60f, 1f));
        }
        else if (key == "GHOST")
        {
            Color cyan = new Color(0.40f, 0.86f, 1f, 1f);
            DrawDisc(pixels, size, 31, 25, 11, cyan);
            DrawThickLine(pixels, size, 19, 39, 29, 31, 4, cyan);
            DrawThickLine(pixels, size, 34, 31, 47, 43, 4, cyan);
            DrawThickLine(pixels, size, 31, 35, 23, 51, 3, cyan);
        }
        else
        {
            for (int i = 0; i < 5; i++) DrawDisc(pixels, size, 16 + i * 8, 34, 4, red);
            DrawDisc(pixels, size, 32, 22, 7, gold);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = "DariusFallbackIcon_" + key;
        DariusLog.Warn("ICON", "Using generated fallback icon for key=" + key);
        return sprite;
    }

    private static void DrawRing(Color[] px, int size, float cx, float cy, float radius, float thickness, Color c)
    {
        float min = (radius - thickness) * (radius - thickness);
        float max = (radius + thickness) * (radius + thickness);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float d = dx * dx + dy * dy;
            if (d >= min && d <= max) px[y * size + x] = c;
        }
    }

    private static void DrawDisc(Color[] px, int size, int cx, int cy, int radius, Color c)
    {
        int rr = radius * radius;
        for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(size - 1, cy + radius); y++)
        for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(size - 1, cx + radius); x++)
        {
            int dx = x - cx;
            int dy = y - cy;
            if (dx * dx + dy * dy <= rr) px[y * size + x] = c;
        }
    }

    private static void DrawThickLine(Color[] px, int size, int x0, int y0, int x1, int y1, int radius, Color c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps <= 0) { DrawDisc(px, size, x0, y0, radius, c); return; }
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            DrawDisc(px, size, x, y, radius, c);
        }
    }
}