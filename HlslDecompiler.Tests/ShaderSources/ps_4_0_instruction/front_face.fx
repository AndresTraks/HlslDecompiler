float4 a;
float4 b;

float4 main(nointerpolation uint sv_isfrontface : SV_IsFrontFace) : SV_Target
{
	float4 o;

	o = (sv_isfrontface.x != 0) ? a : b;

	return o;
}
