Texture2DMS<float4, 4> source;

float4 main() : SV_Target
{
	float4 o;

	o.xy = source.GetSamplePosition(1);
	o.zw = source.GetSamplePosition(2);

	return o;
}
