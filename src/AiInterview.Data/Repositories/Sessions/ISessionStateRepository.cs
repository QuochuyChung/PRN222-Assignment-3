using AiInterview.Data.Models;

namespace AiInterview.Data.Repositories.Sessions;

/// <summary>Trạng thái phiên thi đang chạy. Real = Redis Hash (Dev C, ENG-01). Fake = in-memory.</summary>
public interface ISessionStateRepository
{
    Task<SessionState?> GetAsync(string sessionId, CancellationToken ct = default);

    Task SaveAsync(string sessionId, SessionState state, CancellationToken ct = default);

    /// <summary>Khóa chống trùng theo <c>turnId</c> (SET NX EX). Trả false nếu đã có người xử lý.</summary>
    Task<bool> TryLockAsync(string sessionId, string turnId, TimeSpan ttl, CancellationToken ct = default);

    Task DeleteAsync(string sessionId, CancellationToken ct = default);
}
