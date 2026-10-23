cbuffer C : register(b0)
{
	uint2 srcSize;
	float2 texel;
	uint isOdd;
};

SamplerState lin;
Texture2D src;
RWTexture2D<float4> dst : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	int4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	r0.xy = (float2)(uint2)sv_dispatchthreadid.xy;
	r0.xy = r0.xy + float2(0.25, 0.25);
	r0.zw = r0.xy * texel.xy;
	r1.x = isOdd & 3;
	switch (r1.x) {
		case 0:
		r2.xy = texel.xy * float2(0.25, 0.25) + r0.zw;
		r1 = asint(src.SampleLevel(lin, r2.xy, 0));
		break;
		case 1:
		r2 = src.SampleLevel(lin, r0.zw, 0);
		r3.x = texel.x * 0.5;
		r3.y = 0;
		r3.xy = texel.xy * r0.xy + r3.xy;
		r3 = src.SampleLevel(lin, r3.xy, 0);
		r2 = r2 + r3;
		r1 = asint(r2 * float4(0.5, 0.5, 0.5, 0.5));
		break;
		case 2:
		r2 = src.SampleLevel(lin, r0.zw, 0);
		r3.x = 0;
		r3.y = texel.y * 0.5;
		r3.xy = texel.xy * r0.xy + r3.xy;
		r3 = src.SampleLevel(lin, r3.xy, 0);
		r2 = r2 + r3;
		r1 = asint(r2 * float4(0.5, 0.5, 0.5, 0.5));
		break;
		default:
		r2 = src.SampleLevel(lin, r0.zw, 0);
		r3.xyz = texel.xyx * float3(0.5, 0.5, 0.5);
		r3.w = 0;
		r3 = texel.xyxy * r0.xyxy + r3.zwxy;
		r4 = src.SampleLevel(lin, r3.xy, 0);
		r2 = r2 + r4;
		r3.x = 0;
		r3.y = texel.y * 0.5;
		r0.xy = texel.xy * r0.xy + r3.xy;
		r0 = src.SampleLevel(lin, r0.xy, 0);
		r0 = r0 + r2;
		r2 = src.SampleLevel(lin, r3.zw, 0);
		r0 = r0 + r2;
		r1 = asint(r0 * float4(0.25, 0.25, 0.25, 0.25));
		break;
	}
	dst[sv_dispatchthreadid.xy] = asfloat(r1);
}
