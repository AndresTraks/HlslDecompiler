float2 constant2;
float3 constant3;
float4 constant4;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(VS_IN i) : POSITION
{
	float4 o;

	float2 r0;
	o.x = dot(constant4, i.position);
	o.y = dot(constant3.xyz, i.texcoord.xyz);
	r0.xy = constant2.xy * i.texcoord1.xy;
	o.z = r0.y + r0.x;
	o.w = 4;

	return o;
}
