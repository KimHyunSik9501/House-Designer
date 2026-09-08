namespace HouseDesigner.Models;

/// <summary>
/// UI 프레임워크와 독립적인 2D 좌표입니다. 모델 좌표의 1 단위는 1 cm입니다.
/// </summary>
public readonly record struct Point(double X, double Y)
{
    public static Point operator +(Point point, Vector vector) => new(point.X + vector.X, point.Y + vector.Y);
    public static Point operator -(Point point, Vector vector) => new(point.X - vector.X, point.Y - vector.Y);
    public static Vector operator -(Point left, Point right) => new(left.X - right.X, left.Y - right.Y);
    public static implicit operator Avalonia.Point(Point point) => new(point.X, point.Y);
    public static implicit operator Point(Avalonia.Point point) => new(point.X, point.Y);
}

public struct Vector(double x, double y)
{
    public double X { readonly get; set; } = x;
    public double Y { readonly get; set; } = y;
    public readonly double Length => Math.Sqrt(LengthSquared);
    public readonly double LengthSquared => X * X + Y * Y;

    public void Normalize()
    {
        var length = Length;
        if (length > double.Epsilon)
        {
            X /= length;
            Y /= length;
        }
    }

    public static double Multiply(Vector left, Vector right) => left.X * right.X + left.Y * right.Y;
    public static Vector operator +(Vector left, Vector right) => new(left.X + right.X, left.Y + right.Y);
    public static Vector operator -(Vector left, Vector right) => new(left.X - right.X, left.Y - right.Y);
    public static Vector operator -(Vector value) => new(-value.X, -value.Y);
    public static Vector operator *(Vector value, double scale) => new(value.X * scale, value.Y * scale);
    public static Vector operator *(double scale, Vector value) => value * scale;
    public static Vector operator /(Vector value, double scale) => new(value.X / scale, value.Y / scale);
    public static implicit operator Avalonia.Vector(Vector vector) => new(vector.X, vector.Y);
    public static implicit operator Vector(Avalonia.Vector vector) => new(vector.X, vector.Y);
}

public struct Rect : IEquatable<Rect>
{
    private bool _isEmpty;

    public Rect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        _isEmpty = false;
    }

    public Rect(Point first, Point second)
        : this(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
            Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y))
    {
    }

    public Rect(Avalonia.Size size) : this(0, 0, size.Width, size.Height) { }

    public double X { readonly get; private set; }
    public double Y { readonly get; private set; }
    public double Width { readonly get; private set; }
    public double Height { readonly get; private set; }
    public readonly double Left => X;
    public readonly double Top => Y;
    public readonly double Right => X + Width;
    public readonly double Bottom => Y + Height;
    public readonly Point TopLeft => new(Left, Top);
    public readonly Point TopRight => new(Right, Top);
    public readonly Point BottomLeft => new(Left, Bottom);
    public readonly Point BottomRight => new(Right, Bottom);
    public readonly bool IsEmpty => _isEmpty;

    public static Rect Empty => new() { _isEmpty = true };

    public readonly bool Contains(Point point) => !IsEmpty && point.X >= Left && point.X <= Right
                                                   && point.Y >= Top && point.Y <= Bottom;

    public void Inflate(double horizontal, double vertical)
    {
        if (IsEmpty) return;
        X -= horizontal;
        Y -= vertical;
        Width += horizontal * 2;
        Height += vertical * 2;
    }

    public void Union(Rect rect)
    {
        if (rect.IsEmpty) return;
        if (IsEmpty)
        {
            this = rect;
            return;
        }

        var left = Math.Min(Left, rect.Left);
        var top = Math.Min(Top, rect.Top);
        var right = Math.Max(Right, rect.Right);
        var bottom = Math.Max(Bottom, rect.Bottom);
        this = new Rect(left, top, right - left, bottom - top);
    }

    public readonly bool Equals(Rect other) => _isEmpty == other._isEmpty && X.Equals(other.X)
        && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);
    public override readonly bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height, _isEmpty);
    public static bool operator ==(Rect left, Rect right) => left.Equals(right);
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);
    public static implicit operator Avalonia.Rect(Rect rect) => rect.IsEmpty
        ? default
        : new Avalonia.Rect(rect.X, rect.Y, rect.Width, rect.Height);
    public static implicit operator Rect(Avalonia.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
