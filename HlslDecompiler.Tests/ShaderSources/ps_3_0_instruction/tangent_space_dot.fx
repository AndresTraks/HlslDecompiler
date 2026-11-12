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
	float4 o;

	float4 r0;
	float3 r1;
	r0 = tex2D(normals, i.texcoord.xy);
	r0.xyz = r0.xyz * 2 + -1;
	r1.x = dot(i.texcoord1.xyz, light.xyz);
	r1.y = dot(i.texcoord2.xyz, light.xyz);
	r1.z = dot(i.texcoord3.xyz, light.xyz);
	r0.x = dot(r0.xyz, r1.xyz);
	o.x = r0.x * light.w;
	o.yzw = float3(0, 0, 1);

	return o;
}
