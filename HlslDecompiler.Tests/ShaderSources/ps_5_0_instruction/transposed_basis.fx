cbuffer Occlusion : register(b0)
{
	float4x4 projection;
	float4 kernel[8];
	float2 noiseScale;
	float radius;
	float bias;
};

SamplerState pointSampler;
Texture2D depthMap;
Texture2D normalMap;
Texture2D noiseMap;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	int4 r2;
	float4 r3;
	float4 r4;
	float3 r5;
	r0.xyz = normalMap.Sample(pointSampler, i.texcoord.xy).xyz;
	r0.xyz = r0.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r1.xy = i.texcoord.xy * noiseScale.xy;
	r1.xyz = noiseMap.Sample(pointSampler, r1.xy).xyz;
	r1.xyz = r1.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r2.z = asint(depthMap.Sample(pointSampler, i.texcoord.xy).x);
	r0.w = dot(r1.xyz, r0.xyz);
	r1.xyz = -(r0.xyz) * r0.www + r1.xyz;
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = rsqrt(r0.w);
	r1.xyz = r0.www * r1.xyz;
	r3.xyz = r0.zxy * r1.yzx;
	r3.xyz = r0.yzx * r1.zxy + -(r3.xyz);
	r2.xy = asint(i.texcoord.xy * float2(2, 2) + float2(-1, -1));
	r4.w = 1;
	r0.w = 0;
	r1.w = 0;
	while (true) {
		r2.w = (r1.w >= 8) ? -1 : 0;
		if (r2.w != 0) break;
		r5 = r3.xyz * kernel[r1.w].yyy;
		r5 = r1.xyz * kernel[r1.w].xxx + r5.xyz;
		r5 = r0.xyz * kernel[r1.w].zzz + r5.xyz;
		r4.xyz = r5.xyz * radius + asfloat(r2.xyz);
		r5.x = dot(r4, transpose(projection)[0]);
		r5.y = dot(r4, transpose(projection)[1]);
		r2.w = asint(dot(r4, transpose(projection)[3]));
		r4.xy = r5.xy / asfloat(r2.ww);
		r4.xy = r4.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
		r2.w = asint(depthMap.Sample(pointSampler, r4.xy).x);
		r3.w = r4.z + bias;
		r2.w = (asfloat(r2.w) >= r3.w) ? -1 : 0;
		r2.w = r2.w & 1065353216;
		r0.w = r0.w + asfloat(r2.w);
		r1.w = r1.w + 1;
	}
	o = -(r0.w) * float4(0.125, 0.125, 0.125, 0.125) + float4(1, 1, 1, 1);

	return o;
}
