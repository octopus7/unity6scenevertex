#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::{
    fs,
    path::PathBuf,
    time::{SystemTime, UNIX_EPOCH},
};

use eframe::egui::{
    self, Align, CentralPanel, Color32, ColorImage, Context, Direction, Layout, PointerButton,
    Pos2, Rect, Sense, TextureHandle, TextureOptions, TopBottomPanel, Vec2, ViewportBuilder,
};
use image::{ColorType, ImageFormat};
use noise::{NoiseFn, OpenSimplex, Perlin};

const BITMAP_SIZE: usize = 1024;
const BITMAP_PIXELS: usize = BITMAP_SIZE * BITMAP_SIZE;
const MIN_BRUSH_RADIUS: f32 = 4.0;
const MAX_BRUSH_RADIUS: f32 = 160.0;
const SEA_LEVEL: f32 = 0.42;
const HEIGHTMAP_FILE: &str = "generated/heightmap_latest.png";

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

struct TerrainApp {
    heightmap: Vec<f32>,
    texture: Option<TextureHandle>,
    texture_dirty: bool,
    brush_radius: f32,
    brush_strength: f32,
    selected_tool: usize,
    last_drag_pos: Option<Pos2>,
    hover_pixel: Option<(usize, usize)>,
    seed: u32,
    save_status: String,
    pending_save: bool,
}

impl TerrainApp {
    fn new(cc: &eframe::CreationContext<'_>) -> Self {
        let mut app = Self {
            heightmap: vec![0.0; BITMAP_PIXELS],
            texture: None,
            texture_dirty: true,
            brush_radius: 28.0,
            brush_strength: 0.028,
            selected_tool: 0,
            last_drag_pos: None,
            hover_pixel: None,
            seed: 0,
            save_status: String::new(),
            pending_save: false,
        };
        app.regenerate_terrain();
        app.ensure_texture(&cc.egui_ctx);
        app
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
            || response.clicked_by(PointerButton::Primary);
        let secondary_active = response.dragged_by(PointerButton::Secondary)
            || response.clicked_by(PointerButton::Secondary);

        if let Some(pointer_pos) = response.interact_pointer_pos() {
            let bitmap_pos = screen_to_bitmap(rect, pointer_pos);
            let hover_x = bitmap_pos.x.round().clamp(0.0, (BITMAP_SIZE - 1) as f32) as usize;
            let hover_y = bitmap_pos.y.round().clamp(0.0, (BITMAP_SIZE - 1) as f32) as usize;
            self.hover_pixel = Some((hover_x, hover_y));

            let delta = if primary_active {
                Some(self.brush_strength)
            } else if secondary_active {
                Some(-self.brush_strength)
            } else {
                None
            };

            if let Some(amount) = delta {
                let previous = self.last_drag_pos.unwrap_or(bitmap_pos);
                self.paint_stroke(previous, bitmap_pos, amount);
                self.last_drag_pos = Some(bitmap_pos);
            } else {
                self.last_drag_pos = None;
            }
        } else {
            self.hover_pixel = None;
            self.last_drag_pos = None;
        }

        if !primary_active && !secondary_active {
            if was_dragging && self.pending_save {
                self.save_heightmap_to_disk();
            }
            self.last_drag_pos = None;
        }
    }

    fn paint_stroke(&mut self, from: Pos2, to: Pos2, amount: f32) {
        let distance = from.distance(to);
        let spacing = (self.brush_radius * 0.35).max(1.0);
        let steps = (distance / spacing).ceil().max(1.0) as usize;

        for step in 0..=steps {
            let t = step as f32 / steps as f32;
            let point = Pos2::new(lerp(from.x, to.x, t), lerp(from.y, to.y, t));
            self.paint_disc(point, amount);
        }
    }

    fn paint_disc(&mut self, center: Pos2, amount: f32) {
        let radius = self.brush_radius;
        let radius_sq = radius * radius;
        let x_min = (center.x - radius).floor().max(0.0) as i32;
        let x_max = (center.x + radius).ceil().min((BITMAP_SIZE - 1) as f32) as i32;
        let y_min = (center.y - radius).floor().max(0.0) as i32;
        let y_max = (center.y + radius).ceil().min((BITMAP_SIZE - 1) as f32) as i32;

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

        self.texture_dirty = true;
        self.pending_save = true;
    }

    fn regenerate_terrain(&mut self) {
        self.seed = next_seed();
        self.heightmap = generate_heightmap(self.seed);
        self.texture_dirty = true;
        self.pending_save = true;
        self.last_drag_pos = None;
        self.save_heightmap_to_disk();
    }

    fn save_heightmap_to_disk(&mut self) {
        let path = PathBuf::from(HEIGHTMAP_FILE);
        if let Some(parent) = path.parent() {
            if let Err(error) = fs::create_dir_all(parent) {
                self.save_status = format!("Save failed: {error}");
                return;
            }
        }

        let mut grayscale = Vec::with_capacity(BITMAP_PIXELS);
        for &height in &self.heightmap {
            grayscale.push((height.clamp(0.0, 1.0) * 255.0).round() as u8);
        }

        match image::save_buffer_with_format(
            &path,
            &grayscale,
            BITMAP_SIZE as u32,
            BITMAP_SIZE as u32,
            ColorType::L8,
            ImageFormat::Png,
        ) {
            Ok(()) => {
                self.save_status = format!("Saved {}", path.display());
                self.pending_save = false;
            }
            Err(error) => {
                self.save_status = format!("Save failed: {error}");
            }
        }
    }
}

impl eframe::App for TerrainApp {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        TopBottomPanel::top("toolbar_info").show(ctx, |ui| {
            ui.horizontal(|ui| {
                ui.label("Left drag: raise terrain");
                ui.separator();
                ui.label("Right drag: lower terrain");
                ui.separator();
                ui.label("Wheel: brush size");
                ui.separator();
                ui.label(format!("Brush {:.0}px", self.brush_radius));
                if let Some((x, y)) = self.hover_pixel {
                    let idx = y * BITMAP_SIZE + x;
                    ui.separator();
                    ui.label(format!("Height ({x}, {y}) = {:.3}", self.heightmap[idx]));
                }
                ui.separator();
                ui.label(format!("Sea {:.2}", SEA_LEVEL));
                ui.separator();
                ui.label(&self.save_status);
                ui.with_layout(Layout::right_to_left(Align::Center), |ui| {
                    ui.label(format!("Seed {}", self.seed));
                });
            });
        });

        TopBottomPanel::bottom("bottom_tools")
            .resizable(false)
            .min_height(68.0)
            .show(ctx, |ui| {
                ui.horizontal_wrapped(|ui| {
                    if ui.button("Regenerate").clicked() {
                        self.regenerate_terrain();
                    }

                    for tool_idx in 1..10 {
                        let label = format!("Tool {:02}", tool_idx + 1);
                        let selected = self.selected_tool == tool_idx;
                        if ui.selectable_label(selected, label).clicked() {
                            self.selected_tool = tool_idx;
                        }
                    }
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
