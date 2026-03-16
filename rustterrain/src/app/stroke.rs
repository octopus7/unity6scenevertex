use std::collections::HashMap;

use eframe::egui::Pos2;

use crate::{
    constants::{BITMAP_SIZE, STROKE_TILE_SIZE},
    tool::StrokeAction,
};

use super::history::TilePatch;

#[allow(dead_code)]
#[derive(Clone, Debug)]
pub(super) struct ReplayStrokeRecord {
    pub sequence: u64,
    pub tool: StrokeAction,
    pub flow: f32,
    pub opacity: f32,
    pub radius: f32,
    pub target_height: f32,
    pub polyline: Vec<[f32; 2]>,
}

#[derive(Clone, Copy, Debug)]
pub(super) struct StrokePixelState {
    pub base: f32,
    pub coverage: f32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord)]
struct TileCoord {
    x: usize,
    y: usize,
}

struct StrokeTile {
    coord: TileCoord,
    origin_x: usize,
    origin_y: usize,
    width: usize,
    height: usize,
    base: Vec<f32>,
    coverage: Vec<f32>,
    dirty: bool,
}

pub(super) struct StrokeSession {
    action: StrokeAction,
    tiles: HashMap<TileCoord, StrokeTile>,
    changed: bool,
    polyline: Vec<[f32; 2]>,
}

impl StrokeSession {
    pub(super) fn new(action: StrokeAction) -> Self {
        Self {
            action,
            tiles: HashMap::new(),
            changed: false,
            polyline: Vec::new(),
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

    pub(super) fn mark_tile_dirty(&mut self, heightmap: &[f32], x: usize, y: usize) {
        self.mark_changed();
        let tile = self.ensure_tile(heightmap, x, y);
        tile.dirty = true;
    }

    pub(super) fn record_segment(&mut self, from: Pos2, to: Pos2) {
        self.push_polyline_point(from);
        self.push_polyline_point(to);
    }

    pub(super) fn build_replay_record(
        &self,
        flow: f32,
        opacity: f32,
        radius: f32,
        target_height: f32,
    ) -> ReplayStrokeRecord {
        ReplayStrokeRecord {
            sequence: 0,
            tool: self.action,
            flow,
            opacity,
            radius,
            target_height,
            polyline: self.polyline.clone(),
        }
    }

    pub(super) fn export_tile_patches(&self, heightmap: &[f32]) -> Vec<TilePatch> {
        let mut coords: Vec<_> = self.tiles.keys().copied().collect();
        coords.sort();

        let mut patches = Vec::new();
        for coord in coords {
            let Some(tile) = self.tiles.get(&coord) else {
                continue;
            };
            if !tile.dirty {
                continue;
            }

            let mut after = vec![0.0; tile.width * tile.height];
            for row in 0..tile.height {
                let src_offset = (tile.origin_y + row) * BITMAP_SIZE + tile.origin_x;
                let dst_offset = row * tile.width;
                after[dst_offset..dst_offset + tile.width]
                    .copy_from_slice(&heightmap[src_offset..src_offset + tile.width]);
            }

            let patch = TilePatch::new(
                (tile.coord.x, tile.coord.y),
                tile.origin_x,
                tile.origin_y,
                tile.width,
                tile.height,
                tile.base.clone(),
                after,
            );
            if !patch.matches() {
                patches.push(patch);
            }
        }

        patches
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

    fn push_polyline_point(&mut self, point: Pos2) {
        let encoded = [point.x, point.y];
        if self.polyline.last().copied() != Some(encoded) {
            self.polyline.push(encoded);
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
            coord,
            origin_x,
            origin_y,
            width,
            height,
            base,
            coverage: vec![0.0; width * height],
            dirty: false,
        }
    }

    fn local_index(&self, x: usize, y: usize) -> usize {
        debug_assert!(x >= self.origin_x && x < self.origin_x + self.width);
        debug_assert!(y >= self.origin_y && y < self.origin_y + self.height);
        (y - self.origin_y) * self.width + (x - self.origin_x)
    }
}
