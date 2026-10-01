Texture2DMS<float4> tex;

float4 main() : SV_Target
{
	return tex.GetSamplePosition(1).xxyy;
}
