# Native Chat Migration

> **상태:** 개인 작업 노트 (팀 쉐어용 아님).
> Gateway에서 서빙하는 WebView2 채팅을 완전한 네이티브 WinUI surface로 옮기기 위한 플랜.

## TL;DR

채팅을 gateway-served WebView2에서 네이티브 WinUI surface로 옮긴다. 기존 코드는 건드리지 않고, 새 surface를 feature flag 뒤에서 병렬로 돌려서 known-good fallback과 A/B 검증 가능하게 만든다.

### Milestones

- **M1 (이 플랜)** — 순수 채팅 + agent CoT/tool 카드. 전송, 스트리밍 수신, **stop / abort**, Markdown (paragraph / fenced / inline / link), phase chip이 붙은 tool 카드, **reasoning block (collapsible)**, **재연결 시 history rehydrate**가 포함된 connection state, 두 surface (`ChatWindow` + `ChatPage`), composer 드롭다운 (channel / model / reasoning).
- **M2** — Slash-command palette, welcome chips.
- **M3** — Code-block 복사 + syntax highlighting, attachment / inline image, per-message hover action.
- **M4** — Conversation list rail, multi-session, history rehydrate.
- **M5** — Speech / voice conversation을 네이티브 surface에 통합 (Ranjesh의 최근 데모와 parity). @regis 요청 사항.
- **M6** — Flag 기본값을 native로 뒤집고, 한 cycle 안정 운영 후 WebView2 채팅 코드 제거.

## 1. Goals

1. 메시지 thread, 스트리밍 응답, agent CoT/tool 카드, 연결 상태 — 채팅 경험 전체를 네이티브 WinUI 컨트롤로 렌더한다.
2. 마이그레이션 동안 기존 WebView2 채팅 경로를 **완전히 그대로** 둔다. 동일 gateway, 동일 session에서 known-good surface와 A/B 검증할 수 있게.
3. 이미 네이티브인 부분은 재사용한다: `ChatShell` chrome (header, composer, dropdown rail, hamburger)이 새 native thread를 호스트한다. 입력 UX 재구현 안 함.
4. wire 레벨에서 이미 맞는 부분도 재사용한다: 전송은 `OpenClawGatewayClient.SendChatMessageAsync`, 수신은 기존 이벤트 구독.
5. cutover를 안전하고 reversible하게. Feature flag로 surface 선택, 코드 수술 없이 양쪽 어디로든 되돌릴 수 있게.

## 2. Audit findings

### 2.1 현재 채팅 아키텍처

지금 채팅 surface는 하이브리드:

- `ChatShell` (UserControl) — 완전 네이티브 chrome: header bar, hamburger toggle, conversations rail, 텍스트 입력 + channel/model/reasoning `ComboBox` + 액션 버튼이 들어간 composer. `SendRequested`, `DropdownChanged` 이벤트와 `SetDropdownState`(프로그래매틱 채움용) 노출.
- `ChatSurface` (UserControl) — `ChatShell`을 감싸고 shell의 `ThreadContent` slot에 `WebView2`를 박는다. WebView2를 per-app user data folder로 초기화하고 `https://gateway-host/?token=...`로 navigate한 다음:
  - `AddScriptToExecuteOnDocumentCreatedAsync`로 CSS 주입해서 gateway 웹 chrome (`header.topbar`, `aside.sidebar`, `.content-header`, `.agent-chat__input`)을 숨기고 flexbox column으로 reflow.
  - `ExecuteScriptAsync`로 JS 주입해서 네이티브 composer 텍스트를 gateway 페이지의 `<textarea>`에 relay하고 send 버튼 클릭 (React-aware value setter 포함).
  - Gateway의 `<select>` 엘리먼트를 polling해서 네이티브 `ChatShell` 드롭다운으로 mirror.
- `ChatWindow` (tray popup)와 `Pages/ChatPage` (Hub tab) 둘 다 `ChatSurface`를 호스트. 채팅 진입점은 이 둘뿐.
- `GatewayChatHelper`와 `GatewayChatUrlBuilder`가 채팅 URL을 빌드 (ws → http scheme swap, token query, optional session key) 하고 WebView2 설정.

### 2.2 현재 채팅 surface가 사용자에게 노출하는 것

라이브 gateway 채팅 + 위 컨트롤에서 뽑은 인벤토리. 네이티브 재구축 트리아지의 universe.

**상단 toolbar:** channel / model / reasoning 셀렉터, 상태 / 에러 pill, refresh, agent panel toggle, tools toggle, snapshot trigger, recent / history.

**Empty / welcome 상태:** 봇 헤더 (아바타 / 이름 / status dot), quick-reply suggestion chip.

**Composer:** 멀티라인 입력 (Enter-to-send, Shift+Enter 줄바꿈), slash-command 힌트, inline error banner, attach (paperclip), realtime voice (broadcast), `+` context insert, export, send.

**Thread rendering:** user 버블, 스트리밍 assistant 버블 (token-by-token), Markdown (paragraph, list, link, inline code, fenced code block), code-block chrome (language tag + copy 버튼), agent CoT / tool-call 카드 (phase: `start` / `result` / `error`, per-tool 아이콘), collapsible tool-result detail, per-tool streaming progress, auto-scroll-to-bottom (사용자 스크롤업 시 일시정지), per-message timestamp, inline image / attachment 렌더링, hover action (copy / re-run / share).

**Session & state:** URL-keyed session (`?session=...`), per-channel routing, per-model selection, 연결 상태, 재오픈 시 history rehydrate.

### 2.3 wire 레벨에서 이미 맞아 있는 것

`OpenClawGatewayClient`는 M1에서 **새 gateway 프로토콜 surface 없이도 충분**:

- `SendChatMessageAsync(message, sessionKey)` — `chat.send`를 request-id correlation, 30초 timeout, 자동 생성 `idempotencyKey`(line ~268 — gateway 프로토콜 필수)와 함께 발행.
- `AgentEventReceived`와 `ActivityChanged`가 `ClassifyTool`로 이미 분류된 스트리밍 tool / CoT 시그널을 운반.
- `ProcessMessage`의 assistant message 추출이 modern `payload.message.role + content[].text`와 legacy `role: assistant` 양쪽 형태를 모두 인식.
- `SessionsUpdated`, `ModelsListUpdated`, pairing-required 상태, auth-failed 상태 모두 이미 노출됨.

**M1에 필요한 wire 레이어 갭:**

- `chat.abort` RPC 래퍼 — `OpenClawGatewayClient`에 아직 없음. M1 stop 버튼이 필요함 (§4.6).
- 재연결 시 `chat.history` 재호출 — connection-state 핸들러에 wiring 안 됨. 스트리밍 재전송은 retroactive하지 않음 → 재호출 없으면 disconnect 윈도우 동안 emit된 delta 유실.

### 2.4 갭

네이티브 갭은 렌더링이지 프로토콜이 아니다. 필요한 것:

- 기존 이벤트를 구독하고, 스트리밍 delta를 활성 assistant 버블로 coalesce하고, user / assistant / agent-event / system-notice 아이템을 렌더하는 네이티브 메시지 thread.
- 스트리밍 친화적인 Markdown 렌더러 (paragraph, fenced + inline code, link).
- `ActivityChanged` + `AgentEventReceived` 기반의 phase chip 달린 agent CoT / tool-call 카드.
- `ChatWindow`와 `ChatPage`에 surface-selection seam — flag off면 기존 `ChatSurface`, on이면 새 surface. `ChatSurface` 자체는 안 건드림.
- M1 드롭다운: 웹 `<select>` 스크래핑 대신 기존 gateway 상태 (`SessionsUpdated`, `ModelsListUpdated`)에 바인딩, 선택값을 `chat.send`에 실어서 전달.

## 3. Approach

### 3.1 Feature flag로 게이트되는 병렬 surface

`ChatSurface`의 public shape (`Initialize(gatewayUrl, token)`, `NavigateHome()`, `Reload()`, `OpenInBrowser()` no-op) 을 미러링하는 새 `NativeChatSurface` UserControl 도입. 기존 `ChatShell`을 호스트하고, `ThreadContent`에 새 `NativeChatThread`를 박는다. 기존 `ChatSurface`와 `GatewayChatHelper`는 안 건드림.

Feature flag로 어느 surface를 호스트할지 선택:

- 설정: `SettingsManager.UseNativeChat` (bool, 기본 `false`).
- Override: `OPENCLAW_TRAY_NATIVE_CHAT=1` 환경변수.
- Helper: `OpenClawTray.Helpers.NativeChatFeature.IsEnabled()`.
- Selection seam: `ChatWindow.ctor`와 `ChatPage.OnNavigatedTo`가 helper 기준으로 `ChatSurface` 또는 `NativeChatSurface`를 인스턴스화. 호스트 측 수정은 이 두 군데가 전부.

이렇게 하면 flag-off 실행은 현재 출시 채팅 경로와 bit-identical하고, flag-on 실행은 동일 gateway, 동일 session에 대해 네이티브 재구축을 그대로 검증한다.

### 3.2 네이티브 컴포넌트

```
ChatWindow / ChatPage
   └─ NativeChatSurface (신규)
        └─ ChatShell (기존 — composer, header, rail)
             └─ ThreadContent: NativeChatThread (신규)
                   • ItemsRepeater가 ChatTranscriptStore.Items에 바인딩
                   • DataTemplateSelector가 아이템별 템플릿 선택:
                       - UserMessageBubble
                       - AssistantMessageBubble  (streaming-capable Markdown)
                       - AgentEventCard          (tool / CoT, phase-aware)
                       - SystemNoticeRow         (error, reconnect, pairing)

ChatTranscriptStore (신규 — OpenClawTray.Services)
   • OpenClawGatewayClient 이벤트 구독
   • ObservableCollection<ChatTimelineItem> 소유
   • 활성 assistant 버블에 스트리밍 delta coalesce
   • ActivityChanged + AgentEventReceived → AgentEventCardVm 매핑
   • Per-session keyed (sessionKey) → surface의 session에 스코프 가능
```

전송 경로: `ChatShell.SendRequested` → `OpenClawGatewayClient.SendChatMessageAsync`, 현재 선택된 channel / model / reasoning을 payload에 첨부. 사용자 버블은 `await` 전에 optimistic하게 append; 실패 시 errored 마킹 + `SystemNoticeRow` append.

수신 경로: `ChatTranscriptStore`가 한 번만 구독하고 `ActiveAssistantId` 커서 하나를 유지. 새 delta는 그 버블에 append, 종료 시그널 (다음 user send, 또는 finalized assistant message) 도착 시 close. Tool start → result 전이는 새 카드 추가가 아니라 기존 `AgentEventCardVm`을 mutate.

### 3.3 재구현 아닌 재사용

- `ChatShell`은 composer / header / hamburger / dropdown 위젯에 그대로 재사용. 네이티브 surface는 JS 스크래핑 대신 `SessionsUpdated` / `ModelsListUpdated`에서 드롭다운을 채운다.
- `OpenClawGatewayClient`는 그대로 소비. parity 갭이 새 이벤트나 RPC를 요구하면 문서화하고 re-plan, 조용히 patch 안 함.
- `AGENTS.md`의 `SettingsManager` 테스트 격리 룰 준수: tray 테스트는 `OPENCLAW_TRAY_DATA_DIR` 또는 temp dir 사용. 실제 `%APPDATA%`를 향한 `new SettingsManager()` 절대 금지.

## 4. M1 구체화

### 4.1 Markdown 렌더링 예시

M1에서 지원되는 Markdown은 paragraph, fenced code block, inline code, link 네 가지. 구체적으로:

**Example A — code answer.** Gateway에서 오는 raw 텍스트:

````
The tray icon color is set in `Helpers/IconHelper.cs` at line 83:

```csharp
_appIcon = CreateLobsterIcon(Color.FromArgb(255, 99, 71)); // Lobster red
```

To change it to blue, modify the `Color.FromArgb` arguments. The standard Windows accent blue is `(0, 120, 212)`. See [the docs](https://learn.microsoft.com/...) for the full palette.
````

렌더링: `IconHelper.cs`, `Color.FromArgb`, `(0, 120, 212)`은 monospace + 옅은 배경. C# 블록은 `csharp` language tag가 붙은 코드 패널 안에 (M1에서는 copy 버튼 없음, M3에서 추가). 링크는 클릭 가능, 시스템 브라우저로 열림.

**Example B — M1이 의도적으로 렌더 안 하는 것.** Raw 텍스트:

```
I checked the file and found **3 issues**:

- Unused `using` on line 5
- `GetAccentColor()` is dead code
- Missing null check at line 42
```

Bold와 list가 없으니 M1은 `**3 issues**`를 그대로 보여주고 (asterisk 그대로 노출), bullet들은 `-` 문자가 남은 채 한 줄로 collapse. 메시지가 여전히 가독성 있어서 M1 trade-off로는 OK. 리뷰 피드백에서 너무 거칠다고 하면 bold + bullet은 cheap하게 추가 가능 — merge 전 끌어올림.

### 4.2 Agent CoT / tool 카드 예시

`ActivityChanged`와 `AgentEventReceived`가 이미 tool name, phase, label을 운반. 카드 템플릿이 렌더:

```
┌─────────────────────────────────────────┐
│ 🔍  Searching repo for "AccentColor"    │  ← label, tool 아이콘
│     [Running]                           │  ← phase chip (yellow)
│  ▾ tool: grep · 2.1s                    │  ← collapsible detail
└─────────────────────────────────────────┘
```

같은 tool이 `result`를 emit하면 카드가 in place로 mutate (새 카드 안 만듦): chip이 `[Done]` (green)으로, detail이 펼쳐져서 output count나 첫 result 표시. `error`면 chip이 `[Error]` (red)로 + detail auto-expanded.

전형적인 streaming turn:

```
You:           Find dead code in IconHelper.cs

🔧 Card:       Reading IconHelper.cs           [Done]
🔧 Card:       Searching for callers           [Done]
Assistant:     I found one method that has no callers:
               `GetAccentColor()` at line 31...
```

네 아이템 모두 transcript에서 sibling, gateway에서 도착한 순서. Store가 assistant 버블이 single item으로 stream되는 걸 보장 — delta는 `ActiveAssistantId`로 coalesce, 절대 split되지 않음.

### 4.3 Composer 드롭다운

세 드롭다운은 이미 `ChatShell`에 네이티브 `ComboBox`로 있음. M1은 채움 소스만 바꾼다:

- **Channel** — `SessionsUpdated` / `SessionPreviewUpdated`로 노출되는 채널에 바인딩.
- **Model** — `ModelsListUpdated`에 바인딩.
- **Reasoning** — `models.list`의 모델 엔트리에 실린 per-model reasoning option에 바인딩.

선택값 wire 경로 두 가지 (Open Question #1):

1. **Native param** — `chat.send`에 `channel`, `model`, `reasoning` (top-level 또는 `options` 아래).
2. **Slash-command shim** — channel / model / reasoning 토글이 이미 gateway slash command (`/model gpt-4`, `/reasoning on|off`, `/new`) 로 존재. 드롭다운 변경 시 다음 user 메시지의 payload 바꾸는 대신, slash 텍스트로 hidden `chat.send` 한 번 emit. 확인할 프로토콜 surface가 적고, 지금 당장 작동.

사용자가 아무 것도 선택 안 했으면 gateway 기본값이 그대로 유지됨.

### 4.4 Reasoning 콘텐츠 렌더링

Gateway는 `isReasoning: true`로 플래그된 assistant payload를 emit — 모델 thinking, 사용자에게 보이는 답변과 별개. M1에서는:

- Reasoning delta는 별도의 collapsible "Thinking" 블록에 누적 (assistant 버블 위 또는 inline 앞).
- 기본 collapsed. Tool 카드처럼 expand 토글.
- Reasoning이 보이는지 자체는 reasoning 드롭다운이 컨트롤 (wire 레벨에서는 `/reasoning on|off` — §4.3).
- `chat.history`는 visible transcript에서 reasoning을 이미 strip하므로, rehydrate 시 historical reasoning 블록은 안 그려도 됨.

### 4.5 Connection 상태 + rehydrate

Transcript의 `SystemNoticeRow` 아이템과 `ChatShell`의 inline banner로 surface:

- `connected` — banner 숨김, notice 없음.
- `disconnected` / `reconnecting` — yellow banner, 재연결 성공 시 optional notice row.
- `pairing-required` — red banner + pairing flow deep-link, transcript에 notice row.
- `auth-failed` / `error` — red banner + 에러 메시지 notice row.

**재연결 시 rehydrate.** Streamed agent 이벤트는 replayable 아님 — disconnect 윈도우 동안 emit된 delta는 사라짐. 재연결 성공할 때마다 store가 `chat.history`를 호출해서 응답으로 transcript 재구축 (gateway가 `[[reply_to_*]]`, tool-call XML, `NO_REPLY`, oversized 엔트리, reasoning 콘텐츠를 visible history에서 이미 strip함). 같은 핸들러가 daily 4 AM gateway session reset도 커버.

**방어적 `NO_REPLY` 필터.** Gateway가 `chat.history`에서 `NO_REPLY` / `no_reply`를 strip하지만, agent stream은 run 도중에 silent assistant turn을 여전히 emit할 수 있음. Store가 렌더러 도달 전에 클라 측에서 필터.

### 4.6 Stop / abort

Run이 in flight인 동안 (open `lifecycle.start`에 매칭되는 `end` / `error`가 없는 상태), composer의 send affordance가 stop으로 flip. 누르면 새 `OpenClawGatewayClient.AbortChatAsync(runId)` 래퍼 호출 → gateway `chat.abort` RPC. Gateway는 생성된 partial assistant 텍스트를 persist; store는 "aborted" 인디케이터 붙여 렌더하고 그 버블에 더 이상 누적 안 함.

### 4.7 테스트

- `ChatTranscriptStore` 유닛 테스트: 스트리밍 delta coalesce, tool start/result 전이, error 이벤트, multi-tool interleave, 스트리밍 중 user send, **스트리밍 중 abort**, **`NO_REPLY` 필터**, **reasoning vs assistant 라우팅**, **재연결 후 rehydrate** (faked `chat.history` payload가 in-memory state를 대체).
- Surface-selection seam 테스트: flag-on이 native 선택, flag-off가 WebView2 선택, env override가 setting을 이김.
- 전송 실패 경로 테스트: optimistic user 버블이 send timeout / transport failure 시 errored로 flip.
- Tray 테스트는 `AGENTS.md`의 `SettingsManager` 격리 준수.

### 4.8 Voice / speech (M5, M1 아님)

@regis 코멘트: "I will also want to integrate the speech conversations that Ranjesh's latest demo showcased into that native chat UI, once the basics are in place." 잊어먹지 않게 여기 캡처 — 텍스트 채팅 M1은 집중 유지, voice는 M1–M4가 안정된 후 M5로. 현 WebView2 채팅에는 realtime-voice 버튼이 노출되어 있는데, 네이티브 등가물은 기존 gateway-served provider가 아니라 Ranjesh 데모가 쓰는 프로토콜 기준으로 설계할 예정. M5 스코프 (진입점, 마이크 권한 UX, 텍스트 turn과의 transcript interleaving) 는 데모 lineage 리뷰 전까지 의도적으로 open.

### 4.9 Component Library 등록

`Windows/ComponentLibraryWindow.xaml`에는 이미 "Chat" / "Agent run card" 섹션이 있음. M1에서 새로 만드는 네이티브 조각은 전부 여기에도 등록 — 라이브 gateway 없이도 갤러리에서 시연/회귀 확인 가능하게.

대상:
- `UserMessageBubble`
- `AssistantMessageBubble` (streaming 중 / finalized / errored 세 상태)
- `AgentEventCard` (`Running` / `Done` / `Error` phase chip 각각)
- `SystemNoticeRow` (reconnect / pairing / auth-failed / aborted 변형)
- Collapsible "Thinking" 블록 (collapsed / expanded)
- Composer stop affordance (idle / streaming → stop)
- `NativeChatThread` 샘플 (fake transcript 데이터로 채움)

`NavigationViewItem` 추가 + 기존 Chat / Agent run card 페이지 패턴 그대로 따라가기. Mock 데이터는 deterministic — 갤러리가 라이브 gateway에 의존 안 하게.

### 4.10 AGENTS.md validation

`./build.ps1`, `dotnet test ./tests/OpenClaw.Shared.Tests/...`, `dotnet test ./tests/OpenClaw.Tray.Tests/...`. 추가로 manual side-by-side: 같은 gateway, 같은 session — flag off (WebView2)와 flag on (native) — 동일 prompt가 동일 assistant 텍스트 + 동일 tool 카드 set 생성하는지 확인 (렌더링은 다를 수 있어도 콘텐츠는 일치해야 함).

## 5. Out of scope

- **M1–M4의** voice / talk surface. 자체 milestone (M5)이 있음 — §4.8.
- Hub 레벨 navigation (left main nav, 채팅 surface 외부의 conversation list). `ChatPage` 위쪽이고 영향 없음.
- `OpenClawGatewayClient` wire 프로토콜 변경 (새 RPC 메서드나 이벤트). 갭 발견 시 문서화 + re-plan.
- M1–M4 진행 동안 `ChatSurface.xaml`, `ChatSurface.xaml.cs`, `GatewayChatHelper.cs`, `GatewayChatUrlBuilder.cs` 편집. Fallback 경로는 M6 cutover까지 frozen.
- `AgentRunCard` 컨트롤. 별개 개념 (Hub home / run card), 채팅 thread 아이템 아님.

## 6. Risk와 mitigation

- **Tool 카드 parity drift.** 웹 채팅은 풍부한 tool 카드를 렌더, M1은 단순하게 ship. Mitigation: flag toggle하면서 같은 라이브 session에 side-by-side 체크, 사용자가 현재 볼 수 있는 이벤트가 native 측에서 누락 안 되는지 확인.
- **스트리밍 delta 라우팅.** 잘못 라우트된 delta가 assistant turn을 여러 버블로 split할 수 있음. Mitigation: store에 `ActiveAssistantId` 커서 하나 + fake event sequence 유닛 테스트.
- **Markdown 서프라이즈.** 핸드롤 렌더링은 의존성 적게 유지하지만 미묘한 파서 버그 위험. Mitigation: 엄격한 지원 subset (paragraph, fenced + inline code, link), raw HTML 금지, 집중된 유닛 테스트 corpus. M3에서 Markdig 재평가.
- **드롭다운 wire shape.** Channel / model / reasoning이 `chat.send`로 정확히 흘러야 함. Mitigation: UI 바인딩 전에 gateway에 대해 payload shape 확인. Undocumented면 M1 작업의 일부로 문서화.
- **SettingsManager 테스트 오염.** 테스트의 실제 `%APPDATA%` 쓰기 금지. Mitigation: 모든 테스트가 `OPENCLAW_TRAY_DATA_DIR` 또는 temp dir 사용.
- **Cutover 후회.** WebView2 경로 제거는 one-way. Mitigation: M6는 native 기본값으로 한 cycle 안정 dogfood 후에만 ship, 제거는 두 PR로 staged (default flip → code 제거).

## 7. 미해결 질문 (리뷰어용 → 본인용)

1. **드롭다운 wire 경로.** `chat.send`에 native param vs slash-command shim (`/model …`, `/reasoning on|off`) — §4.3. Slash 경로는 작동 확인됨, native-param shape은 아직 문서화 안 됨. 누가 native param을 강하게 push하지 않는 한 M1은 slash-shim 추천.
2. **Markdown subset.** M1은 paragraph / fenced / inline / link만. Bold/italic과 bullet/numbered list는 추가 cheap하고 LLM 출력에 흔함 — day one부터 M1에 넣을지? (§4.1 Example B에 list 빠진 모습 있음.)
3. **Welcome 상태.** M1에 quick-reply chip 없음. Empty-state 타이틀 + status dot만으로 첫 사용 usable한지, 아니면 chip strip 정도는 M1에 끌어올릴지?
4. **Cutover 트리거.** M6 전에 어떤 신호를 원하는지 — 정해진 dogfood window, @scott / @regis / @peter 각각의 thumbs-up, 메트릭, 또는 다른?
5. **M5 voice 스코프 (@regis).** 어느 Ranjesh 데모를 reference로? Speech 경로가 기존 gateway voice provider, 새 provider, 아니면 model-side realtime API 직결 중 뭔지? Voice turn이 텍스트 transcript를 공유할지 parallel timeline에 살지?

## 8. Reference — gateway 채팅 프로토콜

`docs/openclaw-chat-interface.md`가 gateway WebSocket 프로토콜의 권위 있는 deep-dive (frame type, handshake, `chat.*` RPC, agent run lifecycle, streaming/chunking, session RPC, edge case). 이 플랜이 `chat.history`, `chat.send`, `chat.abort`, `agent` 이벤트, `lifecycle` phase, `isReasoning`, `NO_REPLY`, idempotency key, daily session reset을 언급할 때 그 문서가 source. M1 작업 시작 전에 한 번 읽기.

후속 milestone에 걸치는 hook들:

- **M3 / Mermaid:** 프로토콜 spec이 fenced ```mermaid``` 블록을 별도 렌더 카테고리로 명시. Code-block copy + highlighting과 같이 묶기 좋음.
- **M4 / sessions.\* RPC:** `sessions.list`, `sessions.subscribe`, `sessions.messages.subscribe`, `sessions.preview`, `sessions.steer`, `sessions.patch`, `sessions.compact`이 conversation list rail + history rehydrate 필요한 것 다 커버. 새 wire 작업 없음.
- **Block-streamed chunk:** Telegram/Discord/Slack 채널이 logically-one assistant turn을 multiple coalesced 메시지로 emit. Operator role (우리)은 raw delta 받음 → M1 coalesce 로직은 이걸 처리할 필요 없음. 하지만 미래 milestone이 그런 채널 노출하면 store에 "logical message id" join 필요.

## 9. 리뷰어 피드백 로그

- **@regis (2026-05-05):** Ranjesh 최근 데모의 voice / speech conversation을 basic 들어선 후 native 채팅 UI에 통합해줘. → 위 **M5**로 캡처; voice가 완전 out-of-scope 아니라 M1–M4 뒤로 sequence됨. 프로토콜 스코프 확정 위해 Open Question #5 추가.
- **@scott:** _pending_
- **@peter:** _pending_
