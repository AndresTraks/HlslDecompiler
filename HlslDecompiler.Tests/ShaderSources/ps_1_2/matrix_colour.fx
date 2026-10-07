sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = tex2D(s0, i.texcoord).xyz;
	return float4(dot(i.texcoord1, t0) * i.color.x, dot(i.texcoord2, t0) * i.color.y, dot(i.texcoord3, t0) * i.color.z, i.color.w);
}
