using System.Text;
using System.Text.Json;
using BlogDraftWebApp.Core.Models;

namespace BlogDraftWebApp.Core.Services;

public static class EditorialPlanPromptComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static Prompt Compose(
        string overview,
        PlanGenerationMode mode,
        EditorialPlan? currentPlan)
    {
        var system = """
            あなたはブログ記事の編集計画を作るPlannerです。
            出力はJSONオブジェクトだけにしてください。Markdownコードフェンスや説明文は付けないでください。
            自由入力にない技術的事実、観測結果、製品仕様、具体例、手順を作らないでください。
            入力由来または入力を再整理した事実項目には、入力本文から完全一致するsourceExcerptを必ず付けてください。
            推論してよいのはreaderAssumptionsとexcludedScopeの編集上の制約だけです。その場合originはInferredEditorialConstraintにしてください。
            情報が少ない場合は項目や見出しを増やさず、空の配列を返してください。
            すべての項目とセクションには一意なidを付けてください。
            JSONの配列プロパティは省略せず、空の場合は[]にしてください。
            """;

        var user = new StringBuilder();
        user.AppendLine("[EDITORIAL_PLAN_JSON]");
        user.AppendLine("## 自由入力");
        user.AppendLine("<free_input>");
        user.AppendLine(overview);
        user.AppendLine("</free_input>");
        user.AppendLine();
        user.AppendLine("## 必須JSON形状");
        user.AppendLine("thesis, focalPoints, triedOrObserved, judgements, readerAssumptions, excludedScope, sections");

        if (mode == PlanGenerationMode.SectionsOnly)
        {
            user.AppendLine();
            user.AppendLine("## 現在の編集計画");
            user.AppendLine("次の計画の項目を一字一句変更せず保持し、sectionsだけを再提案してください。既存項目を参照するsourceItemIdsを使ってください。");
            user.AppendLine(JsonSerializer.Serialize(currentPlan, JsonOptions));
        }
        else
        {
            user.AppendLine();
            user.AppendLine("自由入力を中心命題、重要ポイント、観測、判断、読者前提、対象外、見出し案へ整理してください。");
        }

        return new Prompt
        {
            SystemMessage = system.Trim(),
            UserOverview = user.ToString().Trim(),
        };
    }
}
