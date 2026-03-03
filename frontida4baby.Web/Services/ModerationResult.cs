using frontida4baby.Web.Models.Entities;

namespace frontida4baby.Web.Services;

public record ModerationResult(
    ModerationStatus Status,
    string?          Reason,
    string?          ViolationCategory,
    ModerationStage  Stage,
    float?           Confidence
);
