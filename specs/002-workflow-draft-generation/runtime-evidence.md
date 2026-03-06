# Runtime Evidence: 002-workflow-draft-generation

## 2026-03-06 Revision (Current Behavior)

- ワークフロー本体は **Step1: アウトライン** と **Step2: 下書き** の2段階です。
- Step2（下書き）確定時点で `Completed` へ遷移します。
- タイトル/冒頭段落案は **独立機能**（UI: `/title-hook`, API: `/titlehook`, `/titlehook/preview`）として提供し、WorkflowSession を引き継ぎません。
- 本節以降に残る旧仕様（ワークフロー内 Step3 タイトル生成）記述は、上記改訂内容で読み替えてください。
> NOTE
> - 本資料は `specs/002-workflow-draft-generation/spec.md` と `plan.md` に基づき、実行時（画面・API・永続化）の協調を **シーケンス図で先に固定** する。
> - ここでの "Runtime Evidence" は「現状実装の正しさ」を保証するものではなく、**期待される協調（期待シーケンス）** を明文化する。

---

## Scenario Sections

### Scenario S-201: Create session

#### Sequence (PlantUML)
```plantuml
@startuml
title S-201 Create session

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Start new workflow
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions {overview}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] CreateSessionAsync(overview)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] CreateAsync(session)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] created(sessionId, currentStep=Step1_Outline)
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E6] session metadata
S_WorkflowApi --> U_WebUI : [E7] 201 Created (sessionId, currentStep, createdAt, deleteAt)
U_WebUI --> User : [E8] Show session + Step1 UI

== Variations ==
alt [E4] storage error
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E9] throw StorageException
  S_WorkflowApi --> U_WebUI : [E10] 500 STORAGE_ERROR
  U_WebUI --> User : [E11] Show error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E8, E11 |
| U-WebUI | E1, E2, E7, E8, E10, E11 |
| S-WorkflowApi | E2, E3, E6, E7, E10 |
| Cmp-WorkflowOrchestrator | E3, E4, E5, E6, E9 |
| D-WorkflowStore | E4, E5 |

---

### Scenario S-211: Generate outline (execute; creates RagSnapshot)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-211 Generate outline (execute)

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
participant "Cmp-PromptComposer\nPromptComposer" as Cmp_PromptComposer
participant "Cmp-Retrieval\nIRetrievalService" as Cmp_Retrieval
participant "Cmp-Llm\nILlmClient" as Cmp_Llm
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Click "アウトライン生成" (execute)
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/outline/generate {regenerate=false}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] GenerateStepAsync(id, Step1_Outline, regenerate)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session (includes initialInput)

Cmp_WorkflowOrchestrator -> Cmp_Retrieval : [E6] SearchAsync(query from initialInput)
Cmp_Retrieval --> Cmp_WorkflowOrchestrator : [E7] chunks
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E8] SaveRagSnapshotAsync(snapshot)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E9] snapshotId

Cmp_WorkflowOrchestrator -> Cmp_PromptComposer : [E10] ComposeOutlinePrompt(initialInput, snapshot, styleCard)
Cmp_PromptComposer --> Cmp_WorkflowOrchestrator : [E11] prompt
Cmp_WorkflowOrchestrator -> Cmp_Llm : [E12] GenerateAsync(prompt)
Cmp_Llm --> Cmp_WorkflowOrchestrator : [E13] outlineGenerated

Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E14] UpdateAsync(session: OutlineGenerated=..., OutlineEdited=..., RagSnapshotId=snapshotId)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E15] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E16] generated + metadata
S_WorkflowApi --> U_WebUI : [E17] 200 OK {generated, ragHitCount, generatedAt}
U_WebUI --> User : [E18] Show outline editor + preview pane

== Variations ==
alt [E6] RAG search fails / 0 hit
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E19] error (RAG_ERROR)
  S_WorkflowApi --> U_WebUI : [E20] 500/400 (RAG_ERROR)
  U_WebUI --> User : [E21] Show error, allow retry
else [E12] LLM timeout
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E22] error (LLM_TIMEOUT)
  S_WorkflowApi --> U_WebUI : [E23] 504 LLM_TIMEOUT
  U_WebUI --> User : [E24] Show error, allow retry
else [E14] storage error
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E25] 500 STORAGE_ERROR
  S_WorkflowApi --> U_WebUI : [E26] 500 STORAGE_ERROR
  U_WebUI --> User : [E27] Show error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E18, E21, E24, E27 |
| U-WebUI | E1, E2, E17, E18, E20, E21, E23, E24, E26, E27 |
| S-WorkflowApi | E2, E3, E16, E17, E20, E23, E26 |
| Cmp-WorkflowOrchestrator | E3-E16, E19, E22, E25 |
| Cmp-Retrieval | E6, E7 |
| Cmp-PromptComposer | E10, E11 |
| Cmp-Llm | E12, E13 |
| D-WorkflowStore | E4, E5, E8, E9, E14, E15 |

---

### Scenario S-212: Preview outline prompt (no LLM call)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-212 Preview outline prompt (no LLM)

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
participant "Cmp-PromptComposer\nPromptComposer" as Cmp_PromptComposer
participant "Cmp-Retrieval\nIRetrievalService" as Cmp_Retrieval
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Click "プロンプトをプレビュー" (outline)
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/outline/preview {}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] PreviewPromptAsync(id, Step1_Outline)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session (initialInput, maybe ragSnapshotId)

alt [E6] session has ragSnapshotId
  Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E6] GetRagSnapshotAsync(ragSnapshotId)
  D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E7] snapshot
else [E8] session has no snapshot yet
  Cmp_WorkflowOrchestrator -> Cmp_Retrieval : [E8] SearchAsync(query from initialInput)
  Cmp_Retrieval --> Cmp_WorkflowOrchestrator : [E9] chunks (ephemeral)
end

Cmp_WorkflowOrchestrator -> Cmp_PromptComposer : [E10] ComposeOutlinePrompt(initialInput, snapshot/chunks, styleCard)
Cmp_PromptComposer --> Cmp_WorkflowOrchestrator : [E11] prompt
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E12] prompt + warning (no log)
S_WorkflowApi --> U_WebUI : [E13] 200 OK {prompt, ragSnapshotId?, warning}
U_WebUI --> User : [E14] Display prompt text

== Variations ==
alt [E4] session not found
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E15] 404 SESSION_NOT_FOUND
  S_WorkflowApi --> U_WebUI : [E16] 404
  U_WebUI --> User : [E17] Show not-found error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E14, E17 |
| U-WebUI | E1, E2, E13, E14, E16, E17 |
| S-WorkflowApi | E2, E3, E12, E13, E16 |
| Cmp-WorkflowOrchestrator | E3-E12, E15 |
| Cmp-Retrieval | E8, E9 |
| Cmp-PromptComposer | E10, E11 |
| D-WorkflowStore | E4, E5, E6, E7 |

---

### Scenario S-213: Auto-save edited outline

#### Sequence (PlantUML)
```plantuml
@startuml
title S-213 Auto-save edited outline

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Edit outline text area
U_WebUI -> U_WebUI : [E2] Debounce (e.g., 300-800ms)
U_WebUI -> S_WorkflowApi : [E3] POST /workflow/sessions/{id}/steps/outline/save {editedContent}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E4] SaveEditAsync(id, Step1_Outline, editedContent)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E5] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E6] session
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E7] UpdateAsync(session: OutlineEdited=editedContent)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E8] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E9] saved=true
S_WorkflowApi --> U_WebUI : [E10] 200 OK
U_WebUI --> User : [E11] Keep editing (optionally show "Saved")

== Variations ==
alt [E7] storage error
  S_WorkflowApi --> U_WebUI : [E12] 500 STORAGE_ERROR
  U_WebUI --> User : [E13] Show error; do not assume saved
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E11, E13 |
| U-WebUI | E1-E3, E10, E11, E12, E13 |
| S-WorkflowApi | E3, E4, E9, E10, E12 |
| Cmp-WorkflowOrchestrator | E4-E9 |
| D-WorkflowStore | E5-E8 |

---

### Scenario S-214: Confirm outline (enables Step2)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-214 Confirm outline

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Click "確定" (outline)
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/outline/confirm {confirmedContent}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] ConfirmStepAsync(id, Step1_Outline, confirmedContent)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session
Cmp_WorkflowOrchestrator -> Cmp_WorkflowOrchestrator : [E6] Validate constraints + state transition
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E7] UpdateAsync(session: OutlineConfirmed=..., CurrentStep=Step2_Draft)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E8] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E9] confirmed=true, nextStep=draft
S_WorkflowApi --> U_WebUI : [E10] 200 OK
U_WebUI --> User : [E11] Navigate/enable Draft step UI

== Variations ==
alt [E6] outline constraint violation
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E12] 400 OUTLINE_CONSTRAINT_VIOLATION
  S_WorkflowApi --> U_WebUI : [E13] 400
  U_WebUI --> User : [E14] Show validation error
else [E4] session not found
  S_WorkflowApi --> U_WebUI : [E15] 404 SESSION_NOT_FOUND
  U_WebUI --> User : [E16] Show not-found
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E11, E14, E16 |
| U-WebUI | E1, E2, E10, E11, E13, E14, E15, E16 |
| S-WorkflowApi | E2, E3, E9, E10, E13, E15 |
| Cmp-WorkflowOrchestrator | E3-E9, E12 |
| D-WorkflowStore | E4, E5, E7, E8 |

---

### Scenario S-221: Preview draft prompt (must use confirmed outline + RagSnapshot)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-221 Preview draft prompt (uses confirmed outline)

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
participant "Cmp-PromptComposer\nPromptComposer" as Cmp_PromptComposer
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Click "プロンプトをプレビュー" (draft)
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/draft/preview {}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] PreviewPromptAsync(id, Step2_Draft)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session (initialInput, ragSnapshotId, outlineConfirmed)

Cmp_WorkflowOrchestrator -> Cmp_WorkflowOrchestrator : [E6] Validate step precondition: outlineConfirmed != null
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E7] GetRagSnapshotAsync(ragSnapshotId)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E8] snapshot

Cmp_WorkflowOrchestrator -> Cmp_PromptComposer : [E9] ComposeDraftPrompt(initialInput, snapshot, styleCard, outlineConfirmed)
Cmp_PromptComposer --> Cmp_WorkflowOrchestrator : [E10] prompt
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E11] prompt + ragSnapshotId + warning
S_WorkflowApi --> U_WebUI : [E12] 200 OK {prompt, ragSnapshotId, warning}
U_WebUI --> User : [E13] Display prompt (must reflect edited+confirmed outline)

== Variations ==
alt [E6] outline not confirmed
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E14] 400 INVALID_STEP_TRANSITION
  S_WorkflowApi --> U_WebUI : [E15] 400 "アウトラインを先に確定してください"
  U_WebUI --> User : [E16] Show error; keep Step1
else [E7] snapshot missing
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E17] 500 STORAGE_ERROR (inconsistent data)
  S_WorkflowApi --> U_WebUI : [E18] 500
  U_WebUI --> User : [E19] Show error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E13, E16, E19 |
| U-WebUI | E1, E2, E12, E13, E15, E16, E18, E19 |
| S-WorkflowApi | E2, E3, E11, E12, E15, E18 |
| Cmp-WorkflowOrchestrator | E3-E11, E14, E17 |
| Cmp-PromptComposer | E9, E10 |
| D-WorkflowStore | E4, E5, E7, E8 |

---

### Scenario S-222: Generate draft (execute; must match preview prompt)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-222 Generate draft (execute)

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
participant "Cmp-PromptComposer\nPromptComposer" as Cmp_PromptComposer
participant "Cmp-Llm\nILlmClient" as Cmp_Llm
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Click "下書き生成" (execute)
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/draft/generate {regenerate=false}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] GenerateStepAsync(id, Step2_Draft, regenerate)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session (outlineConfirmed, ragSnapshotId, initialInput)

Cmp_WorkflowOrchestrator -> Cmp_WorkflowOrchestrator : [E6] Validate step precondition: outlineConfirmed != null
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E7] GetRagSnapshotAsync(ragSnapshotId)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E8] snapshot
Cmp_WorkflowOrchestrator -> Cmp_PromptComposer : [E9] ComposeDraftPrompt(initialInput, snapshot, styleCard, outlineConfirmed)
Cmp_PromptComposer --> Cmp_WorkflowOrchestrator : [E10] prompt

note right of Cmp_WorkflowOrchestrator
[E9-E10] MUST be identical to S-221 (Preview) output.
end note

Cmp_WorkflowOrchestrator -> Cmp_Llm : [E11] GenerateAsync(prompt)
Cmp_Llm --> Cmp_WorkflowOrchestrator : [E12] draftGenerated
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E13] UpdateAsync(session: DraftGenerated=..., DraftEdited=...)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E14] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E15] generated + metadata
S_WorkflowApi --> U_WebUI : [E16] 200 OK {generated}
U_WebUI --> User : [E17] Show draft editor + preview pane

== Variations ==
alt [E6] outline not confirmed
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E18] 400 INVALID_STEP_TRANSITION
  S_WorkflowApi --> U_WebUI : [E19] 400 "アウトラインを先に確定してください"
  U_WebUI --> User : [E20] Show error
else [E11] LLM timeout/error
  Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E21] 504/502 LLM_TIMEOUT/LLM_ERROR
  S_WorkflowApi --> U_WebUI : [E22] mapped error
  U_WebUI --> User : [E23] Show error, allow retry
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E17, E20, E23 |
| U-WebUI | E1, E2, E16, E17, E19, E20, E22, E23 |
| S-WorkflowApi | E2, E3, E15, E16, E19, E22 |
| Cmp-WorkflowOrchestrator | E3-E15, E18, E21 |
| Cmp-PromptComposer | E9, E10 |
| Cmp-Llm | E11, E12 |
| D-WorkflowStore | E4, E5, E7, E8, E13, E14 |

---

### Scenario S-223: Auto-save edited draft

#### Sequence (PlantUML)
```plantuml
@startuml
title S-223 Auto-save edited draft

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Edit draft text area
U_WebUI -> U_WebUI : [E2] Debounce
U_WebUI -> S_WorkflowApi : [E3] POST /workflow/sessions/{id}/steps/draft/save {editedContent}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E4] SaveEditAsync(id, Step2_Draft, editedContent)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E5] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E6] session
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E7] UpdateAsync(session: DraftEdited=editedContent)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E8] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E9] saved=true
S_WorkflowApi --> U_WebUI : [E10] 200 OK
U_WebUI --> User : [E11] Keep editing

== Variations ==
alt [E7] storage error
  S_WorkflowApi --> U_WebUI : [E12] 500 STORAGE_ERROR
  U_WebUI --> User : [E13] Show error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E11, E13 |
| U-WebUI | E1-E3, E10, E11, E12, E13 |
| S-WorkflowApi | E3, E4, E9, E10, E12 |
| Cmp-WorkflowOrchestrator | E4-E9 |
| D-WorkflowStore | E5-E8 |

---

### Scenario S-224: Confirm draft (completes workflow)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-224 Confirm draft

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Click "確定" (draft)
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/draft/confirm {confirmedContent}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] ConfirmStepAsync(id, Step2_Draft, confirmedContent)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session
Cmp_WorkflowOrchestrator -> Cmp_WorkflowOrchestrator : [E6] Validate + state transition
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E7] UpdateAsync(session: DraftConfirmed=..., CurrentStep=Completed)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E8] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E9] confirmed=true, nextStep=completed
S_WorkflowApi --> U_WebUI : [E10] 200 OK
U_WebUI --> User : [E11] Show workflow completed state

== Variations ==
alt [E6] invalid state transition
  S_WorkflowApi --> U_WebUI : [E12] 400 INVALID_STEP_TRANSITION
  U_WebUI --> User : [E13] Show error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E11, E13 |
| U-WebUI | E1, E2, E10, E11, E12, E13 |
| S-WorkflowApi | E2, E3, E9, E10, E12 |
| Cmp-WorkflowOrchestrator | E3-E9 |
| D-WorkflowStore | E4, E5, E7, E8 |

---

### Scenario S-231: Reload/resume workflow session

#### Sequence (PlantUML)
```plantuml
@startuml
title S-231 Reload/resume workflow session

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator
database "D-WorkflowStore\nIWorkflowRepository" as D_WorkflowStore

== Main ==
User -> U_WebUI : [E1] Reload page / revisit with sessionId
U_WebUI -> S_WorkflowApi : [E2] GET /workflow/sessions/{id}
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] GetSessionAsync(id)
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E4] GetByIdAsync(id)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E5] session state + artifacts
Cmp_WorkflowOrchestrator -> D_WorkflowStore : [E6] UpdateAsync(LastAccessedAt=now, DeleteAt=now+30d)
D_WorkflowStore --> Cmp_WorkflowOrchestrator : [E7] ok
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E8] session DTO
S_WorkflowApi --> U_WebUI : [E9] 200 OK
U_WebUI --> User : [E10] Render correct step + load Edited/Generated/Confirmed

== Variations ==
alt [E4] session not found
  S_WorkflowApi --> U_WebUI : [E11] 404 SESSION_NOT_FOUND
  U_WebUI --> User : [E12] Show not-found
else [E4] session expired
  S_WorkflowApi --> U_WebUI : [E13] 410 SESSION_EXPIRED
  U_WebUI --> User : [E14] Show expired message
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E10, E12, E14 |
| U-WebUI | E1, E2, E9, E10, E11, E12, E13, E14 |
| S-WorkflowApi | E2, E3, E8, E9, E11, E13 |
| Cmp-WorkflowOrchestrator | E3-E8 |
| D-WorkflowStore | E4-E7 |

---

### Scenario S-241: Concurrency guard (session busy)

#### Sequence (PlantUML)
```plantuml
@startuml
title S-241 Concurrency guard (SESSION_BUSY)

autonomous

actor "User" as User
participant "U-WebUI\nBlazor UI" as U_WebUI
participant "S-WorkflowApi\nWorkflowEndpoints" as S_WorkflowApi
participant "Cmp-WorkflowOrchestrator\nWorkflowOrchestrator" as Cmp_WorkflowOrchestrator

== Main ==
User -> U_WebUI : [E1] Click generate repeatedly
U_WebUI -> S_WorkflowApi : [E2] POST /workflow/sessions/{id}/steps/draft/generate
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E3] GenerateStepAsync(...) (first request)
... request in progress ...
U_WebUI -> S_WorkflowApi : [E4] POST /workflow/sessions/{id}/steps/draft/generate (second)
S_WorkflowApi -> Cmp_WorkflowOrchestrator : [E5] GenerateStepAsync(...) (second)
Cmp_WorkflowOrchestrator --> S_WorkflowApi : [E6] reject (busy)
S_WorkflowApi --> U_WebUI : [E7] 409 SESSION_BUSY
U_WebUI --> User : [E8] Show "生成中です"; keep first running

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E8 |
| U-WebUI | E1, E2, E4, E7, E8 |
| S-WorkflowApi | E2, E4, E5, E7 |
| Cmp-WorkflowOrchestrator | E3, E5, E6 |

---

### Scenario S-301: Standalone title/hook generation

#### Sequence (PlantUML)
```plantuml
@startuml
title S-301 Standalone title/hook generation

autonomous

actor "User" as User
participant "U-WebUI\nWorkflowTitle" as U_WebUI
participant "S-TitleHookApi\nTitleHookEndpoints" as S_TitleHookApi
participant "Cmp-PromptComposer\nPromptComposer" as Cmp_PromptComposer
participant "Cmp-Llm\nILlmClient" as Cmp_Llm

== Main ==
User -> U_WebUI : [E1] Input completed article body
U_WebUI -> S_TitleHookApi : [E2] POST /titlehook {articleBody}
S_TitleHookApi -> Cmp_PromptComposer : [E3] ComposeTitleHookAsync(articleBody, styleCard)
Cmp_PromptComposer --> S_TitleHookApi : [E4] prompt (article body only)
S_TitleHookApi -> Cmp_Llm : [E5] GenerateAsync(prompt)
Cmp_Llm --> S_TitleHookApi : [E6] generated content
S_TitleHookApi --> U_WebUI : [E7] 200 OK {options[3], model, generatedAt}
U_WebUI --> User : [E8] Show/edit 3 options

== Variations ==
alt [E2] invalid input
  S_TitleHookApi --> U_WebUI : [E9] 400 INVALID_REQUEST
  U_WebUI --> User : [E10] Show validation error
end

@enduml
```

#### Component–Step Map

| Component (C4-ish) | Steps |
|---|---|
| User | E1, E8, E10 |
| U-WebUI | E1, E2, E7, E8, E9, E10 |
| S-TitleHookApi | E2, E3, E5, E7, E9 |
| Cmp-PromptComposer | E3, E4 |
| Cmp-Llm | E5, E6 |

---

## Scenario Ledger (extracted)

| Scenario ID | Title | Purpose/Value | Trigger (When) | Result (Then) | Participants | Primary API / IO | Errors / Timeouts / Retry | Link |
|---|---|---|---|---|---|---|---|---|
| S-201 | Create session | 新規セッションを作り Step1 を開始可能にする | UI で新規開始 | sessionId と currentStep=Step1 が返る | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, D-WorkflowStore | `POST /workflow/sessions` | STORAGE_ERROR | #scenario-s-201-create-session |
| S-211 | Generate outline (execute) | RagSnapshot を作りアウトラインを生成する | 「アウトライン生成」実行 | OutlineGenerated/Edited と RagSnapshotId が保存される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, Cmp-Retrieval, Cmp-PromptComposer, Cmp-Llm, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/outline/generate` | RAG_ERROR / LLM_TIMEOUT / STORAGE_ERROR | #scenario-s-211-generate-outline-execute-creates-ragsnapshot |
| S-212 | Preview outline prompt | LLM 送信せず送信予定プロンプトを確認する | 「プロンプトをプレビュー」(outline) | prompt が表示される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, Cmp-Retrieval, Cmp-PromptComposer, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/outline/preview` | SESSION_NOT_FOUND | #scenario-s-212-preview-outline-prompt-no-llm-call |
| S-213 | Auto-save edited outline | 編集中のアウトラインが失われない | 編集入力（デバウンス） | OutlineEdited が保存される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/outline/save` | STORAGE_ERROR | #scenario-s-213-auto-save-edited-outline |
| S-214 | Confirm outline | Step2 に進める状態にする | 「確定」(outline) | OutlineConfirmed が保存され CurrentStep=Step2 | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/outline/confirm` | OUTLINE_CONSTRAINT_VIOLATION / SESSION_NOT_FOUND | #scenario-s-214-confirm-outline-enables-step2 |
| S-221 | Preview draft prompt | **確定アウトライン**から Step2 のプロンプトを確認する | 「プロンプトをプレビュー」(draft) | prompt が表示され、アウトラインが反映される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, Cmp-PromptComposer, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/draft/preview` | INVALID_STEP_TRANSITION / STORAGE_ERROR | #scenario-s-221-preview-draft-prompt-must-use-confirmed-outline--ragsnapshot |
| S-222 | Generate draft (execute) | **プレビューと同一**のプロンプトで下書きを生成する | 「下書き生成」実行 | DraftGenerated/Edited が保存される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, Cmp-PromptComposer, Cmp-Llm, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/draft/generate` | INVALID_STEP_TRANSITION / LLM_TIMEOUT / LLM_ERROR | #scenario-s-222-generate-draft-execute-must-match-preview-prompt |
| S-223 | Auto-save edited draft | 編集中の下書きが失われない | 編集入力（デバウンス） | DraftEdited が保存される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/draft/save` | STORAGE_ERROR | #scenario-s-223-auto-save-edited-draft |
| S-224 | Confirm draft | ワークフローを完了状態にする | 「確定」(draft) | DraftConfirmed が保存され CurrentStep=Completed | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, D-WorkflowStore | `POST /workflow/sessions/{id}/steps/draft/confirm` | INVALID_STEP_TRANSITION | #scenario-s-224-confirm-draft-completes-workflow |
| S-231 | Reload/resume workflow session | リロード/再訪で状態を復元する | GET session | Step と成果物が復元される | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator, D-WorkflowStore | `GET /workflow/sessions/{id}` | SESSION_NOT_FOUND / SESSION_EXPIRED | #scenario-s-231-reloadresume-workflow-session |
| S-241 | Concurrency guard | 多重送信を抑止する | 連打/並行リクエスト | 2回目以降は 409 で拒否 | User, U-WebUI, S-WorkflowApi, Cmp-WorkflowOrchestrator | `POST .../generate` | 409 SESSION_BUSY | #scenario-s-241-concurrency-guard-session-busy |
| S-301 | Standalone title/hook generation | 完成本文だけを入力にタイトル/冒頭段落案を生成する | /title-hook で生成実行 | 3案が返り、ユーザーが編集可能 | User, U-WebUI, S-TitleHookApi, Cmp-PromptComposer, Cmp-Llm | `POST /titlehook` | INVALID_REQUEST / LLM_TIMEOUT / LLM_ERROR | #scenario-s-301-standalone-titlehook-generation |





