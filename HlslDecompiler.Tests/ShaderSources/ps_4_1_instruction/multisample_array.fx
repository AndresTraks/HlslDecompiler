Texture2DMSArray<float4> tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	uint4 dimensions0;
	tex.GetDimensions(dimensions0.x, dimensions0.y, dimensions0.z, dimensions0.w);
	r0.x = dimensions0.w;
	r0.x = r0.x + -1;
	r1.xyz = (int3)texcoord.xyz;
	r1.w = 0;
	r0 = tex.Load(r1.xyz, r0.x);
	uint4 dimensions1;
	tex.GetDimensions(dimensions1.x, dimensions1.y, dimensions1.z, dimensions1.w);
	r1.x = dimensions1.z;
	r1.x = (float)(uint)r1.x;
	o = r0 * r1.x;

	return o;
}
