cbuffer Params : register(b0)
{
	float3 cameraPos;
	float stepSize;
	float3 fogColour;
	float density;
	uint3 gridSize;
};

SamplerState linearSampler;
Texture3D<float> noise;
RWTexture3D<float4> fogVolume : register(u0);

[numthreads(4, 4, 4)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	float4 r1;
	float4 r2;
	int r3;
	r0.xyz = (sv_dispatchthreadid.xyz >= gridSize.xyz) ? -1 : 0;
	r0.x = r0.y | r0.x;
	r0.x = r0.z | r0.x;
	if (r0.x != 0) {
		return;
	}
	r0.xyz = asint((float3)(uint3)sv_dispatchthreadid.xyz);
	r0.xyz = asint(asfloat(r0.xyz) + float3(0.5, 0.5, 0.5));
	r1.xyz = (float3)(uint3)gridSize.xyz;
	r0.xyz = asint(asfloat(r0.xyz) / r1.xyz);
	r0.xyz = asint(asfloat(r0.xyz) * float3(2, 2, 2) + float3(-1, -1, -1));
	r0.w = asint(dot(asfloat(r0.xyz), asfloat(r0.xyz)));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r0.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz));
	r1 = float4(0, 0, 0, 1);
	r0.w = 0;
	while (true) {
		r2.x = ((uint)r0.w >= 16) ? -1 : 0;
		if (asint(r2.x) != 0) break;
		r2.x = (float)(uint)r0.w;
		r2.x = r2.x * stepSize;
		r2.xyz = asfloat(r0.xyz) * r2.xxx + cameraPos.xyz;
		r2.xyz = r2.xyz * float3(0.100000001, 0.100000001, 0.100000001);
		r2.x = noise.SampleLevel(linearSampler, r2.xyz, 0).x;
		r2.x = saturate(r2.x);
		r2.x = r2.x * density;
		r2.y = -(r2.x) * stepSize;
		r2.y = r2.y * 1.44269502;
		r2.y = exp2(r2.y);
		r2.xzw = r2.xxx * fogColour.xyz;
		r2.xzw = r1.www * r2.xzw;
		r2.xzw = r2.xzw * stepSize + r1.xyz;
		r2.y = r1.w * r2.y;
		r3 = (r2.y < 0.00999999978) ? -1 : 0;
		if (r3.x != 0) {
			r1 = r2.xzwy;
			break;
		}
		r0.w = r0.w + 1;
		r1 = r2.xzwy;
	}
	fogVolume[sv_dispatchthreadid.xyz] = r1;
}
