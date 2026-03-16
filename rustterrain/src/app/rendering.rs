use eframe::egui::{ColorImage, Context, TextureOptions};

use crate::{
    constants::{BITMAP_PIXELS, BITMAP_SIZE, SEA_LEVEL},
    utils::{contour_bucket, lerp, smoothstep},
};

use super::TerrainApp;

impl TerrainApp {
    pub(super) fn ensure_texture(&mut self, ctx: &Context) {
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

    pub(super) fn build_contour_image(&self) -> Vec<u8> {
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
