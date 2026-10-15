SamplerComparisonState cmp;
Texture2D<uint2> stencil;
Texture2D<float> depth;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 uv : UV;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	r0.xy = (int2)i.sv_position.xy;
	r0.zw = int2(0, 0);
	r0.xy = stencil.Load(r0.xyz).xy;
	r0.z = r0.x & 255;
	o.y = (float)(uint)r0.z;
	o.w = 1;
	o.x = depth.SampleCmpLevelZero(cmp, i.uv.xy, asfloat(r0.y));
	o.z = asfloat((uint)r0.x >> 8);

	return o;
}
