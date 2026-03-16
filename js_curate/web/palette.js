(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});
  const Params = App.Params;

  const DEFAULT_VARIANTS = {
    grass: 1,
    soil: 0,
    sand: 2,
    pebble: 1,
    water: 0,
    road: 1,
    tree: 0,
    rock: 1,
    bush: 2,
    mushroom: 0,
    fence: 2
  };

  const BASE_ELEMENTS = [
    { id: "water", layer: "ground", shape: "terrain", baseRadius: 21, variants: [] },
    { id: "pebble", layer: "ground", shape: "terrain", baseRadius: 10.5, variants: [] },
    { id: "sand", layer: "ground", shape: "terrain", baseRadius: 14, variants: [] },
    { id: "soil", layer: "ground", shape: "terrain", baseRadius: 14.5, variants: [] },
    { id: "grass", layer: "ground", shape: "terrain", baseRadius: 24, variants: [] },
    { id: "road", layer: "road", shape: "segment", segmentLength: 18, segmentThickness: 5.8, variants: [] },
    { id: "tree", layer: "prop", shape: "tree", baseRadius: 6.5, variants: [] },
    { id: "rock", layer: "prop", shape: "rock", baseRadius: 4.5, variants: [] },
    { id: "bush", layer: "prop", shape: "bush", baseRadius: 5, variants: [] },
    { id: "mushroom", layer: "prop", shape: "mushroom", baseRadius: 1.75, variants: [] },
    { id: "fence", layer: "fence", shape: "fence", segmentLength: 8.5, segmentThickness: 2, variants: [] }
  ];

  const PALETTES = {
    "soft-olive": {
      grass: ["#92ab78", "#86a26b", "#75945d"],
      soil: ["#7f6543", "#725739", "#654a31"],
      sand: ["#d6c08e", "#cab17d", "#bd9e6a"],
      pebble: ["#9c9688", "#8f887c", "#7f776d"],
      water: ["#648a95", "#597d87", "#4f717a"],
      road: ["#927652", "#836849", "#745a3f"],
      tree: ["#5f7540", "#566c3a", "#6e8650"],
      rock: ["#80796d", "#736d63", "#91897e"],
      bush: ["#6a8350", "#5d7647", "#72905e"],
      mushroom: ["#b86a4e", "#a45a42", "#c47c65"],
      fence: ["#916f46", "#815f39", "#9e7d52"]
    },
    "dry-pasture": {
      grass: ["#b1ad70", "#a39e64", "#928d56"],
      soil: ["#8b603f", "#785130", "#684428"],
      sand: ["#dec58d", "#d2b77f", "#c4a56e"],
      pebble: ["#a19a88", "#948d7b", "#867f6f"],
      water: ["#7a8f8f", "#6d8282", "#627777"],
      road: ["#a9845a", "#966f4d", "#845d40"],
      tree: ["#787c44", "#696d3d", "#8a8d58"],
      rock: ["#938676", "#857867", "#a39a89"],
      bush: ["#888957", "#75774b", "#989a63"],
      mushroom: ["#c08356", "#ab7349", "#ce9570"],
      fence: ["#9e7642", "#8c6638", "#ad8752"]
    },
    "cool-morning": {
      grass: ["#82a78f", "#759d85", "#678e78"],
      soil: ["#766759", "#685b4f", "#5b4f45"],
      sand: ["#c7c2af", "#b8b39f", "#aaa48f"],
      pebble: ["#8e9790", "#818981", "#727a71"],
      water: ["#5e859c", "#54798f", "#4c6d82"],
      road: ["#837767", "#756a5c", "#675d51"],
      tree: ["#4d684f", "#456048", "#58745c"],
      rock: ["#7b807d", "#6f7470", "#8d928e"],
      bush: ["#63805f", "#587457", "#6e8b6d"],
      mushroom: ["#a8675d", "#96584f", "#b9776b"],
      fence: ["#81745f", "#716451", "#8f816a"]
    }
  };

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function createCatalog(state) {
    const palette = PALETTES[state.palettePreset] || PALETTES["soft-olive"];
    return {
      canvasWidth: Params.CANVAS_WIDTH,
      canvasHeight: Params.CANVAS_HEIGHT,
      elements: BASE_ELEMENTS.map(function (element) {
        const copy = clone(element);
        copy.variants = palette[element.id].slice();
        return copy;
      })
    };
  }

  function createTypeVariants(selection) {
    const source = selection || DEFAULT_VARIANTS;
    return Object.keys(DEFAULT_VARIANTS).map(function (id) {
      return {
        id: id,
        variantIndex: typeof source[id] === "number" ? source[id] : DEFAULT_VARIANTS[id]
      };
    });
  }

  function getElementById(catalog, id) {
    return (catalog.elements || []).find(function (element) {
      return element.id === id;
    }) || null;
  }

  function createColorMap(catalog, typeVariants) {
    const selected = {};
    (typeVariants || []).forEach(function (entry) {
      selected[entry.id] = entry.variantIndex;
    });

    const result = {};
    (catalog.elements || []).forEach(function (element) {
      const index = Math.max(0, Math.min(element.variants.length - 1, selected[element.id] || 0));
      result[element.id] = element.variants[index];
    });
    return result;
  }

  App.Palette = {
    DEFAULT_VARIANTS: DEFAULT_VARIANTS,
    PALETTES: PALETTES,
    createCatalog: createCatalog,
    createTypeVariants: createTypeVariants,
    createColorMap: createColorMap,
    getElementById: getElementById
  };
})();
