sampler2D s;
float2 s_size;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = ddx(texcoord.yx) * s_size;
	float2 t1 = ddy(texcoord.yx) * s_size;
	float t2 = 0.5 * log2(max(dot(t0, t0), dot(t1, t1)));
	return float4((t2 < 1) ? 1 : 0, -((t2 < 1) ? 1 : 0) >= 0 ? (t2 < 2) ? 1 : 0 : 0, -((t2 < 1) ? 1 : 0) >= 0 ? step(2, t2) : 0, 1);
}
