Texture2DMS<float4> t;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	uint3 t0;
	t.GetDimensions(t0.x, t0.y, t0.z);
	float t1 = t.GetSamplePosition(i.sv_sampleindex).x;
	float t2 = t.GetSamplePosition(i.sv_sampleindex).y;
	return t.Load((int2)i.sv_position.xy, i.sv_sampleindex) * (t1 + 0.5) + float4(t1, t2, (float2)t0.zx * float2(1, 0.0000999999975));
}
