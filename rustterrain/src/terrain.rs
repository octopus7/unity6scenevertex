use noise::{NoiseFn, OpenSimplex, Perlin};

use crate::{
    constants::{BITMAP_PIXELS, BITMAP_SIZE},
    utils::smoothstep,
};

pub fn generate_heightmap(seed: u32) -> Vec<f32> {
    let land_macro = OpenSimplex::new(seed);
    let rolling = Perlin::new(seed ^ 0x6D2B_79F5);
    let mountain_mask_noise = OpenSimplex::new(seed ^ 0x1B56_C4E9);
    let ridge_noise = Perlin::new(seed ^ 0x9E37_79B9);
    let basin_macro = OpenSimplex::new(seed ^ 0xA53A_9E5D);
    let basin_detail = Perlin::new(seed ^ 0xC13F_A9A9);

    let mut heightmap = vec![0.0; BITMAP_PIXELS];

    for y in 0..BITMAP_SIZE {
        for x in 0..BITMAP_SIZE {
            let nx = x as f64 / BITMAP_SIZE as f64 - 0.5;
            let ny = y as f64 / BITMAP_SIZE as f64 - 0.5;

            let broad_land =
                (((land_macro.get([nx * 1.15, ny * 1.15, 0.3]) as f32) * 0.5) + 0.5).powf(1.15);
            let rolling_land = fbm_perlin(&rolling, nx * 2.8, ny * 2.8, 5, 1.0, 2.05, 0.53);

            let mountain_region = smoothstep(
                0.48,
                0.82,
                ((mountain_mask_noise.get([nx * 1.7, ny * 1.7, 1.9]) as f32) * 0.5) + 0.5,
            );
            let ridges = ridged_fbm(&ridge_noise, nx * 4.4, ny * 4.4, 5, 1.0, 2.1, 0.55);
            let mountains = mountain_region * ridges.powf(1.6) * 0.48;

            let plains =
                0.26 + broad_land * 0.28 + ((rolling_land as f32) * 0.14) + mountain_region * 0.03;

            let basin_region = smoothstep(
                0.58,
                0.86,
                ((basin_macro.get([nx * 1.1, ny * 1.1, 5.7]) as f32) * 0.5) + 0.5,
            );
            let basin_shape = smoothstep(
                0.38,
                0.92,
                ((basin_detail.get([nx * 3.2, ny * 3.2, 2.4]) as f32) * 0.5) + 0.5,
            );
            let basins = basin_region * (0.16 + basin_shape * 0.26);

            let altitude = (plains + mountains - basins).clamp(0.0, 1.0);
            heightmap[y * BITMAP_SIZE + x] = altitude;
        }
    }

    heightmap
}

fn fbm_perlin(
    noise: &Perlin,
    x: f64,
    y: f64,
    octaves: usize,
    mut amplitude: f64,
    lacunarity: f64,
    gain: f64,
) -> f64 {
    let mut frequency = 1.0;
    let mut sum = 0.0;
    let mut amplitude_sum = 0.0;

    for octave in 0..octaves {
        sum += noise.get([x * frequency, y * frequency, octave as f64 * 0.73]) * amplitude;
        amplitude_sum += amplitude;
        amplitude *= gain;
        frequency *= lacunarity;
    }

    if amplitude_sum == 0.0 {
        0.0
    } else {
        sum / amplitude_sum
    }
}

fn ridged_fbm(
    noise: &Perlin,
    x: f64,
    y: f64,
    octaves: usize,
    mut amplitude: f64,
    lacunarity: f64,
    gain: f64,
) -> f32 {
    let mut frequency = 1.0;
    let mut sum = 0.0;
    let mut amplitude_sum = 0.0;

    for octave in 0..octaves {
        let sample = noise.get([x * frequency, y * frequency, octave as f64 * 0.61]);
        let ridge = 1.0 - sample.abs();
        sum += ridge * amplitude;
        amplitude_sum += amplitude;
        amplitude *= gain;
        frequency *= lacunarity;
    }

    if amplitude_sum == 0.0 {
        0.0
    } else {
        (sum / amplitude_sum) as f32
    }
}
