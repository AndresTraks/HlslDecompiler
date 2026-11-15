float4 k;
sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2D(s0, i.texcoord.xy);
	o.xyz = i.texcoord1.xyz * r0.xxx + k.xyz;
	o.w = i.texcoord.x * r0.x + k.w;

	return o;
}
