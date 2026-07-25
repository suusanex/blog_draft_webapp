using BlogDraftWebApp.Core.Exceptions;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public static class EditorialPlanValidator
{
    public static void Validate(EditorialPlan? plan, string overview)
    {
        _ = NormalizeAndValidate(plan, overview);
    }

    public static EditorialPlan NormalizeAndValidate(
        EditorialPlan? plan,
        string overview,
        bool allowUserEdited = true,
        bool validateSections = true)
    {
        if (plan is null)
        {
            throw new EditorialPlanValidationException("編集計画が指定されていません");
        }

        var normalized = EditorialPlanNormalizer.CloneAndTrimIds(plan);
        ValidateCore(normalized, overview, allowUserEdited, validateSections);
        return normalized;
    }

    public static EditorialPlan NormalizeAndValidateBrief(EditorialPlan? plan, string overview)
    {
        return NormalizeAndValidate(plan, overview, allowUserEdited: true, validateSections: false);
    }

    private static void ValidateCore(
        EditorialPlan plan,
        string overview,
        bool allowUserEdited,
        bool validateSections)
    {
        if (plan.FocalPoints is null
            || plan.TriedOrObserved is null
            || plan.Judgements is null
            || plan.ReaderAssumptions is null
            || plan.ExcludedScope is null
            || (validateSections && plan.Sections is null))
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
        foreach (var (item, area) in allItems)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                throw new EditorialPlanValidationException($"{area} の項目IDが空です");
            }

            if (!itemIds.Add(item.Id))
            {
                throw new EditorialPlanValidationException($"項目IDが重複しています: {item.Id}");
            }

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                throw new EditorialPlanValidationException($"{area} の項目本文が空です: {item.Id}");
            }

            if (!allowUserEdited && item.Origin == BriefItemOrigin.UserEdited)
            {
                throw new EditorialPlanValidationException($"Planner応答にUserEdited項目を含められません: {item.Id}");
            }

            var isInferredConstraint = item.Origin == BriefItemOrigin.InferredEditorialConstraint;
            if (isInferredConstraint && area is not ("ReaderAssumptions" or "ExcludedScope"))
            {
                throw new EditorialPlanValidationException($"推論による編集制約を事実項目として扱えません: {item.Id}");
            }

            if (item.Origin is BriefItemOrigin.Input or BriefItemOrigin.Reorganized
                && (string.IsNullOrWhiteSpace(item.SourceExcerpt)
                    || !overview.Contains(item.SourceExcerpt, StringComparison.Ordinal)))
            {
                throw new EditorialPlanValidationException($"入力に存在する根拠断片がありません: {item.Id}");
            }

            if (item.Origin is BriefItemOrigin.InferredEditorialConstraint or BriefItemOrigin.UserEdited
                && item.SourceExcerpt is not null)
            {
                throw new EditorialPlanValidationException($"制約またはユーザー編集項目に入力断片を指定できません: {item.Id}");
            }
        }

        if (!validateSections)
        {
            return;
        }

        var materialIds = new HashSet<string>(StringComparer.Ordinal);
        if (plan.Thesis is not null)
        {
            materialIds.Add(plan.Thesis.Id);
        }

        materialIds.UnionWith(plan.FocalPoints.Select(item => item.Id));
        materialIds.UnionWith(plan.TriedOrObserved.Select(item => item.Id));
        materialIds.UnionWith(plan.Judgements.Select(item => item.Id));
        var excludedIds = new HashSet<string>(plan.ExcludedScope.Select(item => item.Id), StringComparer.Ordinal);
        var sectionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in plan.Sections)
        {
            if (section is null)
            {
                throw new EditorialPlanValidationException("セクション配列にnull要素があります");
            }

            if (string.IsNullOrWhiteSpace(section.Id) || !sectionIds.Add(section.Id))
            {
                throw new EditorialPlanValidationException($"セクションIDが空または重複しています: {section.Id}");
            }

            if (itemIds.Contains(section.Id))
            {
                throw new EditorialPlanValidationException($"項目とセクションでIDが重複しています: {section.Id}");
            }

            if (string.IsNullOrWhiteSpace(section.Heading) || string.IsNullOrWhiteSpace(section.Purpose))
            {
                throw new EditorialPlanValidationException($"セクションの見出しと目的は必須です: {section.Id}");
            }

            if (section.SourceItemIds is null || section.ExcludedScopeItemIds is null)
            {
                throw new EditorialPlanValidationException($"セクションの参照配列はnullにできません: {section.Id}");
            }

            if (section.SourceItemIds.Count == 0)
            {
                throw new EditorialPlanValidationException($"セクションは本文材料を1件以上参照してください: {section.Id}");
            }

            if (section.SourceItemIds.Any(string.IsNullOrWhiteSpace)
                || section.ExcludedScopeItemIds.Any(string.IsNullOrWhiteSpace))
            {
                throw new EditorialPlanValidationException($"セクションの参照IDに空要素があります: {section.Id}");
            }

            if (section.SourceItemIds.Count != section.SourceItemIds.Distinct(StringComparer.Ordinal).Count()
                || section.ExcludedScopeItemIds.Count != section.ExcludedScopeItemIds.Distinct(StringComparer.Ordinal).Count())
            {
                throw new EditorialPlanValidationException($"セクションの参照IDが重複しています: {section.Id}");
            }

            foreach (var sourceId in section.SourceItemIds)
            {
                if (!itemIds.Contains(sourceId))
                {
                    throw new EditorialPlanValidationException($"セクションが存在しない項目を参照しています: {sourceId}");
                }

                if (!materialIds.Contains(sourceId))
                {
                    throw new EditorialPlanValidationException("読者前提または対象外項目を本文材料として指定できません");
                }
            }

            if (section.SourceItemIds.Any(excludedIds.Contains))
            {
                throw new EditorialPlanValidationException("対象外項目を使用する情報として指定できません");
            }

            foreach (var excludedId in section.ExcludedScopeItemIds)
            {
                if (!excludedIds.Contains(excludedId))
                {
                    throw new EditorialPlanValidationException($"対象外項目ではないIDを除外指定しています: {excludedId}");
                }
            }
        }
    }

    private static void AddItems(List<(BriefItem Item, string Area)> destination, List<BriefItem>? items, string area)
    {
        if (items is null)
        {
            throw new EditorialPlanValidationException($"{area} はnullにできません");
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                throw new EditorialPlanValidationException($"{area} にnull要素があります");
            }

            destination.Add((item, area));
        }
    }
}
