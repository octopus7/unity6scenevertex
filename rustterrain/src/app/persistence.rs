use std::{
    fs,
    path::{Path, PathBuf},
};

use image::{ColorType, ImageFormat, ImageReader, imageops::FilterType};
use rfd::FileDialog;

use crate::constants::{AUTOSAVE_HEIGHTMAP_FILE, BITMAP_PIXELS, BITMAP_SIZE};

use super::TerrainApp;

impl TerrainApp {
    pub(super) fn autosave_heightmap(&mut self) {
        let path = PathBuf::from(AUTOSAVE_HEIGHTMAP_FILE);
        match self.save_heightmap_png(&path) {
            Ok(()) => {
                self.status_message = format!("Autosaved {}", path.display());
                self.pending_autosave = false;
            }
            Err(error) => {
                self.status_message = format!("Autosave failed: {error}");
            }
        }
    }

    pub(super) fn save_heightmap_dialog(&mut self) {
        self.finish_active_stroke();
        let dialog = self
            .png_dialog()
            .set_file_name(self.suggested_heightmap_name());
        if let Some(path) = dialog.save_file() {
            match self.save_heightmap_png(&path) {
                Ok(()) => {
                    self.active_heightmap_path = Some(path.clone());
                    self.status_message = format!("Saved {}", path.display());
                }
                Err(error) => {
                    self.status_message = format!("Save failed: {error}");
                }
            }
        }
    }

    pub(super) fn load_heightmap_dialog(&mut self) {
        self.finish_active_stroke();
        if let Some(path) = self.png_dialog().pick_file() {
            match self.load_heightmap_png(&path) {
                Ok(()) => {
                    self.active_heightmap_path = Some(path.clone());
                    self.push_history_snapshot();
                    self.status_message = format!("Loaded {}", path.display());
                }
                Err(error) => {
                    self.status_message = format!("Load failed: {error}");
                }
            }
        }
    }

    pub(super) fn export_contours_dialog(&mut self) {
        self.finish_active_stroke();
        let dialog = self
            .png_dialog()
            .set_file_name(self.suggested_contour_name());
        if let Some(path) = dialog.save_file() {
            match self.save_contour_png(&path) {
                Ok(()) => {
                    self.status_message = format!("Exported contours to {}", path.display());
                }
                Err(error) => {
                    self.status_message = format!("Contour export failed: {error}");
                }
            }
        }
    }

    fn png_dialog(&self) -> FileDialog {
        let mut dialog = FileDialog::new().add_filter("PNG image", &["png"]);
        if let Some(active_path) = &self.active_heightmap_path {
            if let Some(parent) = active_path.parent() {
                dialog = dialog.set_directory(parent);
            }
        } else {
            dialog = dialog.set_directory("generated");
        }
        dialog
    }

    fn suggested_heightmap_name(&self) -> String {
        self.active_heightmap_path
            .as_ref()
            .and_then(|path| path.file_name())
            .map(|name| name.to_string_lossy().into_owned())
            .unwrap_or_else(|| "heightmap.png".to_owned())
    }

    fn suggested_contour_name(&self) -> String {
        if let Some(path) = &self.active_heightmap_path {
            if let Some(stem) = path.file_stem() {
                return format!("{}_contours.png", stem.to_string_lossy());
            }
        }

        "heightmap_contours.png".to_owned()
    }

    fn save_heightmap_png(&self, path: &Path) -> Result<(), String> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|error| error.to_string())?;
        }

        let mut grayscale = Vec::with_capacity(BITMAP_PIXELS);
        for &height in &self.heightmap {
            grayscale.push((height.clamp(0.0, 1.0) * 255.0).round() as u8);
        }

        image::save_buffer_with_format(
            path,
            &grayscale,
            BITMAP_SIZE as u32,
            BITMAP_SIZE as u32,
            ColorType::L8,
            ImageFormat::Png,
        )
        .map_err(|error| error.to_string())
    }

    fn load_heightmap_png(&mut self, path: &Path) -> Result<(), String> {
        let reader = ImageReader::open(path).map_err(|error| error.to_string())?;
        let image = reader.decode().map_err(|error| error.to_string())?;
        let grayscale = image.to_luma8();
        let grayscale = if grayscale.width() != BITMAP_SIZE as u32
            || grayscale.height() != BITMAP_SIZE as u32
        {
            image::imageops::resize(
                &grayscale,
                BITMAP_SIZE as u32,
                BITMAP_SIZE as u32,
                FilterType::Triangle,
            )
        } else {
            grayscale
        };

        self.heightmap = grayscale
            .into_raw()
            .into_iter()
            .map(|value| value as f32 / 255.0)
            .collect();
        self.texture_dirty = true;
        self.last_drag_pos = None;
        self.hover_pixel = None;
        self.hover_bitmap_pos = None;
        self.pending_autosave = false;

        if let Err(error) = self.save_heightmap_png(Path::new(AUTOSAVE_HEIGHTMAP_FILE)) {
            self.status_message = format!("Autosave mirror failed: {error}");
        }

        Ok(())
    }

    fn save_contour_png(&self, path: &Path) -> Result<(), String> {
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|error| error.to_string())?;
        }

        let rgba = self.build_contour_image();
        image::save_buffer_with_format(
            path,
            &rgba,
            BITMAP_SIZE as u32,
            BITMAP_SIZE as u32,
            ColorType::Rgba8,
            ImageFormat::Png,
        )
        .map_err(|error| error.to_string())
    }
}
