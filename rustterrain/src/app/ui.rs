use eframe::egui::{
    self, Align, Button, CentralPanel, Color32, Context, Key, Layout, Sense, SidePanel, Slider,
    Stroke, TopBottomPanel, Vec2,
};

use crate::{
    constants::{
        AUTOSAVE_HEIGHTMAP_FILE, BITMAP_SIZE, HISTORY_CAPACITY, MAX_BRUSH_RADIUS, MAX_CONTOUR_STEP,
        MIN_BRUSH_RADIUS, MIN_CONTOUR_STEP, SEA_LEVEL,
    },
    tool::ToolKind,
};

use super::TerrainApp;

impl TerrainApp {
    const BRUSH_SHORTCUT_STEP: f32 = 4.0;

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
                ui.add(Slider::new(&mut self.brush_flow, 0.01..=1.0).text("Flow"));
                ui.add(Slider::new(&mut self.brush_opacity, 0.01..=1.0).text("Opacity"));
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

    fn history_ui(&mut self, ctx: &Context) {
        TopBottomPanel::bottom("history_panel")
            .resizable(false)
            .min_height(62.0)
            .show(ctx, |ui| {
                ui.horizontal(|ui| {
                    let undo_enabled = self.history_undo_steps() > 0;
                    if ui.add_enabled(undo_enabled, Button::new("Undo")).clicked() {
                        self.undo();
                    }

                    let redo_enabled = self.history_redo_steps() > 0;
                    if ui.add_enabled(redo_enabled, Button::new("Redo")).clicked() {
                        self.redo();
                    }

                    ui.separator();
                    ui.label(format!(
                        "Stack {}/{}  Undo {}  Redo {}  Tile {}  History {}",
                        self.history_cursor() + 1,
                        self.history_len(),
                        self.history_undo_steps(),
                        self.history_redo_steps(),
                        format_bytes(self.tile_memory_bytes()),
                        format_bytes(self.history_memory_bytes()),
                    ));
                });

                ui.horizontal(|ui| {
                    for slot in 0..HISTORY_CAPACITY {
                        let (rect, _) =
                            ui.allocate_exact_size(Vec2::new(18.0, 14.0), Sense::hover());
                        let fill = if slot == self.history_cursor() && slot < self.history_len() {
                            Color32::from_rgb(240, 190, 84)
                        } else if slot < self.history_cursor() {
                            Color32::from_rgb(87, 146, 120)
                        } else if slot < self.history_len() {
                            Color32::from_rgb(106, 121, 148)
                        } else {
                            Color32::from_gray(36)
                        };

                        ui.painter().rect_filled(rect, 3.0, fill);
                        ui.painter().rect_stroke(
                            rect,
                            3.0,
                            Stroke::new(1.0, Color32::from_gray(90)),
                            egui::StrokeKind::Outside,
                        );
                    }
                });
            });
    }

    fn handle_shortcuts(&mut self, ctx: &Context) {
        let (undo_pressed, redo_pressed, decrease_brush, increase_brush, pointer_down) = ctx.input(|input| {
            (
                input.modifiers.command && input.key_pressed(Key::Z),
                input.modifiers.command && input.key_pressed(Key::Y),
                input.key_pressed(Key::OpenBracket),
                input.key_pressed(Key::CloseBracket),
                input.pointer.any_down(),
            )
        });

        if pointer_down || self.active_stroke.is_some() {
            return;
        }

        if undo_pressed {
            self.undo();
        } else if redo_pressed {
            self.redo();
        }

        if decrease_brush {
            self.adjust_brush_radius(-Self::BRUSH_SHORTCUT_STEP);
        }

        if increase_brush {
            self.adjust_brush_radius(Self::BRUSH_SHORTCUT_STEP);
        }
    }
}

fn format_bytes(bytes: usize) -> String {
    const UNITS: [&str; 4] = ["B", "KB", "MB", "GB"];

    let mut value = bytes as f64;
    let mut unit = 0;
    while value >= 1024.0 && unit + 1 < UNITS.len() {
        value /= 1024.0;
        unit += 1;
    }

    if unit == 0 {
        format!("{bytes} {}", UNITS[unit])
    } else {
        format!("{value:.2} {}", UNITS[unit])
    }
}

impl eframe::App for TerrainApp {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        self.handle_shortcuts(ctx);
        self.controls_ui(ctx);
        self.history_ui(ctx);

        TopBottomPanel::top("toolbar_info").show(ctx, |ui| {
            ui.horizontal(|ui| {
                ui.label(self.active_tool.label());
                ui.separator();
                ui.label(self.active_tool.description());
                ui.separator();
                ui.label("Wheel / [ ]: brush size");
                ui.separator();
                ui.label(format!("Brush {:.0}px", self.brush_radius));
                ui.separator();
                ui.label(format!("Flow {:.2}", self.brush_flow));
                ui.separator();
                ui.label(format!("Opacity {:.2}", self.brush_opacity));
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
