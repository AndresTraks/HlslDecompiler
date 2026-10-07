cbuffer Decal : register(b0)
{
	float4x4 invViewProjection;
	float4x4 decalMatrix;
	float4 decalTint;
	float fade;
};

SamplerState linearSampler;
Texture2D depthMap;
Texture2D decalMap;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	r0.z = asint(depthMap.Sample(linearSampler, i.texcoord.xy).x);
	r0.xy = asint(i.texcoord.xy * float2(2, 2) + float2(-1, -1));
	r0.w = 1065353216;
	r1.x = dot(asfloat(r0), transpose(invViewProjection)[0]);
	r1.y = dot(asfloat(r0), transpose(invViewProjection)[1]);
	r1.z = dot(asfloat(r0), transpose(invViewProjection)[2]);
	r1.w = dot(asfloat(r0), transpose(invViewProjection)[3]);
	r0 = asint(r1 / r1.w);
	r1.z = dot(asfloat(r0), transpose(decalMatrix)[2]);
	r1.x = dot(asfloat(r0), transpose(decalMatrix)[0]);
	r1.y = dot(asfloat(r0), transpose(decalMatrix)[1]);
	r0.xyz = (float3(0.5, 0.5, 0.5) >= abs(r1.xyz)) ? -1 : 0;
	r1.xy = r1.xy + float2(0.5, 0.5);
	r1 = decalMap.Sample(linearSampler, r1.xy);
	r0.xyz = r0.xyz & int3(1065353216, 1065353216, 1065353216);
	r0.x = asint(asfloat(r0.x) * r1.w);
	o.xyz = r1.xyz * decalTint.xyz;
	r0.x = asint(asfloat(r0.y) * asfloat(r0.x));
	r0.x = asint(asfloat(r0.z) * asfloat(r0.x));
	o.w = asfloat(r0.x) * fade;

	return o;
}
