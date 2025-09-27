float4 main(float4 position : POSITION) : POSITION
{
	return float4(normalize(position.yxz), 1);
}
