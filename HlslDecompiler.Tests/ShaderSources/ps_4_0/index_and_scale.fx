float4 palette[4];
uint stride;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		t0 = palette[t1 * stride & 3] * ((float4)t1 + texcoord.x) + t0;
	}
	return t0 + (float4)(((uint4)asint(texcoord.y) >> 23) & 255);
}
