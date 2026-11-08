float4 k[16];

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = abs(texcoord.xy) - frac(abs(texcoord.xy));
	float2 t1 = texcoord.xy >= 0 ? t0 : -t0;
	return k[t1.x] + k[t1.y + 3];
}
