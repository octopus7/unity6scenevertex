(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});
  const Params = App.Params;

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function lerp(a, b, t) {
    return a + (b - a) * t;
  }

  function smoothstep(edge0, edge1, x) {
    const t = clamp((x - edge0) / (edge1 - edge0), 0, 1);
    return t * t * (3 - 2 * t);
  }

  function mulberry32(seed) {
    let value = seed >>> 0;
    return function () {
      value += 0x6d2b79f5;
      let mixed = value;
      mixed = Math.imul(mixed ^ (mixed >>> 15), mixed | 1);
      mixed ^= mixed + Math.imul(mixed ^ (mixed >>> 7), mixed | 61);
      return ((mixed ^ (mixed >>> 14)) >>> 0) / 4294967296;
    };
  }

  function hash2D(seed, x, y) {
    let value = seed ^ (x * 374761393) ^ (y * 668265263);
    value = (value ^ (value >> 13)) * 1274126177;
    value = value ^ (value >> 16);
    return (value >>> 0) / 4294967295;
  }

  function valueNoise(seed, x, y) {
    const x0 = Math.floor(x);
    const y0 = Math.floor(y);
    const x1 = x0 + 1;
    const y1 = y0 + 1;
    const tx = x - x0;
    const ty = y - y0;
    const sx = tx * tx * (3 - 2 * tx);
    const sy = ty * ty * (3 - 2 * ty);

    const n00 = hash2D(seed, x0, y0);
    const n10 = hash2D(seed, x1, y0);
    const n01 = hash2D(seed, x0, y1);
    const n11 = hash2D(seed, x1, y1);

    const nx0 = lerp(n00, n10, sx);
    const nx1 = lerp(n01, n11, sx);
    return lerp(nx0, nx1, sy);
  }

  function fbm(seed, x, y, octaves) {
    let amplitude = 0.5;
    let frequency = 1;
    let sum = 0;
    let weight = 0;

    for (let index = 0; index < octaves; index += 1) {
      sum += valueNoise(seed + index * 131, x * frequency, y * frequency) * amplitude;
      weight += amplitude;
      amplitude *= 0.5;
      frequency *= 2;
    }

    return sum / Math.max(weight, 0.0001);
  }

  function ridgedFbm(seed, x, y, octaves) {
    let amplitude = 0.5;
    let frequency = 1;
    let sum = 0;
    let weight = 0;

    for (let index = 0; index < octaves; index += 1) {
      const sample = fbm(seed + index * 97, x * frequency, y * frequency, 1);
      const ridge = 1 - Math.abs(sample * 2 - 1);
      sum += ridge * amplitude;
      weight += amplitude;
      amplitude *= 0.55;
      frequency *= 1.9;
    }

    return sum / Math.max(weight, 0.0001);
  }

  function terrainIndex(terrain, x, y) {
    return y * terrain.width + x;
  }

  function worldToGrid(terrain, x, y) {
    return {
      x: clamp(x / terrain.cellWidth, 0, terrain.width - 1),
      y: clamp(y / terrain.cellHeight, 0, terrain.height - 1)
    };
  }

  function gridToWorld(terrain, x, y) {
    return {
      x: x * terrain.cellWidth,
      y: y * terrain.cellHeight
    };
  }

  function sampleArrayBilinear(array, terrain, x, y) {
    const grid = worldToGrid(terrain, x, y);
    const x0 = Math.floor(grid.x);
    const y0 = Math.floor(grid.y);
    const x1 = Math.min(terrain.width - 1, x0 + 1);
    const y1 = Math.min(terrain.height - 1, y0 + 1);
    const tx = grid.x - x0;
    const ty = grid.y - y0;

    const a = array[terrainIndex(terrain, x0, y0)];
    const b = array[terrainIndex(terrain, x1, y0)];
    const c = array[terrainIndex(terrain, x0, y1)];
    const d = array[terrainIndex(terrain, x1, y1)];
    return lerp(lerp(a, b, tx), lerp(c, d, tx), ty);
  }

  function sampleHeight(terrain, x, y) {
    return sampleArrayBilinear(terrain.heights, terrain, x, y);
  }

  function sampleWear(terrain, x, y) {
    return sampleArrayBilinear(terrain.wear, terrain, x, y);
  }

  function sampleTraffic(terrain, x, y) {
    return sampleArrayBilinear(terrain.traffic, terrain, x, y);
  }

  function sampleSlope(terrain, x, y) {
    const dx = terrain.cellWidth;
    const dy = terrain.cellHeight;
    const hx0 = sampleHeight(terrain, x - dx, y);
    const hx1 = sampleHeight(terrain, x + dx, y);
    const hy0 = sampleHeight(terrain, x, y - dy);
    const hy1 = sampleHeight(terrain, x, y + dy);
    return Math.hypot(hx1 - hx0, hy1 - hy0);
  }

  function quantile(values, ratio) {
    const sorted = values.slice().sort(function (a, b) {
      return a - b;
    });
    const index = clamp(Math.floor(sorted.length * ratio), 0, sorted.length - 1);
    return sorted[index];
  }

  function chooseCoastMode(state, rng) {
    if (!state.hasPond) {
      return null;
    }

    const styleBoost = state.stylePreset === "pond-side-grazing" ? 0.42 : 0;
    const coastalChance = clamp(0.14 + styleBoost + state.pondSize * 0.12, 0, 0.75);
    if (rng() > coastalChance) {
      return null;
    }

    const edgeIndex = Math.floor(rng() * 4);
    return ["west", "east", "north", "south"][edgeIndex];
  }

  function createBasins(state, terrain, rng, coastMode) {
    const basins = [];
    const worldCenter = {
      x: Params.CANVAS_WIDTH * 0.5 + state.pondOffsetX * Params.CANVAS_WIDTH * 0.34,
      y: Params.CANVAS_HEIGHT * 0.5 + state.pondOffsetY * Params.CANVAS_HEIGHT * 0.28
    };

    const basinCount = state.hasPond
      ? (coastMode ? 2 + Math.floor(rng() * 3) : 1 + Math.floor(rng() * 2 + state.pondSize))
      : 0;

    if (coastMode) {
      const offscreen = {
        west: { x: -Params.CANVAS_WIDTH * lerp(0.06, 0.16, rng()), y: lerp(0.18, 0.82, rng()) * Params.CANVAS_HEIGHT },
        east: { x: Params.CANVAS_WIDTH * (1.06 + lerp(0.02, 0.16, rng())), y: lerp(0.18, 0.82, rng()) * Params.CANVAS_HEIGHT },
        north: { x: lerp(0.18, 0.82, rng()) * Params.CANVAS_WIDTH, y: -Params.CANVAS_HEIGHT * lerp(0.08, 0.22, rng()) },
        south: { x: lerp(0.18, 0.82, rng()) * Params.CANVAS_WIDTH, y: Params.CANVAS_HEIGHT * (1.08 + lerp(0.04, 0.18, rng())) }
      }[coastMode];

      basins.push({
        center: offscreen,
        radiusX: Params.CANVAS_WIDTH * lerp(0.34, 0.56, rng()),
        radiusY: Params.CANVAS_HEIGHT * lerp(0.24, 0.44, rng()),
        depth: lerp(0.16, 0.28, state.pondSize * 0.5),
        wobble: lerp(0.18, 0.3, rng()),
        seed: Math.floor(rng() * 100000)
      });
    }

    for (let index = 0; index < basinCount; index += 1) {
      const spreadX = coastMode ? 0.22 : 0.28;
      const spreadY = coastMode ? 0.18 : 0.24;
      const center = index === 0
        ? worldCenter
        : {
            x: worldCenter.x + lerp(-spreadX, spreadX, rng()) * Params.CANVAS_WIDTH,
            y: worldCenter.y + lerp(-spreadY, spreadY, rng()) * Params.CANVAS_HEIGHT
          };

      basins.push({
        center: {
          x: clamp(center.x, -Params.CANVAS_WIDTH * 0.12, Params.CANVAS_WIDTH * 1.12),
          y: clamp(center.y, -Params.CANVAS_HEIGHT * 0.12, Params.CANVAS_HEIGHT * 1.12)
        },
        radiusX: Params.CANVAS_WIDTH * lerp(0.08, 0.18, rng()) * state.pondSize,
        radiusY: Params.CANVAS_HEIGHT * lerp(0.08, 0.22, rng()) * state.pondSize,
        depth: lerp(0.08, 0.2, rng()) * (0.8 + state.pondSize * 0.35),
        wobble: lerp(0.14, 0.28, rng()),
        seed: Math.floor(rng() * 100000)
      });
    }

    return basins;
  }

  function basinInfluence(worldX, worldY, basin) {
    const dx = (worldX - basin.center.x) / basin.radiusX;
    const dy = (worldY - basin.center.y) / basin.radiusY;
    const warp = (fbm(basin.seed, dx * 2.1 + 8, dy * 2.1 + 13, 2) - 0.5) * basin.wobble;
    const radial = Math.hypot(dx, dy) * (1 + warp);
    if (radial >= 1.6) {
      return 0;
    }
    return (1 - smoothstep(0.38, 1.12, radial)) * basin.depth;
  }

  function coastDrop(worldX, worldY, terrain, coastMode, seed, intensity) {
    if (!coastMode) {
      return 0;
    }

    const nx = worldX / Params.CANVAS_WIDTH;
    const ny = worldY / Params.CANVAS_HEIGHT;
    let coastCoord = 0;

    switch (coastMode) {
      case "west":
        coastCoord = nx;
        break;
      case "east":
        coastCoord = 1 - nx;
        break;
      case "north":
        coastCoord = ny;
        break;
      case "south":
        coastCoord = 1 - ny;
        break;
    }

    const coastWarp = (fbm(seed + 701, nx * 3.4, ny * 3.4, 3) - 0.5) * 0.16;
    const reach = clamp(coastCoord + coastWarp, 0, 1);
    return (1 - smoothstep(0.08, 0.74, reach)) * intensity;
  }

  function analyzeWater(terrain) {
    const visited = new Array(terrain.width * terrain.height).fill(false);
    const shorelineCells = [];
    const waterBodies = [];
    const steps = [[-1, 0], [1, 0], [0, -1], [0, 1]];

    for (let y = 0; y < terrain.height; y += 1) {
      for (let x = 0; x < terrain.width; x += 1) {
        const index = terrainIndex(terrain, x, y);
        if (visited[index] || !terrain.waterMask[index]) {
          continue;
        }

        const queue = [{ x: x, y: y }];
        const cells = [];
        const shoreline = [];
        visited[index] = true;

        while (queue.length) {
          const current = queue.pop();
          const currentIndex = terrainIndex(terrain, current.x, current.y);
          cells.push(current);
          let touchesLand = false;

          for (let stepIndex = 0; stepIndex < steps.length; stepIndex += 1) {
            const step = steps[stepIndex];
            const nextX = current.x + step[0];
            const nextY = current.y + step[1];
            if (nextX < 0 || nextY < 0 || nextX >= terrain.width || nextY >= terrain.height) {
              continue;
            }

            const nextIndex = terrainIndex(terrain, nextX, nextY);
            if (terrain.waterMask[nextIndex] && !visited[nextIndex]) {
              visited[nextIndex] = true;
              queue.push({ x: nextX, y: nextY });
            }

            if (!terrain.waterMask[nextIndex]) {
              touchesLand = true;
              shoreline.push({ x: nextX, y: nextY });
            }
          }

          if (touchesLand) {
            shorelineCells.push(current);
          }
        }

        let centroidX = 0;
        let centroidY = 0;
        cells.forEach(function (cell) {
          centroidX += cell.x;
          centroidY += cell.y;
        });

        waterBodies.push({
          area: cells.length,
          centroid: {
            x: centroidX / Math.max(cells.length, 1),
            y: centroidY / Math.max(cells.length, 1)
          },
          shoreline: shoreline
        });
      }
    }

    waterBodies.sort(function (a, b) {
      return b.area - a.area;
    });

    terrain.waterBodies = waterBodies;
    terrain.shorelineCells = shorelineCells;
  }

  function createTerrain(state) {
    const width = 160;
    const height = 90;
    const rng = mulberry32((state.seed ^ 0x7e57d123) >>> 0);
    const coastMode = chooseCoastMode(state, rng);
    const terrain = {
      width: width,
      height: height,
      cellWidth: Params.CANVAS_WIDTH / (width - 1),
      cellHeight: Params.CANVAS_HEIGHT / (height - 1),
      wetBand: lerp(0.018, 0.045, clamp(state.sandAmount * 0.5, 0, 1)),
      dryBand: lerp(0.05, 0.095, clamp(state.sandAmount * 0.5 + 0.12, 0, 1)),
      pebbleBand: lerp(0.072, 0.138, clamp(state.pebbleAmount * 0.5 + 0.08, 0, 1)),
      heights: new Array(width * height),
      wear: new Array(width * height).fill(0),
      traffic: new Array(width * height).fill(0),
      waterMask: new Array(width * height).fill(false),
      waterLevel: 0.4,
      basins: [],
      coastline: coastMode,
      waterBodies: [],
      shorelineCells: []
    };

    terrain.basins = createBasins(state, terrain, rng, coastMode);

    for (let y = 0; y < height; y += 1) {
      for (let x = 0; x < width; x += 1) {
        const nx = x / (width - 1);
        const ny = y / (height - 1);
        const world = gridToWorld(terrain, x, y);
        const warpX = (fbm(state.seed + 101, nx * 2.2, ny * 2.2, 3) - 0.5) * 0.2;
        const warpY = (fbm(state.seed + 151, nx * 2.2, ny * 2.2, 3) - 0.5) * 0.2;
        const wx = nx + warpX;
        const wy = ny + warpY;
        const rolling = (fbm(state.seed + 11, wx * 3, wy * 3, 5) - 0.5) * 0.22;
        const ridges = (ridgedFbm(state.seed + 41, wx * 5.1, wy * 5.1, 3) - 0.5) * 0.12;
        const detail = (fbm(state.seed + 59, wx * 12.6, wy * 12.6, 2) - 0.5) * 0.05;
        const edge = 1 - Math.min(Math.min(nx, 1 - nx), Math.min(ny, 1 - ny)) * 2;
        const edgeRise = smoothstep(0.18, 1, edge) * lerp(0.03, 0.14, state.edgeGroveStrength);
        const centerOpening = (1 - smoothstep(0.16, 0.88, Math.hypot(nx - 0.5, ny - 0.5) * 1.62)) * state.centerOpenness * 0.055;
        let heightValue = 0.56 + rolling + ridges + detail + edgeRise - centerOpening;

        heightValue -= coastDrop(world.x, world.y, terrain, coastMode, state.seed, lerp(0.08, 0.26, state.pondSize * 0.5));
        terrain.basins.forEach(function (basin) {
          heightValue -= basinInfluence(world.x, world.y, basin);
        });

        terrain.heights[terrainIndex(terrain, x, y)] = clamp(heightValue, 0, 1);
      }
    }

    const targetCoverage = state.hasPond
      ? clamp(
          (coastMode ? 0.13 : 0.025) +
          state.pondSize * (coastMode ? 0.12 : 0.055) +
          (state.stylePreset === "pond-side-grazing" ? 0.06 : 0),
          0.02,
          coastMode ? 0.34 : 0.18
        )
      : 0.006;
    terrain.waterLevel = quantile(terrain.heights, targetCoverage);

    for (let index = 0; index < terrain.heights.length; index += 1) {
      terrain.waterMask[index] = terrain.heights[index] <= terrain.waterLevel;
    }

    analyzeWater(terrain);
    return terrain;
  }

  function addTraffic(terrain, polyline, radius, amount) {
    const cellRadiusX = Math.ceil(radius / terrain.cellWidth);
    const cellRadiusY = Math.ceil(radius / terrain.cellHeight);

    polyline.forEach(function (point) {
      const grid = worldToGrid(terrain, point.x, point.y);
      const centerX = Math.round(grid.x);
      const centerY = Math.round(grid.y);

      for (let offsetY = -cellRadiusY; offsetY <= cellRadiusY; offsetY += 1) {
        for (let offsetX = -cellRadiusX; offsetX <= cellRadiusX; offsetX += 1) {
          const x = centerX + offsetX;
          const y = centerY + offsetY;
          if (x < 0 || y < 0 || x >= terrain.width || y >= terrain.height) {
            continue;
          }

          const world = gridToWorld(terrain, x, y);
          const distance = Math.hypot(world.x - point.x, world.y - point.y);
          if (distance > radius) {
            continue;
          }

          const influence = 1 - distance / radius;
          terrain.traffic[terrainIndex(terrain, x, y)] += influence * amount;
        }
      }
    });
  }

  function erodeAlongPolyline(terrain, polyline, radius, depth) {
    const cellRadiusX = Math.ceil(radius / terrain.cellWidth);
    const cellRadiusY = Math.ceil(radius / terrain.cellHeight);

    polyline.forEach(function (point) {
      const grid = worldToGrid(terrain, point.x, point.y);
      const centerX = Math.round(grid.x);
      const centerY = Math.round(grid.y);

      for (let offsetY = -cellRadiusY; offsetY <= cellRadiusY; offsetY += 1) {
        for (let offsetX = -cellRadiusX; offsetX <= cellRadiusX; offsetX += 1) {
          const x = centerX + offsetX;
          const y = centerY + offsetY;
          if (x < 0 || y < 0 || x >= terrain.width || y >= terrain.height) {
            continue;
          }

          const world = gridToWorld(terrain, x, y);
          const distance = Math.hypot(world.x - point.x, world.y - point.y);
          if (distance > radius) {
            continue;
          }

          const influence = 1 - distance / radius;
          const index = terrainIndex(terrain, x, y);
          terrain.heights[index] = clamp(terrain.heights[index] - influence * depth, 0, 1);
          terrain.wear[index] = clamp(terrain.wear[index] + influence * 0.24, 0, 1);
        }
      }
    });
  }

  function isWaterCell(terrain, x, y, thresholdOffset) {
    return terrain.heights[terrainIndex(terrain, x, y)] <= terrain.waterLevel + (thresholdOffset || 0);
  }

  App.Heightmap = {
    createTerrain: createTerrain,
    terrainIndex: terrainIndex,
    gridToWorld: gridToWorld,
    worldToGrid: worldToGrid,
    sampleHeight: sampleHeight,
    sampleWear: sampleWear,
    sampleTraffic: sampleTraffic,
    sampleSlope: sampleSlope,
    addTraffic: addTraffic,
    erodeAlongPolyline: erodeAlongPolyline,
    isWaterCell: isWaterCell
  };
})();
