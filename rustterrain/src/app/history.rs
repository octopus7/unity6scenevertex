use std::path::PathBuf;

use crate::constants::HISTORY_CAPACITY;

use super::TerrainApp;

#[derive(Clone, PartialEq)]
pub(super) struct HistoryEntry {
    heightmap: Vec<f32>,
    seed: u32,
    active_heightmap_path: Option<PathBuf>,
}

impl HistoryEntry {
    fn capture(app: &TerrainApp) -> Self {
        Self {
            heightmap: app.heightmap.clone(),
            seed: app.seed,
            active_heightmap_path: app.active_heightmap_path.clone(),
        }
    }
}

#[derive(Default)]
pub(super) struct HistoryStack {
    entries: Vec<HistoryEntry>,
    cursor: usize,
}

impl HistoryStack {
    pub(super) fn with_initial(entry: HistoryEntry) -> Self {
        Self {
            entries: vec![entry],
            cursor: 0,
        }
    }

    pub(super) fn push(&mut self, entry: HistoryEntry) {
        self.entries.truncate(self.cursor + 1);

        if self.entries.last() == Some(&entry) {
            self.cursor = self.entries.len().saturating_sub(1);
            return;
        }

        self.entries.push(entry);
        if self.entries.len() > HISTORY_CAPACITY {
            self.entries.remove(0);
        }
        self.cursor = self.entries.len().saturating_sub(1);
    }

    pub(super) fn undo(&mut self) -> Option<HistoryEntry> {
        if !self.can_undo() {
            return None;
        }

        self.cursor -= 1;
        self.entries.get(self.cursor).cloned()
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
}

impl TerrainApp {
    pub(super) fn initialize_history(&mut self) {
        self.history = HistoryStack::with_initial(HistoryEntry::capture(self));
    }

    pub(super) fn push_history_snapshot(&mut self) {
        self.history.push(HistoryEntry::capture(self));
    }

    pub(super) fn undo(&mut self) {
        if let Some(entry) = self.history.undo() {
            self.restore_history_entry(entry);
            self.autosave_heightmap();
            self.status_message =
                format!("Undo {}/{}", self.history.cursor() + 1, self.history.len());
        }
    }

    pub(super) fn redo(&mut self) {
        if let Some(entry) = self.history.redo() {
            self.restore_history_entry(entry);
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

    fn restore_history_entry(&mut self, entry: HistoryEntry) {
        self.heightmap = entry.heightmap;
        self.seed = entry.seed;
        self.active_heightmap_path = entry.active_heightmap_path;
        self.active_stroke = None;
        self.texture_dirty = true;
        self.pending_autosave = false;
        self.last_drag_pos = None;
        self.hover_pixel = None;
        self.hover_bitmap_pos = None;
    }
}
