use eframe::egui::{
    self, Align, CentralPanel, Color32, Context, Direction, Layout, PointerButton, Pos2, Rect,
    Sense, SidePanel, Slider, Stroke, TopBottomPanel, Ui, Vec2,
};

use crate::{
    constants::{
        AUTOSAVE_HEIGHTMAP_FILE, BITMAP_SIZE, MAX_BRUSH_RADIUS, MAX_CONTOUR_STEP, MIN_BRUSH_RADIUS,
        MIN_CONTOUR_STEP, SEA_LEVEL,
    },
    tool::ToolKind,
    utils::{bitmap_to_screen, screen_to_bitmap},
};

use super::TerrainApp;

impl TerrainApp {
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

    fn canvas_ui(&mut self, ui: &mut Ui) {
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
                        Stroke::new(1.0, Color32::from_gray(70)),
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

            if let Some(action) = self.stroke_action(primary_active, secondary_active) {
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

    fn draw_brush_preview(&self, ui: &mut Ui, rect: Rect) {
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
