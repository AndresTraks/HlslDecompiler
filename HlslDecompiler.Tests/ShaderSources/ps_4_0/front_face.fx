float4 a;
float4 b;

float4 main(nointerpolation uint sv_isfrontface : SV_IsFrontFace) : SV_Target
{
	return sv_isfrontface ? a : b;
}
