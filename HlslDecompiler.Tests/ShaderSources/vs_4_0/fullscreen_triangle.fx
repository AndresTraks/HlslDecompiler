struct VS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

VS_OUT main(uint sv_vertexid : SV_VertexID)
{
	VS_OUT o;

	float2 t0 = float2((float)((sv_vertexid * 2) & 2), (float)(sv_vertexid & 2));
	o.sv_position = float4(float2(2, -2) * t0 + float2(-1, 1), 0, 1);
	o.texcoord = t0;

	return o;
}
