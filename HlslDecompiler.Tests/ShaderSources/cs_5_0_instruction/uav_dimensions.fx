Texture2D source;
RWTexture2D<float4> plane : register(u0);
RWTexture3D<float4> volume : register(u1);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	int2 r1;
	uint2 dimensions0;
	source.GetDimensions(dimensions0.x, dimensions0.y);
	r0.xy = dimensions0.xy;
	r0.xy = (float2)(uint2)r0.xy;
	uint2 dimensions1;
	plane.GetDimensions(dimensions1.x, dimensions1.y);
	r1 = dimensions1.xy;
	r0.zw = (float2)(uint2)r1.xy;
	plane[sv_dispatchthreadid.xy] = r0;
	uint3 dimensions2 = 0;
	volume.GetDimensions(dimensions2.x, dimensions2.y, dimensions2.z);
	r0.xyz = dimensions2.xyz;
	r0.xyz = (float3)(uint3)r0.xyz;
	r0.w = 1;
	volume[sv_dispatchthreadid.xyz] = r0;
}
