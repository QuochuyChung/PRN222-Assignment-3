using AiInterview.DAL.Entities;
using AiInterview.DAL.Models;
using AiInterview.BLL.Documents;
using AiInterview.BLL.Interview;
using AiInterview.BLL.Questions;
using AiInterview.BLL.TurnLog;

namespace AiInterview.BLL.Mapping;

/// <summary>
/// Chuyển entity/model của lớp DAL thành DTO của lớp Service. Chỉ dùng nội bộ trong BLL:
/// Web không bao giờ nhìn thấy entity, chỉ nhìn thấy DTO.
/// </summary>
internal static class EntityMappers
{
    public static QuestionDto ToDto(this Question q) => new(
        q.Id,
        q.Content,
        q.ReferenceAnswer,
        q.KeyPoints.ToList(),
        q.BloomLevel,
        q.SourceDocumentId,
        q.SourceChunkIds.ToList(),
        q.Status,
        q.CreatedAt,
        q.UpdatedAt);

    public static SessionDto ToDto(this InterviewSession s) => new(
        s.Id,
        s.Title,
        s.StudentName,
        s.Status,
        s.QuestionCount,
        s.MaxFollowUps,
        s.AnswerSeconds,
        s.CreatedAt,
        s.StartedAt,
        s.CompletedAt);

    public static TurnDto ToDto(this InterviewTurn t) => new(
        t.TurnId,
        t.SessionId,
        t.QuestionId,
        t.IsFollowUp,
        t.QuestionText,
        t.Transcript,
        t.NeedFollowUp,
        t.Reason,
        t.Sources.ToList(),
        t.StartedAt,
        t.EndedAt);

    public static DocumentDto ToDto(this DocumentRecord d) => new(
        d.Id,
        d.FileName,
        d.ContentType,
        d.Size,
        d.Status,
        d.ErrorMessage,
        d.ChunkCount,
        d.PageCount,
        d.UploadedBy,
        d.UploadedAt,
        d.UpdatedAt);
}
