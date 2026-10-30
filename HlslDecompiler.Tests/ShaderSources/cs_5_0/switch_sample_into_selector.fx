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
	float2 t0 = (float2)sv_dispatchthreadid.xy + 0.25;
	float2 t1 = t0 * texel;
	float4 t2;
	switch (isOdd & 3) {
		case 0:
			t2 = src.SampleLevel(lin, 0.25 * texel + t1, 0);
			break;
		case 1:
			float4 t3 = src.SampleLevel(lin, float2(texel.x * t0.x + 0.5 * texel.x, texel.y * t0.y), 0);
			t2 = 0.5 * (src.SampleLevel(lin, t1, 0) + t3);
			break;
		case 2:
			float4 t4 = src.SampleLevel(lin, float2(texel.x * t0.x, texel.y * t0.y + 0.5 * texel.y), 0);
			t2 = 0.5 * (src.SampleLevel(lin, t1, 0) + t4);
			break;
		default:
			float4 t5 = src.SampleLevel(lin, float2(texel.x * t0.x + 0.5 * texel.x, texel.y * t0.y), 0);
			float4 t6 = src.SampleLevel(lin, float2(texel.x * t0.x, texel.y * t0.y + 0.5 * texel.y), 0);
			float4 t7 = src.SampleLevel(lin, texel * t0 + 0.5 * texel, 0);
			t2 = 0.25 * (src.SampleLevel(lin, t1, 0) + t5 + t6 + t7);
			break;
	}
	dst[sv_dispatchthreadid.xy] = t2;
}
