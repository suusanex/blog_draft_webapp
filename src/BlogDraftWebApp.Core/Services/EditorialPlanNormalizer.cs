using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

internal static class EditorialPlanNormalizer
{
    public static EditorialPlan CloneAndTrimIds(EditorialPlan source)
    {
        return new EditorialPlan
        {
            Thesis = CloneItem(source.Thesis),
            FocalPoints = CloneItems(source.FocalPoints),
            TriedOrObserved = CloneItems(source.TriedOrObserved),
            Judgements = CloneItems(source.Judgements),
            ReaderAssumptions = CloneItems(source.ReaderAssumptions),
            ExcludedScope = CloneItems(source.ExcludedScope),
            Sections = source.Sections is null
                ? null!
                : source.Sections.Select(CloneSection).ToList(),
        };
    }

    private static List<BriefItem> CloneItems(List<BriefItem>? items)
    {
        return items is null ? null! : items.Select(item => CloneItem(item)!).ToList();
    }

    private static BriefItem? CloneItem(BriefItem? item)
    {
        return item is null
            ? null
            : new BriefItem
            {
                Id = item.Id?.Trim()!,
                Text = item.Text,
                Origin = item.Origin,
                SourceExcerpt = item.SourceExcerpt,
            };
    }

    private static PlannedSection CloneSection(PlannedSection? section)
    {
        return section is null
            ? null!
            : new PlannedSection
            {
                Id = section.Id?.Trim()!,
                Heading = section.Heading,
                Purpose = section.Purpose,
                SourceItemIds = TrimIds(section.SourceItemIds),
                ExcludedScopeItemIds = TrimIds(section.ExcludedScopeItemIds),
            };
    }

    private static List<string> TrimIds(List<string>? ids)
    {
        return ids is null ? null! : ids.Select(id => id?.Trim()!).ToList();
    }
}
