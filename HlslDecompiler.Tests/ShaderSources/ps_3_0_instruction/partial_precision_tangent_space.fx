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
	float4 o;

	float4 r0;
	float4 r1;
	half3 r2;
	r0 = tex2D(normals, i.texcoord.xy);
	r0.xyz = half3(r0.xyz * 2 + -1);
	r1 = tex2D(directions, i.texcoord.xy);
	r2.x = dot(i.texcoord1.xyz, r1.xyz);
	r2.y = dot(i.texcoord2.xyz, r1.xyz);
	r2.z = dot(i.texcoord3.xyz, r1.xyz);
	o.x = dot(r0.xyz, r2.xyz);
	o.yzw = float3(0, 0, 1);

	return o;
}
