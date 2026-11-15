# Pyrite9 Sleeve Roll

버전 0.1.3 (2026-11-15)

VRChat 아바타용 소매 걷기 기믹입니다. 프리팹을 아바타에 넣고 소매 달린 옷을 고르기만 하면, 빌드할 때 그 옷의 메시로 "걷힌 소매" 모양을 계산해 넣습니다.
Expression Menu 토글을 켜면 양 소매가 걷혀 올라가고, 남에게도 그대로 보입니다. 이 패키지에는 셰이프키도 메시 데이터도 들어 있지 않습니다. 모양은 빌드할 때 내가 가진 옷으로 만들어집니다.

English: `README_en.md` / 日本語: `README_jp.md` / 이용 규약: `LICENSE.txt`

## 기능

- Expression Menu 토글로 양 소매가 0.2초 만에 걷혀 올라간다 — 남에게도 보인다(동기화 1 bit). 토글은 루트나 원하는 하위 메뉴에 넣을 수 있고, 상태 저장도 고를 수 있다
- 걷힌 모양은 빌드 때 내가 가진 옷으로 계산한다 — 피부를 재서 천이 팔에 박히지 않게, 원래 겹 두께와 헐렁함 일부를 남기고, 걷힌 자리에 주름을 넣는다
- 옷이 소매 밑 팔을 shrink 해 두었으면 걷는 동안 shrink를 해제한다(빌드 때 0이고 옷 토글이 켜는 shrink도) — 걷힌 소맷단 안을 들여다봐도 가는 팔이 안 보인다
- 손목을 꺾거나 비틀어도 걷힌 소맷단은 제자리(손목 보조 본)
- 소매에 PhysBone이 달린 옷(모에 소매)은 평소엔 그대로 흔들리고, 걷혀 있는 동안만 움직이지 않는다
- 겹쳐 입은 옷(재킷 밑 셔츠 등)은 여러 개 고르면 안쪽부터 걷고, 바깥 소매를 안쪽의 걷힌 소맷단 위에 얹는다
- 씬도 옷 메시 파일도 바뀌지 않는다 — 바뀌는 건 빌드용 사본뿐
- 인스펙터에서 옷 후보를 버튼으로 고르고, 소맷단 위치·간격·주름을 게이지로 조절하고, Scene 뷰에서 소맷단이 올 자리를 금색 고리로 바로 본다
- 걷은 소매를 빌드 없이 Scene 뷰에서 본다(**걷는 위치**의 **미리 보기**) — 계산은 빌드와 같다. 단 빌드 때 입혀지는 옷(Modular Avatar 등)의 PhysBone 소매가 걷힐지·물리를 유지할지는 빌드에서 정해지므로 **마지막 빌드**와 빌드 경고로 확인

## 필요한 것

- Unity 2022.3(VRChat)
- VRChat SDK – Avatars 3.10 이상
- Modular Avatar
- NDMF(Non-Destructive Modular Framework)

확인한 환경: Unity 2022.3.22f1, VRChat SDK Avatars 3.10.5, Modular Avatar 1.18.7, NDMF 1.14.8.
이보다 오래된 버전과 프리뷰 버전(alpha·beta)은 확인하지 않았습니다.

- 휴머노이드 아바타만(팔꿈치·손목 위치를 Humanoid 본으로 찾습니다).
- 시험한 옷: Salt 기본 후드, Kipfel 셔츠, Milfy 카디건, Kaguya 카디건과 세일러 셔츠 겹쳐 입기. **PC에서 확인했습니다.** Android / Quest 판은 확인하지 않았습니다. 겹쳐 입기는 이 한 조합으로 확인했습니다.
- PhysBone 소매: 손 본 밑에 PhysBone이 달린 옷 한 벌(Kaguya 카디건)로 Unity Play와 VRChat 안에서 확인했습니다(다른 사람 화면, 그 사람의 인스턴스 재입장 포함).

## 설치

1. **필요한 패키지를 넣습니다.** VCC / ALCOM에서 아바타 프로젝트를 "관리"(Manage Project)로 열고 **Modular Avatar**를 추가합니다.
   NDMF는 Modular Avatar와 함께 자동으로 들어갑니다. 안정판(프리뷰가 아닌 버전)을 쓰세요.
2. **이 패키지를 임포트합니다.** `.unitypackage`를 더블클릭(또는 Unity의 `Assets > Import Package > Custom Package…`)하고, 모두 체크한 채 **Import**를 누릅니다. 파일은 `Assets/Pyrite9/SleeveRoll/`에 들어갑니다.

## 사용법

1. Project 창에서 `Assets/Pyrite9/SleeveRoll/`를 열고 `Pyrite9 Sleeve Roll.prefab`을 Hierarchy의 아바타(VRC Avatar Descriptor가 붙은 오브젝트)에 끌어다 놓습니다. 아바타 바로 아래에 두세요. **아바타 밖에 있으면 아무 일도 일어나지 않습니다.**
   - 프리팹을 Unpack하지 마세요(업데이트가 반영되지 않습니다).
2. 인스펙터 첫 구역(기본)에서:
   - **Menu language**(`EN` / `日本語` / `한국어`)에서 **한국어**를 고르면 인게임 메뉴 이름과 인스펙터가 한국어로 바뀝니다(기본값 EN).
   - **옷**: 이 아바타의 소매 달린 옷이 버튼으로 나옵니다 — 걷을 옷을 누르세요(다시 누르면 빠짐). 겹쳐 입은 옷은 여러 개 누릅니다. 버튼에 없으면 버튼 아래 **목록에 없는 옷** 칸에 옷의 Skinned Mesh Renderer를 끌어다 놓습니다. 몸 메시가 아니라 **옷** 메시를 고르세요.
   - **소매**: 양쪽(기본) / 왼쪽 / 오른쪽.
3. (선택) **걷는 위치**·**걷힌 소매 모양** 구역에서 소맷단 위치·피부와 간격·소매 굵기·주름을 맞춥니다. Scene 뷰의 금색 고리가 소맷단이 올 자리입니다. **미리 보기**를 **ON**으로 두면 걷은 소매를 보면서 맞출 수 있습니다. 씬과 업로드에는 영향이 없습니다. 게이지를 더블클릭하면 기본값으로 돌아갑니다.
4. 시험해 봅니다:
   - Unity에서 **Play**를 누르면 걷힌 소매가 빌드되고, 인스펙터의 **마지막 빌드**가 채워집니다(Play를 멈춘 뒤 표시). Play 중에는 Gesture Manager 등으로 Expression Menu의 토글을 켜 보세요.
   - `VRChat SDK > Show Control Panel > Builder` → **Build & Test** 뒤 VRChat에서 테스트 아바타로 갈아입습니다. Build & Test는 내 화면만 보여 줍니다 — 남에게 어떻게 보이는지는 (Private으로) 업로드한 뒤에 확인할 수 있습니다.
5. 결과를 확인합니다. 정상이면 **마지막 빌드**에 옷 이름과 쪽마다 걷힌 자리(예: `0.30까지`), **경고: 없음**이 나옵니다. 겹쳐 입은 옷은 안쪽부터 나오고, 바깥 옷에 `○○ 위에`가 붙습니다.
   - 경고는 Console에도 `[Pyrite9 Sleeve Roll]`로 시작하는 노란 줄로 나옵니다 — "문제 해결"을 보세요. 자세한 내용은 프로젝트 폴더의 `Logs/Pyrite9_SleeveRoll_build.txt`에 적힙니다.

겹쳐 입은 옷(셔츠 위 재킷 등)은 한 Sleeve Roll에서 둘 다 고르세요. 안팎은 빌드가 팔뚝 둘레로 정합니다(가는 쪽이 안쪽). 옷마다 설정을 다르게 하고 싶으면 Sleeve Roll을 하나 더 넣어 나눠 골라도 됩니다 — 그래도 겹침은 같이 맞춰지고, 토글 하나로 같이 걷힙니다. 둘 다 골랐는데 안쪽 옷이 `is thinner than the skin`으로 실패하면(안쪽 옷을 바깥 옷에 맞게 셰이프키로 shrink해 입힌 블레이저 등) 바깥 옷만 고르세요 — "문제 해결" 참고.

## Expression Menu

| 메뉴 | 동작 |
|---|---|
| `소매 걷기` | 켜면 양 소매가 걷히고, 끄면 내려옵니다(남에게도 보임, 기본 꺼짐, 기본 설정에선 저장 안 함) |

메뉴 이름은 메뉴 언어에 따라 `Sleeve Roll` / `袖まくり` / `소매 걷기`이고, 인스펙터 **메뉴 이름**으로 바꿀 수 있습니다.
- **설치 위치**: 토글을 넣을 메뉴. 비우면 루트(기본). 바로 밑 **아바타 메뉴**에 이 아바타의 Expression Menu가 트리로 나오니, 넣을 메뉴를 누르면 됩니다(메뉴 이름을 몰라도 됩니다). 오른쪽 숫자는 그 메뉴의 항목 수이고, 8/8인 메뉴에 넣으면 Modular Avatar가 끝의 'More'로 옮깁니다. 메뉴 에셋을 칸에 끌어다 놓아도 됩니다. 아바타 메뉴에 없는 메뉴를 고르면 토글이 안 들어갑니다 — 인스펙터와 **마지막 빌드**에 경고가 나옵니다.
- **저장**: 켜면 재입장이나 아바타를 바꾼 뒤에도 토글 상태(걷음·내림)를 유지합니다. 기본 꺼짐(매번 내린 상태로 시작).
- **마지막 빌드**에 토글이 실제로 들어간 자리(예: `루트 / Kemono`)와 저장 여부가 나옵니다.

**메뉴**를 끄면 메뉴를 넣지 않습니다. 그래도 동기화 Bool 파라미터 `Pyrite9/SleeveRoll/Hold`는 만들어지므로, 직접 만든 메뉴나 Contact에서 이 이름으로 켜고 끌 수 있습니다(**저장**은 메뉴를 꺼도 적용됩니다).

### 메뉴를 직접 만들 때

대부분은 **설치 위치**와 **저장**으로 충분합니다. 그래도 토글을 직접 만든다면:

- 파라미터 `Pyrite9/SleeveRoll/Hold`는 **빌드 때 만들어집니다**. 그래서 빌드 전 아바타의 Expression Parameters에는 없습니다. VRChat SDK Expressions Menu의 파라미터 칸(드롭다운)은 Expression Parameters 에셋에 있는 이름만 보여 주므로, 검색해도 안 나옵니다.
- **VRChat 표준 방법:** 아바타 Expression Parameters에 `Pyrite9/SleeveRoll/Hold`를 직접 추가합니다 — Type **Bool**, Default 0, **Saved**(저장하려면), **Synced** 켬. 그다음 메뉴에 Toggle을 만들어 Parameter로 이 이름을 고르고 **Value를 1**로 합니다. 빌드 때 Sleeve Roll의 선언과 하나로 합쳐지므로 동기화 비트는 늘지 않습니다(1 bit 그대로).
- **Modular Avatar 방법:** MA Menu Item(Type Toggle)의 파라미터 칸에 `Pyrite9/SleeveRoll/Hold`를 직접 입력하고 Value를 1로 합니다. 저장하려면 같은 오브젝트에 **MA Parameters**를 붙이고 같은 이름을 **Bool**·동기화·**저장(Saved)**으로 선언합니다.
- **MA Menu Item의 '저장(Saved)'만 켜면 저장되지 않습니다.** 저장은 파라미터 선언(MA Parameters, 또는 Expression Parameters의 Saved)이 정합니다.
- 선언 중 하나라도 저장이면 저장됩니다. Expression Parameters에 Saved를 켜고 추가해 두었다면 인스펙터 **저장**을 꺼도 저장됩니다.
- **0.1.0에서 토글을 직접 만든 분:** 0.1.1로 업데이트하고 인스펙터 **메뉴**를 켠 채로 쓴다면, 직접 만든 토글은 지워 주세요(같은 토글이 두 개가 됩니다).

## 퍼포먼스

| 항목 | 늘어나는 것 |
|---|---|
| BlendShape | 옷에 2(`SleeveUp_L`·`SleeveUp_R`), 옷 밑 팔을 shrink한 몸 메시에 2(`SleeveRoll_Unhide_L/R` — shrink가 있을 때만). 옷 토글 등 애니메이션이 그 shrink를 켜고 끄는 아바타는(빌드 때 0인 shrink도) 그런 shrink 셰이프마다 2(`SleeveRoll_Unhide_L/R__<셰이프 이름>`) — 고정된 shrink와 섞여 있으면 둘 다 |
| 본 | 2(손목 보조 본 `SleeveRoll_Wrist_L/R`). 옷에 따라 더 늘어남: 유지하는 소매 PhysBone이 있으면 옷이 실린 본과 그 위로 체인 시작 본까지 본마다 1(`SleeveRoll_Shadow_…`), 걷히는 정점에 손가락 웨이트가 있는 옷은 손에서 그 손가락 본까지 본마다 1(`SleeveRoll_Shadow_…`), 소매가 따로 움직이는 본(위팔 밑 PhysBone 소매 등)에도 웨이트된 옷은 그 본마다 1(`SleeveRoll_Follow_…`) |
| Constraint | VRC Rotation Constraint 2(손목 보조 본), `SleeveRoll_Shadow_…` 본마다 VRC Rotation Constraint 1, `SleeveRoll_Follow_…` 본마다 VRC Parent Constraint 1 |
| FX 레이어 | 1, 애니메이션이 켜고 끄는 shrink가 있을 때만 +1(`Sleeve Roll Unhide`) |
| 동기화 파라미터 | 1 bit(아바타의 Sleeve Roll 전부 공통) |
| 메뉴 아이콘 | 텍스처 1장, 256×256(메뉴 토글을 쓸 때만) |

메시·머티리얼은 늘지 않습니다. 텍스처도 메뉴 아이콘 말고는 늘지 않습니다. 소매 PhysBone의 개수도 늘지 않습니다(있던 것이 그대로 돕니다).

애니메이션이 켜고 끄는 shrink가 있을 때만(빌드 때 0인 것도) 동기화되지 않는 Animator 파라미터가 늘어납니다 — 그런 shrink 셰이프마다 1(`Pyrite9/SleeveRoll/Live/…`)과 2(`Pyrite9/SleeveRoll/One`·`Pyrite9/SleeveRoll/Rolled`). Expression Parameters에는 들어가지 않으므로 동기화 비트는 그대로입니다.

## 설정(Sleeve Roll 컴포넌트)

- **기본**: Menu Language · Garments(옷, 후보 버튼 — 여러 개 가능, 목록에 없는 옷 칸) · Sleeves(양쪽 / 왼쪽 / 오른쪽).
- **메뉴**: Install Menu(+ Menu Name · Install To) · Saved. 아바타에 Sleeve Roll이 여럿이면 옷을 고른 첫 Sleeve Roll의 값만 씁니다.
- **걷는 위치**(0 = 손목 · 1 = 팔꿈치, 클수록 많이 걷힘): Roll To(소맷단 위치, 기본 0.30. 게이지 노란 구간(0.45~)·주황 구간(0.60~)은 어색해질 수 있으니 **미리 보기**로 확인) · Unhide When Rolled(걷는 동안 shrink 해제, 끄면 shrink를 유지하고 shrink된 팔이 보이기 전에 멈춤(멈추는 자리는 빌드 때 걸린 shrink로 정함). 끈 경우 인게임에서 소매 안을 꼭 확인).
- **걷힌 소매 모양**(mm, 소매 굵기는 0~1 비율): Margin(피부와 간격) · Looseness(소매 굵기) · Fold Amplitude(주름 깊이) · Fold Length(주름 간격) · Start At(움직임 시작, 0 = 손목 · 1 = 팔꿈치, 이보다 팔꿈치 쪽은 안 움직임. 기본 0.75. 세부 조절용: 모양이 달라지니 **미리 보기**로 시험. 팔꿈치에 너무 가까우면 소매가 줄어든 것처럼 보이고, 옷에 따라 팔꿈치를 굽힐 때 피부가 뚫릴 수 있으니 인게임에서 확인).
- **고급**: Wrist Proxy(걷힌 소맷단 고정) · Body Meshes(피부를 잴 메시, 비우면 자동 — 꺼진 오브젝트는 뺌).
- **마지막 빌드**: 그 아바타의 마지막 빌드 결과(옷마다 — 안쪽부터, 바깥 옷은 어느 옷 위인지 — 쪽마다 걷힌 자리, shrink 경계에서 멈췄는지, shrink를 해제한 정점 수), 메뉴 자리와 저장 여부, 경고, 빌드 로그·README 버튼.
- **버전**: 하루 한 번 GitHub에 최신 버전을 묻고, 새 버전이 있으면 알려 줍니다(아래 "참고").
- **변경 기록**: 이번 버전의 변경 사항(이 README에서 읽어 옴).

## 문제 해결

Console 경고는 영어로 나옵니다. 인스펙터 **마지막 빌드**에도 같은 영어 문장이 나오고, 그 밑에 할 일이 메뉴 언어로 붙습니다.

| 메시지 또는 증상 | 할 일 |
|---|---|
| `Not a humanoid avatar — Sleeve Roll not built` | 아바타 Rig이 Humanoid인지 확인하세요. 휴머노이드만 지원합니다. |
| `no humanoid forearm or hand bone` | 아바타 Humanoid 설정에 아래팔(Lower Arm)·손(Hand) 본이 들어 있는지 확인하세요. |
| `the forearm has zero length` | 이 옷의 바인드 포즈에서 아래팔(Lower Arm)과 손(Hand) 본이 같은 자리에 있어 걷을 방향을 정할 수 없습니다. 아바타 Humanoid 설정(아래팔과 손이 다른 본인지)을 확인하고, 문제없으면 빌드 로그를 보내 주세요(아래 "문의"). |
| `no garment picked — this Sleeve Roll does nothing` | 인스펙터 **옷**에서 소매 달린 옷을 고르세요. |
| `not inside this avatar — skipped` | 그 옷은 다른 아바타의 것입니다(컴포넌트를 복사함?). 이 아바타의 옷을 고르세요. |
| `no mesh — skipped` | 이 옷의 Skinned Mesh Renderer에 메시가 없습니다(Mesh 칸이 비었거나 에셋이 없음). 메시를 넣거나 인스펙터 **옷**에서 빼세요. |
| `picked in two Sleeve Rolls — the first one's settings are used` | 둘 중 하나에서 빼세요(옷 하나는 Sleeve Roll 하나에만). |
| `the sleeve (… mm) is thinner than the skin (… mm) — the picked mesh looks like a body` | 원인은 둘 중 하나입니다. ① 몸 메시를 골랐습니다 — 옷 메시를 고르세요. ② 바깥 옷(블레이저 등) 밑에 맞게 셰이프키로 shrink한 안쪽 옷을 바깥 옷과 같이 골랐습니다(바깥 옷이 조금밖에 안 걷힐 수 있습니다) — 바깥 옷만 고르세요(아래 "바깥 옷만 걷고 싶음" 행). |
| `the garment is not weighted to the forearm bone` | 이 옷의 소매가 아래팔 본에 묶여 있지 않습니다(반소매 등). 걷을 수 없는 옷입니다. MA Merge Armature로 입힌 옷이면 옷의 아래팔 본이 소체 아래팔에 합쳐지는지(본 이름이 맞는지) 확인하세요. |
| `no sleeve vertices on the forearm` | 팔뚝에 소매가 없는 메시입니다(반소매거나 옷이 아님?). 걷을 수 없으니 빼세요. |
| `the sleeve is already shorter than Roll To` | 소매가 이미 소맷단 위치보다 짧습니다. **소맷단 위치**를 높이세요(팔꿈치 쪽). |
| `the body mesh has forearm skin only from t=… (cut away under the outfit)` | 몸 메시가 옷 밑 팔뚝을 잘라 낸 모델입니다. 팔뚝이 있는 몸 메시로 바꾸면 걷힙니다. 피부가 있는 데까지만 걷고 경고한 경우(`— rolled only to t=…`)도 같은 방법으로 끝까지 걷힙니다. |
| `no skin (body) mesh found on the forearm` | 피부를 잴 몸을 못 찾았습니다. **고급 > Body Meshes**에 몸 메시를 넣으세요. |
| `the rolled shape … — skipped`, `normalizing produced an invalid position — skipped` | 이 소매는 걷은 모양을 제대로 계산하지 못해 그대로 뒀습니다. 아바타 팔 본(아래팔과 손이 떨어져 있는지)을 확인하고, 문제없으면 빌드 로그를 보내 주세요(아래 "문의"). |
| `the garment already has a shape key with this name — skipped` | 옷에 이미 `SleeveUp_L/R`가 있습니다(다른 도구나 구운 사본?). 원래 옷 메시를 쓰세요. |
| `an MA Shape Changer in Delete mode removes vertices at build` | 이 몸 메시는 빌드 때 정점을 지웁니다. 걷힌 자리에 구멍이 보이면 그 Shape Changer를 Set 모드로 바꾸세요. |
| `an animation changes …, which move the sleeve — the roll is made for the values at build time` | 애니메이션(메뉴 토글 등)이 이 옷의 소매를 움직이는 셰이프키를 바꿉니다. 걷은 모양은 빌드 때 값으로 만들어서 값이 바뀌면 안 맞을 수 있습니다. 주로 쓰는 상태의 값으로 씬을 맞춰 두고 빌드하세요. |
| `… the follow shadow follows …, the rolled cuff of … may sit off`(`the wrist proxy follows`·`the finger shadow follows`도 같음) | 두 옷이 같은 본을 서로 다른 바인드 포즈로 쓰고, Sleeve Roll은 먼저 처리한 옷을 따릅니다. 나중 옷의 걷은 소맷단이 조금 어긋날 수 있습니다. 눈에 안 띄면 그대로 둬도 됩니다. |
| `hide shape '…' is animated in the … layer — the arm brought back under a rolled sleeve follows it only in FX` | FX가 아닌 레이어(Gesture 등)의 애니메이션이 이 shrink 셰이프를 바꿉니다. 걷는 동안의 shrink 해제는 FX의 애니메이션만 따라가므로, 그 레이어가 shrink를 풀면 걷은 소매 밑 팔이 부풀 수 있습니다. 그 애니메이션을 FX 레이어로 옮기세요. |
| `'…' (built at 0, the FX sets up to …) has … frames — its share of the restored arm assumes the shape moves in a straight line with its value` | 이 shrink 셰이프에는 중간 프레임이 있습니다. 걷은 소매 밑에서 되돌리는 팔은 0과 FX가 넣는 값에서는 정확하지만, 그 사이 값(토글이 바뀌는 중이나 슬라이더)에서는 조금 어긋날 수 있습니다. 켜고 끄기만 하는 토글이면 그대로 둬도 됩니다. |
| `could not read the animator controllers (…) — the arm an outfit hides is brought back as it was at build, even when the outfit is toggled off` | 애니메이터 컨트롤러를 읽다가 오류가 났습니다(이유는 괄호 안). 빌드는 계속되고 소매도 걷히지만, shrink 해제가 옷 토글을 따라가지 않습니다(0.1.2와 같은 동작 — 걷은 채 옷을 끄면 팔뚝이 부풀 수 있음). 빌드 로그를 보내 주세요(아래 "문의"). |
| `FX: layer 'Sleeve Roll' not found — …` | Sleeve Roll이 만든 FX 레이어('Sleeve Roll')가 빌드된 FX에 없습니다. 다른 빌드 도구가 지웠거나 이름을 바꿨을 수 있습니다. 줄표 뒤가 동작하지 않게 되는 부분이고, 소매 자체가 안 걷힐 수도 있습니다. 빌드 로그를 보내 주세요(아래 "문의"). |
| `FX: no curve for '…' on … found — its share of the restored arm stays on while rolled` | 이 shrink 셰이프를 바꾸는 애니메이션이 빌드 도중에는 있었는데 최종 FX에는 없습니다(다른 빌드 도구가 옮기거나 이름을 바꿈?). 소매는 걷히지만 이 shrink 해제가 그 토글을 따라가지 않습니다. 빌드 로그를 보내 주세요(아래 "문의"). |
| `Sleeve Roll stopped on an internal error — the sleeves may be left half set up` | 이 아바타에서 Sleeve Roll 버그가 났습니다. 소매가 제대로 안 걷힐 수 있습니다. `Logs/Pyrite9_SleeveRoll_build.txt`(EXCEPTION 줄)를 보내 주세요(아래 "문의"). |
| `the menu toggle is not in the built Expression Menu — Install to (…) is not a menu of this avatar` | **설치 위치** 메뉴가 이 아바타의 메뉴가 아닙니다. 비우거나(루트) 아바타 Expression Menu의 하위 메뉴를 고르세요. |
| `N controls in the Expression Menu drive 'Pyrite9/SleeveRoll/Hold' — one is enough` | 메뉴에 같은 소매 스위치가 두 개 이상 있습니다(0.1.0 때 직접 만든 토글이 남아 있는 등). 직접 만든 토글을 지우거나, **메뉴**를 끄고 직접 만든 것만 남기세요. |
| 걷힌 천에 피부가 비침 | **걷힌 소매 모양 > 피부와 간격**을 올리세요. |
| 걷힌 소매가 너무 두껍거나 얇음 | **소매 굵기**를 조절하세요(왼쪽 끝이면 팔에 딱 붙음). |
| 겹쳐 입은 옷의 안쪽 천이 바깥으로 비침 | 두 옷을 모두 골랐는지 확인하세요(하나만 고르면 다른 옷은 걷히지 않고 그대로). 바깥 옷 쪽 **피부와 간격**을 조금 올려도 됩니다. 안쪽 옷이 `is thinner than the skin`으로 실패한 경우는 아래 행을 보세요. |
| 바깥 옷만 걷고 싶음(블레이저 등, 안쪽 옷을 셰이프키로 shrink해 입힘) | **바깥 옷만** 고르세요. 안쪽 옷은 걷힌 소매가 내준 자리까지만 풀립니다. 안쪽 옷의 원래 소매가 바깥 옷보다 두꺼우면 걷힌 자리가 두툼해 보일 수 있습니다(그 옷 본래 부피). 이런 옷은 둘 다 고르면 안쪽 옷이 `is thinner than the skin`으로 실패할 수 있습니다 — 둘 다 골라 문제없이 걷히는 옷은 그대로 두세요. |
| `this sleeve's PhysBone stops moving (…)` | 이 소매의 PhysBone을 안전하게 유지할 수 없어서(이유는 괄호 안) 소매가 물리 없이 손을 따라갑니다. 걷히기는 합니다. 물리를 살리려면 괄호 안 원인을 없애세요. |
| `a PhysBone on the forearm (…) holds this sleeve and cannot be held still while rolled (…)` | 이 쪽 소매는 걷히지 않습니다. 물리를 멈춰 둘 수 없어서입니다. 두 번째 괄호 안 원인을 없애세요. |
| `another PhysBone (…) also moves … — the two fight over it` | 옷의 PhysBone 두 개가 같은 본을 움직입니다(같은 PhysBone이 두 번 달렸거나, 한쪽 범위가 다른 쪽을 덮음). 옷 자체 설정이라 Sleeve Roll이 없어도 같은 흔들림이고, 그대로 둬도 걷힙니다. 고치려면 하나를 지우거나, 바깥쪽 PhysBone의 Ignore Transforms에 안쪽 PhysBone의 시작 본을 넣으세요. |
| `a PhysBone under the upper arm (…) holds this sleeve down to t=… — it cannot be rolled` | 위팔의 PhysBone이 소맷단까지 잡고 있어 걷을 수 없는 옷입니다. |
| 걷기 시작할 때 소매가 한 번 툭 움직임 | 정상입니다. 걷힌 모양은 PhysBone이 쉬는 자세로 계산하므로, 처진 소매가 그 자세로 돌아갑니다. 내리면 다시 흔들립니다. |
| 걷힌 소맷단이 손목을 따라 꺾임 | **고급 > Wrist Proxy**가 켜져 있는지 확인하세요. |
| 소매를 걷은 채 옷을 메뉴로 끄면 팔뚝이 부풂 | 0.1.3에서 고쳤습니다. 그래도 부풀면 그 옷 토글이 Sleeve Roll보다 늦게 FX를 만드는 도구(VRCFury 등)로 만들어진 경우입니다 — 따라가지 못합니다. Modular Avatar(Object Toggle·Shape Changer)나 FX에 직접 넣은 애니메이션은 따라갑니다. |
| 옷을 끄거나 켜는 순간 팔뚝 모양이 한 프레임 어긋남 | 알려진 현상입니다. 걷는 동안의 shrink 해제가 옷 토글을 한 프레임 늦게 따라갑니다. |
| 걷힌 소매 안에 가는 팔이 그대로 보임(옷이 팔을 shrink 하는데 해제가 안 됨) | 먼저 **Unhide When Rolled**가 켜져 있는지 확인하세요. 걷는 동안에는 빌드 때 걸려 있는 shrink와, 씬에서 0이어도 FX 애니메이션(옷 토글 등)이 켜는 팔뚝 shrink를 해제합니다 — 옷을 끈 채 업로드한 아바타, 블렌드 트리로 켜는 shrink도 포함합니다. 그래도 해제되지 않는 것: ① 빌드 때 0이고 손만 숨기는(팔뚝을 움직이지 않는) shrink — 씬에서 그 셰이프를 옷이 켜졌을 때 애니메이션이 넣는 값과 같게 켜 두고 빌드하세요. ② FX가 아닌 레이어(Gesture 등)만 켜는 shrink — 그 애니메이션을 FX 레이어로 옮기세요. ③ **Unhide When Rolled**를 끄면 멈추는 자리를 빌드 때 걸려 있는 shrink로만 정하므로, 빌드 때 0인 shrink가 켜지면 가는 팔이 보일 수 있습니다 — 씬에서 그 셰이프를 켜 두고 빌드하세요. 중간 프레임이 있는 shrink는 조금 어긋날 수도 있습니다(위 `has … frames` 행). |
| 걷힌 소매 밑 팔이 설정보다 굵게 돌아옴(빌드 때 0이고 FX 애니메이션이 팔을 가늘게 하려고 켜는 몸 셰이프) | 알려진 한계입니다. 팔뚝을 가늘게 하는 셰이프와 옷 밑에서 숨기는 셰이프를 구별하지 못해, 걷는 동안 걷힌 자리에서 해제됩니다. 피하려면 **Unhide When Rolled**를 끄세요 — shrink 해제가 모두 빠지고, 빌드 때 걸린 shrink가 있으면 그 앞에서 멈춥니다. |
| 다른 옷을 입고 걷기를 켜면 그 옷 소매를 팔이 뚫고 나옴 | 알려진 한계입니다. 걷는 동안의 팔뚝 shrink 해제는 어느 옷이 그 shrink를 켰는지 구별하지 않습니다(다른 옷이 같은 몸 셰이프를 쓰거나, FX가 켜는 다른 옷 전용 shrink 셰이프). 걷는 옷을 입었을 때만 걷기를 켜세요. |
| 소맷단이 팔 아래로 처진 옷(한쪽으로 흘러내린 소매 등) | 소매 끝이 뚫려 있으면 빌드 때 자동으로 바로잡습니다. 빌드 로그에 `normalized`가 나옵니다. 확인한 옷은 Foxy 계열뿐입니다. 소매 끝이 막혔거나, 소맷단이 떨어져 있거나, 큰 부품(리본 등)이 따로 달렸거나, 윗부분이 트인 소매는 바로잡지 못하거나 모양이 깨질 수 있습니다. |

## 업데이트·제거

- **업데이트:** 새 패키지를 위에 덮어 임포트합니다(모두 체크한 채 Import). 설정은 씬의 Sleeve Roll 오브젝트에 저장돼 있어서 그대로 남습니다. `Assets/Pyrite9/SleeveRoll/` 안의 파일은 고치지 마세요 — 업데이트로 덮어써집니다.
- **아바타에서 빼기:** 아바타의 Sleeve Roll 오브젝트를 지웁니다.
- **프로젝트에서 지우기:** 먼저 아바타에서 빼고, 그다음 `Assets/Pyrite9/SleeveRoll/` 폴더를 지웁니다.
- 인스펙터의 변경 기록은 이 README에서 읽어 옵니다 — README 파일은 패키지 폴더에 그대로 두세요.

## 변경 기록

- **0.1.3** (2026-11-15): 걷을 수 있는 옷이 늘었습니다. Modular Avatar가 합치지 않고 남긴 옷 본, 아래팔 밑의 본(트위스트 본, 옷 변환 도구가 남긴 본), 손 본 밑의 손 사본 본에만 웨이트된 옷도 걷히고 손목 보조 본이 붙습니다 — 전에는 `the garment is not weighted to the forearm bone`으로 실패하거나 손목을 꺾으면 걷힌 소맷단이 팔에서 떨어졌습니다. 소매가 따로 움직이는 본(위팔 밑 PhysBone 소매 등)에도 웨이트된 옷에서 걷힌 소맷단이 처지거나 덜 걷히던 문제를 고쳤습니다. 걷은 모양을 켜 둔 셰이프키까지 반영한 입은 모양으로 계산하고, 그 셰이프키를 애니메이션이 바꾸면 경고합니다. 소매를 걷은 채 옷을 메뉴로 끄면 팔뚝이 부풀던 문제를 고쳤습니다 — shrink 해제가 shrink 값을 따라갑니다. 이런 아바타는 셰이프 이름이 `SleeveRoll_Unhide_L/R__<셰이프 이름>`이 되고 FX 레이어가 하나 늘며, 옷을 끄는 순간 한 프레임 어긋날 수 있습니다. 걷는 동안 손톱·손 셰이프키가 기본으로 돌아가던 문제, 손 본 스케일을 바꾼 아바타에서 내린 소매가 달라지던 문제, 엄지를 굽히면 내린 소맷단이 안 따라오던 문제를 고쳤습니다. 팔뚝 피부가 손목 근처에만 있는 몸 메시는 실패 대신 피부가 있는 데까지 걷고 경고하며, 팔뚝 피부 웨이트가 트위스트 본에 실린 몸에서 소매가 거의 안 걷히던 문제를 고쳤습니다. 실패하면 빌드 로그에 진단 줄(`diag:`)이 나오고, 인스펙터 경고 밑 안내를 늘렸습니다. 셰이프키로 shrink한 안쪽 옷 위에 입는 바깥 옷(블레이저 등)을 바깥 옷만 골라 걷을 수 있습니다 — 안쪽 옷은 걷힌 소매가 내준 자리까지만 풉니다. 빌드 때 0이어도 FX 애니메이션(옷 토글 등)이 켜는 팔뚝 shrink를 걷는 동안 해제합니다 — 옷을 끈 채 업로드한 아바타도 걷힌 소매 안에 가는 팔이 안 보입니다. 그 밖에 고친 것: 다른 아바타에서 복사해 온 Sleeve Roll이 원래 아바타의 옷을 가리키면 빌드가 그 옷의 메시를 바꾸던 문제(이제 건너뛰고 경고합니다), 본 목록이 바인드 포즈보다 긴 메시가 아바타에 있으면 Sleeve Roll이 내부 오류로 도중에 멈춰 소매가 반쯤 설정된 채 경고 없이 빌드되던 문제, Roll To(소맷단 위치)를 소매 끝 바로 위에 두고(조금만 걷기, 짧은 소매) Start At(움직임 시작)을 그 가까이 두면 소매가 걷히지 않고 손 쪽으로 늘어나던 문제. 걷은 모양 계산이 깨지면(NaN, 팔에서 날아가는 정점) 그 쪽 소매만 걷지 않고 경고하며, 빌드 중 내부 오류도 경고로 남깁니다. 알려진 한계: Sleeve Roll보다 늦게 FX를 만드는 도구(VRCFury 등)의 옷 토글은 shrink 해제가 따라가지 못하고, FX가 팔을 가늘게 하려고 켜는 몸 셰이프도 걷는 동안 걷힌 자리에서 풀리며, 걷는 동안의 shrink 해제는 어느 옷이 그 shrink를 켰는지 구별하지 않아 다른 옷을 입고 걷기를 켜면 팔이 그 옷 소매를 뚫고 나올 수 있습니다(문제 해결 표 참고).
- **0.1.2** (2026-11-09): 좌우 소매 PhysBone을 한 오브젝트에 같이 단 옷에서 한쪽 소매만 안 걷히던 문제(**마지막 빌드**에 `a second PhysBone on one object`)를 고쳤습니다. 소매 PhysBone이 다른 PhysBone 안에서 시작하거나, 두 PhysBone이 시작 본을 나눠 쓰거나, 소매 PhysBone이 Ignore Transforms로 본 일부를 빼 둔 옷에서, 걷은 소매 전체가 멈추고 내린 소매는 Sleeve Roll이 없을 때와 같이 흔들립니다(전에는 걷어도 일부가 흔들리거나 소맷단이 손목을 따라갔습니다). 두 PhysBone이 같은 본을 움직이면 빌드에서 알려 줍니다. 미리 보기 설명에, 빌드 때 입혀지는 옷의 PhysBone 판정은 빌드에서 정해진다는 것을 적었습니다. 그 밖의 옷은 빌드되는 아바타가 0.1.1과 같습니다.
- **0.1.1** (2026-11-08): 인스펙터 **메뉴**에 **설치 위치**(토글을 넣을 하위 메뉴, 비우면 루트. 아바타 메뉴를 트리로 보여 주고 눌러서 고릅니다)와 **저장**(재입장·아바타 변경 뒤에도 토글 상태 유지, 기본 꺼짐)을 넣었습니다. **마지막 빌드**에 토글 자리와 저장 여부가 나오고, 설치 위치가 아바타 메뉴에 없으면 인스펙터와 빌드에서 경고합니다. 같은 파라미터 토글이 메뉴에 두 개 이상이어도 빌드에서 경고합니다. README에 '메뉴를 직접 만들 때'를 넣었습니다. 기본 설정 그대로면 빌드되는 아바타는 0.1.0과 같습니다.
- **0.1.0** (2026-11-07): 첫 공개.

## 참고

- 걷힌 모양은 빌드할 때마다 그 옷 메시로 새로 계산합니다. 옷을 바꾸거나 고쳐도 다시 빌드하면 맞춰집니다.
- 소매 PhysBone은 빌드 때 살펴서 걷힌 동안 멈춰 둬도 안전할 때만 유지합니다. 다른 본까지 움직임, Is Animated 켬, Max Stretch·Max Squish 사용, Avatar Optimizer Merge PhysBone 사용, 그 PhysBone이나 본을 움직이는 다른 애니메이션이 있음, 쉬는 자세가 메시와 다름 — 이 중 하나면 유지하지 않고 경고합니다. 유지한 개수는 **마지막 빌드**에 나옵니다.
- 옷에 자체 반소매 셰이프키가 있으면 함께 켜질 수 있습니다. 시험한 옷(Kipfel 셔츠·Milfy 카디건)에서는 겹쳐도 크게 깨지지 않았습니다.
- FX 레이어는 아바타의 Write Defaults 설정에 맞춰집니다(Modular Avatar "Match Avatar Write Defaults"). 단, 애니메이션이 켜고 끄는 shrink를 해제하는 `Sleeve Roll Unhide` 레이어는 아바타 설정과 관계없이 Write Defaults 켬입니다(FX 맨 끝, Modular Avatar의 Merge Blend Tree와 같은 방식). Action·Base·Gesture 레이어는 쓰지 않습니다.
- Unity 에디터에서 Sleeve Roll 인스펙터를 열면 하루 한 번 GitHub(`api.github.com`)에 이 패키지의 최신 버전을 묻습니다. 개인 정보는 보내지 않고, 버전 글자를 누르면 바로 다시 확인합니다.

## 문의

- 버그 신고·질문: Booth 메시지(https://pyrite9.booth.pm/) 또는 X **@Pyrite9_VRC**.
- 같이 보내 주세요: 버전(인스펙터에 표시), **마지막 빌드**의 경고 또는 Console의 `[Pyrite9 Sleeve Roll]`로 시작하는 줄, 프로젝트 폴더의 `Logs/Pyrite9_SleeveRoll_build.txt`, 옷 이름, Unity·VRChat SDK·Modular Avatar·NDMF 버전.

## 이용 규약(요약 — 전문은 `LICENSE.txt`)

- 무료입니다. 사용·개변, 이 기믹을 쓴 아바타의 업로드(퍼블릭·프라이빗 모두)가 가능합니다. 단, 옷·아바타 자체의 규약(퍼블릭 업로드 금지 등)은 따로 지켜 주세요.
- 그 아바타를 영상·방송·스크린샷에 쓸 수 있습니다(수익 창출 포함).
- 다른 사람 아바타에 대신 달아 주기(커미션 등)는 그 사람이 이 패키지를 직접 받은 경우에만 가능합니다.
- 파일의 재배포·공유·판매는 개변 여부와 관계없이 금지합니다(배포·판매하는 아바타 데이터나 에셋에 넣는 것도 포함).
- 크레딧 표기는 필요 없지만, 해 주시면 감사하겠습니다("Pyrite9 Sleeve Roll © Pyrite9").
- GitHub에도 공개되어 있습니다: https://github.com/Pyrite9/vrchat-sleeve-roll (GitHub에서 보기·포크는 됩니다).
- 있는 그대로 제공되며 보증하지 않습니다. 받은 버전에는 그 버전에 들어 있는 규약이 적용됩니다.

© Pyrite9
