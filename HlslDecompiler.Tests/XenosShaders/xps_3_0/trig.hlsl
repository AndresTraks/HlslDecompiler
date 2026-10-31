float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = min(texcoord.x, texcoord.w);
	float t1 = max(texcoord.x, texcoord.w);
	float t2 = abs(texcoord.w);
	float t3 = abs(texcoord.x);
	float t4 = min(t2, t3) / max(t2, t3);
	float t5 = t4 * t4;
	float t6 = t4 * (t5 * (t5 * (t5 * (0.0208350997 * t5 - 0.0851330012) + 0.180141002) - 0.330299497) + 0.999866009);
	float t7 = (1.57079637 - 2 * t6) * ((t3 < t2) ? 1 : 0) + t6 - 3.14159274 * ((texcoord.x < -texcoord.x) ? 1 : 0);
	return float4(sin(texcoord.x), cos(texcoord.y), sin(texcoord.z) / cos(texcoord.z), ((t0 < -t0) ? 1 : 0) * step(-t1, t1) * -2 * t7 + t7);
}
