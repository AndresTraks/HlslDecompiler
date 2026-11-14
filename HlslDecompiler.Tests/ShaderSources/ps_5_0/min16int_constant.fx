min16int4 k;

float4 main(nointerpolation min16int4 v : V) : SV_Target
{
	return (float4)(v * k + 3 >> 1);
}
