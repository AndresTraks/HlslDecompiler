uint mip;

Texture2DArray layers;
Texture3D volume;

float4 main() : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	uint3 dimensions0 = 0;
	layers.GetDimensions(dimensions0.x, dimensions0.y, dimensions0.z);
	r0.xyz = dimensions0.xyz;
	r0.xyz = (float3)(uint3)r0.xyz;
	uint4 dimensions1 = 0;
	volume.GetDimensions(mip, dimensions1.x, dimensions1.y, dimensions1.z, dimensions1.w);
	r1 = dimensions1;
	r1 = asint((float4)(uint4)r1);
	r0.w = 0;
	o = r0 + asfloat(r1);

	return o;
}
