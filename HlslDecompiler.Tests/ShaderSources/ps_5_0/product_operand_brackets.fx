float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = (1 - texcoord) * (1 - texcoord) * texcoord;
	float t1 = 3 * t0.y;
	return float4(t1 * (3 * t0.x), t1 * (texcoord.x * texcoord.x * texcoord.x), 3 * t0.x, texcoord.y);
}
