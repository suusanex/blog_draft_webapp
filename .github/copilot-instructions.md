# 共通

1. ドキュメント（コミットログやissueを含む）は日本語で記載する
2. ソースコード上のコメント・XMLコメントは開発者向けのため日本語で記載し、ソースコードやログ出力は英語で記載する
3. 原則として処理失敗時のフォールバックは行わず、処理が失敗したことを示すエラー・例外を返す実装とすること。
4. 全ての例外は、トレースログへException.ToString()の内容を出力すること。そのため、例外を再throwせずに捨てる場合は、その場でトレースログを出力すること。
5. UnitTest と（CIで走る）IntegrationTest は、実OS環境（レジストリ、SetupAPI、サービス、ドライバ、デバイス等）を変更しない。
    - OS依存処理は必ずインターフェースで抽象化し、テスト時はスタブ／モックを注入する（例: ISetupApiWrapper）。
    - CIで実行される統合テストもスタブを使用し、管理者権限や実OS変更を要求しない。
6. 原則としてリフレクションを使用しない。もしも実装上リフレクションを使用するべきだと判断する場合は、コードコメントで必要な理由を説明した上で、チャットでもリフレクションを使用した事実と理由を説明すること。

# Execution policy (Stop-less mode)

- Definition of Done (DoD):
    - Implementation is NOT done when checks pass.
    - Done means: at least one concrete implementation step is completed
    (edit/add files or apply a patch), and you attempted build/tests if available.
- Status-only responses are forbidden:
    - A response that ends after saying "checks passed" is invalid.
    - If checks pass, immediately start the first implementation task in the SAME run.
- No "permission pauses":
    - Do not ask "Shall I proceed?" / "Ready to implement?".
    - If minor ambiguity exists, proceed with safe defaults and log assumptions.
- If you must stop:
    - State exactly ONE blocking reason and the minimal info/action needed.

# プロジェクト固有

1. C#プロジェクトのUnitTestはNUnitとMoqを使用し、AssertはAssert.That形式で記載すること。
