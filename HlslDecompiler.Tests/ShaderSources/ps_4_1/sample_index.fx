float4 resolve;

Texture2DMS<float4, 4> source;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	return lerp(source.Load((int2)i.sv_position.xy, i.sv_sampleindex), source.Load((int2)i.sv_position.xy, i.sv_sampleindex + 1 & 3), resolve.x) * resolve.y;
}
