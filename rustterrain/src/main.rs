#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod app;
mod constants;
mod terrain;
mod tool;
mod utils;

use app::TerrainApp;
use eframe::egui::ViewportBuilder;

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
