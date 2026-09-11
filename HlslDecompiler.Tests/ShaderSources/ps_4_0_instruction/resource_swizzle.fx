float4x4 invViewProj;
float3 lightPos;
float lightRange;
float4 lightColour;

SamplerState samp;
Texture2D albedoTex;
Texture2D normalTex;
Texture2D depthTex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = depthTex.Sample(samp, texcoord.xy);
	r0.xy = texcoord.xy * float2(2, 2) + float2(-1, -1);
	r0.w = 1;
	r1.x = dot(r0, transpose(invViewProj)[0]);
	r1.y = dot(r0, transpose(invViewProj)[1]);
	r1.z = dot(r0, transpose(invViewProj)[2]);
	r0.x = dot(r0, transpose(invViewProj)[3]);
	r0.xyz = r1.xyz / r0.xxx;
	r0.xyz = -(r0.xyz) + lightPos.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r1.x = 1 / sqrt(r0.w);
	r0.w = sqrt(r0.w);
	r0.w = r0.w / lightRange;
	r0.w = -(r0.w) + 1;
	r0.xyz = r0.xyz * r1.xxx;
	r1 = normalTex.Sample(samp, texcoord.xy);
	r1.xyz = r1.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r0.x = dot(r1.xyz, r0.xyz);
	r1 = albedoTex.Sample(samp, texcoord.xy);
	r1 = r1 * lightColour;
	r1 = r0.x * r1;
	o = r0.w * r1;

	return o;
}
