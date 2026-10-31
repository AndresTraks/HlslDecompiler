float4 main(float4 position : POSITION) : POSITION
{
	float t0 = sin(1.75000012 * position.x);
	return float4(0.25 * position.w + t0 + 0.5 * position.x, 1.5 * position.y - position.w + t0, dot(float2(2.5, 7), position.zw) + t0, dot(float2(3.5, 9), position.ww) + t0);
}
