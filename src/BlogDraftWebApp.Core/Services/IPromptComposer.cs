using BlogDraftWebApp.Core.Configuration;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public interface IPromptComposer
{
    Task<Prompt> ComposeAsync(BlogOverview overview, StyleCard styleCard, CancellationToken cancellationToken);

    Task<Prompt> ComposeAsync(
        WorkflowStep step,
        BlogOverview overview,
        StyleCard styleCard,
        string? outline,
        string? draft,
        string? editorialMemo,
        CancellationToken cancellationToken);

    Task<Prompt> ComposeTitleHookAsync(string articleBody, StyleCard styleCard, CancellationToken cancellationToken);

    Prompt ComposeOutlineRepair(
        BlogOverview overview,
        StyleCard styleCard,
        string rawContent,
        string validationMessage,
        WorkflowOptions options);

    Prompt ComposeDraftRepair(
        BlogOverview overview,
        StyleCard styleCard,
        string rawContent,
        string validationMessage,
        string? outline = null,
        string? editorialMemo = null);
}
