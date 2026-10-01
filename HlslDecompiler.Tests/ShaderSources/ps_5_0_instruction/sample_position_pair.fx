Texture2DMS<float4> tex;

float4 main() : SV_Target
{
	float4 o;

	float2 r0;
	r0 = tex.GetSamplePosition(1);
	o = r0.xxyy;

	return o;
}
