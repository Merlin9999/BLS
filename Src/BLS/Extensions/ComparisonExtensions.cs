namespace BLS.Extensions;

// Adapted from: https://stackoverflow.com/a/4870407/677612
public static class ComparisonExtensions
{
    public static IComparer<T> AsComparer<T>(this Comparison<T?> comp)
    {
        if (comp == null)
            throw new ArgumentNullException(nameof(comp));
        return new ComparisonComparer<T>(comp);
    }

    public static IComparer<T> AsComparer<T>(this Func<T, T, int> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        return new ComparisonComparer<T>((x, y) => func(x, y));
    }

    public static IEqualityComparer<T> AsEqualityComparer<T>(this Func<T, T, bool> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        return new EqualityComparer<T>((x, y) => func(x, y));
    }

    public static IEqualityComparer<T> AsEqualityComparer<T>(this Comparison<T?> comp)
    {
        if (comp == null)
            throw new ArgumentNullException(nameof(comp));
        return new ComparisonEqualityComparer<T>(comp);
    }

    public static IEqualityComparer<T> AsEqualityComparer<T>(this Func<T, T, int> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        return new ComparisonEqualityComparer<T>((x, y) => func(x, y));
    }

    private class ComparisonComparer<T>(Comparison<T> comp) : IComparer<T>
    {
        private Comparison<T> Comp { get; } = comp;

        public int Compare(T? x, T? y)
        {
            if (x == null)
                return y == null ? 0 : -1;
            if (y == null)
                return 1;

            return this.Comp(x, y);
        }
    }

    private class EqualityComparer<T>(Func<T, T, bool> compareFunc) : IEqualityComparer<T>
    {
        private Func<T, T, bool> CompareFunc { get; } = compareFunc;
        
        public bool Equals(T? x, T? y)
        {
            if (x == null)
                return y == null;
            if (y == null)
                return false;

            return this.CompareFunc(x, y);
        }

        public int GetHashCode(T obj)
        {
            if (obj == null)
                return 0;

            return obj.GetHashCode();
        }
    }

    private class ComparisonEqualityComparer<T>(Comparison<T> comp) 
        : EqualityComparer<T>((x, y) => comp(x, y) == 0);
}