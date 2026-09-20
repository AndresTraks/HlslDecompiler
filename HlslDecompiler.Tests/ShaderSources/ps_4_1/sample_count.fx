Texture2DMS<float4> tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	uint3 t0;
	tex.GetDimensions(t0.x, t0.y, t0.z);
	return tex.Load((int2)texcoord.xy, t0.z - 1) * (float4)(t0.y + t0.x);
}
