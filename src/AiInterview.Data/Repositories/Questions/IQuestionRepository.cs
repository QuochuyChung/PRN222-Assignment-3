using AiInterview.Data.Entities;
using AiInterview.Common.Enums;

namespace AiInterview.Data.Repositories.Questions;

/// <summary>Ngân hàng câu hỏi (SQL). Dev B (QST-03) mở rộng thêm phương thức khi cần.</summary>
public interface IQuestionRepository
{
    Task AddAsync(Question question, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<Question> questions, CancellationToken ct = default);

    Task<Question?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Question>> ListAsync(QuestionStatus? status = null, CancellationToken ct = default);

    Task<int> CountAsync(QuestionStatus? status = null, CancellationToken ct = default);

    Task UpdateAsync(Question question, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
