mod painting;
mod persistence;
mod rendering;
mod ui;

use std::path::PathBuf;

use eframe::egui::{Pos2, TextureHandle};

use crate::{
    constants::BITMAP_PIXELS, terrain::generate_heightmap, tool::ToolKind, utils::next_seed,
};

pub struct TerrainApp {
    heightmap: Vec<f32>,
    texture: Option<TextureHandle>,
    texture_dirty: bool,
    brush_radius: f32,
    brush_strength: f32,
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
}

impl TerrainApp {
    pub fn new(cc: &eframe::CreationContext<'_>) -> Self {
        let mut app = Self {
            heightmap: vec![0.0; BITMAP_PIXELS],
            texture: None,
            texture_dirty: true,
            brush_radius: 28.0,
            brush_strength: 0.35,
            active_tool: ToolKind::Standard,
            previous_tool: ToolKind::Standard,
            last_drag_pos: None,
            hover_pixel: None,
            hover_bitmap_pos: None,
            seed: 0,
            status_message: String::new(),
            pending_autosave: false,
            target_height: 0.5,
            contour_step: 0.05,
            active_heightmap_path: None,
        };
        app.regenerate_terrain();
        app.ensure_texture(&cc.egui_ctx);
        app
    }

    fn select_tool(&mut self, tool: ToolKind) {
        if tool != ToolKind::PickHeight {
            self.previous_tool = tool;
        } else if self.active_tool != ToolKind::PickHeight {
            self.previous_tool = self.active_tool;
        }

        self.active_tool = tool;
        self.last_drag_pos = None;
    }

    fn regenerate_terrain(&mut self) {
        self.seed = next_seed();
        self.heightmap = generate_heightmap(self.seed);
        self.texture_dirty = true;
        self.pending_autosave = true;
        self.last_drag_pos = None;
        self.active_heightmap_path = None;
        self.hover_bitmap_pos = None;
        self.hover_pixel = None;
        self.target_height = 0.5;
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
