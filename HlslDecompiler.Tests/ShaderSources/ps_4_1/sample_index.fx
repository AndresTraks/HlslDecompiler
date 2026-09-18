float4 resolve;

Texture2DMS<float4, 4> source;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	int2 t0 = (int2)i.sv_position.xy;
	return (resolve.x * (source.Load(t0, i.sv_sampleindex + 1 & 3) - source.Load(t0, i.sv_sampleindex)) + source.Load(t0, i.sv_sampleindex)) * resolve.y;
}
