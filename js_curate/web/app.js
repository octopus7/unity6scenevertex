(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});
  const Params = App.Params;
  const Generator = App.Generator;
  const Renderer = App.Renderer;

  const SUPPORTED_LOCALES = ["ko", "en", "ja"];
  const LOCALE_TEXT = {
    en: {
      htmlLang: "en",
      pageTitle: "Pasture Trail Prototype",
      appEyebrow: "Procedural Terrain",
      appTitle: "Pasture Trail Prototype",
      panelCopy: "Procedural terrain, shoreline, and trail generation with in-memory scene export/import.",
      statusEyebrow: "Status",
      summaryEyebrow: "Snapshot",
      canvasEyebrow: "Canvas",
      canvasTitle: "1024 x 512 Top View",
      languageLabel: "Language",
      buttons: {
        randomizeSeed: "Randomize Seed",
        resetDefaults: "Reset Defaults",
        regenerateScene: "Regenerate",
        exportPng: "Export PNG",
        copyShareUrl: "Copy Share URL",
        exportSceneJson: "Export Scene JSON",
        importSceneJson: "Import Scene JSON"
      },
      status: {
        initializing: "Initializing...",
        rendered: function (args) {
          return "Rendered seed " + args.seed + " with " + args.segments + " road segments.";
        },
        sceneJsonExported: "Scene JSON exported.",
        importedSceneJson: "Imported scene JSON.",
        shareUrlCopied: "Share URL copied.",
        pngExportFailed: "PNG export failed.",
        pngExported: "PNG exported.",
        copyFailed: function (args) {
          return "Copy failed: " + args.message;
        },
        importFailed: function (args) {
          return "Import failed: " + args.message;
        }
      },
      errors: {
        jsonNotObject: "Imported file is not a JSON object.",
        catalogMissing: "catalog.elements is missing.",
        terrainMissing: "scene.terrain is missing.",
        sceneArraysIncomplete: "scene arrays are incomplete."
      },
      summary: {
        terrainCells: "Terrain Cells",
        roadSegments: "Road Segments",
        props: "Props",
        fences: "Fences",
        mainFlows: "Main Flows",
        connectors: "Connectors",
        water: "Water Cells"
      },
      source: {
        procedural: "procedural",
        imported: "imported"
      },
      groups: {
        Core: "Core",
        Paths: "Paths",
        Terrain: "Terrain",
        Vegetation: "Vegetation",
        Render: "Render"
      },
      fields: {
        seed: "Seed",
        stylePreset: "Style Preset",
        palettePreset: "Palette",
        density: "Density",
        elementScale: "Element Scale",
        pathNetworkCount: "Main Flows",
        pathConnectorCount: "Connectors",
        pathLoopChance: "Loop Chance",
        pathWidth: "Path Width",
        pathFade: "Path Fade",
        pathCurviness: "Curviness",
        pathBranchBias: "Branch Bias",
        hasPond: "Has Pond",
        pondSize: "Pond Size",
        pondOffsetX: "Pond Offset X",
        pondOffsetY: "Pond Offset Y",
        sandAmount: "Sand Amount",
        pebbleAmount: "Pebble Amount",
        soilAmount: "Soil Amount",
        grassVariation: "Grass Variation",
        treeCount: "Trees",
        rockCount: "Rocks",
        bushCount: "Bushes",
        mushroomCount: "Mushrooms",
        fenceCount: "Fence Segments",
        edgeGroveStrength: "Edge Grove",
        centerOpenness: "Center Openness",
        pondEdgeDetail: "Pond Edge Detail",
        shadowStrength: "Shadow Strength",
        textureNoise: "Texture Noise",
        debugOverlay: "Debug Overlay"
      },
      options: {
        stylePreset: {
          "meadow-trails": "Meadow Trails",
          "open-pasture": "Open Pasture",
          "pond-side-grazing": "Pond-side Grazing",
          "edge-grove": "Edge Grove"
        },
        palettePreset: {
          "soft-olive": "Soft Olive",
          "dry-pasture": "Dry Pasture",
          "cool-morning": "Cool Morning"
        }
      },
      checkboxEnabled: "Enable",
      booleans: {
        on: "on",
        off: "off"
      }
    },
    ko: {
      htmlLang: "ko",
      pageTitle: "목초지 트레일 프로토타입",
      appEyebrow: "절차적 지형",
      appTitle: "목초지 트레일 프로토타입",
      panelCopy: "외부 JSON 정의 없이 지형, 해안선, 트레일 흔적을 절차적으로 생성합니다. 메모리상의 scene은 JSON으로 export/import할 수 있습니다.",
      statusEyebrow: "상태",
      summaryEyebrow: "스냅샷",
      canvasEyebrow: "캔버스",
      canvasTitle: "1024 x 512 탑뷰",
      languageLabel: "언어",
      buttons: {
        randomizeSeed: "시드 랜덤화",
        resetDefaults: "기본값 복원",
        regenerateScene: "다시 생성",
        exportPng: "PNG 내보내기",
        copyShareUrl: "공유 URL 복사",
        exportSceneJson: "Scene JSON 내보내기",
        importSceneJson: "Scene JSON 가져오기"
      },
      status: {
        initializing: "초기화 중...",
        rendered: function (args) {
          return "시드 " + args.seed + "를 렌더링했고 길 세그먼트 " + args.segments + "개를 생성했습니다.";
        },
        sceneJsonExported: "Scene JSON을 내보냈습니다.",
        importedSceneJson: "Scene JSON을 가져왔습니다.",
        shareUrlCopied: "공유 URL을 복사했습니다.",
        pngExportFailed: "PNG 내보내기에 실패했습니다.",
        pngExported: "PNG를 내보냈습니다.",
        copyFailed: function (args) {
          return "복사 실패: " + args.message;
        },
        importFailed: function (args) {
          return "가져오기 실패: " + args.message;
        }
      },
      errors: {
        jsonNotObject: "가져온 파일이 JSON 객체가 아닙니다.",
        catalogMissing: "catalog.elements가 없습니다.",
        terrainMissing: "scene.terrain이 없습니다.",
        sceneArraysIncomplete: "scene 배열 구성이 불완전합니다."
      },
      summary: {
        terrainCells: "지형 셀",
        roadSegments: "길 세그먼트",
        props: "오브젝트",
        fences: "울타리",
        mainFlows: "주 흐름",
        connectors: "연결 길",
        water: "물 셀"
      },
      source: {
        procedural: "절차 생성",
        imported: "가져옴"
      },
      groups: {
        Core: "기본",
        Paths: "길",
        Terrain: "지형",
        Vegetation: "식생",
        Render: "렌더"
      },
      fields: {
        seed: "시드",
        stylePreset: "스타일 프리셋",
        palettePreset: "팔레트",
        density: "밀도",
        elementScale: "요소 크기",
        pathNetworkCount: "주 흐름 수",
        pathConnectorCount: "연결 길 수",
        pathLoopChance: "루프 확률",
        pathWidth: "길 폭",
        pathFade: "길 페이드",
        pathCurviness: "굽이 정도",
        pathBranchBias: "분기 편향",
        hasPond: "호수 포함",
        pondSize: "호수 크기",
        pondOffsetX: "호수 X 오프셋",
        pondOffsetY: "호수 Y 오프셋",
        sandAmount: "모래 양",
        pebbleAmount: "자갈 양",
        soilAmount: "흙 양",
        grassVariation: "잔디 변화량",
        treeCount: "나무 수",
        rockCount: "바위 수",
        bushCount: "덤불 수",
        mushroomCount: "버섯 수",
        fenceCount: "울타리 세그먼트",
        edgeGroveStrength: "가장자리 수풀",
        centerOpenness: "중앙 개방도",
        pondEdgeDetail: "호숫가 디테일",
        shadowStrength: "그림자 강도",
        textureNoise: "텍스처 노이즈",
        debugOverlay: "디버그 오버레이"
      },
      options: {
        stylePreset: {
          "meadow-trails": "초지 트레일",
          "open-pasture": "열린 목초지",
          "pond-side-grazing": "호숫가 방목지",
          "edge-grove": "가장자리 수림"
        },
        palettePreset: {
          "soft-olive": "부드러운 올리브",
          "dry-pasture": "마른 목초지",
          "cool-morning": "서늘한 아침"
        }
      },
      checkboxEnabled: "사용",
      booleans: {
        on: "켬",
        off: "끔"
      }
    },
    ja: {
      htmlLang: "ja",
      pageTitle: "牧草地トレイル試作",
      appEyebrow: "プロシージャル地形",
      appTitle: "牧草地トレイル試作",
      panelCopy: "外部JSON定義なしで地形、岸線、踏み跡をプロシージャル生成します。メモリ上のsceneはJSONとしてexport/importできます。",
      statusEyebrow: "状態",
      summaryEyebrow: "スナップショット",
      canvasEyebrow: "キャンバス",
      canvasTitle: "1024 x 512 トップビュー",
      languageLabel: "言語",
      buttons: {
        randomizeSeed: "シードをランダム化",
        resetDefaults: "初期値に戻す",
        regenerateScene: "再生成",
        exportPng: "PNGを書き出す",
        copyShareUrl: "共有URLをコピー",
        exportSceneJson: "Scene JSONを書き出す",
        importSceneJson: "Scene JSONを読み込む"
      },
      status: {
        initializing: "初期化中...",
        rendered: function (args) {
          return "シード " + args.seed + " を描画し、道セグメントを " + args.segments + " 個生成しました。";
        },
        sceneJsonExported: "Scene JSONを書き出しました。",
        importedSceneJson: "Scene JSONを読み込みました。",
        shareUrlCopied: "共有URLをコピーしました。",
        pngExportFailed: "PNGの書き出しに失敗しました。",
        pngExported: "PNGを書き出しました。",
        copyFailed: function (args) {
          return "コピー失敗: " + args.message;
        },
        importFailed: function (args) {
          return "読み込み失敗: " + args.message;
        }
      },
      errors: {
        jsonNotObject: "読み込んだファイルはJSONオブジェクトではありません。",
        catalogMissing: "catalog.elements がありません。",
        terrainMissing: "scene.terrain がありません。",
        sceneArraysIncomplete: "scene 配列の構成が不完全です。"
      },
      summary: {
        terrainCells: "地形セル",
        roadSegments: "道セグメント",
        props: "配置物",
        fences: "柵",
        mainFlows: "主経路",
        connectors: "接続路",
        water: "水セル"
      },
      source: {
        procedural: "自動生成",
        imported: "読込済み"
      },
      groups: {
        Core: "基本",
        Paths: "道",
        Terrain: "地形",
        Vegetation: "植生",
        Render: "描画"
      },
      fields: {
        seed: "シード",
        stylePreset: "スタイルプリセット",
        palettePreset: "パレット",
        density: "密度",
        elementScale: "要素スケール",
        pathNetworkCount: "主経路数",
        pathConnectorCount: "接続路数",
        pathLoopChance: "ループ確率",
        pathWidth: "道幅",
        pathFade: "道のフェード",
        pathCurviness: "うねり",
        pathBranchBias: "分岐バイアス",
        hasPond: "水辺を含む",
        pondSize: "水辺サイズ",
        pondOffsetX: "水辺Xオフセット",
        pondOffsetY: "水辺Yオフセット",
        sandAmount: "砂量",
        pebbleAmount: "小石量",
        soilAmount: "土量",
        grassVariation: "草の変化",
        treeCount: "木",
        rockCount: "岩",
        bushCount: "低木",
        mushroomCount: "きのこ",
        fenceCount: "柵セグメント",
        edgeGroveStrength: "縁の茂み",
        centerOpenness: "中央の開放感",
        pondEdgeDetail: "岸辺ディテール",
        shadowStrength: "影の強さ",
        textureNoise: "テクスチャノイズ",
        debugOverlay: "デバッグ表示"
      },
      options: {
        stylePreset: {
          "meadow-trails": "草地の小道",
          "open-pasture": "開けた牧草地",
          "pond-side-grazing": "岸辺の放牧地",
          "edge-grove": "縁の林"
        },
        palettePreset: {
          "soft-olive": "柔らかなオリーブ",
          "dry-pasture": "乾いた牧草地",
          "cool-morning": "涼しい朝"
        }
      },
      checkboxEnabled: "有効",
      booleans: {
        on: "オン",
        off: "オフ"
      }
    }
  };

  let currentState = Params.parseStateFromLocation(window.location.href);
  let currentBundle = null;
  let currentSource = "procedural";
  let currentLocale = detectLocale();
  let frameHandle = 0;
  let lastStatus = { key: "initializing", args: {} };
  const inputs = {};

  const elements = {
    controls: document.getElementById("controls"),
    canvas: document.getElementById("preview-canvas"),
    statusText: document.getElementById("status-text"),
    summary: document.getElementById("scene-summary"),
    sceneName: document.getElementById("scene-name"),
    sceneSource: document.getElementById("scene-source"),
    randomizeSeed: document.getElementById("randomize-seed"),
    resetDefaults: document.getElementById("reset-defaults"),
    regenerateScene: document.getElementById("regenerate-scene"),
    exportPng: document.getElementById("export-png"),
    copyShareUrl: document.getElementById("copy-share-url"),
    exportSceneJson: document.getElementById("export-scene-json"),
    importSceneJson: document.getElementById("import-scene-json"),
    sceneJsonInput: document.getElementById("scene-json-input"),
    appEyebrow: document.getElementById("app-eyebrow"),
    appTitle: document.getElementById("app-title"),
    panelCopy: document.getElementById("panel-copy"),
    statusEyebrow: document.getElementById("status-eyebrow"),
    summaryEyebrow: document.getElementById("summary-eyebrow"),
    canvasEyebrow: document.getElementById("canvas-eyebrow"),
    canvasTitle: document.getElementById("canvas-title"),
    languageLabel: document.getElementById("language-label"),
    languageSelect: document.getElementById("language-select")
  };

  function detectLocale() {
    const languages = Array.isArray(navigator.languages) && navigator.languages.length
      ? navigator.languages
      : [navigator.language || navigator.userLanguage || "en"];

    for (let index = 0; index < languages.length; index += 1) {
      const code = String(languages[index] || "").toLowerCase();
      if (code.indexOf("ko") === 0) {
        return "ko";
      }
      if (code.indexOf("ja") === 0) {
        return "ja";
      }
      if (code.indexOf("en") === 0) {
        return "en";
      }
    }

    return "en";
  }

  function localeBundle(locale) {
    return LOCALE_TEXT[SUPPORTED_LOCALES.indexOf(locale) >= 0 ? locale : "en"];
  }

  function resolveMessage(locale, path) {
    const keys = path.split(".");
    let cursor = localeBundle(locale);
    for (let index = 0; index < keys.length; index += 1) {
      if (!cursor || typeof cursor !== "object" || !(keys[index] in cursor)) {
        return undefined;
      }
      cursor = cursor[keys[index]];
    }
    return cursor;
  }

  function getMessage(path, fallback) {
    const localized = resolveMessage(currentLocale, path);
    if (localized !== undefined) {
      return localized;
    }
    const english = resolveMessage("en", path);
    return english !== undefined ? english : fallback;
  }

  function formatStatus(status) {
    const entry = getMessage("status." + status.key, status.key);
    if (typeof entry === "function") {
      return entry(status.args || {});
    }
    return entry;
  }

  function setStatus(statusKey, args) {
    lastStatus = {
      key: statusKey,
      args: args || {}
    };
    elements.statusText.textContent = formatStatus(lastStatus);
  }

  function createNode(tag, className, text) {
    const node = document.createElement(tag);
    if (className) {
      node.className = className;
    }
    if (typeof text === "string") {
      node.textContent = text;
    }
    return node;
  }

  function getGroupTitle(group) {
    return getMessage("groups." + group.title, group.title);
  }

  function getFieldLabel(field) {
    return getMessage("fields." + field.key, field.label);
  }

  function getOptionLabel(fieldKey, option) {
    return getMessage("options." + fieldKey + "." + option, option);
  }

  function formatFieldValue(field, value) {
    if (field.type === "checkbox") {
      return value ? getMessage("booleans.on", "on") : getMessage("booleans.off", "off");
    }
    if (field.type === "select") {
      return getOptionLabel(field.key, value);
    }
    return Params.formatValue(field, value);
  }

  function updateDisplayedValue(key) {
    const field = Params.fieldMap()[key];
    const entry = inputs[key];
    if (!field || !entry) {
      return;
    }
    if (entry.value) {
      entry.value.textContent = formatFieldValue(field, currentState[key]);
    }
    if (entry.range) {
      entry.range.value = currentState[key];
    }
    if (entry.number) {
      entry.number.value = currentState[key];
    }
    if (entry.checkbox) {
      entry.checkbox.checked = Boolean(currentState[key]);
    }
    if (entry.select) {
      entry.select.value = currentState[key];
    }
  }

  function syncAllInputs() {
    Object.keys(currentState).forEach(updateDisplayedValue);
  }

  function setStatePatch(patch, immediate) {
    currentState = Params.clampState(Object.assign({}, currentState, patch));
    Object.keys(patch).forEach(updateDisplayedValue);
    if (immediate) {
      regenerate("procedural");
    } else {
      if (frameHandle) {
        cancelAnimationFrame(frameHandle);
      }
      frameHandle = requestAnimationFrame(function () {
        frameHandle = 0;
        regenerate("procedural");
      });
    }
  }

  function createControlRow(field) {
    const row = createNode("div", "control-row");
    const head = createNode("div", "control-head");
    head.appendChild(createNode("label", "", getFieldLabel(field)));
    const value = createNode("span", "control-value", "");
    head.appendChild(value);
    row.appendChild(head);
    inputs[field.key] = { value: value };

    if (field.type === "checkbox") {
      const shell = createNode("label", "checkbox-shell");
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.checked = currentState[field.key];
      checkbox.addEventListener("change", function () {
        setStatePatch(Object.fromEntries([[field.key, checkbox.checked]]), true);
      });
      inputs[field.key].checkbox = checkbox;
      shell.appendChild(checkbox);
      shell.appendChild(createNode("span", "", getMessage("checkboxEnabled", "Enable")));
      row.appendChild(shell);
      return row;
    }

    if (field.type === "select") {
      const shell = createNode("div", "select-shell");
      const select = document.createElement("select");
      field.options.forEach(function (option) {
        const optionNode = document.createElement("option");
        optionNode.value = option;
        optionNode.textContent = getOptionLabel(field.key, option);
        select.appendChild(optionNode);
      });
      select.value = currentState[field.key];
      select.addEventListener("change", function () {
        setStatePatch(Object.fromEntries([[field.key, select.value]]), true);
      });
      inputs[field.key].select = select;
      shell.appendChild(select);
      row.appendChild(shell);
      return row;
    }

    if (field.type === "number") {
      const shell = createNode("div", "number-shell");
      const input = document.createElement("input");
      input.type = "number";
      input.min = field.min;
      input.max = field.max;
      input.step = field.step;
      input.value = currentState[field.key];
      input.addEventListener("change", function () {
        setStatePatch(Object.fromEntries([[field.key, Number(input.value)]]), true);
      });
      inputs[field.key].number = input;
      shell.appendChild(input);
      row.appendChild(shell);
      return row;
    }

    const range = document.createElement("input");
    range.type = "range";
    range.min = field.min;
    range.max = field.max;
    range.step = field.step;
    range.value = currentState[field.key];

    const shell = createNode("div", "number-shell");
    const number = document.createElement("input");
    number.type = "number";
    number.min = field.min;
    number.max = field.max;
    number.step = field.step;
    number.value = currentState[field.key];

    range.addEventListener("input", function () {
      number.value = range.value;
      setStatePatch(Object.fromEntries([[field.key, Number(range.value)]]), false);
    });

    number.addEventListener("change", function () {
      range.value = number.value;
      setStatePatch(Object.fromEntries([[field.key, Number(number.value)]]), true);
    });

    inputs[field.key].range = range;
    inputs[field.key].number = number;
    row.appendChild(range);
    shell.appendChild(number);
    row.appendChild(shell);
    return row;
  }

  function buildControls() {
    elements.controls.innerHTML = "";
    Object.keys(inputs).forEach(function (key) {
      delete inputs[key];
    });

    Params.paramGroups.forEach(function (group) {
      const section = createNode("section", "control-group");
      section.appendChild(createNode("h3", "", getGroupTitle(group)));
      const list = createNode("div", "control-list");
      group.fields.forEach(function (field) {
        list.appendChild(createControlRow(field));
      });
      section.appendChild(list);
      elements.controls.appendChild(section);
    });

    syncAllInputs();
  }

  function updateSummary(bundle) {
    elements.sceneName.textContent = bundle.scene.sceneName;
    elements.sceneSource.textContent = getMessage("source." + currentSource, currentSource);
    elements.summary.innerHTML = "";
    [
      [getMessage("summary.terrainCells", "Terrain Cells"), bundle.metrics.terrainCells],
      [getMessage("summary.roadSegments", "Road Segments"), bundle.metrics.roadSegments],
      [getMessage("summary.props", "Props"), bundle.metrics.props],
      [getMessage("summary.fences", "Fences"), bundle.metrics.fences],
      [getMessage("summary.mainFlows", "Main Flows"), bundle.metrics.mainFlows],
      [getMessage("summary.connectors", "Connectors"), bundle.metrics.connectors],
      [getMessage("summary.water", "Water Cells"), bundle.metrics.water]
    ].forEach(function (entry) {
      const dt = document.createElement("dt");
      dt.textContent = entry[0];
      const dd = document.createElement("dd");
      dd.textContent = entry[1];
      elements.summary.appendChild(dt);
      elements.summary.appendChild(dd);
    });
  }

  function renderBundle(bundle) {
    Renderer.render(elements.canvas, bundle);
    updateSummary(bundle);
    currentBundle = bundle;
  }

  function regenerate(source) {
    const bundle = Generator.buildScene(currentState);
    currentSource = source || "procedural";
    renderBundle(bundle);
    Params.writeStateToUrl(currentState);
    setStatus("rendered", {
      seed: currentState.seed,
      segments: bundle.metrics.roadSegments
    });
  }

  function downloadBlob(blob, fileName) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }

  function exportSceneJson() {
    if (!currentBundle) {
      return;
    }

    const payload = {
      version: 1,
      exportedAt: new Date().toISOString(),
      state: currentBundle.state,
      catalog: currentBundle.catalog,
      scene: currentBundle.scene
    };
    downloadBlob(new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" }), "pasture_scene_" + currentState.seed + ".json");
    setStatus("sceneJsonExported");
  }

  function validateImport(payload) {
    if (!payload || typeof payload !== "object") {
      throw new Error(getMessage("errors.jsonNotObject", "Imported file is not a JSON object."));
    }
    if (!payload.catalog || !Array.isArray(payload.catalog.elements)) {
      throw new Error(getMessage("errors.catalogMissing", "catalog.elements is missing."));
    }
    if (!payload.scene || !payload.scene.terrain || !Array.isArray(payload.scene.terrain.heights) || !Array.isArray(payload.scene.terrain.wear)) {
      throw new Error(getMessage("errors.terrainMissing", "scene.terrain is missing."));
    }
    if (!Array.isArray(payload.scene.roadSegments) || !Array.isArray(payload.scene.props) || !Array.isArray(payload.scene.fenceSegments)) {
      throw new Error(getMessage("errors.sceneArraysIncomplete", "scene arrays are incomplete."));
    }
  }

  async function importSceneJson(file) {
    if (!file) {
      return;
    }
    const text = await file.text();
    const payload = JSON.parse(text);
    validateImport(payload);
    if (payload.state) {
      currentState = Params.clampState(payload.state);
      syncAllInputs();
      Params.writeStateToUrl(currentState);
    }
    currentSource = "imported";
    renderBundle({
      state: payload.state ? Params.clampState(payload.state) : currentState,
      catalog: payload.catalog,
      scene: payload.scene,
      metrics: {
        terrainCells: payload.scene.terrain.width * payload.scene.terrain.height,
        roadSegments: payload.scene.roadSegments.length,
        props: payload.scene.props.length,
        fences: payload.scene.fenceSegments.length,
        mainFlows: Array.isArray(payload.scene.pathPolylines) ? payload.scene.pathPolylines.length : 0,
        connectors: 0,
        water: payload.scene.terrain.heights.filter(function (height) {
          return height <= payload.scene.terrain.waterLevel;
        }).length
      },
      debug: {
        mainPaths: [],
        connectors: [],
        pond: payload.scene.terrain.pond || null
      }
    });
    setStatus("importedSceneJson");
  }

  async function copyShareUrl() {
    Params.writeStateToUrl(currentState);
    await navigator.clipboard.writeText(window.location.href);
    setStatus("shareUrlCopied");
  }

  function applyLocale(rebuildControls) {
    document.documentElement.lang = getMessage("htmlLang", currentLocale);
    document.title = getMessage("pageTitle", "Pasture Trail Prototype");
    elements.appEyebrow.textContent = getMessage("appEyebrow", "Procedural Terrain");
    elements.appTitle.textContent = getMessage("appTitle", "Pasture Trail Prototype");
    elements.panelCopy.textContent = getMessage("panelCopy", "");
    elements.statusEyebrow.textContent = getMessage("statusEyebrow", "Status");
    elements.summaryEyebrow.textContent = getMessage("summaryEyebrow", "Snapshot");
    elements.canvasEyebrow.textContent = getMessage("canvasEyebrow", "Canvas");
    elements.canvasTitle.textContent = getMessage("canvasTitle", "1024 x 512 Top View");
    elements.languageLabel.textContent = getMessage("languageLabel", "Language");
    elements.randomizeSeed.textContent = getMessage("buttons.randomizeSeed", "Randomize Seed");
    elements.resetDefaults.textContent = getMessage("buttons.resetDefaults", "Reset Defaults");
    elements.regenerateScene.textContent = getMessage("buttons.regenerateScene", "Regenerate");
    elements.exportPng.textContent = getMessage("buttons.exportPng", "Export PNG");
    elements.copyShareUrl.textContent = getMessage("buttons.copyShareUrl", "Copy Share URL");
    elements.exportSceneJson.textContent = getMessage("buttons.exportSceneJson", "Export Scene JSON");
    elements.importSceneJson.textContent = getMessage("buttons.importSceneJson", "Import Scene JSON");
    elements.languageSelect.value = currentLocale;

    if (rebuildControls) {
      buildControls();
    }

    if (currentBundle) {
      updateSummary(currentBundle);
    }

    elements.statusText.textContent = formatStatus(lastStatus);
  }

  function bindActions() {
    elements.randomizeSeed.addEventListener("click", function () {
      setStatePatch({ seed: Math.floor(Math.random() * 900000) + 100000 }, true);
    });

    elements.resetDefaults.addEventListener("click", function () {
      currentState = Params.clampState(Params.defaultState);
      syncAllInputs();
      regenerate("procedural");
    });

    elements.regenerateScene.addEventListener("click", function () {
      regenerate("procedural");
    });

    elements.exportPng.addEventListener("click", function () {
      elements.canvas.toBlob(function (blob) {
        if (!blob) {
          setStatus("pngExportFailed");
          return;
        }
        downloadBlob(blob, "pasture_preview_" + currentState.seed + ".png");
        setStatus("pngExported");
      });
    });

    elements.copyShareUrl.addEventListener("click", function () {
      copyShareUrl().catch(function (error) {
        setStatus("copyFailed", { message: error.message });
      });
    });

    elements.exportSceneJson.addEventListener("click", exportSceneJson);
    elements.importSceneJson.addEventListener("click", function () {
      elements.sceneJsonInput.click();
    });
    elements.sceneJsonInput.addEventListener("change", function () {
      importSceneJson(elements.sceneJsonInput.files[0]).catch(function (error) {
        setStatus("importFailed", { message: error.message });
      }).finally(function () {
        elements.sceneJsonInput.value = "";
      });
    });

    elements.languageSelect.addEventListener("change", function () {
      currentLocale = SUPPORTED_LOCALES.indexOf(elements.languageSelect.value) >= 0 ? elements.languageSelect.value : "en";
      applyLocale(true);
    });
  }

  function init() {
    buildControls();
    bindActions();
    applyLocale(true);
    setStatus("initializing");
    regenerate("procedural");
  }

  init();
})();
