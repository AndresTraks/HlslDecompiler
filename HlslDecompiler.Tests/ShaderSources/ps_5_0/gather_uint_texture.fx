SamplerState s0;
Texture2D<uint> ids;
Texture2D<int2> offs;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int3 t0 = int3((int2)sv_position.xy, 0);
	int3 t1 = ids.Gather(s0, sv_position.xy).xyw;
	return float4(asfloat(ids.Load(t0).x), 0.5 * (float)(offs.Load(t0).x), (float)(uint)(t1.x + t1.z), asfloat(t1.y ^ ids.Load(t0).x));
}
