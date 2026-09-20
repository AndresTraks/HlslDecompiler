Texture2DMS<float4> tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	uint3 dimensions0;
	tex.GetDimensions(dimensions0.x, dimensions0.y, dimensions0.z);
	r0.x = dimensions0.z;
	r0.x = r0.x + -1;
	r1.xy = (int2)texcoord.xy;
	r1.zw = int2(0, 0);
	r0 = tex.Load(r1.xy, r0.x);
	uint3 dimensions1;
	tex.GetDimensions(dimensions1.x, dimensions1.y, dimensions1.z);
	r1.xy = dimensions1.xy;
	r1.x = r1.y + r1.x;
	r1.x = (float)(uint)r1.x;
	o = r0 * r1.x;

	return o;
}
