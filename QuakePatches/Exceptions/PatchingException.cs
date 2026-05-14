using System;

namespace QuakePatches.Exceptions;

public class PatchingException : Exception
{
    public PatchingException()
    {
    }

    public PatchingException(string message) : base(message)
    {
    }

    public PatchingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
