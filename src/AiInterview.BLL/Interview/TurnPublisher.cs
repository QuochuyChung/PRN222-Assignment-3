using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using AiInterview.DAL.Repositories.Queue;
using Microsoft.Extensions.Logging;

namespace AiInterview.BLL.Interview;

/// <summary>
/// Đẩy lượt vừa hoàn tất vào <c>turns-stream</c> bằng đúng <see cref="TurnMessage.ToFields"/>,
/// nên consumer (LOG-01) đọc lại bằng <see cref="TurnMessage.FromFields"/> không bị lệch định dạng.
/// </summary>
internal sealed class TurnPublisher(IMessageQueueRepository queue, ILogger<TurnPublisher> logger) : ITurnPublisher
{
    private const string File = "TurnPublisher.cs";

    public async Task PublishAsync(TurnMessage turn, CancellationToken ct = default)
    {
        try
        {
            await queue.EnsureGroupAsync(QueueNames.TurnsStream, QueueNames.TurnsGroup, ct);
            var messageId = await queue.PublishAsync(QueueNames.TurnsStream, turn.ToFields(), ct);

            logger.Info(File, ServiceNames.Redis,
                "Đã đẩy lượt thi vào hàng đợi, sessionId={SessionId}, turnId={TurnId}, messageId={MessageId}, isFollowUp={IsFollowUp}",
                turn.SessionId, turn.TurnId, messageId, turn.IsFollowUp);
        }
        catch (Exception ex)
        {
            // Log kèm định danh rồi ném lại: người gọi quyết định có bỏ qua lượt này hay không.
            logger.Error(ex, File, ServiceNames.Redis,
                "Đẩy lượt thi thất bại, sessionId={SessionId}, turnId={TurnId}", turn.SessionId, turn.TurnId);
            throw;
        }
    }
}
