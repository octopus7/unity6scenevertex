(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});

  const CANVAS_WIDTH = 1024;
  const CANVAS_HEIGHT = 512;

  const defaultState = {
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

  const paramGroups = [
    {
      title: "Core",
      fields: [
        { key: "seed", label: "Seed", type: "number", min: 1, max: 999999, step: 1, precision: 0 },
        { key: "stylePreset", label: "Style Preset", type: "select", options: ["meadow-trails", "open-pasture", "pond-side-grazing", "edge-grove"] },
        { key: "palettePreset", label: "Palette", type: "select", options: ["soft-olive", "dry-pasture", "cool-morning"] },
        { key: "density", label: "Density", type: "slider", min: 0.25, max: 4, step: 0.05, precision: 2 },
        { key: "elementScale", label: "Element Scale", type: "slider", min: 0.25, max: 2, step: 0.05, precision: 2 }
      ]
    },
    {
      title: "Paths",
      fields: [
        { key: "pathNetworkCount", label: "Main Flows", type: "slider", min: 2, max: 6, step: 1, precision: 0 },
        { key: "pathConnectorCount", label: "Connectors", type: "slider", min: 1, max: 5, step: 1, precision: 0 },
        { key: "pathLoopChance", label: "Loop Chance", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 },
        { key: "pathWidth", label: "Path Width", type: "slider", min: 0.5, max: 2, step: 0.02, precision: 2 },
        { key: "pathFade", label: "Path Fade", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 },
        { key: "pathCurviness", label: "Curviness", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 },
        { key: "pathBranchBias", label: "Branch Bias", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 }
      ]
    },
    {
      title: "Terrain",
      fields: [
        { key: "hasPond", label: "Has Pond", type: "checkbox" },
        { key: "pondSize", label: "Pond Size", type: "slider", min: 0.5, max: 2, step: 0.02, precision: 2 },
        { key: "pondOffsetX", label: "Pond Offset X", type: "slider", min: -0.5, max: 0.5, step: 0.01, precision: 2 },
        { key: "pondOffsetY", label: "Pond Offset Y", type: "slider", min: -0.5, max: 0.5, step: 0.01, precision: 2 },
        { key: "sandAmount", label: "Sand Amount", type: "slider", min: 0, max: 2, step: 0.02, precision: 2 },
        { key: "pebbleAmount", label: "Pebble Amount", type: "slider", min: 0, max: 2, step: 0.02, precision: 2 },
        { key: "soilAmount", label: "Soil Amount", type: "slider", min: 0, max: 2, step: 0.02, precision: 2 },
        { key: "grassVariation", label: "Grass Variation", type: "slider", min: 0, max: 2, step: 0.02, precision: 2 },
        { key: "pondEdgeDetail", label: "Pond Edge Detail", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 }
      ]
    },
    {
      title: "Vegetation",
      fields: [
        { key: "treeCount", label: "Trees", type: "slider", min: 0, max: 300, step: 1, precision: 0 },
        { key: "rockCount", label: "Rocks", type: "slider", min: 0, max: 240, step: 1, precision: 0 },
        { key: "bushCount", label: "Bushes", type: "slider", min: 0, max: 320, step: 1, precision: 0 },
        { key: "mushroomCount", label: "Mushrooms", type: "slider", min: 0, max: 320, step: 1, precision: 0 },
        { key: "fenceCount", label: "Fence Segments", type: "slider", min: 0, max: 240, step: 1, precision: 0 },
        { key: "edgeGroveStrength", label: "Edge Grove", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 },
        { key: "centerOpenness", label: "Center Openness", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 }
      ]
    },
    {
      title: "Render",
      fields: [
        { key: "shadowStrength", label: "Shadow Strength", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 },
        { key: "textureNoise", label: "Texture Noise", type: "slider", min: 0, max: 1, step: 0.01, precision: 2 },
        { key: "debugOverlay", label: "Debug Overlay", type: "checkbox" }
      ]
    }
  ];

  function cloneState(state) {
    return Object.assign({}, state);
  }

  function flattenFields() {
    return paramGroups.flatMap(function (group) {
      return group.fields;
    });
  }

  function fieldMap() {
    const result = {};
    flattenFields().forEach(function (field) {
      result[field.key] = field;
    });
    return result;
  }

  function roundTo(value, step, precision) {
    return Number((Math.round(value / step) * step).toFixed(precision));
  }

  function clampFieldValue(field, value) {
    if (field.type === "checkbox") {
      return Boolean(value);
    }

    if (field.type === "select") {
      return field.options.indexOf(value) >= 0 ? value : defaultState[field.key];
    }

    const numeric = Number.isFinite(Number(value)) ? Number(value) : Number(defaultState[field.key]);
    const clamped = Math.min(field.max, Math.max(field.min, numeric));
    return roundTo(clamped, field.step || 1, field.precision || 0);
  }

  function clampState(rawState) {
    const fields = fieldMap();
    const next = cloneState(defaultState);
    Object.keys(fields).forEach(function (key) {
      next[key] = clampFieldValue(fields[key], rawState && key in rawState ? rawState[key] : defaultState[key]);
    });
    return next;
  }

  function parseStateFromLocation(locationLike) {
    const fields = fieldMap();
    const url = new URL(typeof locationLike === "string" ? locationLike : locationLike.href, window.location.href);
    const raw = cloneState(defaultState);
    Object.keys(fields).forEach(function (key) {
      if (!url.searchParams.has(key)) {
        return;
      }
      const field = fields[key];
      const value = url.searchParams.get(key);
      if (field.type === "checkbox") {
        raw[key] = value === "1" || value === "true";
        return;
      }
      raw[key] = field.type === "select" ? value : Number(value);
    });
    return clampState(raw);
  }

  function writeStateToUrl(state) {
    const safeState = clampState(state);
    const url = new URL(window.location.href);
    flattenFields().forEach(function (field) {
      const value = safeState[field.key];
      const defaultValue = defaultState[field.key];
      const changed = field.type === "checkbox"
        ? value !== defaultValue
        : String(value) !== String(defaultValue);

      if (!changed) {
        url.searchParams.delete(field.key);
        return;
      }

      url.searchParams.set(field.key, field.type === "checkbox" ? (value ? "1" : "0") : String(value));
    });
    history.replaceState(null, "", url.toString());
  }

  function formatValue(field, value) {
    if (field.type === "checkbox") {
      return value ? "on" : "off";
    }
    if (field.type === "select") {
      return String(value);
    }
    return Number(value).toFixed(field.precision || 0);
  }

  App.Params = {
    CANVAS_WIDTH: CANVAS_WIDTH,
    CANVAS_HEIGHT: CANVAS_HEIGHT,
    defaultState: defaultState,
    paramGroups: paramGroups,
    cloneState: cloneState,
    flattenFields: flattenFields,
    fieldMap: fieldMap,
    clampState: clampState,
    parseStateFromLocation: parseStateFromLocation,
    writeStateToUrl: writeStateToUrl,
    formatValue: formatValue
  };
})();
