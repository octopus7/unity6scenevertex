(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});
  const Params = App.Params;
  const Palette = App.Palette;
  const Heightmap = App.Heightmap;

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

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function lerp(a, b, t) {
    return a + (b - a) * t;
  }

  function distance(a, b) {
    return Math.hypot(a.x - b.x, a.y - b.y);
  }

  function hash2D(seed, x, y) {
    let value = seed ^ (x * 374761393) ^ (y * 668265263);
    value = (value ^ (value >> 13)) * 1274126177;
    value = value ^ (value >> 16);
    return (value >>> 0) / 4294967295;
  }

  function chaikin(points, iterations) {
    let current = points.slice();
    for (let step = 0; step < iterations; step += 1) {
      const next = [current[0]];
      for (let index = 0; index < current.length - 1; index += 1) {
        const a = current[index];
        const b = current[index + 1];
        next.push({ x: lerp(a.x, b.x, 0.25), y: lerp(a.y, b.y, 0.25) });
        next.push({ x: lerp(a.x, b.x, 0.75), y: lerp(a.y, b.y, 0.75) });
      }
      next.push(current[current.length - 1]);
      current = next;
    }
    return current;
  }

  function resamplePolyline(points, spacing) {
    if (points.length < 2) {
      return points.slice();
    }

    const result = [points[0]];
    let remaining = spacing;

    for (let index = 1; index < points.length; index += 1) {
      let from = points[index - 1];
      let to = points[index];
      let segmentLength = distance(from, to);
      if (segmentLength < 0.001) {
        continue;
      }

      while (segmentLength >= remaining) {
        const t = remaining / segmentLength;
        const sample = {
          x: lerp(from.x, to.x, t),
          y: lerp(from.y, to.y, t)
        };
        result.push(sample);
        from = sample;
        segmentLength = distance(from, to);
        remaining = spacing;
      }

      remaining -= segmentLength;
    }

    if (distance(result[result.length - 1], points[points.length - 1]) > spacing * 0.35) {
      result.push(points[points.length - 1]);
    }

    return result;
  }

  function terrainToScene(terrain) {
    return {
      width: terrain.width,
      height: terrain.height,
      cellWidth: terrain.cellWidth,
      cellHeight: terrain.cellHeight,
      waterLevel: terrain.waterLevel,
      wetBand: terrain.wetBand,
      dryBand: terrain.dryBand,
      pebbleBand: terrain.pebbleBand,
      coastline: terrain.coastline,
      waterBodies: terrain.waterBodies.map(function (body) {
        return {
          area: body.area,
          centroid: body.centroid
        };
      }),
      heights: terrain.heights.slice(),
      wear: terrain.wear.slice()
    };
  }

  function cellToPoint(terrain, cell) {
    return Heightmap.gridToWorld(terrain, cell.x, cell.y);
  }

  function pointToCell(terrain, point) {
    const grid = Heightmap.worldToGrid(terrain, point.x, point.y);
    return {
      x: Math.round(grid.x),
      y: Math.round(grid.y)
    };
  }

  function pointLineDistance(point, start, end) {
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const lengthSquared = dx * dx + dy * dy;
    const t = clamp(lengthSquared ? ((point.x - start.x) * dx + (point.y - start.y) * dy) / lengthSquared : 0, 0, 1);
    const projection = {
      x: start.x + dx * t,
      y: start.y + dy * t
    };
    return distance(point, projection);
  }

  function collectEdgeCandidates(terrain) {
    const candidates = [];

    function maybePush(x, y, side) {
      if (Heightmap.isWaterCell(terrain, x, y, terrain.wetBand * 0.2)) {
        return;
      }
      const point = cellToPoint(terrain, { x: x, y: y });
      const height = Heightmap.sampleHeight(terrain, point.x, point.y);
      const slope = Heightmap.sampleSlope(terrain, point.x, point.y);
      const score = (height - terrain.waterLevel) * 4 - slope * 24 + hash2D(9137, x, y) * 0.12;
      candidates.push({
        cell: { x: x, y: y },
        point: point,
        side: side,
        score: score
      });
    }

    for (let x = 4; x < terrain.width - 4; x += 2) {
      maybePush(x, 3, "north");
      maybePush(x, terrain.height - 4, "south");
    }

    for (let y = 4; y < terrain.height - 4; y += 2) {
      maybePush(3, y, "west");
      maybePush(terrain.width - 4, y, "east");
    }

    return candidates;
  }

  function collectShoreCandidates(terrain) {
    const candidates = [];
    terrain.waterBodies.forEach(function (body, bodyIndex) {
      if (body.area < 12) {
        return;
      }

      body.shoreline.forEach(function (shoreCell, shorelineIndex) {
        if (shorelineIndex % 3 !== 0) {
          return;
        }
        if (Heightmap.isWaterCell(terrain, shoreCell.x, shoreCell.y, terrain.wetBand * 0.15)) {
          return;
        }
        const point = cellToPoint(terrain, shoreCell);
        const slope = Heightmap.sampleSlope(terrain, point.x, point.y);
        const edgeDistance = Math.min(point.x, Params.CANVAS_WIDTH - point.x, point.y, Params.CANVAS_HEIGHT - point.y);
        const score = body.area * 0.02 - slope * 18 + clamp(edgeDistance / 120, 0, 1);
        candidates.push({
          cell: shoreCell,
          point: point,
          bodyIndex: bodyIndex,
          score: score
        });
      });
    });
    return candidates;
  }

  function collectMeadowCandidates(terrain, state) {
    const candidates = [];
    for (let y = 8; y < terrain.height - 8; y += 2) {
      for (let x = 8; x < terrain.width - 8; x += 2) {
        if (Heightmap.isWaterCell(terrain, x, y, terrain.dryBand * 0.2)) {
          continue;
        }
        const point = cellToPoint(terrain, { x: x, y: y });
        const height = Heightmap.sampleHeight(terrain, point.x, point.y);
        const slope = Heightmap.sampleSlope(terrain, point.x, point.y);
        const edgeDistance = Math.min(point.x, Params.CANVAS_WIDTH - point.x, point.y, Params.CANVAS_HEIGHT - point.y);
        const centerDistance = Math.hypot(point.x - Params.CANVAS_WIDTH * 0.5, point.y - Params.CANVAS_HEIGHT * 0.5);
        const score = edgeDistance * 0.002 + centerDistance * state.centerOpenness * 0.001 - slope * 28 + (height - terrain.waterLevel) * 2;
        candidates.push({
          cell: { x: x, y: y },
          point: point,
          score: score
        });
      }
    }
    return candidates;
  }

  function selectDiverseCandidates(candidates, count, minDistance, rng) {
    const pool = candidates.slice().sort(function (a, b) {
      return (b.score + rng() * 0.08) - (a.score + rng() * 0.08);
    });
    const selected = [];
    let threshold = minDistance;

    while (selected.length < count && threshold >= minDistance * 0.35) {
      for (let index = 0; index < pool.length && selected.length < count; index += 1) {
        const candidate = pool[index];
        const farEnough = selected.every(function (picked) {
          return distance(candidate.point, picked.point) >= threshold;
        });
        if (!farEnough) {
          continue;
        }
        selected.push(candidate);
      }
      threshold *= 0.75;
    }

    return selected.slice(0, count);
  }

  function nearestCandidate(source, candidates, filter) {
    let best = null;
    let bestDistance = Infinity;
    candidates.forEach(function (candidate) {
      if (filter && !filter(candidate)) {
        return;
      }
      const candidateDistance = distance(source.point || source, candidate.point || candidate);
      if (candidateDistance < bestDistance) {
        bestDistance = candidateDistance;
        best = candidate;
      }
    });
    return best;
  }

  function chooseViaPoint(start, end, candidates, terrain, rng, shoreSeeking) {
    const direct = distance(start.point, end.point);
    if (direct < 180 || !candidates.length || rng() > 0.72) {
      return null;
    }

    let best = null;
    let bestScore = 26;

    candidates.forEach(function (candidate) {
      const detour = distance(start.point, candidate.point) + distance(candidate.point, end.point) - direct;
      if (detour > 220) {
        return;
      }
      const bend = pointLineDistance(candidate.point, start.point, end.point);
      const height = Heightmap.sampleHeight(terrain, candidate.point.x, candidate.point.y);
      const shoreHeight = terrain.waterLevel + terrain.wetBand + terrain.dryBand * 0.6;
      const shorelineAffinity = shoreSeeking ? (1 - Math.abs(height - shoreHeight) * 10) * 18 : 0;
      const score = bend - detour * 0.38 + shorelineAffinity;
      if (score > bestScore) {
        bestScore = score;
        best = candidate;
      }
    });

    return best;
  }

  function routePath(terrain, start, end, options) {
    const width = terrain.width;
    const height = terrain.height;
    const size = width * height;
    const gScore = new Array(size).fill(Infinity);
    const fScore = new Array(size).fill(Infinity);
    const cameFrom = new Array(size).fill(-1);
    const open = [];
    const closed = new Array(size).fill(false);
    const steps = [
      [-1, 0], [1, 0], [0, -1], [0, 1],
      [-1, -1], [1, -1], [-1, 1], [1, 1]
    ];

    function nodeIndex(x, y) {
      return y * width + x;
    }

    function heuristic(x, y) {
      return Math.hypot(end.x - x, end.y - y);
    }

    const startIndex = nodeIndex(start.x, start.y);
    gScore[startIndex] = 0;
    fScore[startIndex] = heuristic(start.x, start.y);
    open.push(startIndex);

    while (open.length) {
      let bestOpen = 0;
      for (let index = 1; index < open.length; index += 1) {
        if (fScore[open[index]] < fScore[open[bestOpen]]) {
          bestOpen = index;
        }
      }

      const currentIndex = open.splice(bestOpen, 1)[0];
      const currentX = currentIndex % width;
      const currentY = Math.floor(currentIndex / width);
      if (currentX === end.x && currentY === end.y) {
        const path = [];
        let cursor = currentIndex;
        while (cursor >= 0) {
          path.push({ x: cursor % width, y: Math.floor(cursor / width) });
          cursor = cameFrom[cursor];
        }
        return path.reverse();
      }

      closed[currentIndex] = true;

      for (let stepIndex = 0; stepIndex < steps.length; stepIndex += 1) {
        const step = steps[stepIndex];
        const nextX = currentX + step[0];
        const nextY = currentY + step[1];
        if (nextX < 0 || nextY < 0 || nextX >= width || nextY >= height) {
          continue;
        }

        const nextIndex = nodeIndex(nextX, nextY);
        if (closed[nextIndex]) {
          continue;
        }

        const nextHeight = terrain.heights[nextIndex];
        if (nextHeight <= terrain.waterLevel + terrain.wetBand * 0.08) {
          continue;
        }

        const currentHeight = terrain.heights[currentIndex];
        const moveCost = stepIndex < 4 ? 1 : 1.4;
        const slopePenalty = Math.abs(nextHeight - currentHeight) * 56;
        const wetPenalty = nextHeight <= terrain.waterLevel + terrain.wetBand ? 10 : 0;
        const edgeDistance = Math.min(nextX, width - 1 - nextX, nextY, height - 1 - nextY);
        const edgePenalty = edgeDistance < 4 ? (4 - edgeDistance) * options.edgePenalty : 0;
        const trafficBoost = 1 + Math.min(terrain.traffic[nextIndex], 3.5) * 0.24 * options.trafficAttraction;
        const shoreTargetHeight = terrain.waterLevel + terrain.wetBand + terrain.dryBand * 0.58;
        const shorelinePenalty = Math.abs(nextHeight - shoreTargetHeight) * 18 * options.shoreSeeking;
        const wanderPenalty = hash2D(options.noiseSeed, nextX, nextY) * options.wander;
        const tentativeG = gScore[currentIndex] + (moveCost + slopePenalty + wetPenalty + edgePenalty + shorelinePenalty + wanderPenalty) / trafficBoost;

        if (tentativeG >= gScore[nextIndex]) {
          continue;
        }

        cameFrom[nextIndex] = currentIndex;
        gScore[nextIndex] = tentativeG;
        fScore[nextIndex] = tentativeG + heuristic(nextX, nextY);
        if (open.indexOf(nextIndex) === -1) {
          open.push(nextIndex);
        }
      }
    }

    return [];
  }

  function combineRouteLegs(terrain, cells) {
    const unique = [];
    cells.forEach(function (cell, index) {
      if (!index || cell.x !== cells[index - 1].x || cell.y !== cells[index - 1].y) {
        unique.push(cell);
      }
    });
    const worldPoints = unique.map(function (cell) {
      return cellToPoint(terrain, cell);
    });
    return resamplePolyline(chaikin(worldPoints, 2), 8.5);
  }

  function buildRoute(terrain, start, end, via, options) {
    const legs = [];
    const points = [start].concat(via ? [via] : [], [end]);
    for (let index = 0; index < points.length - 1; index += 1) {
      const cells = routePath(terrain, points[index].cell, points[index + 1].cell, options);
      if (!cells.length) {
        return null;
      }
      if (index > 0) {
        cells.shift();
      }
      legs.push.apply(legs, cells);
    }
    return combineRouteLegs(terrain, legs);
  }

  function applyRouteImpact(terrain, polyline, route) {
    Heightmap.addTraffic(terrain, polyline, route.trafficRadius, route.trafficAmount);
    Heightmap.erodeAlongPolyline(terrain, polyline, route.erodeRadius, route.erodeDepth);
  }

  function polylineToSegments(polyline, route, terrain, catalog, state) {
    const road = Palette.getElementById(catalog, "road");
    const segments = [];
    for (let index = 1; index < polyline.length; index += 1) {
      const from = polyline[index - 1];
      const to = polyline[index];
      const length = distance(from, to);
      if (length < 4) {
        continue;
      }
      const mid = { x: (from.x + to.x) * 0.5, y: (from.y + to.y) * 0.5 };
      const traffic = Heightmap.sampleTraffic(terrain, mid.x, mid.y);
      const wear = Heightmap.sampleWear(terrain, mid.x, mid.y);
      const widthScale = route.width * lerp(0.82, 1.28, clamp(traffic / 2.8, 0, 1)) * lerp(0.92, 1.1, hash2D(route.seed, Math.round(mid.x), Math.round(mid.y)));
      const opacity = route.opacity * lerp(0.72, 1.08, clamp(wear / 0.32, 0, 1));

      segments.push({
        x: mid.x,
        y: mid.y,
        rotationDeg: Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI,
        lengthScale: Number((length / road.segmentLength).toFixed(3)),
        thicknessScale: Number((widthScale * state.pathWidth * state.elementScale).toFixed(3)),
        opacity: Number(clamp(opacity - state.pathFade * 0.1, 0.28, 0.96).toFixed(3))
      });
    }
    return segments;
  }

  function buildTrailNetwork(terrain, state, catalog) {
    const rng = mulberry32((state.seed ^ 0x9b3f29d7) >>> 0);
    const edgeCandidates = collectEdgeCandidates(terrain);
    const shoreCandidates = collectShoreCandidates(terrain);
    const meadowCandidates = collectMeadowCandidates(terrain, state);
    const entryCount = clamp(Math.round(state.pathNetworkCount), 2, 5);
    const connectorCount = clamp(Math.round(state.pathConnectorCount), 1, 5);
    const entries = selectDiverseCandidates(edgeCandidates, entryCount + 1, 110, rng);
    const shores = selectDiverseCandidates(shoreCandidates, Math.min(5, 2 + Math.round(state.pondSize * 2)), 90, rng);
    const meadows = selectDiverseCandidates(meadowCandidates, 6, 110, rng);
    const routes = [];
    const mainRoutes = [];
    const connectorRoutes = [];

    function pushRoute(kind, start, end, options, viaPool) {
      const via = chooseViaPoint(start, end, viaPool || [], terrain, rng, options.shoreSeeking > 0);
      const routeSeed = Math.floor(rng() * 100000);
      const polyline = buildRoute(terrain, start, end, via, {
        noiseSeed: routeSeed,
        trafficAttraction: options.trafficAttraction,
        shoreSeeking: options.shoreSeeking,
        edgePenalty: options.edgePenalty,
        wander: options.wander
      });
      if (!polyline || polyline.length < 3) {
        return;
      }

      const route = {
        kind: kind,
        seed: routeSeed,
        polyline: polyline,
        width: options.width,
        opacity: options.opacity,
        trafficRadius: options.trafficRadius,
        trafficAmount: options.trafficAmount,
        erodeRadius: options.erodeRadius,
        erodeDepth: options.erodeDepth
      };
      applyRouteImpact(terrain, polyline, route);
      routes.push(route);
      if (kind === "main") {
        mainRoutes.push(route);
      } else {
        connectorRoutes.push(route);
      }
    }

    entries.slice(0, entryCount).forEach(function (entry, index) {
      const shoreTarget = shores.length ? nearestCandidate(entry, shores, function (candidate) {
        return candidate.bodyIndex === 0 || shores.length === 1;
      }) : null;
      const meadowTarget = meadows.length ? nearestCandidate(entry, meadows) : null;
      const primaryTarget = shoreTarget || meadowTarget || entries[(index + 1) % entries.length];
      pushRoute("main", entry, primaryTarget, {
        width: lerp(0.92, 1.12, rng()),
        opacity: lerp(0.7, 0.86, rng()),
        trafficRadius: 24,
        trafficAmount: 1.18,
        erodeRadius: 18,
        erodeDepth: 0.012 * state.soilAmount,
        trafficAttraction: 0.8,
        shoreSeeking: shoreTarget ? 0.2 : 0,
        edgePenalty: 1.8,
        wander: 0.55
      }, meadows);

      if (meadowTarget && meadowTarget !== primaryTarget && rng() < 0.72) {
        pushRoute("main", entry, meadowTarget, {
          width: lerp(0.78, 0.98, rng()),
          opacity: lerp(0.54, 0.72, rng()),
          trafficRadius: 20,
          trafficAmount: 0.94,
          erodeRadius: 15,
          erodeDepth: 0.009 * state.soilAmount,
          trafficAttraction: 1,
          shoreSeeking: 0,
          edgePenalty: 1.6,
          wander: 0.72
        }, shores);
      }
    });

    for (let index = 0; index < connectorCount; index += 1) {
      const shoreStart = shores.length ? shores[index % shores.length] : null;
      const meadowStart = meadows.length ? meadows[index % meadows.length] : null;
      const start = shoreStart && rng() < 0.58 ? shoreStart : meadowStart || entries[index % entries.length];
      const end = shoreStart && meadows.length
        ? meadows[(index + 2) % meadows.length]
        : entries[(index + 1) % entries.length];
      pushRoute("connector", start, end, {
        width: lerp(0.64, 0.86, rng()),
        opacity: lerp(0.42, 0.64, rng()),
        trafficRadius: 16,
        trafficAmount: 0.74,
        erodeRadius: 12,
        erodeDepth: 0.006 * state.soilAmount,
        trafficAttraction: 1.15,
        shoreSeeking: shoreStart ? 0.18 : 0,
        edgePenalty: 1.1,
        wander: 0.88
      }, meadows.concat(shores));
    }

    if (state.pathLoopChance > 0.2 && entries.length >= 2 && shores.length) {
      pushRoute("connector", shores[0], entries[entries.length - 1], {
        width: 0.7,
        opacity: 0.52,
        trafficRadius: 16,
        trafficAmount: 0.7,
        erodeRadius: 12,
        erodeDepth: 0.005 * state.soilAmount,
        trafficAttraction: 1.22,
        shoreSeeking: 0.16,
        edgePenalty: 0.8,
        wander: 0.94
      }, meadows);
    }

    return {
      entries: entries,
      shores: shores,
      meadows: meadows,
      routes: routes,
      mainRoutes: mainRoutes,
      connectorRoutes: connectorRoutes,
      roadSegments: routes.flatMap(function (route) {
        return polylineToSegments(route.polyline, route, terrain, catalog, state);
      })
    };
  }

  function distanceToPolylines(point, polylines) {
    let minimum = Infinity;
    polylines.forEach(function (polyline) {
      for (let index = 1; index < polyline.length; index += 1) {
        const start = polyline[index - 1];
        const end = polyline[index];
        minimum = Math.min(minimum, pointLineDistance(point, start, end));
      }
    });
    return minimum;
  }

  function minDistanceToProps(point, props, ids) {
    let minimum = Infinity;
    props.forEach(function (prop) {
      if (ids && ids.indexOf(prop.id) === -1) {
        return;
      }
      minimum = Math.min(minimum, distance(point, prop));
    });
    return minimum;
  }

  function placeProps(scene, terrain, routes, state) {
    const rng = mulberry32((state.seed ^ 0xa53a9b1d) >>> 0);
    const props = scene.props;
    const polylines = routes.routes.map(function (route) {
      return route.polyline;
    });
    const density = Math.pow(state.density, 0.82);
    const center = { x: Params.CANVAS_WIDTH * 0.5, y: Params.CANVAS_HEIGHT * 0.5 };

    function isDry(point) {
      return Heightmap.sampleHeight(terrain, point.x, point.y) > terrain.waterLevel + terrain.dryBand;
    }

    function centerPenalty(point) {
      return Math.abs(point.x - center.x) < 170 * state.centerOpenness && Math.abs(point.y - center.y) < 100 * state.centerOpenness;
    }

    function randomPolylinePoint() {
      if (!polylines.length) {
        return null;
      }
      const polyline = polylines[Math.floor(rng() * polylines.length)];
      return polyline[Math.floor(rng() * polyline.length)];
    }

    function tryPlace(targetCount, id, sampler, validator, scaleRange, opacityRange) {
      let placed = 0;
      let attempts = 0;
      while (placed < targetCount && attempts < targetCount * 32 + 180) {
        const point = sampler();
        attempts += 1;
        if (!point || !validator(point)) {
          continue;
        }
        point.id = id;
        point.radiusScale = Number((lerp(scaleRange[0], scaleRange[1], rng()) * state.elementScale).toFixed(3));
        point.rotationDeg = Number((rng() * 360 - 180).toFixed(2));
        point.opacity = Number(lerp(opacityRange[0], opacityRange[1], rng()).toFixed(3));
        props.push(point);
        placed += 1;
      }
    }

    tryPlace(Math.round(state.treeCount * density * 0.7), "tree", function () {
      const edge = rng() < 0.74;
      return edge
        ? {
            x: rng() < 0.5 ? lerp(18, 140, rng()) : lerp(Params.CANVAS_WIDTH - 140, Params.CANVAS_WIDTH - 18, rng()),
            y: lerp(20, Params.CANVAS_HEIGHT - 20, rng())
          }
        : {
            x: lerp(20, Params.CANVAS_WIDTH - 20, rng()),
            y: rng() < 0.5 ? lerp(18, 96, rng()) : lerp(Params.CANVAS_HEIGHT - 96, Params.CANVAS_HEIGHT - 18, rng())
          };
    }, function (point) {
      return isDry(point) && !centerPenalty(point) && distanceToPolylines(point, polylines) > 18 && minDistanceToProps(point, props) > 14;
    }, [1.1, 1.9], [0.72, 0.96]);

    tryPlace(Math.round(state.rockCount * density * 0.74), "rock", function () {
      const pick = routes.shores.length && rng() < 0.52
        ? routes.shores[Math.floor(rng() * routes.shores.length)].point
        : randomPolylinePoint();
      if (!pick) {
        return null;
      }
      return {
        x: pick.x + lerp(-26, 26, rng()),
        y: pick.y + lerp(-22, 22, rng())
      };
    }, function (point) {
      const height = Heightmap.sampleHeight(terrain, point.x, point.y);
      return height > terrain.waterLevel + terrain.wetBand * 0.4 && minDistanceToProps(point, props) > 8 && distanceToPolylines(point, polylines) < 44;
    }, [0.8, 1.48], [0.6, 0.9]);

    tryPlace(Math.round(state.bushCount * density * 0.78), "bush", function () {
      const anchor = randomPolylinePoint();
      if (!anchor) {
        return null;
      }
      return {
        x: anchor.x + lerp(-48, 48, rng()),
        y: anchor.y + lerp(-42, 42, rng())
      };
    }, function (point) {
      return isDry(point) && distanceToPolylines(point, polylines) > 8 && distanceToPolylines(point, polylines) < 58 && minDistanceToProps(point, props) > 8;
    }, [0.8, 1.34], [0.56, 0.84]);

    tryPlace(Math.round(state.mushroomCount * density * 0.72), "mushroom", function () {
      const hosts = props.filter(function (prop) {
        return prop.id === "tree" || prop.id === "bush";
      });
      if (!hosts.length) {
        return null;
      }
      const host = hosts[Math.floor(rng() * hosts.length)];
      return {
        x: host.x + lerp(-18, 18, rng()),
        y: host.y + lerp(-16, 16, rng())
      };
    }, function (point) {
      return isDry(point) && distanceToPolylines(point, polylines) > 7 && minDistanceToProps(point, props, ["mushroom"]) > 4.5;
    }, [0.88, 1.25], [0.7, 0.96]);
  }

  function placeFences(scene, terrain, routes, state) {
    const rng = mulberry32((state.seed ^ 0x7f4a7c15) >>> 0);
    const count = Math.round(state.fenceCount * Math.pow(state.density, 0.72) * 0.52);
    const landmarks = routes.entries.concat(routes.meadows).map(function (entry) {
      return entry.point;
    });

    for (let index = 0; index < count; index += 1) {
      const anchor = landmarks.length ? landmarks[Math.floor(rng() * landmarks.length)] : { x: Params.CANVAS_WIDTH * 0.5, y: Params.CANVAS_HEIGHT * 0.5 };
      const point = {
        x: clamp(anchor.x + lerp(-120, 120, rng()), 24, Params.CANVAS_WIDTH - 24),
        y: clamp(anchor.y + lerp(-90, 90, rng()), 24, Params.CANVAS_HEIGHT - 24)
      };

      if (Heightmap.sampleHeight(terrain, point.x, point.y) <= terrain.waterLevel + terrain.wetBand * 0.6) {
        continue;
      }

      scene.fenceSegments.push({
        x: point.x,
        y: point.y,
        rotationDeg: Number(lerp(-18, 18, rng()).toFixed(2)),
        lengthScale: Number((lerp(0.7, 1.36, rng()) * state.elementScale).toFixed(3)),
        opacity: Number(lerp(0.24, 0.54, rng()).toFixed(3))
      });
    }
  }

  function buildScene(state) {
    const catalog = Palette.createCatalog(state);
    const terrain = Heightmap.createTerrain(state);
    const routes = buildTrailNetwork(terrain, state, catalog);
    const scene = {
      sceneName: "PastureTrailPreview",
      typeVariants: Palette.createTypeVariants(Palette.DEFAULT_VARIANTS),
      terrain: terrainToScene(terrain),
      pathPolylines: routes.routes.map(function (route) {
        return route.polyline;
      }),
      roadSegments: routes.roadSegments,
      props: [],
      fenceSegments: []
    };

    placeProps(scene, terrain, routes, state);
    placeFences(scene, terrain, routes, state);

    return {
      state: Params.cloneState(state),
      catalog: catalog,
      scene: scene,
      metrics: {
        terrainCells: terrain.width * terrain.height,
        roadSegments: scene.roadSegments.length,
        props: scene.props.length,
        fences: scene.fenceSegments.length,
        mainFlows: routes.mainRoutes.length,
        connectors: routes.connectorRoutes.length,
        water: terrain.waterBodies.reduce(function (sum, body) {
          return sum + body.area;
        }, 0)
      },
      debug: {
        mainPaths: routes.mainRoutes.map(function (route) { return route.polyline; }),
        connectors: routes.connectorRoutes.map(function (route) { return route.polyline; }),
        shoreline: terrain.shorelineCells.slice(0, 800).map(function (cell) {
          return cellToPoint(terrain, cell);
        })
      }
    };
  }

  App.Generator = {
    buildScene: buildScene
  };
})();
