using System;
using UnityEngine;

public static class LineDrawing
{
    public static void DDA(float p1x, float p1y, float p2x, float p2y,
            Texture2D texture)
    {
        DDA(p1x, p1y, p2x, p2y, texture, Color.red);
    }

    public static void DDA(float p1x, float p1y, float p2x, float p2y,
            Texture2D texture, Color color)
    {
        var dx = p2x - p1x;
        var dy = p2y - p1y;

        float step;

        if (Math.Abs(dx) > Math.Abs(dy))
        {
            step = Math.Abs(dx);
        }
        else
        {
            step = Math.Abs(dy);
        }

        float x_incr = (float)dx / step;
        float y_incr = (float)dy / step;

        float x = p1x;
        float y = p1y;

        for (int i = 0; i < step; i++)
        {
            texture.SetPixel((int)(x), (int)(y), color);

            x += x_incr;
            y += y_incr;
        }
    }

    public static void Bresenham(int p1x, int p1y, int p2x, int p2y,
            Texture2D texture)
    {
        Bresenham(p1x, p1y, p2x, p2y, texture, Color.red);
    }

    public static void Bresenham(int p1x, int p1y, int p2x, int p2y,
            Texture2D texture, Color color)
    {
        var dx = p2x - p1x;
        var dy = p2y - p1y;
        var pk = 2 * dy - dx;

        var y = p1y;

        for (int x = p1x; x <= p2x; x++)
        {
            texture.SetPixel(x, y, color);

            if (pk > 0)
            {
                y++;
                pk = pk - 2 * dx;
            }
            pk = pk + 2 * dy;
        }
    }
    public static void Bresenham_Low(int p1x, int p1y, int p2x, int p2y,
            Texture2D texture)
    {
        Bresenham_Low(p1x, p1y, p2x, p2y, texture, Color.red);
    }

    public static void Bresenham_Low(int p1x, int p1y, int p2x, int p2y,
            Texture2D texture, Color color)
    {
        var dx = p2x - p1x;
        var dy = p2y - p1y;
        var yi = 1;
        if (dy < 0)
        {
            yi = -1;
            dy = -dy;
        }
        var D = 2 * dy - dx;
        var y = p1y;

        for (int x = p1x; x <= p2x; x++)
        {
            texture.SetPixel(x, y, color);

            if (D > 0)
            {
                y = y + yi;
                D = D + 2 * (dy - dx);
            }
            else
            {
                D = D + 2 * dy;
            }
        }
    }
    public static void Bresenham_High(int p1x, int p1y, int p2x, int p2y,
        Texture2D texture)
    {
        Bresenham_High(p1x, p1y, p2x, p2y, texture, Color.red);
    }

    public static void Bresenham_High(int p1x, int p1y, int p2x, int p2y,
        Texture2D texture, Color color)
    {
        var dx = p2x - p1x;
        var dy = p2y - p1y;
        var xi = 1;
        if (dx < 0)
        {
            xi = -1;
            dx = -dx;
        }
        var D = 2 * dx - dy;
        var x = p1x;

        for (int y = p1y; y <= p2y; y++)
        {
            texture.SetPixel(x, y, color);

            if (D > 0)
            {
                x = x + xi;
                D = D + 2 * (dx - dy);
            }
            else
            {
                D = D + 2 * dx;
            }
        }
    }
}
