cbuffer Params : register(b0)
{
	float4x4 projection;
	float2 inverseSize;
	float radius;
	float bias;
	uint sampleCount;
};

static const float4 icb0[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(-1, 0, 0, 0),
	float4(0, -1, 0, 0),
};

static const float4 icb1[8] =
{
	float4(0.5, 0.5, 0.5, 0),
	float4(-0.5, 0.5, 0.5, 0),
	float4(0.5, -0.5, 0.5, 0),
	float4(-0.5, -0.5, 0.5, 0),
	float4(0.7, 0, 0.3, 0),
	float4(-0.7, 0, 0.3, 0),
	float4(0, 0.7, 0.3, 0),
	float4(0, -0.7, 0.3, 0),
};

SamplerState pointSampler;
Texture2D depthMap;
Texture2D normalMap;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	int2 r2;
	r0.x = depthMap.SampleLevel(pointSampler, i.texcoord.xy, 0).x;
	r0.yzw = normalMap.SampleLevel(pointSampler, i.texcoord.xy, 0).xyz;
	r0.yzw = r0.yzw * float3(2, 2, 2) + float3(-1, -1, -1);
	r1.x = (uint)i.sv_position.x;
	r1.x = r1.x & 3;
	r1.yz = int2(0, 0);
	while (true) {
		r1.w = (r1.z >= sampleCount) ? -1 : 0;
		if (r1.w != 0) break;
		r1.w = r1.z & 7;
		r2.x = asint(icb0[r1.x].y * icb1[r1.w].y);
		r2.x = asint(icb1[r1.w].x * icb0[r1.x].x + -(asfloat(r2.x)));
		r2.y = asint(dot(icb1[r1.w].yx, icb0[r1.x].xy));
		r2 = asint(asfloat(r2.xy) * radius);
		r2 = asint(asfloat(r2.xy) * inverseSize.xy + i.texcoord.xy);
		r2.x = asint(depthMap.SampleLevel(pointSampler, asfloat(r2.xy), 0).x);
		r2.x = asint(r0.x + -(asfloat(r2.x)));
		r2.y = (bias < asfloat(r2.x)) ? -1 : 0;
		r2.x = (asfloat(r2.x) < radius) ? -1 : 0;
		r2.x = r2.x & r2.y;
		r1.w = asint(saturate(dot(r0.yzw, icb1[r1.w].xyz)));
		r1.w = r1.w & r2.x;
		r1.y = asint(asfloat(r1.w) + asfloat(r1.y));
		r1.z = r1.z + 1;
	}
	r0.x = (float)(uint)sampleCount;
	r0.x = asfloat(r1.y) / r0.x;
	o = -(r0.x) + float4(1, 1, 1, 1);

	return o;
}
