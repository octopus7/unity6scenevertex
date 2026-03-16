use eframe::egui::Pos2;

use crate::{
    constants::BITMAP_SIZE,
    tool::{StrokeAction, ToolKind},
    utils::{brush_bounds, lerp},
};

use super::{TerrainApp, stroke::StrokePixelState, stroke::StrokeSession};

impl TerrainApp {
    pub(super) fn stroke_action(
        &self,
        primary_active: bool,
        secondary_active: bool,
    ) -> Option<StrokeAction> {
        match self.active_tool {
            ToolKind::Standard if primary_active => Some(StrokeAction::Standard { direction: 1 }),
            ToolKind::Standard if secondary_active => {
                Some(StrokeAction::Standard { direction: -1 })
            }
            ToolKind::TargetHeight if primary_active => Some(StrokeAction::TargetHeight),
            ToolKind::Blur if primary_active => Some(StrokeAction::Blur),
            _ => None,
        }
    }

    pub(super) fn sample_height(&mut self, x: usize, y: usize) {
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

    pub(super) fn begin_stroke(&mut self, action: StrokeAction) {
        let restart = self
            .active_stroke
            .as_ref()
            .is_none_or(|session| session.action() != action);

        if restart {
            self.finish_active_stroke();
            self.active_stroke = Some(StrokeSession::new(action));
        }
    }

    pub(super) fn finish_active_stroke(&mut self) {
        let Some(session) = self.active_stroke.take() else {
            return;
        };

        self.last_drag_pos = None;
        if !session.changed() {
            self.pending_autosave = false;
            return;
        }

        self.push_history_snapshot();
        self.autosave_heightmap();
    }

    pub(super) fn apply_stroke(&mut self, from: Pos2, to: Pos2, action: StrokeAction) {
        self.begin_stroke(action);

        let distance = from.distance(to);
        let spacing = (self.brush_radius * 0.35).max(1.0);
        let steps = (distance / spacing).ceil().max(1.0) as usize;

        for step in 0..=steps {
            let t = step as f32 / steps as f32;
            let point = Pos2::new(lerp(from.x, to.x, t), lerp(from.y, to.y, t));
            match action {
                StrokeAction::Standard { direction } => {
                    self.paint_standard_disc(point, direction);
                }
                StrokeAction::TargetHeight => self.paint_target_disc(point),
                StrokeAction::Blur => self.paint_blur_disc(point),
            }
        }

        self.texture_dirty = true;
        self.pending_autosave = true;
    }

    fn paint_standard_disc(&mut self, center: Pos2, direction: i8) {
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

                let flow = self.brush_flow * (1.0 - (distance_sq / radius_sq));
                let pixel = self.stroke_accumulate_pixel(x as usize, y as usize, flow);
                let delta = self.brush_opacity * pixel.coverage * direction as f32;
                let next = (pixel.base + delta).clamp(0.0, 1.0);
                self.apply_stroke_result(x as usize, y as usize, next);
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

                let flow = self.brush_flow * (1.0 - (distance_sq / radius_sq));
                let pixel = self.stroke_accumulate_pixel(x as usize, y as usize, flow);
                let alpha = (self.brush_opacity * pixel.coverage).clamp(0.0, 1.0);
                let next = lerp(pixel.base, self.target_height, alpha);
                self.apply_stroke_result(x as usize, y as usize, next);
            }
        }
    }

    fn paint_blur_disc(&mut self, center: Pos2) {
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

                let flow = self.brush_flow * (1.0 - (distance_sq / radius_sq));
                let pixel = self.stroke_accumulate_pixel(x as usize, y as usize, flow);
                let blurred = self.sample_blur_from_stroke_start(x as usize, y as usize);
                let alpha = (self.brush_opacity * pixel.coverage).clamp(0.0, 1.0);
                let next = lerp(pixel.base, blurred, alpha);
                self.apply_stroke_result(x as usize, y as usize, next);
            }
        }
    }

    fn sample_blur_from_stroke_start(&mut self, x: usize, y: usize) -> f32 {
        let mut blurred_sum = 0.0;
        let mut weight_sum = 0.0;

        for kernel_y in -1..=1 {
            for kernel_x in -1..=1 {
                let weight = match (kernel_x, kernel_y) {
                    (0, 0) => 4.0,
                    (0, _) | (_, 0) => 2.0,
                    _ => 1.0,
                };
                let sample_x = (x as i32 + kernel_x).clamp(0, BITMAP_SIZE as i32 - 1) as usize;
                let sample_y = (y as i32 + kernel_y).clamp(0, BITMAP_SIZE as i32 - 1) as usize;
                blurred_sum += self.stroke_original_value(sample_x, sample_y) * weight;
                weight_sum += weight;
            }
        }

        blurred_sum / weight_sum
    }

    fn stroke_original_value(&mut self, x: usize, y: usize) -> f32 {
        self.active_stroke
            .as_mut()
            .expect("active stroke should exist")
            .original_value(&self.heightmap, x, y)
    }

    fn stroke_accumulate_pixel(&mut self, x: usize, y: usize, flow: f32) -> StrokePixelState {
        self.active_stroke
            .as_mut()
            .expect("active stroke should exist")
            .accumulate(&self.heightmap, x, y, flow.clamp(0.0, 1.0))
    }

    fn apply_stroke_result(&mut self, x: usize, y: usize, next: f32) {
        let idx = y * BITMAP_SIZE + x;
        let previous = self.heightmap[idx];
        if (next - previous).abs() <= f32::EPSILON {
            return;
        }

        self.heightmap[idx] = next;
        if let Some(session) = &mut self.active_stroke {
            session.mark_changed();
        }
    }
}
