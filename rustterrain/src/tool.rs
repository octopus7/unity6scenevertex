use eframe::egui::Color32;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ToolKind {
    Standard,
    TargetHeight,
    PickHeight,
    Blur,
}

impl ToolKind {
    pub fn label(self) -> &'static str {
        match self {
            Self::Standard => "Standard",
            Self::TargetHeight => "Target Height",
            Self::PickHeight => "Pick Height",
            Self::Blur => "Blur",
        }
    }

    pub fn description(self) -> &'static str {
        match self {
            Self::Standard => "Left drag raises terrain. Right drag lowers terrain.",
            Self::TargetHeight => {
                "Left drag moves terrain toward the target height without overshooting."
            }
            Self::PickHeight => {
                "Click the canvas to sample a height, then the previous tool is restored."
            }
            Self::Blur => "Left drag smooths sharp height transitions inside the brush.",
        }
    }

    pub fn preview_color(self) -> Color32 {
        match self {
            Self::Standard => Color32::from_rgb(239, 196, 73),
            Self::TargetHeight => Color32::from_rgb(76, 186, 182),
            Self::PickHeight => Color32::from_rgb(242, 242, 242),
            Self::Blur => Color32::from_rgb(202, 118, 74),
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum StrokeAction {
    Standard { direction: i8 },
    TargetHeight,
    Blur,
}
