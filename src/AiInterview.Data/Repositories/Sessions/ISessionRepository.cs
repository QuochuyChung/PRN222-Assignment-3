using AiInterview.Data.Entities;
using AiInterview.Common.Enums;

namespace AiInterview.Data.Repositories.Sessions;

/// <summary>Phiên thi (SQL). Dev C (ENG-02) mở rộng thêm phương thức khi cần.</summary>
public interface ISessionRepository
{
    Task AddAsync(InterviewSession session, CancellationToken ct = default);

    Task<InterviewSession?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<InterviewSession>> ListAsync(SessionStatus? status = null, CancellationToken ct = default);

    Task<int> CountAsync(SessionStatus? status = null, CancellationToken ct = default);

    Task UpdateAsync(InterviewSession session, CancellationToken ct = default);
}
