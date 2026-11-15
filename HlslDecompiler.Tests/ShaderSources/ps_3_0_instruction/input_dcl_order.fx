sampler2D detail;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord2 : TEXCOORD2;
	float4 color : COLOR;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float3 r0;
	float4 r1;
	r0 = normalize(i.texcoord1.xyz);
	r1.xyz = normalize(i.texcoord2.xyz);
	r0.x = saturate(dot(r0.xyz, r1.xyz));
	r1 = tex2D(detail, i.texcoord.xy);
	r1 = r1 * i.color;
	o.xyz = r0.xxx * r1.xyz;
	o.w = r1.w;

	return o;
}
