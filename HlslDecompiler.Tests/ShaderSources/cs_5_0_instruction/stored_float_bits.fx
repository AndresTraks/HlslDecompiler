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
	float4 r0;
	r0.xy = (float2)sv_dispatchthreadid.xy;
	r0.zw = int2(0, 0);
	r0.x = asfloat(packedLuminance.Load(r0.xyz).x);
	r0.y = r0.x * scale;
	r0.x = r0.x * scale + 1;
	brightMask[sv_dispatchthreadid.xy] = asuint(r0.x);
	scaledBits[sv_dispatchthreadid.x] = asuint(r0.y);
	r0.x = r0.y + r0.y;
	brightPixels.Append(asuint(r0.x));
}
