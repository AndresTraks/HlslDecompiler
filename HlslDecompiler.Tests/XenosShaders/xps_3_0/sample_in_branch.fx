float4 k;
sampler2D s;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = k;
	[branch]
	if (((0.5 < texcoord.x) ? 1 : 0) != 0) {
		t0 = tex2D(s, texcoord) * k;
	}
	return t0;
}
