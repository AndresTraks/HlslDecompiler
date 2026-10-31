float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = -step(0, texcoord) * step(texcoord, 0) + 1;
	float t1 = max(max(-t0.x, -t0.z), max(-t0.y, -t0.w));
	float t2 = max(max(t0.x, t0.z), max(t0.y, t0.w)) == 0 ? 0.0 : 0.5;
	return t1 == 0 ? t2 : 1;
}
