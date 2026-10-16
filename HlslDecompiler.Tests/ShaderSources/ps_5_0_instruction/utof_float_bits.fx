cbuffer C : register(b0)
{
	float4 f;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	o.y = (float)asint(texcoord.y);
	o.w = 1;
	r0 = (float2)asuint(f.xz);
	o.z = r0.y * texcoord.x;
	o.x = r0.x;

	return o;
}
