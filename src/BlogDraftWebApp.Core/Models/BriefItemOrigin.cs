using System.Text.Json.Serialization;

namespace BlogDraftWebApp.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BriefItemOrigin
{
    Input,
    Reorganized,
    InferredEditorialConstraint,
    UserEdited,
}
