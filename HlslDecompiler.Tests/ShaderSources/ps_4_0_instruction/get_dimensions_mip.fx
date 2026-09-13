uint mip;

Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	r0.xy = texcoord.xy * float2(512, 512);
	r0.xy = (uint2)r0.xy;
	uint4 dimensions0 = 0;
	tex.GetDimensions(mip, dimensions0.x, dimensions0.y, dimensions0.w);
	r1 = dimensions0;
	r0.xy = r0.xy % r1.xy;
	r1.x = r1.w + -1;
	r0.zw = min(r1.xx, mip);
	o = tex.Load(r0.xyz);

	return o;
}
