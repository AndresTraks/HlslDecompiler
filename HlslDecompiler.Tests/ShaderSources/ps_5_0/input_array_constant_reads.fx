struct PS_IN
{
	float4 sv_position : SV_Position;
	float weights[3] : WEIGHTS;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(i.weights[0] + i.texcoord.x, i.weights[1], i.texcoord.y * i.weights[2], 1);
}
