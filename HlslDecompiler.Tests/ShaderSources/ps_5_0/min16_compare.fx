min16float4 c;

float4 main(min16float4 color : COLOR) : SV_Target
{
	float4 t0 = color * c;
	return t0.y < t0.x ? t0 : t0.yzwx;
}
