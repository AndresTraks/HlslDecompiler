Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	uint2 t0;
	tex.GetDimensions(t0.x, t0.y);
	return tex.Load(int3((uint2)(512 * texcoord.xy) % t0, 0));
}
