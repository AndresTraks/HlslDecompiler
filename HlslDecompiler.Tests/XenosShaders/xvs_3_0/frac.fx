float4 main(float4 position : POSITION) : POSITION
{
	return float4(frac(position.xy), sin(position.z), exp(position.w));
}
