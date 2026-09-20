uint packed;
int signedPacked;

float4 main() : SV_Target
{
	float4 o;

	int2 r0;
	r0 = ((uint2)packed >> int2(3, 12)) & ((1 << int2(5, 8)) - 1);
	o.xy = (float2)(uint2)r0.xy;
	r0.x = (signedPacked << (32 - 8 - 4)) >> (32 - 8);
	o.z = (float)r0.x;
	r0.x = (packed & ~(((1 << 8) - 1) << 8)) | ((signedPacked << 8) & (((1 << 8) - 1) << 8));
	o.w = (float)(uint)r0.x;

	return o;
}
