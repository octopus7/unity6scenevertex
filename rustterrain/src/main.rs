#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::{
    fs,
    path::{Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

use eframe::egui::{
    self, Align, CentralPanel, Color32, ColorImage, Context, Direction, Layout, PointerButton,
    Pos2, Rect, Sense, SidePanel, Slider, Stroke, TextureHandle, TextureOptions, TopBottomPanel,
    Vec2, ViewportBuilder,
};
use image::{ColorType, ImageFormat, ImageReader, imageops::FilterType};
use noise::{NoiseFn, OpenSimplex, Perlin};
use rfd::FileDialog;

const BITMAP_SIZE: usize = 1024;
const BITMAP_PIXELS: usize = BITMAP_SIZE * BITMAP_SIZE;
const MIN_BRUSH_RADIUS: f32 = 4.0;
const MAX_BRUSH_RADIUS: f32 = 160.0;
const SEA_LEVEL: f32 = 0.42;
const STANDARD_STRENGTH_SCALE: f32 = 0.08;
const AUTOSAVE_HEIGHTMAP_FILE: &str = "generated/heightmap_latest.png";
const MIN_CONTOUR_STEP: f32 = 0.01;
const MAX_CONTOUR_STEP: f32 = 0.25;

fn main() -> eframe::Result<()> {
    let options = eframe::NativeOptions {
        viewport: ViewportBuilder::default()
            .with_title("Rust Terrain")
            .with_inner_size([1365.0, 920.0])
            .with_min_inner_size([900.0, 720.0]),
        renderer: eframe::Renderer::Wgpu,
        ..Default::default()
    };

    eframe::run_native(
        "Rust Terrain",
        options,
        Box::new(|cc| Ok(Box::new(TerrainApp::new(cc)))),
    )
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum ToolKind {
    Standard,
    TargetHeight,
    PickHeight,
    Blur,
}

impl ToolKind {
    fn label(self) -> &'static str {
        match self {
            Self::Standard => "Standard",
            Self::TargetHeight => "Target Height",
            Self::PickHeight => "Pick Height",
            Self::Blur => "Blur",
        }
    }

    fn description(self) -> &'static str {
        match self {
            Self::Standard => "Left drag raises terrain. Right drag lowers terrain.",
            Self::TargetHeight => {
                "Left drag moves terrain toward the target height without overshooting."
            }
            Self::PickHeight => {
                "Click the canvas to sample a height, then the previous tool is restored."
            }
            Self::Blur => "Left drag smooths sharp height transitions inside the brush.",
        }
    }

    fn preview_color(self) -> Color32 {
        match self {
            Self::Standard => Color32::from_rgb(239, 196, 73),
            Self::TargetHeight => Color32::from_rgb(76, 186, 182),
            Self::PickHeight => Color32::from_rgb(242, 242, 242),
            Self::Blur => Color32::from_rgb(202, 118, 74),
        }
    }
}

#[derive(Clone, Copy)]
enum StrokeAction {
    Standard(f32),
    TargetHeight,
    Blur,
}

struct TerrainApp {
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
    fn new(cc: &eframe::CreationContext<'_>) -> Self {
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

    fn ensure_texture(&mut self, ctx: &Context) {
        if self.texture.is_some() && !self.texture_dirty {
            return;
        }

        let image = self.build_color_image();
        match &mut self.texture {
            Some(texture) if self.texture_dirty => {
                texture.set(image, TextureOptions::LINEAR);
            }
            None => {
                self.texture = Some(ctx.load_texture("terrain_map", image, TextureOptions::LINEAR));
            }
            _ => {}
        }
        self.texture_dirty = false;
    }

    fn build_color_image(&self) -> ColorImage {
        let mut rgba = Vec::with_capacity(BITMAP_PIXELS * 4);

        for y in 0..BITMAP_SIZE {
            let y_up = y.saturating_sub(1);
            let y_down = (y + 1).min(BITMAP_SIZE - 1);

            for x in 0..BITMAP_SIZE {
                let x_left = x.saturating_sub(1);
                let x_right = (x + 1).min(BITMAP_SIZE - 1);
                let height = self.heightmap[y * BITMAP_SIZE + x];
                let left = self.heightmap[y * BITMAP_SIZE + x_left];
                let right = self.heightmap[y * BITMAP_SIZE + x_right];
                let up = self.heightmap[y_up * BITMAP_SIZE + x];
                let down = self.heightmap[y_down * BITMAP_SIZE + x];

                let dx = right - left;
                let dy = down - up;
                let hillshade = if height < SEA_LEVEL {
                    (0.86 + (-dx * 0.22) + (-dy * 0.14)).clamp(0.72, 1.04)
                } else {
                    (0.88 + (-dx * 1.85) + (-dy * 1.25)).clamp(0.56, 1.18)
                };

                let [r, g, b] = terrain_color(height);
                rgba.push((r as f32 * hillshade).clamp(0.0, 255.0) as u8);
                rgba.push((g as f32 * hillshade).clamp(0.0, 255.0) as u8);
                rgba.push((b as f32 * hillshade).clamp(0.0, 255.0) as u8);
                rgba.push(255);
            }
        }

        ColorImage::from_rgba_unmultiplied([BITMAP_SIZE, BITMAP_SIZE], &rgba)
    }

    fn controls_ui(&mut self, ctx: &Context) {
        SidePanel::left("controls_panel")
            .resizable(false)
            .default_width(252.0)
            .show(ctx, |ui| {
                ui.heading("Terrain Tools");
                ui.label(self.active_tool.description());
                ui.separator();

                ui.label("Mode");
                for tool in [
                    ToolKind::Standard,
                    ToolKind::TargetHeight,
                    ToolKind::PickHeight,
                    ToolKind::Blur,
                ] {
                    if ui
                        .selectable_label(self.active_tool == tool, tool.label())
                        .clicked()
                    {
                        self.select_tool(tool);
                    }
                }

                ui.separator();
                ui.add(
                    Slider::new(&mut self.brush_radius, MIN_BRUSH_RADIUS..=MAX_BRUSH_RADIUS)
                        .text("Brush"),
                );
                ui.add(Slider::new(&mut self.brush_strength, 0.02..=1.0).text("Strength"));
                ui.add(Slider::new(&mut self.target_height, 0.0..=1.0).text("Target"));
                ui.add(
                    Slider::new(&mut self.contour_step, MIN_CONTOUR_STEP..=MAX_CONTOUR_STEP)
                        .text("Contour Step"),
                );

                if ui.button("Regenerate").clicked() {
                    self.regenerate_terrain();
                }

                ui.separator();
                if ui.button("Save Heightmap").clicked() {
                    self.save_heightmap_dialog();
                }
                if ui.button("Load Heightmap").clicked() {
                    self.load_heightmap_dialog();
                }
                if ui.button("Export Contours").clicked() {
                    self.export_contours_dialog();
                }

                ui.separator();
                ui.label(format!("Source {}", self.source_summary()));
                ui.label(format!("Autosave {}", AUTOSAVE_HEIGHTMAP_FILE));
            });
    }

    fn canvas_ui(&mut self, ui: &mut egui::Ui) {
        self.ensure_texture(ui.ctx());

        let available = ui.available_size();
        let side = available.x.min(available.y).max(64.0);
        let image_size = Vec2::splat(side);

        ui.allocate_ui_with_layout(
            available,
            Layout::centered_and_justified(Direction::TopDown),
            |ui| {
                let (rect, response) = ui.allocate_exact_size(image_size, Sense::click_and_drag());
                if let Some(texture) = &self.texture {
                    ui.painter().rect_stroke(
                        rect.expand(1.0),
                        6.0,
                        egui::Stroke::new(1.0, Color32::from_gray(70)),
                        egui::StrokeKind::Outside,
                    );
                    ui.painter().image(
                        texture.id(),
                        rect,
                        Rect::from_min_max(Pos2::new(0.0, 0.0), Pos2::new(1.0, 1.0)),
                        Color32::WHITE,
                    );
                }

                self.handle_canvas_input(ui.ctx(), &response, rect);
                self.draw_brush_preview(ui, rect);
            },
        );
    }

    fn handle_canvas_input(&mut self, ctx: &Context, response: &egui::Response, rect: Rect) {
        let was_dragging = self.last_drag_pos.is_some();

        if response.hovered() {
            let scroll_delta = ctx.input(|input| input.raw_scroll_delta.y);
            if scroll_delta.abs() > f32::EPSILON {
                self.brush_radius = (self.brush_radius + scroll_delta * 0.05)
                    .clamp(MIN_BRUSH_RADIUS, MAX_BRUSH_RADIUS);
            }
        }

        let primary_active = response.dragged_by(PointerButton::Primary)
            || (response.hovered() && ctx.input(|input| input.pointer.primary_down()));
        let secondary_active = response.dragged_by(PointerButton::Secondary)
            || (response.hovered() && ctx.input(|input| input.pointer.secondary_down()));

        if let Some(pointer_pos) = response
            .hover_pos()
            .or_else(|| response.interact_pointer_pos())
        {
            let bitmap_pos = screen_to_bitmap(rect, pointer_pos);
            let hover_x = bitmap_pos.x.round().clamp(0.0, (BITMAP_SIZE - 1) as f32) as usize;
            let hover_y = bitmap_pos.y.round().clamp(0.0, (BITMAP_SIZE - 1) as f32) as usize;
            self.hover_pixel = Some((hover_x, hover_y));
            self.hover_bitmap_pos = Some(bitmap_pos);

            if self.active_tool == ToolKind::PickHeight
                && response.clicked_by(PointerButton::Primary)
            {
                self.sample_height(hover_x, hover_y);
                self.last_drag_pos = None;
                return;
            }

            let action = match self.active_tool {
                ToolKind::Standard if primary_active => Some(StrokeAction::Standard(
                    self.brush_strength * STANDARD_STRENGTH_SCALE,
                )),
                ToolKind::Standard if secondary_active => Some(StrokeAction::Standard(
                    -self.brush_strength * STANDARD_STRENGTH_SCALE,
                )),
                ToolKind::TargetHeight if primary_active => Some(StrokeAction::TargetHeight),
                ToolKind::Blur if primary_active => Some(StrokeAction::Blur),
                _ => None,
            };

            if let Some(action) = action {
                let previous = self.last_drag_pos.unwrap_or(bitmap_pos);
                self.apply_stroke(previous, bitmap_pos, action);
                self.last_drag_pos = Some(bitmap_pos);
            } else {
                self.last_drag_pos = None;
            }
        } else {
            self.hover_pixel = None;
            self.hover_bitmap_pos = None;
            self.last_drag_pos = None;
        }

        if !primary_active && !secondary_active {
            if was_dragging && self.pending_autosave {
                self.autosave_heightmap();
            }
            self.last_drag_pos = None;
        }
    }

    fn draw_brush_preview(&self, ui: &mut egui::Ui, rect: Rect) {
        let Some(hover_bitmap_pos) = self.hover_bitmap_pos else {
            return;
        };

        let center = bitmap_to_screen(rect, hover_bitmap_pos);
        let radius = rect.width() * (self.brush_radius / (BITMAP_SIZE as f32 - 1.0));
        let color = self.active_tool.preview_color();

        ui.painter()
            .circle_stroke(center, radius.max(1.0), Stroke::new(2.0, color));
        ui.painter()
            .circle_stroke(center, (radius * 0.32).max(2.0), Stroke::new(1.0, color));
        ui.painter().circle_filled(center, 1.8, color);

        if self.active_tool == ToolKind::PickHeight {
            let arm = 9.0;
            ui.painter().line_segment(
                [
                    Pos2::new(center.x - arm, center.y),
                    Pos2::new(center.x + arm, center.y),
                ],
                Stroke::new(1.5, color),
            );
            ui.painter().line_segment(
                [
                    Pos2::new(center.x, center.y - arm),
                    Pos2::new(center.x, center.y + arm),
                ],
                Stroke::new(1.5, color),
            );
        }
    }

    fn sample_height(&mut self, x: usize, y: usize) {
        let idx = y * BITMAP_SIZE + x;
        self.target_height = self.heightmap[idx];
        self.status_message = format!(
            "Sampled target height {:.3} from ({x}, {y})",
            self.target_height
        );
        self.active_tool = if self.previous_tool == ToolKind::PickHeight {
            ToolKind::Standard
        } else {
            self.previous_tool
        };
    }

    fn apply_stroke(&mut self, from: Pos2, to: Pos2, action: StrokeAction) {
        let distance = from.distance(to);
        let spacing = (self.brush_radius * 0.35).max(1.0);
        let steps = (distance / spacing).ceil().max(1.0) as usize;

        for step in 0..=steps {
            let t = step as f32 / steps as f32;
            let point = Pos2::new(lerp(from.x, to.x, t), lerp(from.y, to.y, t));
            match action {
                StrokeAction::Standard(amount) => self.paint_standard_disc(point, amount),
                StrokeAction::TargetHeight => self.paint_target_disc(point),
                StrokeAction::Blur => self.paint_blur_disc(point),
            }
        }

        self.texture_dirty = true;
        self.pending_autosave = true;
    }

    fn paint_standard_disc(&mut self, center: Pos2, amount: f32) {
        let radius = self.brush_radius;
        let radius_sq = radius * radius;
        let (x_min, x_max, y_min, y_max) = brush_bounds(center, radius);

        for y in y_min..=y_max {
            for x in x_min..=x_max {
                let dx = x as f32 - center.x;
                let dy = y as f32 - center.y;
                let distance_sq = dx * dx + dy * dy;
                if distance_sq > radius_sq {
                    continue;
                }

                let falloff = 1.0 - (distance_sq / radius_sq);
                let idx = y as usize * BITMAP_SIZE + x as usize;
                let current = self.heightmap[idx];
                let updated = (current + amount * falloff).clamp(0.0, 1.0);
                self.heightmap[idx] = updated;
            }
        }
    }

    fn paint_target_disc(&mut self, center: Pos2) {
        let radius = self.brush_radius;
        let radius_sq = radius * radius;
        let (x_min, x_max, y_min, y_max) = brush_bounds(center, radius);

        for y in y_min..=y_max {
            for x in x_min..=x_max {
                let dx = x as f32 - center.x;
                let dy = y as f32 - center.y;
                let distance_sq = dx * dx + dy * dy;
                if distance_sq > radius_sq {
                    continue;
                }

                let falloff = 1.0 - (distance_sq / radius_sq);
                let idx = y as usize * BITMAP_SIZE + x as usize;
                let current = self.heightmap[idx];
                let blend = (self.brush_strength * falloff).clamp(0.0, 1.0);
                self.heightmap[idx] = lerp(current, self.target_height, blend);
            }
        }
    }

    fn paint_blur_disc(&mut self, center: Pos2) {
        let radius = self.brush_radius;
        let radius_sq = radius * radius;
        let (x_min, x_max, y_min, y_max) = brush_bounds(center, radius);
        let sample_x_min = (x_min - 1).max(0);
        let sample_x_max = (x_max + 1).min(BITMAP_SIZE as i32 - 1);
        let sample_y_min = (y_min - 1).max(0);
        let sample_y_max = (y_max + 1).min(BITMAP_SIZE as i32 - 1);
        let sample_width = (sample_x_max - sample_x_min + 1) as usize;
        let sample_height = (sample_y_max - sample_y_min + 1) as usize;
        let mut source = vec![0.0; sample_width * sample_height];

        for sample_y in sample_y_min..=sample_y_max {
            let src_offset = sample_y as usize * BITMAP_SIZE + sample_x_min as usize;
            let dst_offset = (sample_y - sample_y_min) as usize * sample_width;
            source[dst_offset..dst_offset + sample_width]
                .copy_from_slice(&self.heightmap[src_offset..src_offset + sample_width]);
        }

        for y in y_min..=y_max {
            for x in x_min..=x_max {
                let dx = x as f32 - center.x;
                let dy = y as f32 - center.y;
                let distance_sq = dx * dx + dy * dy;
                if distance_sq > radius_sq {
                    continue;
                }

                let mut blurred_sum = 0.0;
                let mut weight_sum = 0.0;
                let sample_local_x = x - sample_x_min;
                let sample_local_y = y - sample_y_min;

                for kernel_y in -1..=1 {
                    for kernel_x in -1..=1 {
                        let weight = match (kernel_x, kernel_y) {
                            (0, 0) => 4.0,
                            (0, _) | (_, 0) => 2.0,
                            _ => 1.0,
                        };
                        let source_x =
                            (sample_local_x + kernel_x).clamp(0, sample_width as i32 - 1) as usize;
                        let source_y =
                            (sample_local_y + kernel_y).clamp(0, sample_height as i32 - 1) as usize;
                        blurred_sum += source[source_y * sample_width + source_x] * weight;
                        weight_sum += weight;
                    }
                }

                let falloff = 1.0 - (distance_sq / radius_sq);
                let blur_strength = (self.brush_strength * falloff).clamp(0.0, 1.0);
                let idx = y as usize * BITMAP_SIZE + x as usize;
                let blurred = blurred_sum / weight_sum;
                let current = self.heightmap[idx];
                self.heightmap[idx] = lerp(current, blurred, blur_strength);
            }
        }
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

    fn autosave_heightmap(&mut self) {
        let path = PathBuf::from(AUTOSAVE_HEIGHTMAP_FILE);
        match self.save_heightmap_png(&path) {
            Ok(()) => {
                self.status_message = format!("Autosaved {}", path.display());
                self.pending_autosave = false;
            }
            Err(error) => {
                self.status_message = format!("Autosave failed: {error}");
            }
        }
    }

    fn save_heightmap_dialog(&mut self) {
        let dialog = self
            .png_dialog()
            .set_file_name(self.suggested_heightmap_name());
        if let Some(path) = dialog.save_file() {
            match self.save_heightmap_png(&path) {
                Ok(()) => {
                    self.active_heightmap_path = Some(path.clone());
                    self.status_message = format!("Saved {}", path.display());
                }
                Err(error) => {
                    self.status_message = format!("Save failed: {error}");
                }
            }
        }
    }

    fn load_heightmap_dialog(&mut self) {
        if let Some(path) = self.png_dialog().pick_file() {
            match self.load_heightmap_png(&path) {
                Ok(()) => {
                    self.active_heightmap_path = Some(path.clone());
                    self.status_message = format!("Loaded {}", path.display());
                }
                Err(error) => {
                    self.status_message = format!("Load failed: {error}");
                }
            }
        }
    }

    fn export_contours_dialog(&mut self) {
        let dialog = self
            .png_dialog()
            .set_file_name(self.suggested_contour_name());
        if let Some(path) = dialog.save_file() {
            match self.save_contour_png(&path) {
                Ok(()) => {
                    self.status_message = format!("Exported contours to {}", path.display());
                }
                Err(error) => {
                    self.status_message = format!("Contour export failed: {error}");
                }
            }
        }
    }

    fn png_dialog(&self) -> FileDialog {
        let mut dialog = FileDialog::new().add_filter("PNG image", &["png"]);
        if let Some(active_path) = &self.active_heightmap_path {
            if let Some(parent) = active_path.parent() {
                dialog = dialog.set_directory(parent);
            }
        } else {
            dialog = dialog.set_directory("generated");
        }
        dialog
    }

    fn suggested_heightmap_name(&self) -> String {
        self.active_heightmap_path
            .as_ref()
            .and_then(|path| path.file_name())
            .map(|name| name.to_string_lossy().into_owned())
            .unwrap_or_else(|| "heightmap.png".to_owned())
    }

    fn suggested_contour_name(&self) -> String {
        if let Some(path) = &self.active_heightmap_path {
            if let Some(stem) = path.file_stem() {
                return format!("{}_contours.png", stem.to_string_lossy());
            }
        }

        "heightmap_contours.png".to_owned()
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

    fn save_heightmap_png(&self, path: &Path) -> Result<(), String> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|error| error.to_string())?;
        }

        let mut grayscale = Vec::with_capacity(BITMAP_PIXELS);
        for &height in &self.heightmap {
            grayscale.push((height.clamp(0.0, 1.0) * 255.0).round() as u8);
        }

        image::save_buffer_with_format(
            &path,
            &grayscale,
            BITMAP_SIZE as u32,
            BITMAP_SIZE as u32,
            ColorType::L8,
            ImageFormat::Png,
        )
        .map_err(|error| error.to_string())
    }

    fn load_heightmap_png(&mut self, path: &Path) -> Result<(), String> {
        let reader = ImageReader::open(path).map_err(|error| error.to_string())?;
        let image = reader.decode().map_err(|error| error.to_string())?;
        let grayscale = image.to_luma8();
        let grayscale = if grayscale.width() != BITMAP_SIZE as u32
            || grayscale.height() != BITMAP_SIZE as u32
        {
            image::imageops::resize(
                &grayscale,
                BITMAP_SIZE as u32,
                BITMAP_SIZE as u32,
                FilterType::Triangle,
            )
        } else {
            grayscale
        };

        self.heightmap = grayscale
            .into_raw()
            .into_iter()
            .map(|value| value as f32 / 255.0)
            .collect();
        self.texture_dirty = true;
        self.last_drag_pos = None;
        self.hover_pixel = None;
        self.hover_bitmap_pos = None;
        self.pending_autosave = false;

        if let Err(error) = self.save_heightmap_png(Path::new(AUTOSAVE_HEIGHTMAP_FILE)) {
            self.status_message = format!("Autosave mirror failed: {error}");
        }

        Ok(())
    }

    fn save_contour_png(&self, path: &Path) -> Result<(), String> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|error| error.to_string())?;
        }

        let rgba = self.build_contour_image();
        image::save_buffer_with_format(
            path,
            &rgba,
            BITMAP_SIZE as u32,
            BITMAP_SIZE as u32,
            ColorType::Rgba8,
            ImageFormat::Png,
        )
        .map_err(|error| error.to_string())
    }

    fn build_contour_image(&self) -> Vec<u8> {
        let mut rgba = Vec::with_capacity(BITMAP_PIXELS * 4);

        for y in 0..BITMAP_SIZE {
            let y_up = y.saturating_sub(1);
            let y_down = (y + 1).min(BITMAP_SIZE - 1);

            for x in 0..BITMAP_SIZE {
                let x_left = x.saturating_sub(1);
                let x_right = (x + 1).min(BITMAP_SIZE - 1);
                let idx = y * BITMAP_SIZE + x;
                let height = self.heightmap[idx];
                let bucket = contour_bucket(height, self.contour_step);
                let neighbor_changed = bucket
                    != contour_bucket(self.heightmap[y * BITMAP_SIZE + x_left], self.contour_step)
                    || bucket
                        != contour_bucket(
                            self.heightmap[y * BITMAP_SIZE + x_right],
                            self.contour_step,
                        )
                    || bucket
                        != contour_bucket(
                            self.heightmap[y_up * BITMAP_SIZE + x],
                            self.contour_step,
                        )
                    || bucket
                        != contour_bucket(
                            self.heightmap[y_down * BITMAP_SIZE + x],
                            self.contour_step,
                        );

                let color = if neighbor_changed {
                    if height < SEA_LEVEL {
                        [78, 121, 181, 255]
                    } else {
                        [52, 44, 38, 255]
                    }
                } else {
                    [247, 244, 238, 255]
                };
                rgba.extend_from_slice(&color);
            }
        }

        rgba
    }
}

impl eframe::App for TerrainApp {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        self.controls_ui(ctx);

        TopBottomPanel::top("toolbar_info").show(ctx, |ui| {
            ui.horizontal(|ui| {
                ui.label(self.active_tool.label());
                ui.separator();
                ui.label(self.active_tool.description());
                ui.separator();
                ui.label("Wheel: brush size");
                ui.separator();
                ui.label(format!("Brush {:.0}px", self.brush_radius));
                ui.separator();
                ui.label(format!("Strength {:.2}", self.brush_strength));
                ui.separator();
                ui.label(format!("Target {:.3}", self.target_height));
                if let Some((x, y)) = self.hover_pixel {
                    let idx = y * BITMAP_SIZE + x;
                    ui.separator();
                    ui.label(format!("Height ({x}, {y}) = {:.3}", self.heightmap[idx]));
                }
                ui.separator();
                ui.label(format!("Sea {:.2}", SEA_LEVEL));
                ui.separator();
                ui.label(&self.status_message);
                ui.with_layout(Layout::right_to_left(Align::Center), |ui| {
                    ui.label(self.source_summary());
                });
            });
        });

        CentralPanel::default().show(ctx, |ui| {
            self.canvas_ui(ui);
        });
    }
}

fn generate_heightmap(seed: u32) -> Vec<f32> {
    let land_macro = OpenSimplex::new(seed);
    let rolling = Perlin::new(seed ^ 0x6D2B_79F5);
    let mountain_mask_noise = OpenSimplex::new(seed ^ 0x1B56_C4E9);
    let ridge_noise = Perlin::new(seed ^ 0x9E37_79B9);
    let basin_macro = OpenSimplex::new(seed ^ 0xA53A_9E5D);
    let basin_detail = Perlin::new(seed ^ 0xC13F_A9A9);

    let mut heightmap = vec![0.0; BITMAP_PIXELS];

    for y in 0..BITMAP_SIZE {
        for x in 0..BITMAP_SIZE {
            let nx = x as f64 / BITMAP_SIZE as f64 - 0.5;
            let ny = y as f64 / BITMAP_SIZE as f64 - 0.5;

            let broad_land =
                (((land_macro.get([nx * 1.15, ny * 1.15, 0.3]) as f32) * 0.5) + 0.5).powf(1.15);
            let rolling_land = fbm_perlin(&rolling, nx * 2.8, ny * 2.8, 5, 1.0, 2.05, 0.53);

            let mountain_region = smoothstep(
                0.48,
                0.82,
                ((mountain_mask_noise.get([nx * 1.7, ny * 1.7, 1.9]) as f32) * 0.5) + 0.5,
            );
            let ridges = ridged_fbm(&ridge_noise, nx * 4.4, ny * 4.4, 5, 1.0, 2.1, 0.55);
            let mountains = mountain_region * ridges.powf(1.6) * 0.48;

            let plains =
                0.26 + broad_land * 0.28 + ((rolling_land as f32) * 0.14) + mountain_region * 0.03;

            let basin_region = smoothstep(
                0.58,
                0.86,
                ((basin_macro.get([nx * 1.1, ny * 1.1, 5.7]) as f32) * 0.5) + 0.5,
            );
            let basin_shape = smoothstep(
                0.38,
                0.92,
                ((basin_detail.get([nx * 3.2, ny * 3.2, 2.4]) as f32) * 0.5) + 0.5,
            );
            let basins = basin_region * (0.16 + basin_shape * 0.26);

            let altitude = (plains + mountains - basins).clamp(0.0, 1.0);
            heightmap[y * BITMAP_SIZE + x] = altitude;
        }
    }

    heightmap
}

fn fbm_perlin(
    noise: &Perlin,
    x: f64,
    y: f64,
    octaves: usize,
    mut amplitude: f64,
    lacunarity: f64,
    gain: f64,
) -> f64 {
    let mut frequency = 1.0;
    let mut sum = 0.0;
    let mut amplitude_sum = 0.0;

    for octave in 0..octaves {
        sum += noise.get([x * frequency, y * frequency, octave as f64 * 0.73]) * amplitude;
        amplitude_sum += amplitude;
        amplitude *= gain;
        frequency *= lacunarity;
    }

    if amplitude_sum == 0.0 {
        0.0
    } else {
        sum / amplitude_sum
    }
}

fn ridged_fbm(
    noise: &Perlin,
    x: f64,
    y: f64,
    octaves: usize,
    mut amplitude: f64,
    lacunarity: f64,
    gain: f64,
) -> f32 {
    let mut frequency = 1.0;
    let mut sum = 0.0;
    let mut amplitude_sum = 0.0;

    for octave in 0..octaves {
        let sample = noise.get([x * frequency, y * frequency, octave as f64 * 0.61]);
        let ridge = 1.0 - sample.abs();
        sum += ridge * amplitude;
        amplitude_sum += amplitude;
        amplitude *= gain;
        frequency *= lacunarity;
    }

    if amplitude_sum == 0.0 {
        0.0
    } else {
        (sum / amplitude_sum) as f32
    }
}

fn brush_bounds(center: Pos2, radius: f32) -> (i32, i32, i32, i32) {
    (
        (center.x - radius).floor().max(0.0) as i32,
        (center.x + radius).ceil().min((BITMAP_SIZE - 1) as f32) as i32,
        (center.y - radius).floor().max(0.0) as i32,
        (center.y + radius).ceil().min((BITMAP_SIZE - 1) as f32) as i32,
    )
}

fn bitmap_to_screen(rect: Rect, position: Pos2) -> Pos2 {
    let u = position.x / (BITMAP_SIZE as f32 - 1.0);
    let v = position.y / (BITMAP_SIZE as f32 - 1.0);
    Pos2::new(
        rect.left() + u * rect.width(),
        rect.top() + v * rect.height(),
    )
}

fn screen_to_bitmap(rect: Rect, position: Pos2) -> Pos2 {
    let u = ((position.x - rect.left()) / rect.width()).clamp(0.0, 1.0);
    let v = ((position.y - rect.top()) / rect.height()).clamp(0.0, 1.0);
    Pos2::new(
        u * (BITMAP_SIZE as f32 - 1.0),
        v * (BITMAP_SIZE as f32 - 1.0),
    )
}

fn smoothstep(edge0: f32, edge1: f32, x: f32) -> f32 {
    let t = ((x - edge0) / (edge1 - edge0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

fn contour_bucket(height: f32, contour_step: f32) -> i32 {
    (height / contour_step.max(MIN_CONTOUR_STEP)).floor() as i32
}

fn terrain_color(height: f32) -> [u8; 3] {
    if height < SEA_LEVEL {
        let depth = smoothstep(0.0, SEA_LEVEL, height);
        return lerp_color([8, 34, 92], [78, 168, 236], depth.powf(0.78));
    }

    let land = (height - SEA_LEVEL) / (1.0 - SEA_LEVEL);
    if land < 0.05 {
        lerp_color([214, 198, 146], [188, 177, 123], land / 0.05)
    } else if land < 0.28 {
        lerp_color([124, 168, 92], [92, 146, 77], (land - 0.05) / 0.23)
    } else if land < 0.55 {
        lerp_color([92, 146, 77], [134, 126, 72], (land - 0.28) / 0.27)
    } else if land < 0.78 {
        lerp_color([134, 126, 72], [124, 116, 112], (land - 0.55) / 0.23)
    } else {
        lerp_color([124, 116, 112], [244, 246, 248], (land - 0.78) / 0.22)
    }
}

fn lerp_color(start: [u8; 3], end: [u8; 3], t: f32) -> [u8; 3] {
    [
        lerp(start[0] as f32, end[0] as f32, t).round() as u8,
        lerp(start[1] as f32, end[1] as f32, t).round() as u8,
        lerp(start[2] as f32, end[2] as f32, t).round() as u8,
    ]
}

fn next_seed() -> u32 {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_nanos() as u64)
        .unwrap_or(1);
    let mixed = nanos ^ (nanos >> 17) ^ (nanos << 13);
    (mixed as u32).wrapping_mul(0x9E37_79B9)
}

fn lerp(start: f32, end: f32, t: f32) -> f32 {
    start + (end - start) * t
}
