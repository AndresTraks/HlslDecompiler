uint4 v;

float4 main() : SV_Target
{
	float4 o;

	int3 r0;
	r0 = (int3(0, 0, 0) & ~(int3(7, 255, 15) << int3(2, 8, 4))) | ((v.xyz << int3(2, 8, 4)) & (int3(7, 255, 15) << int3(2, 8, 4)));
	o.xyz = (float3)(uint3)r0.xyz;
	o.w = (float)(uint)v.w;

	return o;
}
