# JSON 제작 및 수정 규칙

이 문서는 `placement_elements.json` 과 `placement_layout_curated.json` 을 사람이 직접 수정하거나, 이후 에이전트에게 수정 요청할 때 따라야 하는 단일 기준 문서다.

## 1. 두 JSON의 역할

- `placement_elements.json`
  - 요소의 정의 파일이다.
  - 각 요소의 `id`, `layer`, `shape`, 반경 또는 세그먼트 크기, 3가지 색 배리에이션을 가진다.
  - 이 파일은 자주 바꾸지 않는다.
- `placement_layout_curated.json`
  - 실제 장면 구성 파일이다.
  - 어떤 색 배리에이션을 이번 장면에서 쓸지, 바닥 패치와 길과 소품과 팬스를 어디에 둘지 정한다.
  - 대부분의 큐레이션 수정은 이 파일에서만 해결한다.

## 2. 절대 바꾸면 안 되는 공통 규칙

- 캔버스는 항상 `1024x512` 이다.
- 좌표 원점은 좌상단이다.
- `x` 는 오른쪽으로 증가하고 `y` 는 아래로 증가한다.
- `rotationDeg` 는 시계방향 각도다.
- `0` 도는 좌에서 우로 향한다.
- 요소 종류는 아래 11개만 허용한다.
  - `water`, `pebble`, `sand`, `soil`, `grass`, `road`, `tree`, `rock`, `bush`, `mushroom`, `fence`
- 각 요소는 항상 3개의 색 배리에이션을 가진다.
- `typeVariants` 는 위 11개 모든 요소를 반드시 한 번씩 가져야 한다.
- 씬은 장면 오브젝트를 직접 생성하지 않고 JSON 두 개만 읽어 렌더한다.

## 3. 허용 필드와 필수 필드

### 3-1. `placement_elements.json`

- 루트 필드
  - `canvasWidth`
  - `canvasHeight`
  - `elements`
- 각 `elements[]` 항목 필수 필드
  - `id`
  - `layer`
  - `shape`
  - `variants`
- 바닥과 소품 계열 필수 추가 필드
  - `baseRadius`
- 길과 팬스 계열 필수 추가 필드
  - `segmentLength`
  - `segmentThickness`

### 3-2. `placement_layout_curated.json`

- 루트 필드
  - `sceneName`
  - `typeVariants`
  - `groundPatches`
  - `roadSegments`
  - `props`
  - `fenceSegments`
- `typeVariants[]`
  - `id`
  - `variantIndex`
- `groundPatches[]`
  - `id`
  - `x`
  - `y`
  - `radiusScale`
  - `aspectX`
  - `aspectY`
  - `rotationDeg`
  - `opacity`
- `roadSegments[]`
  - `x`
  - `y`
  - `rotationDeg`
  - `lengthScale`
  - `thicknessScale`
  - `opacity`
- `props[]`
  - `id`
  - `x`
  - `y`
  - `radiusScale`
  - `rotationDeg`
  - `opacity`
- `fenceSegments[]`
  - `x`
  - `y`
  - `rotationDeg`
  - `lengthScale`
  - `opacity`

## 4. 요소 의미와 레이어 규칙

- `grass`
  - 기본 배경과 초지 질감 보강용 패치다.
- `soil`
  - 길 어깨, 나무 밑, 눌린 땅 느낌을 만든다.
- `sand`
  - 물가의 완충 띠를 만든다.
- `pebble`
  - 물 가장자리나 경계 질감을 강조한다.
- `water`
  - 장면의 시선을 붙잡는 좌측 포컬 포인트다.
- `road`
  - 시선을 좌하단에서 우중앙으로 이끄는 흐름이다.
- `tree`
  - 큰 질량과 프레이밍을 담당한다.
- `rock`
  - 형태 대비와 경계 강조를 담당한다.
- `bush`
  - 중간 밀도를 채우고 나무와 길을 연결한다.
- `mushroom`
  - 작은 강조색 포인트다.
- `fence`
  - 우측 경계를 정리하고 화면 프레임 역할을 한다.

레이어 해석 규칙은 아래와 같다.

- `ground`
  - 바닥 패치 전용이다.
- `road`
  - 길 세그먼트 전용이다.
- `prop`
  - 나무, 바위, 덤불, 버섯 전용이다.
- `fence`
  - 팬스 세그먼트 전용이다.

## 5. 색 배리에이션 선택 규칙

- 이번 장면은 요소별 글로벌 선택만 허용한다.
- 같은 요소는 장면 안에서 `typeVariants` 의 `variantIndex` 하나만 쓴다.
- 인스턴스별 색 선택 필드는 만들지 않는다.
- 현재 큐레이션 기준값
  - `grass=1`
  - `soil=0`
  - `sand=2`
  - `pebble=1`
  - `water=0`
  - `road=1`
  - `tree=0`
  - `rock=1`
  - `bush=2`
  - `mushroom=0`
  - `fence=2`

색 수정 시 지켜야 할 미감 규칙은 아래와 같다.

- 물은 저채도 청록 계열을 유지한다.
- 잔디, 나무, 덤불은 서로 아주 멀지 않은 녹색 계열을 유지한다.
- 길과 흙은 따뜻한 갈색 계열을 유지한다.
- 버섯만 상대적으로 눈에 띄게 하되 과채도는 금지한다.
- 팬스는 날것의 나무색보다 오래된 목재 느낌이 낫다.

## 6. 자연스럽고 예쁘게 보이게 만드는 구도 규칙

- 모든 요소를 화면 전체에 균등 분포시키지 않는다.
- 좌측 1/3 지점에 물을 둬서 포컬 포인트를 만든다.
- 물 주변은 `water -> sand -> pebble -> grass` 흐름으로 읽히게 만든다.
- 길은 화면을 자르는 직선이 아니라 부드러운 S 흐름으로 둔다.
- 큰 나무는 좌상단과 우측 외곽에 두어 프레임을 만든다.
- 나무 밑에는 덤불과 버섯이 연관되게 놓이게 한다.
- 바위는 물가와 길 바깥 곡면에 둬서 경계를 잡는다.
- 팬스는 장면의 주인공이 아니므로 우측 일부 구간만 사용한다.
- 화면 중앙에는 너무 많은 큰 요소를 몰지 않는다.
- 숨 쉴 빈 공간을 반드시 남긴다.

## 7. 수정 요청을 JSON 변경으로 번역하는 규칙

- 색만 바꾸는 요청
  - 우선 `placement_layout_curated.json` 의 `typeVariants` 를 본다.
  - 색 배리에이션 자체가 부족하면 그때만 `placement_elements.json` 의 `variants` 를 수정한다.
- 크기만 바꾸는 요청
  - 특정 장면 인스턴스는 `radiusScale`, `lengthScale`, `thicknessScale` 로 해결한다.
  - 요소 전체 기본 크기를 바꾸고 싶을 때만 `placement_elements.json` 의 `baseRadius` 또는 세그먼트 크기를 바꾼다.
- 개수와 위치를 바꾸는 요청
  - 항상 `placement_layout_curated.json` 에서 해결한다.
- “더 자연스럽게”, “덜 답답하게”, “균형 다시 잡아” 같은 요청
  - 기본적으로 `placement_layout_curated.json` 만 수정한다.
  - `placement_elements.json` 은 유지한다.

## 8. 수정 시 우선순위

1. 먼저 `placement_elements.json` 을 유지할 수 있는지 판단한다.
2. 가능하면 `placement_layout_curated.json` 만 수정한다.
3. 색 팔레트 자체가 부적절하거나 요소 정의가 틀렸을 때만 `placement_elements.json` 을 수정한다.
4. 새 필드는 추가하지 않는다. 기존 스키마 안에서 해결한다.

## 9. 예시 변경 요청 5개

### 예시 1. "물을 지금보다 조금 더 넓게 해줘"

- 수정 파일
  - `placement_layout_curated.json`
- 수정 위치
  - `groundPatches` 배열에서 `id == "water"` 인 항목들
  - 그 주변의 `sand`, `pebble` 항목 일부
- 수정 방법
  - 물 패치의 `radiusScale`, `aspectX`, `aspectY` 를 소폭 증가시킨다.
  - 모래와 자갈 패치를 바깥쪽으로 조금 확장해 경계 층이 깨지지 않게 한다.
- 건드리지 말 것
  - `placement_elements.json` 의 `water.baseRadius`

### 예시 2. "나무 수를 줄이고 오른쪽이 덜 답답했으면 좋겠어"

- 수정 파일
  - `placement_layout_curated.json`
- 수정 위치
  - `props` 배열에서 `id == "tree"` 인 항목 일부
  - 필요하면 근처 `bush` 와 `mushroom` 항목도 같이 정리
- 수정 방법
  - 우측 군집의 나무 1~2개를 제거하거나 외곽으로 이동한다.
  - 트리 제거 후 남는 덤불과 버섯은 함께 정리해 관계가 끊기지 않게 한다.
- 건드리지 말 것
  - `tree` 의 색과 기본 반경 정의

### 예시 3. "길이 조금 더 부드럽게 오른쪽 위를 향하게 바꿔줘"

- 수정 파일
  - `placement_layout_curated.json`
- 수정 위치
  - `roadSegments`
- 수정 방법
  - 세그먼트의 `x`, `y`, `rotationDeg` 를 순차적으로 조정해 S 커브를 더 매끈하게 만든다.
  - 급격한 회전 변화 대신 인접 세그먼트끼리 각도 차를 작게 유지한다.
- 건드리지 말 것
  - `road.segmentLength`
  - `road.segmentThickness`

### 예시 4. "팬스를 더 세워 보이게 하고 방향을 정리해줘"

- 수정 파일
  - `placement_layout_curated.json`
- 수정 위치
  - `fenceSegments`
- 수정 방법
  - 각 항목의 `rotationDeg` 를 80~120도 부근으로 재조정한다.
  - 필요하면 `lengthScale` 을 미세 조정해서 연결감만 보정한다.
- 건드리지 말 것
  - `fence` 색상 팔레트와 두께 정의

### 예시 5. "전체적으로 더 따뜻한 색감으로 만들어줘"

- 수정 파일
  - 우선 `placement_layout_curated.json`
  - 부족하면 `placement_elements.json`
- 수정 위치
  - 1차: `typeVariants`
  - 2차: 각 요소의 `variants`
- 수정 방법
  - 먼저 `sand`, `soil`, `road`, `fence` 의 `variantIndex` 를 더 따뜻한 값으로 바꾼다.
  - 그래도 부족하면 `placement_elements.json` 에서 해당 색 배리에이션의 hex 값을 더 따뜻한 쪽으로 조정한다.
- 주의
  - `water` 와 `grass` 까지 같이 과하게 따뜻하게 만들면 장면이 탁해질 수 있다.

## 10. 변경 후 자체 검수 체크리스트

- `typeVariants` 에 11개 요소가 모두 있는가
- `variantIndex` 가 모두 0~2 범위인가
- `groundPatches` 에는 바닥 요소만 들어 있는가
- `props` 에는 `tree`, `rock`, `bush`, `mushroom` 만 들어 있는가
- 물 주변이 층처럼 읽히는가
- 길이 시선을 유도하는가
- 우측 팬스가 주인공처럼 보이지 않는가
- 큰 오브젝트가 화면 중앙을 과도하게 막지 않는가
- 버섯이 작은 포인트 역할만 하고 있는가
- 빈 공간이 남아 있는가
