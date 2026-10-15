namespace RaceDay.Api.DTOs.Results;

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
