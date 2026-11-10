namespace AiInterview.Core.Models;

public class DocStreamMessage
{
    public string DocumentId { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
}

public class DocStreamMessageEnvelope
{
    public string MessageId { get; set; } = string.Empty;
    public DocStreamMessage Message { get; set; } = new();
    public int DeliveryCount { get; set; } = 1;
}

