Texture2DMS<float4, 4> source;

float4 main() : SV_Target
{
	return float4(GetRenderTargetSamplePosition(1), GetRenderTargetSampleCount(), source.GetSamplePosition(2).x);
}
