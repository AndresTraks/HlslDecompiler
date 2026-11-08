float4 k[16];

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = trunc(texcoord.xy);
	return k[t0.x] + k[t0.y + 3];
}
