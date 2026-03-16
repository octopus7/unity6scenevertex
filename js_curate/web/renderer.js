(function () {
  const App = (window.TrailPrototype = window.TrailPrototype || {});
  const Params = App.Params;
  const Palette = App.Palette;
  const Heightmap = App.Heightmap;

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function hexToRgb(hex) {
    const value = hex.replace("#", "");
    const normalized = value.length === 3
      ? value.split("").map(function (part) { return part + part; }).join("")
      : value;
    const numeric = parseInt(normalized, 16);
    return {
      r: (numeric >> 16) & 255,
      g: (numeric >> 8) & 255,
      b: numeric & 255
    };
  }

  function mixColor(a, b, t) {
    return {
      r: Math.round(a.r + (b.r - a.r) * t),
      g: Math.round(a.g + (b.g - a.g) * t),
      b: Math.round(a.b + (b.b - a.b) * t)
    };
  }

  function scaleColor(color, factor) {
    return {
      r: clamp(Math.round(color.r * factor), 0, 255),
      g: clamp(Math.round(color.g * factor), 0, 255),
      b: clamp(Math.round(color.b * factor), 0, 255)
    };
  }

  function pixelHash(seed, x, y) {
    let value = seed ^ (x * 374761393) ^ (y * 668265263);
    value = (value ^ (value >> 13)) * 1274126177;
    value = value ^ (value >> 16);
    return ((value >>> 0) / 4294967295) * 2 - 1;
  }

  function drawTerrain(ctx, bundle) {
    const terrain = bundle.scene.terrain;
    const colors = Palette.createColorMap(bundle.catalog, bundle.scene.typeVariants);
    const rgb = Object.keys(colors).reduce(function (result, key) {
      result[key] = hexToRgb(colors[key]);
      return result;
    }, {});

    const imageData = ctx.createImageData(Params.CANVAS_WIDTH, Params.CANVAS_HEIGHT);
    const pixels = imageData.data;
    const wetSand = scaleColor(rgb.sand, 0.76);
    const drySand = rgb.sand;
    const grass = rgb.grass;
    const pebble = rgb.pebble;
    const soil = rgb.soil;
    const waterDeep = scaleColor(rgb.water, 0.82);
    const waterShallow = scaleColor(rgb.water, 1.06);

    for (let y = 0; y < Params.CANVAS_HEIGHT; y += 1) {
      for (let x = 0; x < Params.CANVAS_WIDTH; x += 1) {
        const height = Heightmap.sampleHeight(terrain, x, y);
        const wear = Heightmap.sampleWear(terrain, x, y);
        const slope = Heightmap.sampleSlope(terrain, x, y);
        let color;

        if (height <= terrain.waterLevel) {
          const depth = clamp((terrain.waterLevel - height) / Math.max(terrain.waterLevel, 0.001), 0, 1);
          color = mixColor(waterShallow, waterDeep, depth);
        } else if (height <= terrain.waterLevel + terrain.wetBand) {
          const t = clamp((height - terrain.waterLevel) / Math.max(terrain.wetBand, 0.001), 0, 1);
          color = mixColor(wetSand, drySand, t * 0.45);
        } else if (height <= terrain.waterLevel + terrain.dryBand) {
          const t = clamp((height - (terrain.waterLevel + terrain.wetBand)) / Math.max(terrain.dryBand - terrain.wetBand, 0.001), 0, 1);
          color = mixColor(drySand, grass, t * 0.18);
        } else {
          color = grass;
        }

        if (height > terrain.waterLevel + terrain.wetBand && height <= terrain.waterLevel + terrain.pebbleBand && slope > 0.012) {
          color = mixColor(color, pebble, clamp((slope - 0.012) * 22, 0, 0.4));
        }

        if (wear > 0.02 && height > terrain.waterLevel + terrain.wetBand * 0.35) {
          color = mixColor(color, soil, clamp(wear * bundle.state.soilAmount * 1.3, 0, 0.78));
        }

        const light = 1 + (pixelHash(bundle.state.seed + 71, x, y) * 0.06 * bundle.state.textureNoise);
        const slopeShade = 1 - clamp(slope * 6 * bundle.state.shadowStrength, 0, 0.22);
        const finalColor = scaleColor(color, light * slopeShade);
        const index = (y * Params.CANVAS_WIDTH + x) * 4;
        pixels[index] = finalColor.r;
        pixels[index + 1] = finalColor.g;
        pixels[index + 2] = finalColor.b;
        pixels[index + 3] = 255;
      }
    }

    ctx.putImageData(imageData, 0, 0);
  }

  function drawRoads(ctx, bundle) {
    const roadColor = Palette.createColorMap(bundle.catalog, bundle.scene.typeVariants).road;
    bundle.scene.roadSegments.forEach(function (segment) {
      const road = Palette.getElementById(bundle.catalog, "road");
      const length = road.segmentLength * segment.lengthScale;
      const thickness = road.segmentThickness * segment.thicknessScale;
      const radians = segment.rotationDeg * Math.PI / 180;
      const dx = Math.cos(radians) * length * 0.5;
      const dy = Math.sin(radians) * length * 0.5;

      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      ctx.strokeStyle = "rgba(0,0,0," + (segment.opacity * (0.03 + bundle.state.shadowStrength * 0.035)).toFixed(3) + ")";
      ctx.lineWidth = thickness * 1.02;
      ctx.beginPath();
      ctx.moveTo(segment.x - dx + 3.5, segment.y - dy + 4.5);
      ctx.lineTo(segment.x + dx + 3.5, segment.y + dy + 4.5);
      ctx.stroke();

      ctx.strokeStyle = roadColor;
      ctx.globalAlpha = segment.opacity * 0.45;
      ctx.lineWidth = thickness * 0.92;
      ctx.beginPath();
      ctx.moveTo(segment.x - dx, segment.y - dy);
      ctx.lineTo(segment.x + dx, segment.y + dy);
      ctx.stroke();

      ctx.strokeStyle = "rgba(255,255,255," + (segment.opacity * 0.06).toFixed(3) + ")";
      ctx.lineWidth = thickness * 0.22;
      ctx.beginPath();
      ctx.moveTo(segment.x - dx - 1.5, segment.y - dy - 1.5);
      ctx.lineTo(segment.x + dx - 1.5, segment.y + dy - 1.5);
      ctx.stroke();
      ctx.globalAlpha = 1;
    });
  }

  function drawTree(ctx, prop, color, shadowStrength) {
    const radius = 6.5 * prop.radiusScale;
    ctx.fillStyle = "rgba(0,0,0," + (prop.opacity * (0.08 + shadowStrength * 0.08)).toFixed(3) + ")";
    ctx.beginPath();
    ctx.ellipse(prop.x + 6, prop.y + 8, radius * 1.05, radius * 0.8, prop.rotationDeg * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();

    ["#000000"].forEach(function () {
      ctx.fillStyle = color;
      [
        { x: -0.48, y: -0.1, sx: 0.8, sy: 0.74 },
        { x: 0.36, y: -0.28, sx: 0.72, sy: 0.68 },
        { x: 0.08, y: 0.32, sx: 0.77, sy: 0.74 }
      ].forEach(function (part, index) {
        ctx.globalAlpha = prop.opacity * (0.86 - index * 0.06);
        ctx.beginPath();
        ctx.ellipse(prop.x + part.x * radius, prop.y + part.y * radius, radius * part.sx, radius * part.sy, (prop.rotationDeg + index * 17) * Math.PI / 180, 0, Math.PI * 2);
        ctx.fill();
      });
      ctx.globalAlpha = 1;
    });
  }

  function drawRock(ctx, prop, color, shadowStrength) {
    const radius = 4.5 * prop.radiusScale;
    ctx.fillStyle = "rgba(0,0,0," + (prop.opacity * (0.08 + shadowStrength * 0.04)).toFixed(3) + ")";
    ctx.beginPath();
    ctx.ellipse(prop.x + 4, prop.y + 5, radius * 0.95, radius * 0.68, prop.rotationDeg * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = color;
    ctx.globalAlpha = prop.opacity * 0.9;
    ctx.beginPath();
    ctx.ellipse(prop.x - radius * 0.16, prop.y + radius * 0.02, radius * 0.84, radius * 0.62, (prop.rotationDeg + 18) * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(prop.x + radius * 0.24, prop.y - radius * 0.18, radius * 0.58, radius * 0.48, (prop.rotationDeg - 24) * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 1;
  }

  function drawBush(ctx, prop, color, shadowStrength) {
    const radius = 5 * prop.radiusScale;
    ctx.fillStyle = "rgba(0,0,0," + (prop.opacity * (0.06 + shadowStrength * 0.04)).toFixed(3) + ")";
    ctx.beginPath();
    ctx.ellipse(prop.x + 4, prop.y + 4, radius, radius * 0.72, prop.rotationDeg * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = color;
    ctx.globalAlpha = prop.opacity * 0.82;
    [
      { x: -0.32, y: 0.05, sx: 0.55, sy: 0.48 },
      { x: 0.28, y: -0.18, sx: 0.48, sy: 0.45 },
      { x: 0.12, y: 0.26, sx: 0.52, sy: 0.48 }
    ].forEach(function (part, index) {
      ctx.beginPath();
      ctx.ellipse(prop.x + part.x * radius, prop.y + part.y * radius, radius * part.sx, radius * part.sy, (prop.rotationDeg + index * 12) * Math.PI / 180, 0, Math.PI * 2);
      ctx.fill();
    });
    ctx.globalAlpha = 1;
  }

  function drawMushroom(ctx, prop, color, shadowStrength) {
    const radius = 1.75 * prop.radiusScale;
    ctx.fillStyle = "rgba(0,0,0," + (prop.opacity * (0.05 + shadowStrength * 0.03)).toFixed(3) + ")";
    ctx.beginPath();
    ctx.ellipse(prop.x + 1.2, prop.y + 1.6, radius * 1.1, radius * 0.7, prop.rotationDeg * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "rgba(235,226,209," + (prop.opacity * 0.34).toFixed(3) + ")";
    ctx.beginPath();
    ctx.ellipse(prop.x, prop.y + 1.1, radius * 0.42, radius * 0.32, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = color;
    ctx.globalAlpha = prop.opacity * 0.9;
    ctx.beginPath();
    ctx.ellipse(prop.x, prop.y - 0.8, radius * 0.95, radius * 0.72, prop.rotationDeg * Math.PI / 180, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 1;
  }

  function drawFence(ctx, segment, color, shadowStrength) {
    const length = 8.5 * segment.lengthScale;
    const thickness = 2;
    const radians = segment.rotationDeg * Math.PI / 180;
    const dx = Math.cos(radians) * length * 0.5;
    const dy = Math.sin(radians) * length * 0.5;
    const px = -Math.sin(radians) * thickness * 0.45;
    const py = Math.cos(radians) * thickness * 0.45;

    ctx.lineCap = "round";
    ctx.strokeStyle = "rgba(0,0,0," + (segment.opacity * (0.05 + shadowStrength * 0.04)).toFixed(3) + ")";
    ctx.lineWidth = thickness * 1.25;
    ctx.beginPath();
    ctx.moveTo(segment.x - dx + 2, segment.y - dy + 3);
    ctx.lineTo(segment.x + dx + 2, segment.y + dy + 3);
    ctx.stroke();

    ctx.strokeStyle = color;
    ctx.globalAlpha = segment.opacity * 0.9;
    ctx.lineWidth = thickness * 0.42;
    ctx.beginPath();
    ctx.moveTo(segment.x - dx + px, segment.y - dy + py);
    ctx.lineTo(segment.x + dx + px, segment.y + dy + py);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(segment.x - dx - px, segment.y - dy - py);
    ctx.lineTo(segment.x + dx - px, segment.y + dy - py);
    ctx.stroke();
    ctx.globalAlpha = 1;
  }

  function drawProps(ctx, bundle) {
    const colors = Palette.createColorMap(bundle.catalog, bundle.scene.typeVariants);
    const shadowStrength = bundle.state.shadowStrength;
    bundle.scene.props.filter(function (prop) { return prop.id === "rock"; }).forEach(function (prop) {
      drawRock(ctx, prop, colors.rock, shadowStrength);
    });
    bundle.scene.props.filter(function (prop) { return prop.id === "bush"; }).forEach(function (prop) {
      drawBush(ctx, prop, colors.bush, shadowStrength);
    });
    bundle.scene.props.filter(function (prop) { return prop.id === "mushroom"; }).forEach(function (prop) {
      drawMushroom(ctx, prop, colors.mushroom, shadowStrength);
    });
    bundle.scene.props.filter(function (prop) { return prop.id === "tree"; }).forEach(function (prop) {
      drawTree(ctx, prop, colors.tree, shadowStrength);
    });
    bundle.scene.fenceSegments.forEach(function (segment) {
      drawFence(ctx, segment, colors.fence, shadowStrength);
    });
  }

  function drawDebug(ctx, bundle) {
    if (!bundle.state.debugOverlay) {
      return;
    }

    ctx.save();
    ctx.lineWidth = 1.4;
    ctx.strokeStyle = "rgba(186,85,36,0.68)";
    (bundle.debug.mainPaths || []).forEach(function (polyline) {
      ctx.beginPath();
      polyline.forEach(function (point, index) {
        if (index === 0) {
          ctx.moveTo(point.x, point.y);
        } else {
          ctx.lineTo(point.x, point.y);
        }
      });
      ctx.stroke();
    });

    ctx.strokeStyle = "rgba(96,74,154,0.58)";
    (bundle.debug.connectors || []).forEach(function (polyline) {
      ctx.beginPath();
      polyline.forEach(function (point, index) {
        if (index === 0) {
          ctx.moveTo(point.x, point.y);
        } else {
          ctx.lineTo(point.x, point.y);
        }
      });
      ctx.stroke();
    });

    ctx.fillStyle = "rgba(39,114,176,0.45)";
    (bundle.debug.shoreline || []).forEach(function (point) {
      ctx.fillRect(point.x - 1, point.y - 1, 2, 2);
    });
    ctx.restore();
  }

  function render(canvas, bundle) {
    const ctx = canvas.getContext("2d");
    drawTerrain(ctx, bundle);
    drawRoads(ctx, bundle);
    drawProps(ctx, bundle);
    drawDebug(ctx, bundle);
  }

  App.Renderer = {
    render: render
  };
})();
