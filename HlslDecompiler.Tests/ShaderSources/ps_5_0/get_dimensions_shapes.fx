uint mip;

Texture2DArray layers;
Texture3D volume;

float4 main() : SV_Target
{
	uint4 t0;
	volume.GetDimensions(mip, t0.x, t0.y, t0.z, t0.w);
	uint3 t1;
	layers.GetDimensions(t1.x, t1.y, t1.z);
	return float4((float3)t1 + (float3)t0.xyz, (float)t0.w);
}
