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
	float t0 = saturate(dot(normalize(i.texcoord1), normalize(i.texcoord2)));
	float4 t1 = tex2D(detail, i.texcoord);
	return float4(t0 * (t1.xyz * i.color.xyz), t1.w * i.color.w);
}
