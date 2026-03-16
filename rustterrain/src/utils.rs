use std::time::{SystemTime, UNIX_EPOCH};

use eframe::egui::{Pos2, Rect};

use crate::constants::{BITMAP_SIZE, MIN_CONTOUR_STEP};

pub fn next_seed() -> u32 {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_nanos() as u64)
        .unwrap_or(1);
    let mixed = nanos ^ (nanos >> 17) ^ (nanos << 13);
    (mixed as u32).wrapping_mul(0x9E37_79B9)
}

pub fn lerp(start: f32, end: f32, t: f32) -> f32 {
    start + (end - start) * t
}

pub fn smoothstep(edge0: f32, edge1: f32, x: f32) -> f32 {
    let t = ((x - edge0) / (edge1 - edge0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

pub fn brush_bounds(center: Pos2, radius: f32) -> (i32, i32, i32, i32) {
    (
        (center.x - radius).floor().max(0.0) as i32,
        (center.x + radius).ceil().min((BITMAP_SIZE - 1) as f32) as i32,
        (center.y - radius).floor().max(0.0) as i32,
        (center.y + radius).ceil().min((BITMAP_SIZE - 1) as f32) as i32,
    )
}

pub fn bitmap_to_screen(rect: Rect, position: Pos2) -> Pos2 {
    let u = position.x / (BITMAP_SIZE as f32 - 1.0);
    let v = position.y / (BITMAP_SIZE as f32 - 1.0);
    Pos2::new(
        rect.left() + u * rect.width(),
        rect.top() + v * rect.height(),
    )
}

pub fn screen_to_bitmap(rect: Rect, position: Pos2) -> Pos2 {
    let u = ((position.x - rect.left()) / rect.width()).clamp(0.0, 1.0);
    let v = ((position.y - rect.top()) / rect.height()).clamp(0.0, 1.0);
    Pos2::new(
        u * (BITMAP_SIZE as f32 - 1.0),
        v * (BITMAP_SIZE as f32 - 1.0),
    )
}

pub fn contour_bucket(height: f32, contour_step: f32) -> i32 {
    (height / contour_step.max(MIN_CONTOUR_STEP)).floor() as i32
}
