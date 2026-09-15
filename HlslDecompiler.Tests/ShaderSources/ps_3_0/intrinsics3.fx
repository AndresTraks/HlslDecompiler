float4 a;
float4 b;

struct PS_IN
{
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : COLOR
{
	return float4(normalize(cross(i.normal, i.texcoord)), dot(a.xy, b.xy) + length(a.xyz - b.xyz));
}
