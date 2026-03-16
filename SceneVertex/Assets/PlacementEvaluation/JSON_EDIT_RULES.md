# 배치 JSON 수정 규칙

이 문서는 `placement_elements.json` 과 `placement_layout_curated.json` 을 수정할 때 따르는 기준 문서다.

핵심 원칙은 하나다.

- `placement_elements.json` 은 "무엇이 존재하는가"를 정의한다.
- `placement_layout_curated.json` 은 "어떤 환경 의도로 배치가 생성되는가"를 정의한다.
- 최종 나무 좌표, 최종 길 세그먼트, 최종 펜스 세그먼트는 이제 JSON에 직접 쓰지 않는다.
- 최종 배치는 에디터 렌더 단계에서 내부적으로 생성된다.

## 1. 두 JSON의 역할

### `placement_elements.json`

- 요소 카탈로그다.
- 각 요소의 `id`, `layer`, `shape`, 크기 기준치, 3색 배리에이션을 가진다.
- 웬만하면 유지한다.
- 아래 경우에만 수정한다.
  - 요소 종류를 추가하거나 제거해야 할 때
  - 모든 결과에 공통으로 적용될 기본 크기를 바꿔야 할 때
  - 색 배리에이션 자체가 부족하거나 잘못되었을 때

### `placement_layout_curated.json`

- 큐레이션 의도 파일이다.
- 장면의 구도, 연못의 덩어리, 길의 흐름, 숲 군집, 산포 영역, 펜스 라인을 정의한다.
- 대부분의 수정 요청은 이 파일에서 처리한다.
- 이 파일에는 더 이상 `groundPatches`, `roadSegments`, `props`, `fenceSegments` 같은 최종 배치 배열이 없다.

## 2. 새 레이아웃 스키마

루트 필드:

- `sceneName`
- `seed`
- `typeVariants`
- `meadow`
- `ponds`
- `trails`
- `groves`
- `scatterZones`
- `fenceLines`

### `typeVariants`

- 각 타입이 이번 장면에서 사용할 색 배리에이션 인덱스를 정한다.
- 모든 11개 타입을 반드시 포함한다.
- 허용 범위는 `0`, `1`, `2` 뿐이다.

### `meadow`

- 목초지의 바탕 분위기를 정한다.
- 필드:
  - `accentGrassCount`
  - `openCenterX`
  - `openCenterY`
  - `openCenterRadiusX`
  - `openCenterRadiusY`
  - `windAngleDeg`

의미:

- `accentGrassCount`: 바탕 잔디 질감 보강량
- `openCenter*`: 중앙 개활지로 비워 둘 영역
- `windAngleDeg`: 잔디 결 방향

### `ponds`

- 연못은 하나 이상의 덩어리로 정의한다.
- 각 항목 필드:
  - `center`
  - `radiusX`
  - `radiusY`
  - `rotationDeg`
  - `sandWidth`
  - `pebbleWidth`
  - `irregularity`
  - `lobes`

의미:

- `center`, `radiusX`, `radiusY`: 연못 본체
- `sandWidth`: 모래 띠 두께
- `pebbleWidth`: 자갈 띠 두께
- `irregularity`: 가장자리 흔들림 정도
- `lobes`: 본체에 붙는 보조 덩어리

### `trails`

- 길은 더 이상 최종 선분이 아니다.
- 각 항목은 `사람이나 짐승이 다니는 흐름` 하나를 정의한다.
- 각 항목 필드:
  - `id`
  - `points`
  - `traffic`
  - `width`
  - `soilExposure`
  - `braid`
  - `wander`

의미:

- `points`: 길의 제어점
- `traffic`: 얼마나 자주 밟히는지
- `width`: 길 폭
- `soilExposure`: 흙이 얼마나 드러나는지
- `braid`: 갈라졌다 다시 합쳐지는 흔적의 정도
- `wander`: 완만한 흔들림 정도

### `groves`

- 숲이나 수풀 군집의 큰 덩어리다.
- 각 항목 필드:
  - `id`
  - `center`
  - `radiusX`
  - `radiusY`
  - `rotationDeg`
  - `treeCount`
  - `bushCount`
  - `mushroomCount`
  - `innerClear`
  - `treeEdgeBias`

의미:

- 개별 오브젝트 좌표를 직접 적는 대신 군집의 성격만 적는다.
- 내부 생성기가 이 값을 보고 나무, 덤불, 버섯을 뿌린다.

### `scatterZones`

- 바위 포인트, 추가 덤불, 그늘 버섯 같은 보조 산포 영역이다.
- 각 항목 필드:
  - `id`
  - `kind`
  - `center`
  - `radiusX`
  - `radiusY`
  - `rotationDeg`
  - `count`
  - `innerRadius`
  - `scaleMin`
  - `scaleMax`
  - `opacityMin`
  - `opacityMax`

### `fenceLines`

- 펜스도 더 이상 최종 세그먼트 목록이 아니다.
- 각 항목 필드:
  - `points`
  - `density`
  - `brokenness`
  - `lengthScale`
  - `opacity`

의미:

- `points`: 펜스 라인 제어점
- `density`: 세그먼트 밀도
- `brokenness`: 일부 구간이 비는 정도
- `lengthScale`: 평균 길이 스케일

## 3. 좌표계와 고정 규칙

- 캔버스는 항상 `1024x512`
- 원점은 좌상단
- `x` 는 오른쪽 증가
- `y` 는 아래 증가
- `rotationDeg` 는 시계 방향
- `0` 도는 좌에서 우

## 4. 자연스럽게 보이게 만드는 핵심 규칙

- 길은 `road object` 가 아니라 `grass wear` 로 읽혀야 한다.
- 메인 통행축은 1개, 보조 통행축은 1~3개 정도가 적당하다.
- 길은 직선보다 완만한 눌림 흔적처럼 보여야 한다.
- 큰 개활지 하나는 반드시 남긴다.
- 연못은 독립 원형 하나보다 `본체 + 보조 lobe` 가 더 자연스럽다.
- 나무는 경계와 군집을 만든다.
- 덤불은 나무와 길 어깨를 연결한다.
- 버섯은 그늘과 습기에 묶는다.
- 펜스는 화면 경계 보조다. 중앙을 가르지 않는다.

## 5. 수정 요청을 JSON 변경으로 번역하는 방법

### "길이 더 많이 보여야 해"

- `trails` 배열을 본다.
- 새 `trail` 하나를 추가하거나 기존 `traffic`, `width`, `braid` 를 올린다.
- 이미 있는 길을 더 많이 보이게 하고 싶다면 `soilExposure` 를 약간만 올린다.
- `placement_elements.json` 의 `road` 기본 값은 건드리지 않는다.

### "길이 너무 계획적이야"

- `trails[].points` 의 각 점을 덜 정렬되게 바꾼다.
- `wander` 를 소폭 올린다.
- `braid` 를 너무 크게 준 경우 줄인다.
- `traffic` 가 높은 길 수를 줄인다.

### "연못이 더 풍성했으면"

- `ponds[].lobes` 를 추가하거나 각 lobe 의 `distance`, `radiusXScale`, `radiusYScale` 를 조정한다.
- `sandWidth`, `pebbleWidth` 로 가장자리 층을 정리한다.
- 물 기본 색이나 기본 반경은 카탈로그가 아니라면 손대지 않는다.

### "좌상단 숲을 줄이고 우하단에 무게를 더 줘"

- `groves` 의 `treeCount`, `bushCount`, `mushroomCount` 를 옮긴다.
- 필요한 경우 `groves[].center` 와 `radiusX`, `radiusY` 도 조정한다.
- 추가 포인트는 `scatterZones` 로 보완한다.

### "펜스가 너무 눈에 띄어"

- `fenceLines[].opacity` 를 낮춘다.
- `brokenness` 를 올린다.
- `density` 를 줄인다.

## 6. 절대 바꾸면 안 되는 공통 규칙

- `placement_layout_curated.json` 에 최종 인스턴스 배열을 다시 만들지 않는다.
- `placement_elements.json` 에 장면별 의도를 넣지 않는다.
- `typeVariants` 에서 같은 타입을 중복 정의하지 않는다.
- `trails` 는 최소 2개의 `points` 를 가져야 한다.
- `scatterZones.kind` 는 `tree`, `rock`, `bush`, `mushroom` 중 하나여야 한다.

## 7. 변경 후 자체 검수 체크리스트

- 개활지가 남아 있는가
- 연못이 `water -> sand -> pebble` 층으로 읽히는가
- 길이 갈색 선이 아니라 눌린 초지처럼 보이는가
- 중앙이 너무 복잡하지 않은가
- 좌우 무게가 완전 대칭이 아닌가
- 펜스가 장면 주인공처럼 보이지 않는가
- 버섯이 강조점 이상으로 많지 않은가
- `placement_elements.json` 을 수정하지 않고 해결 가능한 요청인데도 건드리지 않았는가
