namespace Swordfish.Library.Util
{
    public readonly struct Result(bool success, string message = null)
    {
        public readonly bool Success = success;
        public readonly string Message = message;

        public static implicit operator bool(Result result) => result.Success;
    }

    public readonly struct Result<T>(bool success, T value, string message = null)
    {
        public readonly bool Success = success;
        public readonly string Message = message;
        public readonly T Value = value;

        public static implicit operator bool(Result<T> result) => result.Success;
        public static implicit operator T(Result<T> result) => result.Value;

        public static Result<T> FromSuccess(T value, string message = null) => new(true, value, message);

        public static Result<T> FromFailure(string message = null) => new(false, default!, message);
    }
}