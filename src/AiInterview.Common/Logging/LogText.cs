namespace AiInterview.Common.Logging;

public static class LogText
{
    /// <summary>Rút gọn nội dung nhạy cảm/dài (ví dụ transcript) trước khi log: chỉ giữ vài ký tự đầu và độ dài.</summary>
    public static string Preview(string? text, int max = 80)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(rỗng)";
        }

        var single = text.ReplaceLineEndings(" ");
        return single.Length <= max
            ? $"{single} [{text.Length} ký tự]"
            : $"{single[..max]}… [{text.Length} ký tự]";
    }
}
