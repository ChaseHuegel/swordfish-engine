// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

#nullable enable
using System;

namespace Swordfish.Library.Util;

public readonly struct Result(in bool success, in string? message = null, in Exception? exception = null)
{
    public readonly bool Success = success;
    public readonly string? Message = message;
    public readonly Exception? Exception = exception;

    public static implicit operator bool(Result result) => result.Success;
    public static implicit operator Exception(Result result) => new(result.Message);
    
    public static Result FromSuccess(string? message = null) => new(true, message);

    public static Result FromFailure(string? message = null) => new(false, message);
    
    public static Result FromFailure(Exception? exception = null) => new(false, exception?.Message, exception);
}

public readonly struct Result<T>(in bool success, in T value, in string? message = null, in Exception? exception = null)
{
    public readonly bool Success = success;
    public readonly string? Message = message;
    public readonly T Value = value;
    public readonly Exception? Exception = exception;

    public static implicit operator bool(Result<T> result) => result.Success;
    public static implicit operator T(Result<T> result) => result.Value;
    public static implicit operator Exception(Result<T> result) => new(result.Message);

    public static Result<T> FromSuccess(T value, string? message = null) => new(true, value, message);

    public static Result<T> FromFailure(string? message = null) => new(false, default!, message);
    
    public static Result<T> FromFailure(Exception? exception = null) => new(false, default!, exception?.Message, exception);
}