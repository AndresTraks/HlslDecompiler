Texture2DMSArray<float4> tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	uint4 t0;
	tex.GetDimensions(t0.x, t0.y, t0.z, t0.w);
	return tex.Load((int3)texcoord.xyz, t0.w - 1) * (float4)t0.z;
}
