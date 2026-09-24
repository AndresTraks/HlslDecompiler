Texture2D source;
RWTexture2D<float4> plane : register(u0);
RWTexture3D<float4> volume : register(u1);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint2 t0;
	plane.GetDimensions(t0.x, t0.y);
	uint2 t1;
	source.GetDimensions(t1.x, t1.y);
	plane[sv_dispatchthreadid.xy] = float4((float2)t1, (float2)t0);
	uint3 t2;
	volume.GetDimensions(t2.x, t2.y, t2.z);
	volume[sv_dispatchthreadid] = float4((float3)t2, 1);
}
