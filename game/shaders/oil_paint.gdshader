// Oil-paint post-process for the whole game view.
//
// 1. Kuwahara filter: for each pixel, look at four overlapping square regions and keep the
//    mean colour of the least-varied one. Flat areas become smooth "strokes" while edges stay
//    sharp, which is the classic painted look. It samples a sparse 5x5 grid (25 reads per pixel,
//    spaced `spread` pixels apart) instead of every pixel in the window, which keeps it cheap
//    on high-resolution screens.
// 2. Canvas weave and grain so it reads as paint on fabric.
// 3. A warm varnish tint and a soft vignette.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform bool enabled = true;
uniform float spread : hint_range(0.5, 4.0) = 2.0;
uniform float canvas_grain : hint_range(0.0, 0.25) = 0.07;
uniform float varnish : hint_range(0.0, 1.0) = 0.35;
uniform float vignette : hint_range(0.0, 1.0) = 0.22;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec3 result;

	if (!enabled) {
		result = texture(screen_tex, uv).rgb;
	} else {
		vec2 px = SCREEN_PIXEL_SIZE;
		vec3 mean[4];
		vec3 sq[4];
		for (int k = 0; k < 4; k++) {
			mean[k] = vec3(0.0);
			sq[k] = vec3(0.0);
		}

		for (int j = -2; j <= 2; j++) {
			for (int i = -2; i <= 2; i++) {
				vec3 c = texture(screen_tex, uv + vec2(float(i), float(j)) * px * spread).rgb;
				vec3 c2 = c * c;
				if (i <= 0 && j <= 0) { mean[0] += c; sq[0] += c2; }
				if (i >= 0 && j <= 0) { mean[1] += c; sq[1] += c2; }
				if (i <= 0 && j >= 0) { mean[2] += c; sq[2] += c2; }
				if (i >= 0 && j >= 0) { mean[3] += c; sq[3] += c2; }
			}
		}

		float n = 9.0;
		float best = 1e9;
		result = vec3(0.0);
		for (int k = 0; k < 4; k++) {
			vec3 m = mean[k] / n;
			vec3 v = abs(sq[k] / n - m * m);
			float sigma = v.r + v.g + v.b;
			if (sigma < best) {
				best = sigma;
				result = m;
			}
		}

		// Canvas weave: a fine cross-hatch plus per-2px grain.
		vec2 fc = FRAGCOORD.xy;
		float weave = sin(fc.x * 1.9) * sin(fc.y * 1.9) * 0.5 + (hash(floor(fc * 0.5)) - 0.5);
		result *= 1.0 + weave * canvas_grain;

		// Warm, slightly yellowed varnish.
		result = mix(result, result * vec3(1.06, 0.98, 0.86), varnish);
	}

	float vig = smoothstep(0.9, 0.3, distance(uv, vec2(0.5)));
	result *= mix(1.0 - vignette, 1.0, vig);
	COLOR = vec4(result, 1.0);
}
