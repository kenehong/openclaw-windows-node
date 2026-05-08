# Post-Onboarding Flow (POC)

온보딩 위저드가 끝난 뒤 이어지는 다단계 에이전트 셋업 + 채팅 zero-state POC입니다. 현재 `post-onboarding` 브랜치에서 개발 중이며, 팀 리뷰용으로 별도 모드(`--post-onboarding`)로 단독 실행할 수 있습니다.

## 개요

페이지 구성 (`src/OpenClaw.Tray.WinUI/PostOnboarding/Pages/`):

1. **UserNamePage** — 사용자 이름 입력
2. **AgentPickPage** — 프리베이크된 에이전트 카드 선택
3. **AgentDefinePage** — 에이전트 이름/아바타/성격 정의
4. **AgentChatPage** — 첫 채팅 zero-state (제안 프롬프트 칩 포함)

채팅 백엔드는 기본적으로 `MockAgentChatBackend`를 사용하므로 게이트웨이 연결 없이도 흐름을 볼 수 있습니다.

## 실행 방법 (팀원용)

1. 브랜치 받기
   ```powershell
   git fetch origin
   git checkout post-onboarding
   git pull
   ```

2. 빌드 (리포 루트에서)
   ```powershell
   .\build.ps1
   ```

3. Post-Onboarding 모드로 실행
   ```powershell
   .\src\OpenClaw.Tray.WinUI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\OpenClaw.Tray.WinUI.exe --post-onboarding
   ```

> `--post-onboarding` 플래그가 핵심입니다. 플래그 없이 실행하면 일반 트레이 앱이 뜹니다. 일반 트레이 앱과는 별도 mutex(`OpenClawTray-PostOnboarding`)를 쓰기 때문에 동시에 띄울 수 있습니다.

## 피드백

브랜치: https://github.com/kenehong/openclaw-windows-node/tree/post-onboarding

피드백이나 버그는 위 브랜치에 PR/이슈로 남겨주세요.
