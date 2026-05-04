# OpenClaw Tray UI 디자인 인벤토리

목적: Windows tray app의 현재 UI surface를 디자인 관점에서 분석하기 위한 문서. 구현 스펙이 아니라, 현재 화면 구조 / 진입점 / 상태 / 디자인 기회 영역을 정리한 자료다.

## 요약

현재 tray app은 하나의 통합 앱이라기보다, Windows tray에서 여러 utility window를 여는 구조에 가깝다.

핵심 디자인 이슈:

- Status, Activity, Notifications, Diagnostics, Settings, Chat 정보가 여러 창에 흩어져 있음
- Tray menu가 너무 많은 역할을 담당함
- 비슷한 list/card/empty state 패턴이 여러 창에서 반복됨
- 사용자는 “무엇이 메인 화면인지” 헷갈릴 수 있음

추천 방향:

- Tray menu는 가볍게 유지
- 깊은 작업은 통합 Hub / Command Center로 모으기
- Activity / Notification / Diagnostics / Settings를 더 명확한 IA로 재구성

## UI surface 인벤토리

| Surface | 사용자 목적 | 진입점 | 주요 컨트롤 / 액션 | 주요 상태 |
|---|---|---|---|---|
| `TrayMenuWindow` | Tray popup, 메인 launcher | Tray icon click | Status summary, dashboard, web chat, quick send, activity, notification history, health check, updates, settings, setup, autostart, support/debug flyouts, exit | Connected/disconnected, auth failure, node paired/pending/disconnected, activity active/idle, sessions/channels/nodes 있음 |
| `StatusDetailWindow` / Command Center | 운영 상태와 진단 허브 | Tray status, Settings, deep link | Gateway topology, overview, support/debug actions, diagnostics, port diagnostics, permissions, usage, sessions, channels, nodes, recent activity, refresh | Warning 있음/없음, sessions/nodes/activity 없음, tunnel info 표시/숨김, channel actionable/non-actionable |
| `SettingsWindow` | 앱 설정 / 연결 설정 | Tray settings, deep link | Gateway preset, SSH tunnel fields, gateway URL/token/test, autostart, global hotkey, notification filters, node capabilities, TTS, MCP server, save/cancel | SSH collapsed/visible, MCP disabled/pending/listening/error, token hidden/revealed, test connection progress/error |
| `WebChatWindow` | In-app gateway chat / Control UI host | Tray web chat, toast action, deep link | WebView2, toolbar home/refresh/open browser/devtools, loading ring, error panel | Loading, success, connection error, cert error, invalid URL, insecure remote URL blocked, navigation timeout |
| `ActivityStreamWindow` | Activity log / event stream | Tray activity, Command Center, toast tip, deep link | Category filter, list item open, open dashboard, copy support bundle, clear, close | Empty/list, filtered state, item with/without dashboard path |
| `NotificationHistoryWindow` | Toast notification history | Tray history, deep link | Notification list, clear all, close, item click URL | Empty/list, notification with category/action URL |
| `QuickSendDialog` | 빠른 메시지 전송 | Tray quick send, global hotkey, deep link | Text entry, send/cancel, Enter/Esc, error detail | Idle, sending, sent, disconnected, pairing required, missing scope, generic error |
| `CanvasWindow` | Agent가 띄우는 WebView canvas | Node capability: `canvas.present` | WebView content, loading, error, retry, snapshot/eval APIs | Loading, content loaded, navigation error, blocked URL, sanitized HTML |
| `A2UICanvasWindow` | Agent가 띄우는 native A2UI canvas | Node capability: A2UI push/reset | Empty placeholder, single surface host, multi-surface `TabView` | Empty, single surface, multi-surface tabs, reset, surface add/remove |
| `OnboardingWindow` | First-run / setup flow | First launch, setup menu, auth failure nudge, deep link | Wizard/setup flow | Active, completed, reconnect/reinitialize |

## 현재 Navigation / IA 모델

현재 구조:

1. Windows tray icon이 root.
2. Tray menu가 launcher이면서 status surface 역할도 함.
3. 더 깊은 기능은 별도 window로 열림.
4. 일부 flow는 tray를 거치지 않고 deep link, toast, gateway/node command로 바로 열림.

주요 경로:

| Path | Flow |
|---|---|
| Tray -> Dashboard | 외부 browser dashboard 열기 |
| Tray -> Web Chat | native `WebChatWindow`에서 WebView2 embedded UI 열기 |
| Tray -> Quick Send | lightweight message dialog 열기 |
| Tray -> Activity Stream | activity log window 열기 |
| Tray -> Notification History | notification history window 열기 |
| Tray -> Status / Command Center | diagnostic / health window 열기 |
| Tray -> Settings | configuration window 열기 |
| Gateway/node -> Canvas | normal tray navigation 밖에서 canvas window 열기 |
| Gateway/node -> A2UI | normal tray navigation 밖에서 native generated UI window 열기 |

디자인 관점:

- Tray menu가 너무 많은 책임을 가짐
- Tray menu가 home, status, launcher, diagnostics shortcut, settings shortcut, support menu 역할을 동시에 함
- 짧게 열렸다 닫히는 popup에 너무 많은 정보와 액션이 들어감

## 디자인 관찰

### 1. Fragmentation

상태와 진단 정보가 여러 곳에 분산되어 있음:

- Status: tray menu, Command Center, Settings connection test, Activity Stream
- Activity: tray summary, Activity Stream, Command Center recent activity, notification tip
- Notifications: Windows toast, Notification History, Activity Stream
- Support/debug actions: tray flyout, Command Center

결과:

- 사용자는 “어디서 봐야 하는지” 기억해야 함
- 같은 종류의 정보가 여러 UI에 반복됨
- 디자인 system보다 기능별 window가 먼저 생긴 느낌

### 2. Duplicated patterns

반복되는 UI 패턴:

- `ActivityStreamWindow`와 `NotificationHistoryWindow`의 list card 구조가 거의 동일
- Settings와 Command Center 둘 다 stacked section + card block 패턴 사용
- Empty state, footer buttons, section headers, support actions가 surface마다 따로 구현됨

디자인 기회:

- 공통 component화 가능
- List card / Section header / Empty state / Diagnostic row / Footer command bar를 하나의 pattern으로 정리 가능

### 3. Hierarchy / density

Tray menu:

- 너무 많은 기능이 한 popup에 들어감
- status, sessions, channels, nodes, debug, settings, exit이 같은 공간에 있음
- 매일 쓰는 액션과 advanced/debug 액션의 hierarchy가 약함

Command Center:

- 기능은 강력하지만 세로로 길고 dense함
- section header가 많아서 scanning cost 높음
- diagnostics, permissions, usage, sessions, channels, nodes가 한 화면에 길게 이어짐

Settings:

- beginner setup과 advanced/developer 설정이 섞여 있음
- connection, notification, node capability, TTS, MCP, token security가 같은 흐름 안에 있음

### 4. Discoverability

잠재 이슈:

- Canvas / A2UI window는 agent capability로 열리기 때문에 navigation에서 잘 보이지 않음
- 사용자는 갑자기 뜬 window가 왜 나타났는지 모를 수 있음
- Recent activity flyout은 hover 중심이면 keyboard/touch discoverability가 약할 수 있음
- Dashboard와 Web Chat이 둘 다 main destination처럼 보여 사용자가 헷갈릴 수 있음
- WebChat toolbar에 DevTools가 항상 보이는 것은 일반 사용자에게는 noise일 수 있음

### 5. Safety / security messaging

좋은 점:

- Support/debug copy action에서 token, screenshot, recording, camera data, payload가 포함되지 않는다고 명시함
- MCP token reveal/copy/reset flow가 명확함
- Quick Send는 pairing/scope 문제에 대한 remediation state가 있음

리스크:

- Settings token input이 plain text field처럼 보임. Password-style control 검토 필요
- Dashboard/chat token 전달 방식은 하나의 안전한 pattern으로 통일 필요
- 가능한 경우 query token보다 URL fragment가 더 안전함
- destructive / advanced action이 일반 launcher action과 가까이 있음

### 6. Accessibility / theming notes

WinUXE 기준으로 추가 점검 필요:

- App-level High Contrast handling 확인 필요
- Custom color/status brush는 Light/Dark/HighContrast variant 필요
- Hardcoded `FontSize`, `FontWeight`, fixed dimensions, ad-hoc status color 줄이기
- Emoji icon은 decorative인지 semantic인지 구분 필요
- Fixed-size window/button은 localization 또는 text scaling에서 clipping 가능

## 추천 디자인 방향

### 1. Tray menu를 가볍게 만들기

Tray menu에 남길 것:

- 현재 connection status
- Open Hub
- Quick Send
- Chat
- urgent alert 1개 정도
- Settings
- Exit

Tray menu에서 빼도 되는 것:

- 긴 diagnostics
- support/debug flyout
- session/channel/node management
- detailed activity
- update detail

목표:

- Tray menu = 빠른 launcher + 아주 짧은 status
- Hub = 자세한 관리/진단/설정

### 2. Unified Hub / Command Center

하나의 primary app window로 통합:

- Overview
- Chat / Dashboard
- Activity
- Notifications
- Sessions
- Nodes
- Channels
- Settings
- Support / Diagnostics

효과:

- window sprawl 감소
- 사용자 mental model 단순화
- design system 패턴 재사용 쉬움
- advanced 기능을 더 안전하게 그룹화 가능

### 3. Activity / Notification 통합

Activity Stream을 canonical event timeline으로 만들기.

가능한 tab/filter:

- All
- Notifications
- Sessions
- Nodes
- Gateway
- Errors / Warnings

Notification History는 별도 window보다 Activity의 filtered view나 tab으로 흡수 가능.

### 4. Settings 재구성

추천 grouping:

| Group | Contents |
|---|---|
| Connection | Gateway URL, topology preset, SSH tunnel, test connection |
| Startup | Autostart, global hotkey |
| Notifications | Notification enablement, sound, categories |
| Security & permissions | Token, MCP token, node capabilities, screen/camera/location |
| Developer | MCP server, TTS provider, debug tools |

핵심:

- 일반 사용자 설정과 developer/advanced 설정 분리
- token / MCP / node capability는 security context 안에서 보여주기

### 5. Chat / Dashboard 역할 정리

현재는 Chat과 Dashboard가 둘 다 main destination처럼 보임.

정해야 할 것:

- In-app Chat이 primary인가?
- Browser Dashboard가 primary인가?
- 둘 다 필요하다면 이름과 역할을 명확히 나눌 것

예:

- `Chat` = 대화 중심
- `Open web dashboard` = browser에서 전체 control UI 열기
- `Embedded dashboard` = app 안에서 control UI 보기

### 6. Design system cleanup

공통 pattern 후보:

- Section header
- Status card
- Empty state
- List card
- Diagnostic warning row
- Footer command bar
- Sensitive token row
- Capability toggle group
- Support action group

우선순위:

1. Tray menu simplification
2. Hub IA
3. Activity/Notification merge
4. Settings regrouping
5. Accessibility/theming pass

## Open design questions

- Tray app은 full Hub app이 되어야 하나, lightweight utility launcher로 남아야 하나?
- Chat과 Dashboard 중 무엇이 primary destination인가?
- Activity와 Notification History를 합칠 것인가?
- Tray menu에 남길 액션과 Hub로 옮길 액션의 기준은 무엇인가?
- Agent/system-only capability를 사용자에게 얼마나 노출해야 하나?
- Token, permission, node capability 같은 민감 정보를 어떻게 안전하지만 불안하지 않게 보여줄 것인가?
- Debug/devtools는 일반 사용자 UI에서 숨겨야 하나?

## Source files

- `src\OpenClaw.Tray.WinUI\App.xaml`
- `src\OpenClaw.Tray.WinUI\App.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\TrayMenuWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\TrayMenuWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\StatusDetailWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\StatusDetailWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\SettingsWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\SettingsWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\WebChatWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\WebChatWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\ActivityStreamWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\ActivityStreamWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\NotificationHistoryWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\NotificationHistoryWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\CanvasWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\CanvasWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Windows\A2UICanvasWindow.xaml`
- `src\OpenClaw.Tray.WinUI\Windows\A2UICanvasWindow.xaml.cs`
- `src\OpenClaw.Tray.WinUI\Dialogs\QuickSendDialog.cs`
- `src\OpenClaw.Tray.WinUI\Services\NodeService.cs`

