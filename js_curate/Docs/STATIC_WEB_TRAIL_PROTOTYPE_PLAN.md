# Static Web Prototype Plan

## 목적

이 문서는 현재 Unity JSON 기반 작업과 **별개**로, `JSON 없이 정적 웹페이지 + JavaScript 파라미터 입력` 만으로 목초지 배치 프리뷰를 만드는 방향의 구현 계획이다.

목표는 다음과 같다.

- 브라우저에서 바로 열리는 정적 페이지여야 한다.
- 별도 서버, DB, 빌드 파이프라인 없이 동작해야 한다.
- 사용자가 파라미터를 조절하면 즉시 1024x512 탑뷰 프리뷰가 갱신되어야 한다.
- 레이아웃 감성은 `개미굴` 이 아니라 `목초지에 사람이나 짐승이 반복해서 지나며 생긴 길` 이어야 한다.

## 구현 범위

- 포함
  - 단일 정적 웹페이지
  - 실시간 파라미터 UI
  - 캔버스 렌더링
  - 랜덤 시드
  - PNG 내보내기
  - URL 파라미터 또는 해시 기반 상태 공유
- 제외
  - 백엔드
  - JSON import/export
  - 사용자 계정
  - 저장소 동기화
  - Unity 연동

## 기술 선택

- `HTML + CSS + Vanilla JavaScript`
- 렌더링은 `Canvas 2D API`
- 결과 캔버스는 고정 `1024x512`
- 상태는 메모리 객체 1개로 관리
- 선택 이유
  - 정적 페이지 요구에 가장 단순하다
  - 빌드 도구 없이 바로 열 수 있다
  - 브러시성 탑뷰 렌더를 구현하기 쉽다

## 추천 파일 구조

```text
web/
  index.html
  styles.css
  app.js
  renderer.js
  generator.js
  palette.js
  params.js
```

- `index.html`
  - 좌측 또는 상단 제어 패널
  - 우측 또는 하단 1024x512 프리뷰
- `styles.css`
  - 레이아웃, 컬러, 폰트, 반응형
- `app.js`
  - 앱 초기화, 상태 연결, 이벤트 바인딩
- `renderer.js`
  - 캔버스 브러시 렌더
- `generator.js`
  - 파라미터 기반 배치 생성
- `palette.js`
  - 요소별 색상 팔레트
- `params.js`
  - 기본값, 범위, 직렬화 규칙

## 핵심 개념

이 웹 버전은 `데이터를 읽는 도구`가 아니라 `파라미터를 받아 장면을 직접 생성하는 도구`여야 한다.

즉 생성 흐름은 아래와 같다.

1. 사용자가 파라미터를 조절한다.
2. JS가 내부 상태 객체를 갱신한다.
3. `generator.js` 가 현재 상태로 레이아웃을 생성한다.
4. `renderer.js` 가 결과를 1024x512 캔버스에 그린다.
5. 사용자는 결과를 계속 조정하거나 PNG로 저장한다.

## 레이아웃 생성 규칙

### 전체 방향

- 장르는 `열린 목초지`
- 길은 `반복 통행 흔적`
- 시각적 느낌은 `산책로`, `짐승 길`, `초지 압흔`, `우회 흔적`

### 길 규칙

- 긴 직선 주도로를 만들지 않는다.
- 길은 보통 2개의 큰 흐름과 2~4개의 연결길로 구성한다.
- 분기는 가능하지만 `나뭇가지형 단방향 확산` 은 피한다.
- `합류`, `우회`, `얕은 루프`, `짧은 샛길` 이 보여야 한다.
- 연못이나 수풀을 피하며 자연스럽게 휘어야 한다.
- 길의 굵기와 불투명도는 일정하지 않아야 한다.
- 일부 길은 약하게, 일부는 뚜렷하게 보여야 한다.

### 지형 규칙

- 기본은 잔디 우세다.
- 물이 있으면 그 주변은 `water -> sand -> pebble -> grass` 층으로 읽혀야 한다.
- 흙은 길 주변과 많이 밟힌 영역에만 제한적으로 쓴다.
- 바닥 요소는 독립 오브젝트처럼 보이지 말고 겹쳐진 층처럼 보여야 한다.

### 식생 규칙

- 큰 나무는 외곽 프레이밍과 시선 균형용이다.
- 덤불은 나무와 길 사이를 연결하는 중간 밀도층이다.
- 버섯은 그늘과 수풀 아래의 포인트다.
- 바위는 물가, 길 가장자리, 전이 구간에서 형태 대비를 준다.
- 중앙 시선 경로에는 큰 요소를 과도하게 놓지 않는다.

## 파라미터 설계

다음 파라미터 세트를 기본 사양으로 고정한다.

### 공통

- `seed`
  - 정수
  - 전체 랜덤 재현성 제어
- `density`
  - 0.25 ~ 4.0
  - 전체 오브젝트 수 배율
- `elementScale`
  - 0.25 ~ 2.0
  - 전체 요소 기본 크기 배율
- `stylePreset`
  - `meadow-trails`
  - 추후 `pond-heavy`, `open-pasture`, `edge-grove` 추가 가능

### 길 관련

- `pathNetworkCount`
  - 2 ~ 6
  - 큰 흐름 수
- `pathConnectorCount`
  - 1 ~ 5
  - 흐름 간 연결길 수
- `pathLoopChance`
  - 0.0 ~ 1.0
  - 루프 형성 경향
- `pathWidth`
  - 0.5 ~ 2.0
  - 길 두께 배율
- `pathFade`
  - 0.0 ~ 1.0
  - 일부 길이 희미해지는 정도
- `pathCurviness`
  - 0.0 ~ 1.0
  - 직선성 대 곡선성
- `pathBranchBias`
  - 0.0 ~ 1.0
  - 단순 분기 vs 연결 중심
  - 기본값은 `연결 중심` 으로 둔다

### 물과 바닥

- `hasPond`
  - boolean
- `pondSize`
  - 0.5 ~ 2.0
- `pondOffsetX`
  - -0.5 ~ 0.5
- `pondOffsetY`
  - -0.5 ~ 0.5
- `sandAmount`
  - 0.0 ~ 2.0
- `pebbleAmount`
  - 0.0 ~ 2.0
- `soilAmount`
  - 0.0 ~ 2.0
- `grassVariation`
  - 0.0 ~ 2.0

### 식생

- `treeCount`
  - 0 ~ 300
- `rockCount`
  - 0 ~ 240
- `bushCount`
  - 0 ~ 320
- `mushroomCount`
  - 0 ~ 320
- `fenceCount`
  - 0 ~ 240
- `edgeGroveStrength`
  - 0.0 ~ 1.0
- `centerOpenness`
  - 0.0 ~ 1.0
- `pondEdgeDetail`
  - 0.0 ~ 1.0

### 시각 스타일

- `palettePreset`
  - `soft-olive`
  - `dry-pasture`
  - `cool-morning`
- `shadowStrength`
  - 0.0 ~ 1.0
- `textureNoise`
  - 0.0 ~ 1.0
- `debugOverlay`
  - boolean

## 내부 상태 객체 제안

```js
const state = {
  seed: 12345,
  density: 1,
  elementScale: 1,
  stylePreset: "meadow-trails",
  pathNetworkCount: 3,
  pathConnectorCount: 3,
  pathLoopChance: 0.45,
  pathWidth: 1,
  pathFade: 0.35,
  pathCurviness: 0.7,
  pathBranchBias: 0.3,
  hasPond: true,
  pondSize: 1,
  pondOffsetX: -0.18,
  pondOffsetY: 0.02,
  sandAmount: 1,
  pebbleAmount: 1,
  soilAmount: 1,
  grassVariation: 1,
  treeCount: 128,
  rockCount: 96,
  bushCount: 192,
  mushroomCount: 192,
  fenceCount: 160,
  edgeGroveStrength: 0.8,
  centerOpenness: 0.7,
  pondEdgeDetail: 0.8,
  palettePreset: "soft-olive",
  shadowStrength: 0.4,
  textureNoise: 0.35,
  debugOverlay: false
};
```

## 생성 파이프라인

### 1. 레이아웃 프레임 결정

- 캔버스 1024x512 확정
- 연못 존재 여부 결정
- 길 흐름의 앵커 포인트 결정
- 외곽 수풀/나무 프레임 영역 결정

### 2. 길 네트워크 생성

- 큰 흐름 2~3개 생성
- 연결길 2~4개 생성
- 일부 구간은 합류하도록 조정
- 루프는 많아도 1~2개만 허용
- Dead-end는 짧은 샛길에만 허용

### 3. 바닥 패치 생성

- 잔디 기본 배경
- 연못
- 모래 띠
- 자갈 띠
- 길 주변 흙
- 초지 질감 패치

### 4. 오브젝트 배치

- 큰 나무 먼저
- 바위 둘째
- 덤불 셋째
- 버섯 마지막
- 팬스는 맨 끝

### 5. 렌더

- grass
- soil
- sand
- pebble
- water
- paths
- rocks
- bushes
- mushrooms
- trees
- fences

## 렌더 스타일 규칙

- 전체는 탑뷰 일러스트 스타일
- 강한 외곽선은 피한다
- 브러시성 소프트 엣지 사용
- 길은 capsule 스트로크를 여러 개 겹쳐 표현
- 물은 내부 하이라이트와 외곽 어둠을 약하게 준다
- 나무는 canopy 다층 원형
- 바위는 불규칙한 덩어리
- 버섯은 작은 강조색
- 팬스는 얇고 덜 눈에 띄게

## UI 설계

### 레이아웃

- 좌측 고정 컨트롤 패널
- 우측 프리뷰 캔버스
- 모바일에서는 상단 컨트롤, 하단 프리뷰

### 주요 버튼

- `Randomize Seed`
- `Reset Defaults`
- `Regenerate`
- `Export PNG`
- `Copy Share URL`

### 실시간 갱신 정책

- 슬라이더 이동 중에는 `requestAnimationFrame` 기반 debounce
- 숫자 입력 완료 시 즉시 재생성
- `seed` 변경은 즉시 전체 레이아웃 재생성

## 상태 공유 방식

- JSON 파일은 쓰지 않는다
- 상태는 URL query 또는 hash로 직렬화한다
- 페이지 로드 시 URL 상태를 파싱해 복원한다
- 옵션
  - 기본은 querystring
  - 값이 길어지면 compressed hash로 전환 가능

## 개발 단계 제안

### 1단계

- 정적 페이지 골격
- 캔버스 출력
- 수동 파라미터 객체
- 랜덤 배경 렌더

### 2단계

- 길 네트워크 생성
- 연못 + 바닥 패치
- 기본 오브젝트 배치

### 3단계

- 파라미터 UI 연결
- 실시간 재생성
- 시드 고정

### 4단계

- PNG export
- URL 공유
- 디버그 오버레이

### 5단계

- 프리셋 추가
- 성능 정리
- 모바일 레이아웃 보정

## 테스트 기준

- 로컬 파일로 열어도 동작해야 한다
- 1024x512 프리뷰가 항상 생성되어야 한다
- 파라미터 변경 시 오류 없이 다시 그려져야 한다
- 시드가 같으면 결과가 같아야 한다
- `meadow-trails` 프리셋이 `나뭇가지형` 으로 보이면 실패다
- 길이 `합류/우회/연결` 중심으로 읽혀야 한다
- 중앙 시야가 과도하게 막히면 실패다
- 연못이 있을 때 길이 물을 관통하면 실패다

## 구현 시 주의사항

- 처음부터 너무 많은 파라미터를 열지 않는다
- path generator를 먼저 안정화하고 식생은 나중에 얹는다
- path branch 수보다 `merge와 loop의 질` 이 더 중요하다
- 랜덤만으로 해결하려 하지 말고 구조 규칙을 먼저 고정한다
- `개미굴`, `강줄기`, `나뭇가지`, `도로망` 처럼 보이면 실패다
- 목표는 `사람/짐승이 반복해서 다닌 목초지의 눌린 길` 이다

## 권장 기본 프리셋

### meadow-trails

- 연못 있음
- 큰 흐름 2개
- 연결길 3개
- 루프 1개
- 외곽 수풀 강함
- 중앙 개방도 높음

### open-pasture

- 연못 작음 또는 없음
- 길 수 적음
- 식생 적음
- 열린 초지 강조

### pond-side-grazing

- 연못 큼
- 물가 디테일 많음
- 길은 물가 우회 중심
- 버섯과 자갈 밀도 높음

## 결과물 정의

이 계획대로 구현할 경우 최종 결과물은 다음과 같다.

- 브라우저에서 바로 열리는 단일 정적 웹 앱
- JSON 없이 파라미터만으로 장면 생성
- 1024x512 탑뷰 프리뷰
- 목초지 통행로 감성의 레이아웃 생성기
- PNG export 및 URL 공유 가능
