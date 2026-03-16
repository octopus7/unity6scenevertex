mod canvas;
mod history;
mod painting;
mod persistence;
mod rendering;
mod stroke;
mod ui;

use std::path::PathBuf;

use eframe::egui::{Pos2, TextureHandle};

use crate::{terrain::generate_heightmap, tool::ToolKind, utils::next_seed};

use self::{history::HistoryStack, stroke::StrokeSession};

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
        };
        app.initialize_history();
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
        self.texture_dirty = true;
        self.pending_autosave = false;
        self.last_drag_pos = None;
        self.active_heightmap_path = None;
        self.hover_bitmap_pos = None;
        self.hover_pixel = None;
        self.target_height = 0.5;
        self.push_history_snapshot();
        self.autosave_heightmap();
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
