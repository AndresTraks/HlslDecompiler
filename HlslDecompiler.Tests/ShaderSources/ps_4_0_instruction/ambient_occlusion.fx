float4x4 projection;
float4 kernel[12];
float4 occlusion;

SamplerState samp;
Texture2D depthMap;
Texture2D normalMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	r0 = depthMap.Sample(samp, i.texcoord.xy);
	r0.yzw = r0.xxx * i.texcoord1.xyz;
	r1 = normalMap.Sample(samp, i.texcoord.xy);
	r1.xyz = r1.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r2.w = 1;
	r1.w = 0;
	r3.x = 0;
	while (true) {
		r3.y = (r3.x >= 12) ? -1 : 0;
		if (asint(r3.y) != 0) break;
		r3.y = dot(kernel[r3.x].xyz, r1.xyz);
		r3.z = (0 < r3.y) ? -1 : 0;
		r3.y = (r3.y < 0) ? -1 : 0;
		r3.y = -(r3.z) + r3.y;
		r3.y = (float)(int)r3.y;
		r3.yzw = r3.yyy * kernel[r3.x].xyz;
		r2.xyz = r3.yzw * occlusion.xxx + r0.yzw;
		r4.x = dot(r2, transpose(projection)[0]);
		r4.y = dot(r2, transpose(projection)[1]);
		r2.x = dot(r2, transpose(projection)[3]);
		r2.xy = r4.xy / r2.xx;
		r2.xy = r2.xy * float2(0.5, -0.5) + float2(0.5, 0.5);
		r4 = depthMap.Sample(samp, r2.xy);
		r2.x = r4.x * occlusion.y;
		r2.y = i.texcoord1.z * r0.x + -(r2.x);
		r2.y = saturate(occlusion.x / abs(r2.y));
		r2.z = r2.z + occlusion.z;
		r2.x = asfloat((r2.x >= r2.z) ? -1 : 0);
		r2.x = asfloat(asint(r2.y) & asint(r2.x));
		r1.w = r1.w + r2.x;
		r3.x = r3.x + 1;
	}
	o = -(r1.w) * float4(0.0833333358, 0.0833333358, 0.0833333358, 0.0833333358) + float4(1, 1, 1, 1);

	return o;
}
