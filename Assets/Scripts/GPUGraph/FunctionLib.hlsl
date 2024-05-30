#ifndef FUNCTION_LIB_H
#define FUNCTION_LIB_H

#define PI 3.14159265358979323846



float3 Wave (float u, float v, float t) 
{
	float3 p;
	p.x = u;
	p.y = sin(PI * (u + v + t));
	p.z = v;
	return p;
}

float3 MultiWave (float u, float v, float t) {
	float3 p;
	p.x = u;
	p.y = sin(PI * (u + 0.5 * t));
	p.y += 0.5 * sin(2.0 * PI * (v + t));
	p.y += sin(PI * (u + v + 0.25 * t));
	p.y *= 1.0 / 2.5;
	p.z = v;
	return p;
}

float3 Ripple (float u, float v, float t) {
	float d = sqrt(u * u + v * v);
	float3 p;
	p.x = u;
	p.y = sin(PI * (4.0 * d - t));
	p.y /= 1.0 + 10.0 * d;
	p.z = v;
	return p;
}

float3 Sphere (float u, float v, float t) {
	float r = 0.9 + 0.1 * sin(PI * (6.0 * u + 4.0 * v + t));
	float s = r * cos(0.5 * PI * v);
	float3 p;
	p.x = s * sin(PI * u);
	p.y = r * sin(0.5 * PI * v);
	p.z = s * cos(PI * u);
	return p;
}

float3 Torus (float u, float v, float t) {
	float r1 = 0.7 + 0.1 * sin(PI * (6.0 * u + 0.5 * t));
	float r2 = 0.15 + 0.05 * sin(PI * (8.0 * u + 4.0 * v + 2.0 * t));
	float s = r2 * cos(PI * v) + r1;
	float3 p;
	p.x = s * sin(PI * u);
	p.y = r2 * sin(PI * v);
	p.z = s * cos(PI * u);
	return p;
}

#endif