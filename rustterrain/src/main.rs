#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::time::{SystemTime, UNIX_EPOCH};

use eframe::egui::{
    self, Align, CentralPanel, Color32, ColorImage, Context, Direction, Layout, PointerButton,
    Pos2, Rect, Sense, TextureHandle, TextureOptions, TopBottomPanel, Vec2, ViewportBuilder,
};
use noise::{NoiseFn, OpenSimplex, Perlin};

const BITMAP_SIZE: usize = 1024;
const BITMAP_PIXELS: usize = BITMAP_SIZE * BITMAP_SIZE;
const MIN_BRUSH_RADIUS: f32 = 4.0;
const MAX_BRUSH_RADIUS: f32 = 160.0;

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
    bitmap: Vec<u8>,
    texture: Option<TextureHandle>,
    texture_dirty: bool,
    brush_radius: f32,
    brush_strength: f32,
    selected_tool: usize,
    last_drag_pos: Option<Pos2>,
    hover_pixel: Option<(usize, usize)>,
    seed: u32,
}

impl TerrainApp {
    fn new(cc: &eframe::CreationContext<'_>) -> Self {
        let seed = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|duration| duration.as_secs() as u32)
            .unwrap_or(1);

        let mut app = Self {
            bitmap: generate_cloud_bitmap(seed),
            texture: None,
            texture_dirty: true,
            brush_radius: 28.0,
            brush_strength: 0.18,
            selected_tool: 0,
            last_drag_pos: None,
            hover_pixel: None,
            seed,
        };
        app.ensure_texture(&cc.egui_ctx);
        app
    }

    fn ensure_texture(&mut self, ctx: &Context) {
        let image = self.build_color_image();
        match &mut self.texture {
            Some(texture) if self.texture_dirty => {
                texture.set(image, TextureOptions::LINEAR);
            }
            None => {
                self.texture =
                    Some(ctx.load_texture("cloud_bitmap", image, TextureOptions::LINEAR));
            }
            _ => {}
        }
        self.texture_dirty = false;
    }

    fn build_color_image(&self) -> ColorImage {
        let mut rgba = Vec::with_capacity(BITMAP_PIXELS * 4);
        for &value in &self.bitmap {
            let t = value as f32 / 255.0;
            let tint = t.powf(0.9);
            rgba.push(lerp(10.0, 244.0, tint) as u8);
            rgba.push(lerp(18.0, 247.0, tint.powf(0.92)) as u8);
            rgba.push(lerp(34.0, 255.0, tint.powf(0.84)) as u8);
            rgba.push(255);
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
                let current = self.bitmap[idx] as f32 / 255.0;
                let updated = (current + amount * falloff).clamp(0.0, 1.0);
                self.bitmap[idx] = (updated * 255.0).round() as u8;
            }
        }

        self.texture_dirty = true;
    }
}

impl eframe::App for TerrainApp {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        TopBottomPanel::top("toolbar_info").show(ctx, |ui| {
            ui.horizontal(|ui| {
                ui.label("Left drag: add clouds");
                ui.separator();
                ui.label("Right drag: remove clouds");
                ui.separator();
                ui.label("Wheel: brush size");
                ui.separator();
                ui.label(format!("Brush {:.0}px", self.brush_radius));
                if let Some((x, y)) = self.hover_pixel {
                    let idx = y * BITMAP_SIZE + x;
                    ui.separator();
                    ui.label(format!("Pixel ({x}, {y}) = {}", self.bitmap[idx]));
                }
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
                    for tool_idx in 0..10 {
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

fn generate_cloud_bitmap(seed: u32) -> Vec<u8> {
    let base = Perlin::new(seed);
    let detail = Perlin::new(seed ^ 0xA53A_9E5D);
    let warp = OpenSimplex::new(seed.wrapping_add(17));
    let highlight = OpenSimplex::new(seed.wrapping_add(97));

    let mut bitmap = vec![0; BITMAP_PIXELS];

    for y in 0..BITMAP_SIZE {
        for x in 0..BITMAP_SIZE {
            let nx = x as f64 / BITMAP_SIZE as f64 - 0.5;
            let ny = y as f64 / BITMAP_SIZE as f64 - 0.5;

            let warp_x = warp.get([nx * 1.6, ny * 1.6, 0.25]) * 0.35;
            let warp_y = warp.get([nx * 1.6, ny * 1.6, 4.75]) * 0.35;
            let sample_x = nx * 2.8 + warp_x;
            let sample_y = ny * 2.8 + warp_y;

            let cloud = fbm_perlin(&base, sample_x, sample_y, 5, 1.0, 2.0, 0.52);
            let detail_cloud =
                fbm_perlin(&detail, sample_x * 2.4, sample_y * 2.4, 4, 1.0, 2.3, 0.46);
            let highlight_mask =
                (highlight.get([sample_x * 1.15, sample_y * 1.15, 1.7]) as f32 * 0.5) + 0.5;

            let coverage = smoothstep(
                -0.10,
                0.70,
                (cloud as f32 * 0.68) + (detail_cloud as f32 * 0.32),
            );
            let density = (coverage * (0.82 + highlight_mask * 0.18)).powf(1.35);

            bitmap[y * BITMAP_SIZE + x] = (density.clamp(0.0, 1.0) * 255.0).round() as u8;
        }
    }

    bitmap
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

fn lerp(start: f32, end: f32, t: f32) -> f32 {
    start + (end - start) * t
}
