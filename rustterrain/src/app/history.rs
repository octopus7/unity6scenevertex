use std::{mem::size_of, path::PathBuf};

use crate::constants::HISTORY_CAPACITY;

use super::TerrainApp;

#[derive(Clone, Debug, PartialEq)]
pub(super) struct DocumentState {
    pub heightmap: Vec<f32>,
    pub seed: u32,
    pub active_heightmap_path: Option<PathBuf>,
}

impl DocumentState {
    fn estimated_bytes(&self) -> usize {
        self.heightmap.len() * size_of::<f32>()
    }
}

#[derive(Clone, Debug, PartialEq)]
pub(super) struct FullSnapshotEntry {
    before: DocumentState,
    after: DocumentState,
}

impl FullSnapshotEntry {
    fn root(state: DocumentState) -> Self {
        Self {
            before: state.clone(),
            after: state,
        }
    }

    fn estimated_bytes(&self) -> usize {
        self.before.estimated_bytes() + self.after.estimated_bytes()
    }
}

#[derive(Clone, Debug, PartialEq)]
pub(super) struct TilePatch {
    tile_coord: (usize, usize),
    origin_x: usize,
    origin_y: usize,
    width: usize,
    height: usize,
    before: Vec<f32>,
    after: Vec<f32>,
}

impl TilePatch {
    pub(super) fn new(
        tile_coord: (usize, usize),
        origin_x: usize,
        origin_y: usize,
        width: usize,
        height: usize,
        before: Vec<f32>,
        after: Vec<f32>,
    ) -> Self {
        Self {
            tile_coord,
            origin_x,
            origin_y,
            width,
            height,
            before,
            after,
        }
    }

    fn estimated_bytes(&self) -> usize {
        size_of::<Self>() + (self.before.len() + self.after.len()) * size_of::<f32>()
    }

    fn apply_undo(&self, heightmap: &mut [f32], bitmap_width: usize) {
        self.copy_into(heightmap, bitmap_width, &self.before);
    }

    fn apply_redo(&self, heightmap: &mut [f32], bitmap_width: usize) {
        self.copy_into(heightmap, bitmap_width, &self.after);
    }

    pub(super) fn matches(&self) -> bool {
        self.before.len() == self.after.len()
            && self
                .before
                .iter()
                .zip(&self.after)
                .all(|(before, after)| (before - after).abs() <= f32::EPSILON)
    }

    fn copy_into(&self, heightmap: &mut [f32], bitmap_width: usize, source: &[f32]) {
        for row in 0..self.height {
            let src_offset = row * self.width;
            let dst_offset = (self.origin_y + row) * bitmap_width + self.origin_x;
            heightmap[dst_offset..dst_offset + self.width]
                .copy_from_slice(&source[src_offset..src_offset + self.width]);
        }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub(super) struct TileDeltaEntry {
    tiles: Vec<TilePatch>,
    affected_tile_count: usize,
}

impl TileDeltaEntry {
    pub(super) fn new(tiles: Vec<TilePatch>) -> Self {
        let affected_tile_count = tiles.len();
        Self {
            tiles,
            affected_tile_count,
        }
    }

    pub(super) fn is_empty(&self) -> bool {
        self.tiles.is_empty()
    }

    fn estimated_bytes(&self) -> usize {
        size_of::<Self>()
            + self
                .tiles
                .iter()
                .map(TilePatch::estimated_bytes)
                .sum::<usize>()
    }

    fn apply_undo(&self, heightmap: &mut [f32], bitmap_width: usize) {
        for tile in &self.tiles {
            tile.apply_undo(heightmap, bitmap_width);
        }
    }

    fn apply_redo(&self, heightmap: &mut [f32], bitmap_width: usize) {
        for tile in &self.tiles {
            tile.apply_redo(heightmap, bitmap_width);
        }
    }

    fn before_state_from(&self, after: &DocumentState, bitmap_width: usize) -> DocumentState {
        let mut state = after.clone();
        self.apply_undo(&mut state.heightmap, bitmap_width);
        state
    }

    fn after_state_from(&self, before: &DocumentState, bitmap_width: usize) -> DocumentState {
        let mut state = before.clone();
        self.apply_redo(&mut state.heightmap, bitmap_width);
        state
    }
}

#[derive(Clone, Debug, PartialEq)]
pub(super) enum HistoryEntry {
    FullSnapshot(FullSnapshotEntry),
    TileDelta(TileDeltaEntry),
}

impl HistoryEntry {
    fn root(state: DocumentState) -> Self {
        Self::FullSnapshot(FullSnapshotEntry::root(state))
    }

    fn estimated_bytes(&self) -> usize {
        match self {
            Self::FullSnapshot(snapshot) => snapshot.estimated_bytes(),
            Self::TileDelta(delta) => delta.estimated_bytes(),
        }
    }

    fn next_state_from(&self, previous: &DocumentState, bitmap_width: usize) -> DocumentState {
        match self {
            Self::FullSnapshot(snapshot) => snapshot.after.clone(),
            Self::TileDelta(delta) => delta.after_state_from(previous, bitmap_width),
        }
    }
}

#[derive(Default)]
pub(super) struct HistoryStack {
    entries: Vec<HistoryEntry>,
    cursor: usize,
}

impl HistoryStack {
    pub(super) fn with_initial(state: DocumentState) -> Self {
        Self {
            entries: vec![HistoryEntry::root(state)],
            cursor: 0,
        }
    }

    pub(super) fn push(&mut self, entry: HistoryEntry, bitmap_width: usize) {
        self.entries.truncate(self.cursor + 1);
        self.entries.push(entry);
        self.cursor = self.entries.len().saturating_sub(1);

        while self.entries.len() > HISTORY_CAPACITY {
            self.reanchor_oldest_entry(bitmap_width);
        }
    }

    pub(super) fn undo(&mut self) -> Option<HistoryEntry> {
        if !self.can_undo() {
            return None;
        }

        let entry = self.entries.get(self.cursor).cloned();
        self.cursor -= 1;
        entry
    }

    pub(super) fn redo(&mut self) -> Option<HistoryEntry> {
        if !self.can_redo() {
            return None;
        }

        self.cursor += 1;
        self.entries.get(self.cursor).cloned()
    }

    pub(super) fn len(&self) -> usize {
        self.entries.len()
    }

    pub(super) fn cursor(&self) -> usize {
        self.cursor
    }

    pub(super) fn can_undo(&self) -> bool {
        self.cursor > 0
    }

    pub(super) fn can_redo(&self) -> bool {
        self.cursor + 1 < self.entries.len()
    }

    pub(super) fn undo_steps(&self) -> usize {
        self.cursor
    }

    pub(super) fn redo_steps(&self) -> usize {
        self.entries.len().saturating_sub(self.cursor + 1)
    }

    fn reanchor_oldest_entry(&mut self, bitmap_width: usize) {
        if self.entries.len() <= 1 {
            return;
        }

        let oldest_state = match &self.entries[0] {
            HistoryEntry::FullSnapshot(snapshot) => snapshot.after.clone(),
            HistoryEntry::TileDelta(_) => return,
        };
        let next_state = self.entries[1].next_state_from(&oldest_state, bitmap_width);
        self.entries[1] = HistoryEntry::root(next_state);
        self.entries.remove(0);
        self.cursor = self.cursor.saturating_sub(1);
    }
}

impl TerrainApp {
    pub(super) fn history_entry_from_tile_delta(
        &self,
        delta: TileDeltaEntry,
        bitmap_width: usize,
    ) -> HistoryEntry {
        let after = self.capture_document_state();
        let before = delta.before_state_from(&after, bitmap_width);
        let full_snapshot = HistoryEntry::FullSnapshot(FullSnapshotEntry { before, after });

        if delta.estimated_bytes() >= full_snapshot.estimated_bytes() {
            full_snapshot
        } else {
            HistoryEntry::TileDelta(delta)
        }
    }

    pub(super) fn push_history_entry(&mut self, entry: HistoryEntry, bitmap_width: usize) {
        self.history.push(entry, bitmap_width);
    }

    pub(super) fn undo(&mut self) {
        if let Some(entry) = self.history.undo() {
            self.apply_history_undo(entry);
            self.autosave_heightmap();
            self.status_message =
                format!("Undo {}/{}", self.history.cursor() + 1, self.history.len());
        }
    }

    pub(super) fn redo(&mut self) {
        if let Some(entry) = self.history.redo() {
            self.apply_history_redo(entry);
            self.autosave_heightmap();
            self.status_message =
                format!("Redo {}/{}", self.history.cursor() + 1, self.history.len());
        }
    }

    pub(super) fn history_len(&self) -> usize {
        self.history.len()
    }

    pub(super) fn history_cursor(&self) -> usize {
        self.history.cursor()
    }

    pub(super) fn history_undo_steps(&self) -> usize {
        self.history.undo_steps()
    }

    pub(super) fn history_redo_steps(&self) -> usize {
        self.history.redo_steps()
    }

    fn apply_history_undo(&mut self, entry: HistoryEntry) {
        match entry {
            HistoryEntry::FullSnapshot(snapshot) => self.restore_document_state(snapshot.before),
            HistoryEntry::TileDelta(delta) => {
                delta.apply_undo(&mut self.heightmap, crate::constants::BITMAP_SIZE);
                self.reset_interaction_state();
            }
        }
    }

    fn apply_history_redo(&mut self, entry: HistoryEntry) {
        match entry {
            HistoryEntry::FullSnapshot(snapshot) => self.restore_document_state(snapshot.after),
            HistoryEntry::TileDelta(delta) => {
                delta.apply_redo(&mut self.heightmap, crate::constants::BITMAP_SIZE);
                self.reset_interaction_state();
            }
        }
    }
}
