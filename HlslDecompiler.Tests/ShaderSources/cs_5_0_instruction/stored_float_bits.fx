cbuffer Exposure : register(b0)
{
	float scale;
};

Texture2D<uint> packedLuminance;
RWStructuredBuffer<uint> scaledBits : register(u0);
RWTexture2D<uint> brightMask : register(u1);
AppendStructuredBuffer<uint> brightPixels : register(u2);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.xy = sv_dispatchthreadid.xy;
	r0.zw = int2(0, 0);
	r0.x = packedLuminance.Load(r0.xyz).x;
	r0.y = asint(asfloat(r0.x) * scale);
	r0.x = asint(asfloat(r0.x) * scale + 1);
	brightMask[sv_dispatchthreadid.xy] = r0.x;
	scaledBits[sv_dispatchthreadid.x] = r0.y;
	r0.x = asint(asfloat(r0.y) + asfloat(r0.y));
	brightPixels.Append(r0.x);
}
