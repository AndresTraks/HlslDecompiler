cbuffer Exposure : register(b0)
{
	float scale;
};

Texture2D<uint> packedLuminance;
RWStructuredBuffer<uint> scaledBits : register(u0);
RWTexture2D<uint4> brightMask : register(u1);
AppendStructuredBuffer<uint> brightPixels : register(u2);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = asfloat(packedLuminance.Load(int3(sv_dispatchthreadid.xy, 0)).x) * scale;
	brightMask[sv_dispatchthreadid.xy] = asuint(asfloat(packedLuminance.Load(int3(sv_dispatchthreadid.xy, 0)).x) * scale + 1);
	scaledBits[sv_dispatchthreadid.x] = asuint(t0);
	brightPixels.Append(asuint(2 * t0));
}
