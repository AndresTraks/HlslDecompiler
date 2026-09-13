uint mip;

Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	uint4 t0;
	tex.GetDimensions(mip, t0.x, t0.y, t0.w);
	return tex.Load(int3((uint2)(512 * texcoord.xy) % t0.xy, min(t0.w - 1, mip)));
}
