using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public static class EditorialPlanValidator
{
    public static void Validate(EditorialPlan? plan, string overview)
    {
        if (plan is null)
        {
            throw new EditorialPlanValidationException("編集計画が指定されていません");
        }

        if (plan.FocalPoints is null
            || plan.TriedOrObserved is null
            || plan.Judgements is null
            || plan.ReaderAssumptions is null
            || plan.ExcludedScope is null
            || plan.Sections is null)
        {
            throw new EditorialPlanValidationException("編集計画の配列プロパティはnullにできません");
        }

        var allItems = new List<(BriefItem Item, string Area)>();
        if (plan.Thesis is not null)
        {
            allItems.Add((plan.Thesis, "Thesis"));
        }

        AddItems(allItems, plan.FocalPoints, "FocalPoints");
        AddItems(allItems, plan.TriedOrObserved, "TriedOrObserved");
        AddItems(allItems, plan.Judgements, "Judgements");
        AddItems(allItems, plan.ReaderAssumptions, "ReaderAssumptions");
        AddItems(allItems, plan.ExcludedScope, "ExcludedScope");

        if (plan.Thesis is null && plan.FocalPoints.Count == 0)
        {
            throw new EditorialPlanValidationException("中心命題または重要ポイントを1件以上指定してください");
        }

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        var excludedIds = new HashSet<string>(plan.ExcludedScope.Select(x => x.Id), StringComparer.Ordinal);
        foreach (var (item, area) in allItems)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                throw new EditorialPlanValidationException($"{area} の項目IDが空です");
            }

            if (!itemIds.Add(item.Id.Trim()))
            {
                throw new EditorialPlanValidationException($"項目IDが重複しています: {item.Id}");
            }

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                throw new EditorialPlanValidationException($"{area} の項目本文が空です: {item.Id}");
            }

            var isInferredConstraint = item.Origin == BriefItemOrigin.InferredEditorialConstraint;
            if (isInferredConstraint && area is not ("ReaderAssumptions" or "ExcludedScope"))
            {
                throw new EditorialPlanValidationException($"推論による編集制約を事実項目として扱えません: {item.Id}");
            }

            if (item.Origin is BriefItemOrigin.Input or BriefItemOrigin.Reorganized)
            {
                if (string.IsNullOrWhiteSpace(item.SourceExcerpt)
                    || !overview.Contains(item.SourceExcerpt, StringComparison.Ordinal))
                {
                    throw new EditorialPlanValidationException($"入力に存在する根拠断片がありません: {item.Id}");
                }
            }
        }

        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in plan.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Id)
                || !sectionIds.Add(section.Id.Trim()))
            {
                throw new EditorialPlanValidationException($"セクションIDが空または重複しています: {section.Id}");
            }

            if (string.IsNullOrWhiteSpace(section.Heading) || string.IsNullOrWhiteSpace(section.Purpose))
            {
                throw new EditorialPlanValidationException($"セクションの見出しと目的は必須です: {section.Id}");
            }

            foreach (var sourceId in section.SourceItemIds.Concat(section.ExcludedScopeItemIds))
            {
                if (!itemIds.Contains(sourceId))
                {
                    throw new EditorialPlanValidationException($"セクションが存在しない項目を参照しています: {sourceId}");
                }
            }

            if (section.SourceItemIds.Any(excludedIds.Contains))
            {
                throw new EditorialPlanValidationException("対象外項目を使用する情報として指定できません");
            }

            foreach (var excludedId in section.ExcludedScopeItemIds)
            {
                if (!plan.ExcludedScope.Any(x => string.Equals(x.Id, excludedId, StringComparison.Ordinal)))
                {
                    throw new EditorialPlanValidationException($"対象外項目ではないIDを除外指定しています: {excludedId}");
                }
            }
        }
    }

    private static void AddItems(List<(BriefItem Item, string Area)> destination, IEnumerable<BriefItem>? items, string area)
    {
        if (items is null)
        {
            return;
        }

        destination.AddRange(items.Where(x => x is not null).Select(x => (x, area)));
    }
}
