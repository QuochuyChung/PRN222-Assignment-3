using System;

namespace AiInterview.Core.Exceptions;

public class ScannedPdfException : Exception
{
    public ScannedPdfException(string message) : base(message) { }
    public ScannedPdfException(string message, Exception innerException) : base(message, innerException) { }
}

