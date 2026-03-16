use std::collections::HashMap;

use crate::{
    constants::{BITMAP_SIZE, STROKE_TILE_SIZE},
    tool::StrokeAction,
};

#[derive(Clone, Copy, Debug)]
pub(super) struct StrokePixelState {
    pub base: f32,
    pub coverage: f32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
struct TileCoord {
    x: usize,
    y: usize,
}

struct StrokeTile {
    origin_x: usize,
    origin_y: usize,
    width: usize,
    height: usize,
    base: Vec<f32>,
    coverage: Vec<f32>,
}

pub(super) struct StrokeSession {
    action: StrokeAction,
    tiles: HashMap<TileCoord, StrokeTile>,
    changed: bool,
}

impl StrokeSession {
    pub(super) fn new(action: StrokeAction) -> Self {
        Self {
            action,
            tiles: HashMap::new(),
            changed: false,
        }
    }

    pub(super) fn action(&self) -> StrokeAction {
        self.action
    }

    pub(super) fn changed(&self) -> bool {
        self.changed
    }

    pub(super) fn mark_changed(&mut self) {
        self.changed = true;
    }

    pub(super) fn original_value(&mut self, heightmap: &[f32], x: usize, y: usize) -> f32 {
        let tile = self.ensure_tile(heightmap, x, y);
        let index = tile.local_index(x, y);
        tile.base[index]
    }

    pub(super) fn accumulate(
        &mut self,
        heightmap: &[f32],
        x: usize,
        y: usize,
        amount: f32,
    ) -> StrokePixelState {
        let tile = self.ensure_tile(heightmap, x, y);
        let index = tile.local_index(x, y);
        tile.coverage[index] = (tile.coverage[index] + amount).clamp(0.0, 1.0);
        StrokePixelState {
            base: tile.base[index],
            coverage: tile.coverage[index],
        }
    }

    fn ensure_tile<'a>(&'a mut self, heightmap: &[f32], x: usize, y: usize) -> &'a mut StrokeTile {
        let coord = Self::tile_coord(x, y);
        self.tiles
            .entry(coord)
            .or_insert_with(|| StrokeTile::capture(heightmap, coord))
    }

    fn tile_coord(x: usize, y: usize) -> TileCoord {
        TileCoord {
            x: x / STROKE_TILE_SIZE,
            y: y / STROKE_TILE_SIZE,
        }
    }
}

impl StrokeTile {
    fn capture(heightmap: &[f32], coord: TileCoord) -> Self {
        let origin_x = coord.x * STROKE_TILE_SIZE;
        let origin_y = coord.y * STROKE_TILE_SIZE;
        let width = (BITMAP_SIZE - origin_x).min(STROKE_TILE_SIZE);
        let height = (BITMAP_SIZE - origin_y).min(STROKE_TILE_SIZE);
        let mut base = vec![0.0; width * height];

        for row in 0..height {
            let src_offset = (origin_y + row) * BITMAP_SIZE + origin_x;
            let dst_offset = row * width;
            base[dst_offset..dst_offset + width]
                .copy_from_slice(&heightmap[src_offset..src_offset + width]);
        }

        Self {
            origin_x,
            origin_y,
            width,
            height,
            base,
            coverage: vec![0.0; width * height],
        }
    }

    fn local_index(&self, x: usize, y: usize) -> usize {
        debug_assert!(x >= self.origin_x && x < self.origin_x + self.width);
        debug_assert!(y >= self.origin_y && y < self.origin_y + self.height);
        (y - self.origin_y) * self.width + (x - self.origin_x)
    }
}
