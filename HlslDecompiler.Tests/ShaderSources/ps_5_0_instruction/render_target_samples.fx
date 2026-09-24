Texture2DMS<float4, 4> source;

float4 main() : SV_Target
{
	float4 o;

	float2 r0;
	r0 = source.GetSamplePosition(2);
	o.w = r0.x;
	o.xy = GetRenderTargetSamplePosition(1);
	o.z = GetRenderTargetSampleCount();

	return o;
}
