Texture2DMS<float4, 4> ms;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = ms.GetSamplePosition(i.sv_sampleindex).y;
	float t1 = ms.GetSamplePosition(i.sv_sampleindex).x;
	return ms.Load((int2)i.sv_position.xy, i.sv_sampleindex) * t1 + t0;
}
