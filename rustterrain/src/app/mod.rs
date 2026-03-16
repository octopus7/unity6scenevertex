mod canvas;
mod history;
mod painting;
mod persistence;
mod rendering;
mod stroke;
mod ui;

use std::{mem::size_of, path::PathBuf};

use eframe::egui::{Pos2, TextureHandle};

use crate::{terrain::generate_heightmap, tool::ToolKind, utils::next_seed};

use self::{
    history::{DocumentState, HistoryStack},
    stroke::{ReplayStrokeRecord, StrokeSession},
};

pub struct TerrainApp {
    heightmap: Vec<f32>,
    texture: Option<TextureHandle>,
    texture_dirty: bool,
    brush_radius: f32,
    brush_flow: f32,
    brush_opacity: f32,
    active_tool: ToolKind,
    previous_tool: ToolKind,
    last_drag_pos: Option<Pos2>,
    hover_pixel: Option<(usize, usize)>,
    hover_bitmap_pos: Option<Pos2>,
    seed: u32,
    status_message: String,
    pending_autosave: bool,
    target_height: f32,
    contour_step: f32,
    active_heightmap_path: Option<PathBuf>,
    active_stroke: Option<StrokeSession>,
    history: HistoryStack,
    replay_records: Vec<ReplayStrokeRecord>,
    replay_sequence: u64,
}

impl TerrainApp {
    pub fn new(cc: &eframe::CreationContext<'_>) -> Self {
        let seed = next_seed();
        let heightmap = generate_heightmap(seed);
        let mut app = Self {
            heightmap,
            texture: None,
            texture_dirty: true,
            brush_radius: 28.0,
            brush_flow: 0.22,
            brush_opacity: 0.45,
            active_tool: ToolKind::Standard,
            previous_tool: ToolKind::Standard,
            last_drag_pos: None,
            hover_pixel: None,
            hover_bitmap_pos: None,
            seed,
            status_message: String::new(),
            pending_autosave: false,
            target_height: 0.5,
            contour_step: 0.05,
            active_heightmap_path: None,
            active_stroke: None,
            history: HistoryStack::default(),
            replay_records: Vec::new(),
            replay_sequence: 0,
        };
        app.reset_history_with_current_state();
        app.autosave_heightmap();
        app.ensure_texture(&cc.egui_ctx);
        app
    }

    fn select_tool(&mut self, tool: ToolKind) {
        self.finish_active_stroke();

        if tool != ToolKind::PickHeight {
            self.previous_tool = tool;
        } else if self.active_tool != ToolKind::PickHeight {
            self.previous_tool = self.active_tool;
        }

        self.active_tool = tool;
        self.last_drag_pos = None;
    }

    fn regenerate_terrain(&mut self) {
        self.finish_active_stroke();
        self.seed = next_seed();
        self.heightmap = generate_heightmap(self.seed);
        self.active_heightmap_path = None;
        self.target_height = 0.5;
        self.reset_interaction_state();
        self.reset_history_with_current_state();
        self.clear_replay_records();
        self.autosave_heightmap();
    }

    fn capture_document_state(&self) -> DocumentState {
        DocumentState {
            heightmap: self.heightmap.clone(),
            seed: self.seed,
            active_heightmap_path: self.active_heightmap_path.clone(),
        }
    }

    fn restore_document_state(&mut self, state: DocumentState) {
        self.heightmap = state.heightmap;
        self.seed = state.seed;
        self.active_heightmap_path = state.active_heightmap_path;
        self.reset_interaction_state();
    }

    fn reset_history_with_current_state(&mut self) {
        self.history = HistoryStack::with_initial(self.capture_document_state());
    }

    fn reset_interaction_state(&mut self) {
        self.active_stroke = None;
        self.texture_dirty = true;
        self.pending_autosave = false;
        self.last_drag_pos = None;
        self.hover_pixel = None;
        self.hover_bitmap_pos = None;
    }

    fn record_replay_stroke(&mut self, mut record: ReplayStrokeRecord) {
        record.sequence = self.replay_sequence;
        self.replay_sequence += 1;
        self.replay_records.push(record);
    }

    fn clear_replay_records(&mut self) {
        self.replay_records.clear();
        self.replay_sequence = 0;
    }

    fn tile_memory_bytes(&self) -> usize {
        StrokeSession::estimated_full_tile_bytes()
    }

    fn history_memory_bytes(&self) -> usize {
        size_of::<Vec<ReplayStrokeRecord>>()
            + self
                .replay_records
                .iter()
                .map(ReplayStrokeRecord::estimated_bytes)
                .sum::<usize>()
    }

    fn source_summary(&self) -> String {
        if let Some(path) = &self.active_heightmap_path {
            path.file_name()
                .map(|name| name.to_string_lossy().into_owned())
                .unwrap_or_else(|| path.display().to_string())
        } else {
            format!("seed {}", self.seed)
        }
    }
}
