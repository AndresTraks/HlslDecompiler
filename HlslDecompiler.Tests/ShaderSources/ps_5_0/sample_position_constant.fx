Texture2DMS<float4, 4> source;

float4 main() : SV_Target
{
	return float4(source.GetSamplePosition(1), source.GetSamplePosition(2));
}
