struct VS_IN
{
	float4 position : POSITION;
	float3 w : W;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float weights[3] : WEIGHTS;
	float2 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = i.position;
	o.weights[0] = i.w.x;
	o.texcoord = i.position.xy;
	o.weights[1] = i.w.y + i.w.y;
	o.weights[2] = i.w.z + 1;

	return o;
}
