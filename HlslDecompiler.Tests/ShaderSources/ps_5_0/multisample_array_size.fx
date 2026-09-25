Texture2DMSArray<float4, 4> ms;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	uint4 t0;
	ms.GetDimensions(t0.x, t0.y, t0.z, t0.w);
	return ms.Load(int3((int2)i.sv_position.xy, 1), i.sv_sampleindex) + float4((float3)t0.xyz, 4);
}
