sampler2D directions : register(s1);
sampler2D normals;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = tex2D(directions, i.texcoord).xyz;
	float3 t1 = tex2D(normals, i.texcoord).xyz;
	return float4(dot((half3)(2 * t1 - 1), half3((half)dot(i.texcoord1, t0), (half)dot(i.texcoord2, t0), (half)dot(i.texcoord3, t0))), 0, 0, 1);
}
