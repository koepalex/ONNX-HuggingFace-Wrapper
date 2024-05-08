namespace OnnxHuggingFaceWrapper;

public static class Extensions
{
    public static void Apply<T>(this Nullable<T> value, Action<T> action, Nullable<T> defaultValue = null) where T : struct
    {
        if (value.HasValue)
        {
            action(value.Value);
        }
        else if (defaultValue.HasValue)
        {
            action(defaultValue.Value);
        }
    }
}