min16uint4 k;

float4 main(nointerpolation min16uint4 v : V) : SV_Target
{
	return (float4)((v & k) | (v >> 2));
}
