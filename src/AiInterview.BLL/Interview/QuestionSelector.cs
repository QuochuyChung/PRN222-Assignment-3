namespace AiInterview.BLL.Interview;

/// <summary>
/// ENG-02: chọn câu hỏi <c>Approved</c> cho một phiên mới, tránh lặp lại giữa các thí sinh thi liền nhau.
/// Câu chưa dùng ở các phiên gần đây được ưu tiên; hết câu mới đến lượt câu đã dùng (xáo trộn lại),
/// nên hai thí sinh liền nhau nhận tập câu khác nhau khi ngân hàng câu hỏi còn đủ.
/// </summary>
internal static class QuestionSelector
{
    /// <summary>
    /// Trả về đúng <paramref name="count"/> id câu hỏi, không trùng, ưu tiên câu chưa dùng gần đây.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ngân hàng không đủ câu <c>Approved</c> để tạo phiên.</exception>
    public static IReadOnlyList<Guid> Select(
        IReadOnlyList<Guid> approved,
        IReadOnlyList<Guid> recentlyUsed,
        int count,
        Random random)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Số câu hỏi của phiên phải lớn hơn 0.");
        }

        // Distinct giữ thứ tự để kết quả ổn định khi repository lỡ trả về bản trùng.
        var pool = approved.Distinct().ToList();
        if (pool.Count < count)
        {
            throw new InvalidOperationException(
                $"Need {count} approved questions but only {pool.Count} are available. Approve more questions first.");
        }

        var recent = recentlyUsed.ToHashSet();
        var fresh = Shuffle(pool.Where(id => !recent.Contains(id)).ToList(), random);
        var reused = Shuffle(pool.Where(recent.Contains).ToList(), random);

        return fresh.Concat(reused).Take(count).ToList();
    }

    private static List<Guid> Shuffle(List<Guid> items, Random random)
    {
        // Fisher-Yates.
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }
}
