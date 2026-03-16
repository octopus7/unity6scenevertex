(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});
  const Params = App.Params;
  const Generator = App.Generator;
  const Renderer = App.Renderer;

  let currentState = Params.parseStateFromLocation(window.location.href);
  let currentBundle = null;
  let currentSource = "procedural";
  let frameHandle = 0;
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
    sceneJsonInput: document.getElementById("scene-json-input")
  };

  function setStatus(message) {
    elements.statusText.textContent = message;
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

  function updateDisplayedValue(key) {
    const field = Params.fieldMap()[key];
    const entry = inputs[key];
    if (!field || !entry) {
      return;
    }
    if (entry.value) {
      entry.value.textContent = Params.formatValue(field, currentState[key]);
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
    head.appendChild(createNode("label", "", field.label));
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
      shell.appendChild(createNode("span", "", "Enable"));
      row.appendChild(shell);
      return row;
    }

    if (field.type === "select") {
      const shell = createNode("div", "select-shell");
      const select = document.createElement("select");
      field.options.forEach(function (option) {
        const optionNode = document.createElement("option");
        optionNode.value = option;
        optionNode.textContent = option;
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
    Params.paramGroups.forEach(function (group) {
      const section = createNode("section", "control-group");
      section.appendChild(createNode("h3", "", group.title));
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
    elements.sceneSource.textContent = currentSource;
    elements.summary.innerHTML = "";
    [
      ["Terrain Cells", bundle.metrics.terrainCells],
      ["Road Segments", bundle.metrics.roadSegments],
      ["Props", bundle.metrics.props],
      ["Fences", bundle.metrics.fences],
      ["Main Flows", bundle.metrics.mainFlows],
      ["Connectors", bundle.metrics.connectors],
      ["Water Cells", bundle.metrics.water]
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
    setStatus("Rendered seed " + currentState.seed + " with " + bundle.metrics.roadSegments + " road segments.");
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
    setStatus("Scene JSON exported.");
  }

  function validateImport(payload) {
    if (!payload || typeof payload !== "object") {
      throw new Error("Imported file is not a JSON object.");
    }
    if (!payload.catalog || !Array.isArray(payload.catalog.elements)) {
      throw new Error("catalog.elements is missing.");
    }
    if (!payload.scene || !payload.scene.terrain || !Array.isArray(payload.scene.terrain.heights) || !Array.isArray(payload.scene.terrain.wear)) {
      throw new Error("scene.terrain is missing.");
    }
    if (!Array.isArray(payload.scene.roadSegments) || !Array.isArray(payload.scene.props) || !Array.isArray(payload.scene.fenceSegments)) {
      throw new Error("scene arrays are incomplete.");
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
        water: payload.scene.terrain.heights.filter(function (height) { return height <= payload.scene.terrain.waterLevel; }).length
      },
      debug: {
        mainPaths: [],
        connectors: [],
        pond: payload.scene.terrain.pond || null
      }
    });
    setStatus("Imported scene JSON.");
  }

  async function copyShareUrl() {
    Params.writeStateToUrl(currentState);
    await navigator.clipboard.writeText(window.location.href);
    setStatus("Share URL copied.");
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
          setStatus("PNG export failed.");
          return;
        }
        downloadBlob(blob, "pasture_preview_" + currentState.seed + ".png");
        setStatus("PNG exported.");
      });
    });

    elements.copyShareUrl.addEventListener("click", function () {
      copyShareUrl().catch(function (error) {
        setStatus("Copy failed: " + error.message);
      });
    });

    elements.exportSceneJson.addEventListener("click", exportSceneJson);
    elements.importSceneJson.addEventListener("click", function () {
      elements.sceneJsonInput.click();
    });
    elements.sceneJsonInput.addEventListener("change", function () {
      importSceneJson(elements.sceneJsonInput.files[0]).catch(function (error) {
        setStatus("Import failed: " + error.message);
      }).finally(function () {
        elements.sceneJsonInput.value = "";
      });
    });
  }

  function init() {
    buildControls();
    bindActions();
    regenerate("procedural");
  }

  init();
})();
