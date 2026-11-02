using frontida4all.Web.Models.Entities;

namespace frontida4all.Web.Services;

public record ModerationResult(
    ModerationStatus Status,
    string?          Reason,
    string?          ViolationCategory,
    ModerationStage  Stage,
    float?           Confidence
);
