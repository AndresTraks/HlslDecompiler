float4 main(float4 position : POSITION) : POSITION
{
	return position * float4(0.5, 1.5, 2.5, 3.5) + float4(0.25, -1, 7, 9) * position.w + sin(position.x * 1.75);
}
