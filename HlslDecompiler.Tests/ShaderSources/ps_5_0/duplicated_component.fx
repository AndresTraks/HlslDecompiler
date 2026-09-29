StructuredBuffer<int> buf : register(t0);

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(dot(i.sv_position, i.texcoord), dot(i.sv_position, i.texcoord), buf[(int)i.sv_position.y] & 3 ? 0.0 : 1.0, buf[(int)i.sv_position.y] & 3 ? 0.0 : 1.0);
}
