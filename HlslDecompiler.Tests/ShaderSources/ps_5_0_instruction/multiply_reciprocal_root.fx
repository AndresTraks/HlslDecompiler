cbuffer c : register(b0)
{
	float4 light;
};

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0.x = abs(light.w) + 1;
	r0.x = rsqrt(r0.x);
	r0.yzw = -(texcoord.xyz) + light.xyz;
	o.xyz = r0.xxx * r0.yzw;
	o.w = 1;

	return o;
}
