float4 eyePosition : register(c1);
float4 lightDirection;
sampler2D normalMap;
float4 waterColour : register(c2);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float3 r3;
	r0 = tex2D(normalMap, i.texcoord.xy);
	r0.xyz = r0.xyz * 2 + -1;
	r1.xyz = normalize(r0.xyz);
	r1.w = dot(-lightDirection.xyz, r1.xyz);
	r1.w = r1.w + r1.w;
	r0.xyz = r1.xyz * -r1.www + -lightDirection.xyz;
	r2 = -i.texcoord1.xyz + eyePosition.xyz;
	r3 = normalize(r2.xyz);
	r1.w = saturate(dot(r0.xyz, r3.xyz));
	r0.x = saturate(dot(r1.xyz, r3.xyz));
	r0.x = -r0.x + 1;
	r0.y = pow(r1.w, 32);
	r0.yzw = r0.yyy + waterColour.zyx;
	r1.x = r0.x * r0.x;
	r1.x = r1.x * r1.x;
	r0.x = r0.x * r1.x;
	r0.xyz = r0.xxx * waterColour.www + r0.wzy;
	r0.w = 1;
	o = r0;

	return o;
}
