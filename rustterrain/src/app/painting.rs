use eframe::egui::Pos2;

use crate::{
    constants::{BITMAP_SIZE, STANDARD_STRENGTH_SCALE},
    tool::{StrokeAction, ToolKind},
    utils::{brush_bounds, lerp},
};

use super::TerrainApp;

impl TerrainApp {
    pub(super) fn stroke_action(
        &self,
        primary_active: bool,
        secondary_active: bool,
    ) -> Option<StrokeAction> {
        match self.active_tool {
            ToolKind::Standard if primary_active => Some(StrokeAction::Standard(
                self.brush_strength * STANDARD_STRENGTH_SCALE,
            )),
            ToolKind::Standard if secondary_active => Some(StrokeAction::Standard(
                -self.brush_strength * STANDARD_STRENGTH_SCALE,
            )),
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

    pub(super) fn apply_stroke(&mut self, from: Pos2, to: Pos2, action: StrokeAction) {
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
}
