Buffer<float4> colours;
Buffer<uint> indices;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0.x = texcoord.x * 16;
	r0.x = (int)r0.x;
	r0 = indices.Load(r0.x);
	r0 = colours.Load(r0.x);
	o = r0 * texcoord.y;

	return o;
}
