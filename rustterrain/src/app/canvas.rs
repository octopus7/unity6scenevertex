use eframe::egui::{
    self, Color32, Context, Direction, Layout, PointerButton, Pos2, Rect, Sense, Stroke, Ui, Vec2,
};

use crate::{
    constants::BITMAP_SIZE,
    tool::ToolKind,
    utils::{bitmap_to_screen, screen_to_bitmap},
};

use super::TerrainApp;

impl TerrainApp {
    pub(super) fn canvas_ui(&mut self, ui: &mut Ui) {
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
        let was_stroking = self.active_stroke.is_some();

        if response.hovered() {
            let scroll_delta = ctx.input(|input| input.raw_scroll_delta.y);
            if scroll_delta.abs() > f32::EPSILON {
                self.adjust_brush_radius(scroll_delta * 0.05);
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
            if was_stroking {
                self.finish_active_stroke();
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
