float4 light;
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
	float3 t0 = tex2D(normals, i.texcoord).xyz;
	return float4(dot(2 * t0 - 1, float3(dot(light.xyz, i.texcoord1), dot(light.xyz, i.texcoord2), dot(light.xyz, i.texcoord3))) * light.w, 0, 0, 1);
}
